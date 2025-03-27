using System.Collections.Generic;

namespace IdlePlus.Utilities
{
    /// <summary>
    /// Enum defining the available webhook types.
    /// </summary>
    public enum WebhookType
    {
        /// <summary>Minigame/clan event webhooks.</summary>
        Minigame,
        
        /// <summary>Market data webhooks.</summary>
        MarketData
    }

    /// <summary>
    /// Provides configuration settings for each webhook type.
    /// </summary>
    public static class WebhookConfigProvider
    {
        // Dictionary holding configuration for each webhook type.
        private static readonly Dictionary<WebhookType, WebhookConfig> _configs = new Dictionary<WebhookType, WebhookConfig>
        {
            {
                WebhookType.Minigame, new WebhookConfig {
                    RequestMethod = "POST",
                    UrlPath = "/minigame/{action}/{type}",
                    SettingsName = "Minigames (Clan Events)"
                }
            },
            {
                WebhookType.MarketData, new WebhookConfig {
                    RequestMethod = "POST",
                    UrlPath = "/MarketDataTest/{action}/{playerName}/{gameMode}/{clanName}/{timestamp}/{clientVersion}",
                    SettingsName = "Market Data"
                }
            }
        };

        /// <summary>
        /// Retrieves the configuration for the specified webhook type.
        /// </summary>
        /// <param name="type">The webhook type.</param>
        /// <returns>The corresponding <see cref="WebhookConfig"/> if found; otherwise, null.</returns>
        public static WebhookConfig GetConfig(WebhookType type)
        {
            return _configs.TryGetValue(type, out var config) ? config : null;
        }
        
        /// <summary>
        /// Validates a webhook configuration.
        /// </summary>
        /// <param name="type">The webhook type to validate.</param>
        /// <returns>True if the configuration is valid; otherwise, false.</returns>
        public static bool ValidateConfig(WebhookType type)
        {
            if (!_configs.TryGetValue(type, out var config))
                return false;
                
            // Check for required fields
            if (string.IsNullOrEmpty(config.RequestMethod) || string.IsNullOrEmpty(config.UrlPath))
                return false;
                
            // Additional validation could be added here
            
            return true;
        }
        
        /// <summary>
        /// Gets all available webhook types.
        /// </summary>
        /// <returns>An array of all webhook types.</returns>
        public static WebhookType[] GetAvailableTypes()
        {
            var types = new WebhookType[_configs.Count];
            _configs.Keys.CopyTo(types, 0);
            return types;
        }
    }

    /// <summary>
    /// Represents the configuration for a webhook.
    /// </summary>
    public class WebhookConfig
    {
        /// <summary>
        /// Gets or sets the HTTP request method (e.g., "GET" or "POST").
        /// </summary>
        public string RequestMethod { get; set; }
        
        /// <summary>
        /// Gets or sets the URL path template with placeholders (e.g., "/minigame/{action}/{type}").
        /// </summary>
        public string UrlPath { get; set; }
        
        /// <summary>
        /// Gets or sets the display name for the webhook as shown in the settings.
        /// </summary>
        public string SettingsName { get; set; }
    }
}