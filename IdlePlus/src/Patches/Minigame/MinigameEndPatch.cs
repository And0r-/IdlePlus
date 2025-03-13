using HarmonyLib;
using Minigames;

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "EndMinigame")]
    public class EndGameEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            _ = WebHookHelper.SendMinigameWebhookAsync("stop", MinigameTracker.LastEventType);
            MinigameTracker.LastEventType = global::Guilds.UI.ClanEventType.None;
        }
    }
}
