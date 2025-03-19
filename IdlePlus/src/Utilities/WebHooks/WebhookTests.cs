using System.Collections.Generic;
using IdlePlus.Utilities;

namespace IdlePlus.Utilities  // Behalten Sie denselben Namespace wie vorher bei
{
    /// <summary>
    /// Contains test cases for the webhook functionality.
    /// </summary>
    public static class WebhookTests
    {
        /// <summary>
        /// Runs test cases for sending webhook requests.
        /// </summary>
        public static void RunTests()
        {
            IdleLog.Info("[WebhookTests] Starting webhook tests...");

            // Test 1: Call with an Il2CppSystem-compatible JSON string.
            IdleLog.Debug("[WebhookTests] Test 1: Il2CppSystem-compatible JSON string");
            WebhookManager.AddSendWebhook(
                WebhookType.MarketData,
                new Dictionary<string, string>
                {
                    { "action", "testIl2cppString" }
                },
                "[{\"dummy\":true}]"
            );

            // Test 2: Call with null JSON data (should simply send a GET request).
            IdleLog.Debug("[WebhookTests] Test 2: Null JSON data");
            WebhookManager.AddSendWebhook(
                WebhookType.MarketData,
                new Dictionary<string, string>
                {
                    { "action", "testGet" }
                },
                null
            );

            // Test 3: Call with a valid JSON string.
            IdleLog.Debug("[WebhookTests] Test 3: Valid JSON string");
            string validJson = "{\"price\":123,\"currency\":\"USD\"}";
            WebhookManager.AddSendWebhook(
                WebhookType.MarketData,
                new Dictionary<string, string>
                {
                    { "action", "update" }
                },
                validJson
            );

            // Test 4: Call with an invalid JSON string (expected to fail).
            IdleLog.Debug("[WebhookTests] Test 4: Invalid JSON string (expected to fail)");
            WebhookManager.AddSendWebhook(
                WebhookType.MarketData,
                new Dictionary<string, string>
                {
                    { "action", "testInvalid" }
                },
                "asdf"
            );

            IdleLog.Info("[WebhookTests] All webhook tests queued successfully");
        }
        
        /// <summary>
        /// Runs a single test with the specified parameters.
        /// Useful for targeted testing.
        /// </summary>
        public static void RunSingleTest(WebhookType type, string action, string jsonData = null)
        {
            IdleLog.Info($"[WebhookTests] Running single test: Type={type}, Action={action}");
            
            WebhookManager.AddSendWebhook(
                type,
                new Dictionary<string, string>
                {
                    { "action", action }
                },
                jsonData
            );
            
            IdleLog.Info("[WebhookTests] Single test queued successfully");
        }
    }
}