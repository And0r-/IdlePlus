using System;
using System.Collections.Generic;
using IdlePlus.Settings;
using Player;
using Guilds;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IdlePlus.Utilities
{
    public static class WebhookManager
    {
        private static readonly WebhookQueue<WebhookRequest> _webhookQueue = new WebhookRequestQueue();
        private static readonly int MAX_RETRIES = 3;

        // Methods that can have a request body
        private static readonly HashSet<string> MethodsWithBody = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "POST", "PUT", "PATCH"
        };

        /// <summary>
        /// Adds a webhook request to the processing queue.
        /// </summary>
        public static void AddSendWebhook(WebhookType webhookType, Dictionary<string, string> pathParams, string jsonRequestData = null)
        {
            if (!TryGetEnabledConfig(webhookType, out var config))
            {
                IdleLog.Debug($"[Webhook] Webhook type {webhookType} is not enabled or not configured.");
                return;
            }

            try
            {
                string requestMethod = config.RequestMethod.ToUpperInvariant();
                bool supportsBody = MethodsWithBody.Contains(requestMethod);

                // For methods without body support, jsonRequestData should be null
                if (!supportsBody && !string.IsNullOrEmpty(jsonRequestData))
                {
                    jsonRequestData = null;
                }

                // Validate JSON format if data was provided
                if (!string.IsNullOrEmpty(jsonRequestData) && !JsonHelper.IsValidJson(jsonRequestData))
                {
                    IdleLog.Error($"[Webhook] Invalid JSON format for {webhookType}: {jsonRequestData}");
                    return; // Abort if JSON is invalid
                }

                // Collect metadata for path replacement and body if needed
                var metadata = CollectMetadata();

                // Process URL path with metadata replacements
                string processedPath = ReplacePlaceholders(config.UrlPath, pathParams, metadata);

                // Enrich with metadata if the method supports a body
                string finalJsonData = supportsBody ? EnrichWithMetadata(jsonRequestData, pathParams, metadata) : null;

                var request = new WebhookRequest
                {
                    WebhookType = webhookType,
                    PathParams = pathParams,
                    JsonRequestData = finalJsonData,
                    ProcessedUrlPath = processedPath,
                    RetryCount = 0
                };

                _webhookQueue.Enqueue(request);
                IdleLog.Debug($"[Webhook] Request for {webhookType} added to queue with method {requestMethod}.");
            }
            catch (Exception ex)
            {
                IdleLog.Error($"[Webhook] Error preparing webhook {webhookType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a webhook request with an Il2CppSystem.Object payload to the processing queue.
        /// </summary>
        public static void AddSendWebhook(WebhookType webhookType, Dictionary<string, string> pathParams, Il2CppSystem.Object il2cppRequestData)
        {
            try
            {
                string json = JsonHelper.Serialize(il2cppRequestData);
                if (json != null)
                {
                    AddSendWebhook(webhookType, pathParams, json);
                }
            }
            catch (Exception ex)
            {
                IdleLog.Error($"[Webhook] Error preparing webhook {webhookType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Collects metadata about the current game state.
        /// </summary>
        private static Dictionary<string, string> CollectMetadata()
        {
            var metadata = new Dictionary<string, string>();

            // Player information - using null conditional (?.) and null coalescing (??) operators
            metadata["playerName"] = PlayerData.Instance?.Username ?? "Unknown";
            metadata["gameMode"] = PlayerData.Instance?.GameMode.ToString() ?? "Unknown";

            // Clan information - chain of null conditional operators
            metadata["clanName"] = GuildManager.Instance?.OurGuild?.Name ?? "None";

            // Unix Timestamp (seconds since 1/1/1970)
            var unixTimestamp = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            metadata["timestamp"] = unixTimestamp.ToString();

            // Client version - using a safe method to access a property that might throw
            metadata["clientVersion"] = IdlePlus.ModVersion;

            return metadata;
        }

        /// <summary>
        /// Enriches the provided data with metadata.
        /// </summary>
        private static string EnrichWithMetadata(string jsonData, Dictionary<string, string> pathParams, Dictionary<string, string> metadata)
        {
            // Parse existing JSON data or create new object
            JObject payload = !string.IsNullOrEmpty(jsonData) ? JObject.Parse(jsonData) : new JObject();

            // Add metadata
            var metadataObj = new JObject();
            foreach (var item in metadata)
            {
                metadataObj[item.Key] = new JValue(item.Value);
            }
            payload["metadata"] = metadataObj;

            // Add path parameters if not already present
            if (payload["params"] == null && pathParams?.Count > 0)
            {
                var paramsObj = new JObject();
                foreach (var param in pathParams)
                {
                    paramsObj[param.Key] = new JValue(param.Value);
                }
                payload["params"] = paramsObj;
            }

            return payload.ToString(Formatting.None);
        }

        /// <summary>
        /// Replaces placeholders in the URL path with values from pathParams and metadata.
        /// </summary>
        private static string ReplacePlaceholders(string urlPath, Dictionary<string, string> pathParams, Dictionary<string, string> metadata)
        {
            string result = urlPath;

            // Create a merged dictionary with pathParams taking precedence over metadata
            var replacements = new Dictionary<string, string>(metadata);

            // Add path parameters (will overwrite any metadata with the same key)
            if (pathParams != null)
            {
                foreach (var param in pathParams)
                {
                    replacements[param.Key] = param.Value;
                }
            }

            // Replace all placeholders in one pass
            foreach (var replacement in replacements)
            {
                result = result.Replace($"{{{replacement.Key}}}", Uri.EscapeDataString(replacement.Value));
            }

            return result;
        }

        private class WebhookRequestQueue : WebhookQueue<WebhookRequest>
        {
            protected override async System.Threading.Tasks.Task ProcessItemAsync(WebhookRequest request)
            {
                await ProcessWebhookRequest(request);
            }
        }

        private static async System.Threading.Tasks.Task ProcessWebhookRequest(WebhookRequest request)
        {
            try
            {
                var config = WebhookConfigProvider.GetConfig(request.WebhookType);
                if (config == null || !IsWebhookEnabled(request.WebhookType))
                {
                    IdleLog.Error($"[Webhook] Configuration for {request.WebhookType} not found or disabled.");
                    return;
                }

                string baseUrl = ModSettings.Hooks.BackendHookServer.Value;
                string fullUrl = HttpService.Instance.BuildFullUrl(baseUrl, request.ProcessedUrlPath);
                string requestMethod = config.RequestMethod;

                bool success = await HttpService.Instance.SendRequestAsync(
                    fullUrl,
                    requestMethod,
                    request.JsonRequestData
                );

                if (!success && request.RetryCount < MAX_RETRIES)
                {
                    // Retry logic
                    request.RetryCount++;
                    IdleLog.Error($"[Webhook] Retry {request.RetryCount}/{MAX_RETRIES} for {request.WebhookType}");

                    // Wait before retrying (exponential backoff)
                    int delayMs = 500 * (int)Math.Pow(2, request.RetryCount);
                    await System.Threading.Tasks.Task.Delay(delayMs);

                    // Re-queue the request
                    _webhookQueue.Enqueue(request);
                }
                else if (!success)
                {
                    IdleLog.Error($"[Webhook] Maximum retries reached for {request.WebhookType}");
                }
            }
            catch (Exception ex)
            {
                IdleLog.Error($"[Webhook] Error processing webhook: {ex.Message}");
            }
        }

        private static bool IsWebhookEnabled(WebhookType webhookType)
        {
            return ModSettings.Hooks.WebhookToggles.TryGetValue(webhookType, out var toggle) && toggle.Value;
        }

        private static bool TryGetEnabledConfig(WebhookType webhookType, out WebhookConfig config)
        {
            config = WebhookConfigProvider.GetConfig(webhookType);
            return config != null && IsWebhookEnabled(webhookType);
        }

        public class WebhookRequest
        {
            public WebhookType WebhookType;
            public Dictionary<string, string> PathParams;
            public string JsonRequestData;
            public string ProcessedUrlPath;
            public int RetryCount;
        }

        public static int GetQueuedRequestCount()
        {
            return _webhookQueue.Count;
        }
    }
}