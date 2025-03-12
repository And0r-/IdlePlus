using System;
using System.Reflection;
using HarmonyLib;
using IdlePlus.Utilities;
using Minigames;

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "StartGame")]
    public class StartGameEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix(object minigame)
        {
            if (minigame == null)
            {
                IdleLog.Info("StartGameEventPatch: minigame is null");
                return;
            }

            // Get the event type from the minigame instance.
            string eventType = GetPropertyValue(minigame, "EventType");
            if (string.IsNullOrEmpty(eventType))
            {
                return;
            }

            EventSeriesTracker.LastEventStart = DateTime.Now;
            EventSeriesTracker.LastEventType = eventType;

            _ = WebHookHelper.SendMinigameWebhookAsync("start", eventType);

            if (!EventSeriesTracker.IsSeriesActive)
            {
                _ = WebHookHelper.SendMinigameSeriesWebhookAsync("start", eventType);
                EventSeriesTracker.IsSeriesActive = true;
            }
        }

        private static string GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    return value?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                IdleLog.Info($"Error retrieving property {propertyName}: {ex.Message}");
            }
            return "";
        }
    }
}
