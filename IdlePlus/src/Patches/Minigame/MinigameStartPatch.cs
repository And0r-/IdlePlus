using HarmonyLib;
using Minigames;

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "StartGame")]
    public class StartGameEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Minigames.Minigame minigame)
        {
            MinigameTracker.LastEventType = minigame.EventType;
            
            _ = WebHookHelper.SendMinigameWebhookAsync("start", minigame.EventType);
            
            if (!MinigameTracker.IsSeriesActive)
            {
                _ = WebHookHelper.SendMinigameSeriesWebhookAsync("start", minigame.EventType);
                MinigameTracker.IsSeriesActive = true;
            }
        }
    }
}
