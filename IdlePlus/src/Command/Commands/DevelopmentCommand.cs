using System;
using System.IO;
using Brigadier.NET;
using Brigadier.NET.Context;
using Databases;
using IdlePlus.Utilities;
using IdlePlus.Utilities.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Player;

namespace IdlePlus.Command.Commands {
	internal static class DevelopmentCommand {

		internal static void Register(CommandDispatcher<CommandSender> registry) {
			var command = Literal.Of("dev");

			command.Then(Literal.Of("export")
				.Then(Literal.Of("items").Executes(HandleExportItems)));
				//.Then(Literal.Of("tasks").Executes(HandleExportTasks)));

			command.Then(Literal.Of("print")
				.Then(Argument.Of("message", Arguments.GreedyString())
					.Executes(HandlePrint)));
			
			command.Then(Literal.Of("say")
				.Then(Argument.Of("message", Arguments.GreedyString())
					.Executes(HandleSay)));

            // Webhook Commands
            var webhookCommand = Literal.Of("webhook");
            
            // Test Command
            webhookCommand.Then(Literal.Of("test").Executes(HandleWebhookTest));
            
            // Start Repeater Command
            webhookCommand.Then(Literal.Of("start")
                .Executes(context => HandleWebhookStartRepeater(context, 5))
                .Then(Argument.Of("interval", Arguments.Integer(1, 60))
                    .Executes(context => HandleWebhookStartRepeater(context, context.GetArgument<int>("interval")))));
            
            // Stop Repeater Command
            webhookCommand.Then(Literal.Of("stop").Executes(HandleWebhookStopRepeater));
            
            // Status Command
            webhookCommand.Then(Literal.Of("status").Executes(HandleWebhookStatus));
            
            // Stats Command
            webhookCommand.Then(Literal.Of("stats").Executes(HandleWebhookStats));
            
            // Stats Reset Command
            webhookCommand.Then(Literal.Of("stats_reset").Executes(HandleWebhookStatsReset));
            
            // Cleanup Command
            webhookCommand.Then(Literal.Of("cleanup").Executes(HandleWebhookCleanup));
            
            // Add the webhook command to the main command
            command.Then(webhookCommand);
            
			registry.Register(command);
		}
		
		/*
		 * Export
		 */

