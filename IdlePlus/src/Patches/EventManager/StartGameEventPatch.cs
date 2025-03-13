using HarmonyLib;
using Minigames;
using IdlePlus.Utilities;
using IdlePlus.Settings;

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "StartGame")]
    public class StartGameEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Minigames.Minigame minigame)
        {
            // Speichere den aktuellen EventType im Tracker
            MinigameTracker.LastEventType = minigame.EventType;
            
            // Sende den "minigame start" Webhook
            _ = WebHookHelper.SendMinigameWebhookAsync("start", minigame.EventType);
            
            // Falls noch keine Serie aktiv ist, starte auch die Serie
            if (!MinigameTracker.IsSeriesActive)
            {
                _ = WebHookHelper.SendMinigameSeriesWebhookAsync("start", minigame.EventType);
                MinigameTracker.IsSeriesActive = true;
            }
        }
    }
}
