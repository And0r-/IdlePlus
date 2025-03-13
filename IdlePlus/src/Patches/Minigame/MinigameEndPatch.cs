using System;
using System.Reflection;
using HarmonyLib;
using Minigames;
using Network;
using IdlePlus.Utilities;

namespace IdlePlus.Patches.Minigame
{
    [HarmonyPatch(typeof(MinigameManager), "EndMinigame")]
    public class EndGameEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix(MinigameEndedMessage message)
        {
            _ = WebHookHelper.SendMinigameWebhookAsync("stop", MinigameTracker.LastEventType);


            if (message.NextMinigameType.ToString() == "None")
            {
                IdleLog.Info("NextMinigameType is empty or 'None'. Detailed message debug output:");
                Type msgType = message.GetType();
                var properties = msgType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(message);
                        IdleLog.Info($"Property: {prop.Name} = {value}");
                    }
                    catch (Exception ex)
                    {
                        IdleLog.Info($"Property: {prop.Name} could not be read: {ex.Message}");
                    }
                }

                var fields = msgType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    try
                    {
                        var value = field.GetValue(message);
                        IdleLog.Info($"Field: {field.Name} = {value}");
                    }
                    catch (Exception ex)
                    {
                        IdleLog.Info($"Field: {field.Name} could not be read: {ex.Message}");
                    }
                }

                _ = WebHookHelper.SendMinigameSeriesWebhookAsync("stop", MinigameTracker.LastEventType);
                MinigameTracker.IsSeriesActive = false;
                MinigameTracker.LastEventType = global::Guilds.UI.ClanEventType.None;
            }
        }
    }
}
