using System;
using System.Threading.Tasks;
using HarmonyLib;
using IdlePlus.Utilities;
using Minigames;
using IdlePlus.Settings; // To access ModSettings.Hooks

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "EndMinigame")]
    public class EndMinigamePatch
    {
        [HarmonyPostfix]
        public static async void Postfix()
        {
            _ = WebHookHelper.SendMinigameWebhookAsync("stop", EventSeriesTracker.LastEventType);

            // Wait for 2 seconds to allow a potential new event start.
            await Task.Delay(2000);

            if (EventSeriesTracker.IsSeriesActive && (DateTime.Now - EventSeriesTracker.LastEventStart).TotalSeconds >= 2)
            {
                _ = WebHookHelper.SendMinigameSeriesWebhookAsync("stop", EventSeriesTracker.LastEventType);
                EventSeriesTracker.IsSeriesActive = false;
                EventSeriesTracker.LastEventStart = DateTime.MinValue;
                EventSeriesTracker.LastEventType = "";
            }
        }
    }
}