		private static void HandleExportItems(CommandContext<CommandSender> context) {
			var path = Path.Combine(BepInEx.Paths.PluginPath, "IdlePlus", "export");
			Directory.CreateDirectory(path);

			var root = new JObject();
			root.Add("exported_at", new JValue($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.FFFZ}"));
			root.Add("version", new JObject(
				new JProperty("config", SettingsDatabase.SharedSettings.ConfigVersion),
				new JProperty("latest_build", SettingsDatabase.SharedSettings.LatestBuildVersion),
				new JProperty("required_build", SettingsDatabase.SharedSettings.RequiredBuildVersion))
			);
			root.Add("items", new JArray().Do(arr => {
				foreach (var item in ItemDatabase.ItemList._values) {
					arr.Add(item.ToJson());
				}
			}));

			var json = root.ToString(Formatting.Indented);
			File.WriteAllText(Path.Combine(path, "items.json"), json);
			
			context.Source.SendMessage("Exported item data to 'IdlePlus/export/items.json'.");
		}

		private static void HandleExportTasks(CommandContext<CommandSender> context) {
			if (true) return;
			var path = Path.Combine(BepInEx.Paths.PluginPath, "IdlePlus", "export");
			Directory.CreateDirectory(path);

			var root = new JObject();
			root.Add("exported_at", new JValue($"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.FFFZ}"));
			root.Add("version", new JObject(
				new JProperty("config", SettingsDatabase.SharedSettings.ConfigVersion),
				new JProperty("latest_build", SettingsDatabase.SharedSettings.LatestBuildVersion),
				new JProperty("required_build", SettingsDatabase.SharedSettings.RequiredBuildVersion))
			);
			
			root.Add("tasks", new JArray().Do(arr => {
				foreach (var entry in TaskDatabase.Tasks) {
					var type = entry.key;
				}
			}));
		}
		
		/*
		 * Say
		 */

		private static void HandlePrint(CommandContext<CommandSender> context) {
			var sender = context.Source;
			var message = context.GetArgument<string>("message");
			sender.SendMessage(message);
		}
		
		private static void HandleSay(CommandContext<CommandSender> context) {
			var sender = context.Source;
			var message = context.GetArgument<string>("message");
			message = $"[00:00:00] [TAG] {PlayerData.Instance.Username}: {message}";
			sender.SendMessage(message, mode: GameMode.Default, premium: true, moderator: true);
		}

        /*
         * Webhook Handlers
         */

        private static int HandleWebhookTest(CommandContext<CommandSender> context) {
            try {
                IdleLog.Info("[DevCommand] Running webhook tests...");
                WebhookTests.RunTests();
                IdleLog.Info("[DevCommand] Webhook tests queued successfully.");
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error running webhook tests: {ex.Message}");
                return 0;
            }
        }

        private static int HandleWebhookStartRepeater(CommandContext<CommandSender> context, int intervalSeconds) {
            try {
                bool started = WebhookTests.StartTestRepeater(intervalSeconds);
                
                if (started) {
                    IdleLog.Info($"[DevCommand] Started webhook test repeater with {intervalSeconds}s interval.");
                } else {
                    IdleLog.Info("[DevCommand] Webhook test repeater is already running.");
                }
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error starting webhook repeater: {ex.Message}");
                return 0;
            }
        }

        private static int HandleWebhookStopRepeater(CommandContext<CommandSender> context) {
            try {
                bool stopped = WebhookTests.StopTestRepeater();
                
                if (stopped) {
                    IdleLog.Info("[DevCommand] Webhook test repeater stopped.");
                } else {
                    IdleLog.Info("[DevCommand] No webhook test repeater was running.");
                }
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error stopping webhook repeater: {ex.Message}");
                return 0;
            }
        }

        private static int HandleWebhookStatus(CommandContext<CommandSender> context) {
            try {
                bool isRunning = WebhookTests.IsRepeaterRunning();
                int queuedCount = WebhookManager.GetQueuedRequestCount();
                
                if (isRunning) {
                    int interval = WebhookTests.GetRepeaterInterval();
                    IdleLog.Info($"[DevCommand] Webhook test repeater is running with a {interval}s interval. Queued requests: {queuedCount}");
                } else {
                    IdleLog.Info($"[DevCommand] No webhook test repeater is currently running. Queued requests: {queuedCount}");
                }
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error checking webhook status: {ex.Message}");
                return 0;
            }
        }

        private static int HandleWebhookStats(CommandContext<CommandSender> context) {
            try {
                string report = WebhookMetrics.GetReport();
                IdleLog.Info($"[DevCommand] Webhook Statistics:\n{report}");
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error getting webhook stats: {ex.Message}");
                return 0;
            }
        }

        private static int HandleWebhookStatsReset(CommandContext<CommandSender> context) {
            try {
                WebhookMetrics.Reset();
                IdleLog.Info("[DevCommand] Webhook statistics have been reset.");
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error resetting webhook stats: {ex.Message}");
                return 0;
            }
        }

        private static int HandleWebhookCleanup(CommandContext<CommandSender> context) {
            try {
                WebhookManager.CleanupAsync().ContinueWith(task => {
                    if (task.Exception != null) {
                        IdleLog.Error($"[DevCommand] Error during webhook cleanup: {task.Exception.Message}");
                    }
                });
                IdleLog.Info("[DevCommand] Webhook resources are being cleaned up.");
                return 1;
            } catch (Exception ex) {
                IdleLog.Error($"[DevCommand] Error during webhook cleanup: {ex.Message}");
                return 0;
            }
        }
    }
}