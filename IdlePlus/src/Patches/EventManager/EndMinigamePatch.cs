using System;
using HarmonyLib;
using IdlePlus.Utilities;
using Minigames;
using IdlePlus.Settings;
using Network;

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "EndMinigame")]
    public class EndGameEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MinigameEndedMessage message)
        {
            try
            {
                // Sende den normalen "minigame stop" Webhook, basierend auf dem im Tracker gespeicherten EventType
                _ = WebHookHelper.SendMinigameWebhookAsync("stop", MinigameTracker.LastEventType);
                
                // Prüfe, ob der nächste Event leer ist (NextMinigameType == "None")
                if (message.NextMinigameType.ToString() == "None")
                {
                    IdleLog.Info("Kein weiterer Event erkannt – sende 'minigameserie stop' Webhook.");
                    _ = WebHookHelper.SendMinigameSeriesWebhookAsync("stop", MinigameTracker.LastEventType);
                    
                    // Reset des Trackers
                    MinigameTracker.IsSeriesActive = false;
                    MinigameTracker.LastEventType = global::Guilds.UI.ClanEventType.None;
                }
                else
                {
                    IdleLog.Info($"Nächster Event erkannt, NextMinigameType: {message.NextMinigameType}");
                }
            }
            catch (Exception ex)
            {
                IdleLog.Error($"Fehler im EndGameEventPatch: {ex.Message}");
            }
        }
    }
}
