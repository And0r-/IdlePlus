using System;
using System.Threading.Tasks;
using Il2CppSystem.Net.Http;
using IdlePlus.Settings;
using IdlePlus.Utilities;

namespace IdlePlus.Patches.Minigame
{
    public static class WebHookHelper
    {
        private static readonly HttpClient client = new HttpClient();

        // Private helper that sends a GET request for a given endpoint.
        // It automatically prepends the backend URL from settings,
        // sets the security token in the header, logs the URL, and sends the request.
        #pragma warning disable CS1998
        private static async Task SendGetRequestAsync(string endpoint)
        {
            // Assemble full URL
            string baseUrl = ModSettings.Hooks.BackendHookServer.Value;
            string fullUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
            IdleLog.Info($"[Backend] Sending GET request: {fullUrl}");

            // Create the HTTP GET request.
            var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);

            // Set the security token in the header if available.
            string token = ModSettings.Hooks.BackendHookBarrer.Value;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.TryAddWithoutValidation("Authorization", token);
            }

            try
            {
                _ = client.SendAsync(request);
            }
            catch (Exception ex)
            {
                IdleLog.Error($"[Backend] Error sending GET request: {ex.Message}");
            }
        }

        public static async Task SendMinigameWebhookAsync(string action, global::Guilds.UI.ClanEventType eventType)
        {
            if (!ModSettings.Hooks.ClanEventsEnabled.Value)
                return;

            string endpoint = $"minigame/{action}/{eventType}";
            await SendGetRequestAsync(endpoint);
        }

        public static async Task SendMinigameSeriesWebhookAsync(string action, global::Guilds.UI.ClanEventType eventType)
        {
            if (!ModSettings.Hooks.ClanEventSeriesEnabled.Value)
                return;

            string endpoint = $"minigameserie/{action}/{eventType}";
            await SendGetRequestAsync(endpoint);
        }
    }
}
