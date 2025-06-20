using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BepInEx.Configuration;
using HarmonyLib;
using IdlePlus.Utilities;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace IdlePlus.Patches
{
    public class DebugExplorer
    {
        // Config entries
        private static ConfigEntry<KeyCode> debugKey;
        private static ConfigEntry<KeyCode> deepAnalysisKey;
        private static ConfigEntry<KeyCode> trackEventsKey;
        private static ConfigEntry<KeyCode> quickSearchKey;
        private static ConfigEntry<bool> logAllUIChanges;
        private static ConfigEntry<bool> logToBepInEx;
        
        // Debug log file
        private static string debugLogPath;
        private static StreamWriter debugLogWriter;
        
        private static HashSet<string> loggedComponents = new HashSet<string>();
        private static List<string> eventLog = new List<string>();
        
        public static void Initialize(ConfigFile config)
        {
            // Config setup
            debugKey = config.Bind("Debug", "DebugKey", KeyCode.F9, "Key to analyze current UI");
            deepAnalysisKey = config.Bind("Debug", "DeepAnalysisKey", KeyCode.F10, "Key for deep GameObject analysis");
            trackEventsKey = config.Bind("Debug", "TrackEventsKey", KeyCode.F11, "Toggle event tracking");
            quickSearchKey = config.Bind("Debug", "QuickSearchKey", KeyCode.F12, "Quick search for specific components");
            logAllUIChanges = config.Bind("Debug", "LogAllUIChanges", false, "Log all UI instantiations");
            logToBepInEx = config.Bind("Debug", "LogToBepInEx", false, "Also log to BepInEx console (can cause lag with many messages)");
            
            // Setup debug log file
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            debugLogPath = Path.Combine(BepInEx.Paths.ConfigPath, $"IdlePlus_Debug_{timestamp}.log");
            
            try
            {
                debugLogWriter = new StreamWriter(debugLogPath, true) { AutoFlush = true };
                LogDebug("=== IdlePlus Debug Explorer Started (IL2CPP Version) ===");
                LogDebug($"Debug log file: {debugLogPath}");
                LogDebug("Keys: F9=UI Analysis, F10=Deep Analysis, F11=Event Tracking, F12=Quick Search");
                IdleLog.Info("Debug Explorer initialized! Debug log: {}", debugLogPath);
            }
            catch (Exception e)
            {
                IdleLog.Error("Failed to create debug log file", e);
            }
        }
        
        private static void LogDebug(string message, bool forceConsole = false)
        {
            try
            {
                debugLogWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }
            catch { }
            
            if (logToBepInEx.Value || forceConsole)
            {
                IdleLog.Info(message);
            }
        }
        
        private static void LogWarning(string message, bool forceConsole = false)
        {
            try
            {
                debugLogWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [!] {message}");
            }
            catch { }
            
            if (logToBepInEx.Value || forceConsole)
            {
                IdleLog.Warn(message);
            }
        }
        
        private static void LogError(string message, Exception e = null)
        {
            try
            {
                debugLogWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] {message}");
                if (e != null)
                {
                    debugLogWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ERROR] {e}");
                }
            }
            catch { }
            
            if (e != null)
            {
                IdleLog.Error(message, e);
            }
            else
            {
                IdleLog.Error(message);
            }
        }
        
        public static void Shutdown()
        {
            try
            {
                LogDebug("=== Debug Explorer Shutdown ===");
                debugLogWriter?.Close();
                debugLogWriter?.Dispose();
            }
            catch { }
        }
        
        public static void OnUpdate()
        {
            try
            {
                if (Input.GetKeyDown(debugKey.Value))
                {
                    SafeExecute("UI Analysis", AnalyzeCurrentUI);
                }
                
                if (Input.GetKeyDown(deepAnalysisKey.Value))
                {
                    SafeExecute("Deep Analysis", DeepAnalysis);
                }
                
                if (Input.GetKeyDown(trackEventsKey.Value))
                {
                    logAllUIChanges.Value = !logAllUIChanges.Value;
                    LogWarning($"Event tracking is now: {(logAllUIChanges.Value ? "ON" : "OFF")}", true);
                }
                
                if (Input.GetKeyDown(quickSearchKey.Value))
                {
                    SafeExecute("Quick Search", QuickSearch);
                }
            }
            catch (Exception e)
            {
                LogError("Error in OnUpdate", e);
            }
        }
        
        private static void SafeExecute(string actionName, Action action)
        {
            try
            {
                LogDebug($"Starting {actionName}...");
                action();
                LogDebug($"{actionName} completed");
            }
            catch (Exception e)
            {
                LogError($"Error during {actionName}", e);
            }
        }
        
        private static void AnalyzeCurrentUI()
        {
            LogWarning("=== STARTING UI ANALYSIS (IL2CPP Safe) ===");
            
            try
            {
                // 1. Finde alle aktiven Canvas-Objekte (IL2CPP safe)
                var allCanvas = Resources.FindObjectsOfTypeAll<Canvas>();
                LogDebug($"Found {allCanvas.Length} Canvas objects (including inactive)");
                
                int activeCount = 0;
                foreach (var canvas in allCanvas)
                {
                    try
                    {
                        if (canvas != null && canvas.gameObject != null && canvas.gameObject.activeInHierarchy)
                        {
                            activeCount++;
                            LogDebug($"\nCanvas: {canvas.name} (active)");
                            AnalyzeGameObjectSafe(canvas.gameObject, 1);
                        }
                    }
                    catch (Exception e)
                    {
                        LogDebug($"Error analyzing canvas: {e.Message}");
                    }
                }
                LogDebug($"Active canvas count: {activeCount}");
            }
            catch (Exception e)
            {
                LogError("Error finding Canvas objects", e);
            }
            
            // 2. Suche nach Text-Komponenten
            try
            {
                LogWarning("\n=== SEARCHING FOR TEXT COMPONENTS ===");
                var allTexts = Resources.FindObjectsOfTypeAll<Text>();
                LogDebug($"Found {allTexts.Length} Text components");
                
                var potentialNames = new List<string>();
                foreach (var text in allTexts)
                {
                    try
                    {
                        if (text != null && text.gameObject != null && text.gameObject.activeInHierarchy)
                        {
                            var content = text.text;
                            if (!string.IsNullOrEmpty(content) && content.Length > 2 && content.Length < 30 
                                && !content.Contains(" ") && !content.Contains("\n"))
                            {
                                potentialNames.Add($"'{content}' in {GetGameObjectPathSafe(text.gameObject)}");
                            }
                        }
                    }
                    catch { }
                }
                
                LogDebug($"\nPotential player names found: {potentialNames.Count}");
                foreach (var name in potentialNames.Take(20))
                {
                    LogDebug($"  {name}");
                }
            }
            catch (Exception e)
            {
                LogError("Error searching text components", e);
            }
            
            LogWarning("=== UI ANALYSIS COMPLETE ===");
            LogDebug($"Check the debug log file for full results: {debugLogPath}", true);
        }
        
        private static void DeepAnalysis()
        {
            LogWarning("=== DEEP ANALYSIS MODE (IL2CPP) ===");
            
            // Suche nach Komponenten mit bestimmten Namen
            try
            {
                LogWarning("\n=== SEARCHING FOR SPECIFIC COMPONENTS ===");
                
                string[] searchTerms = { "Clan", "Event", "Party", "Skill", "Member", "Manager", "Controller" };
                
                foreach (var term in searchTerms)
                {
                    LogDebug($"\nSearching for components containing: {term}");
                    
                    try
                    {
                        // Nutze Resources.FindObjectsOfTypeAll für IL2CPP
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        var found = 0;
                        
                        foreach (var go in allObjects)
                        {
                            try
                            {
                                if (go != null && go.name.Contains(term))
                                {
                                    found++;
                                    LogDebug($"  GameObject: {go.name} (active: {go.activeInHierarchy})");
                                    
                                    // Versuche Komponenten zu finden
                                    var components = go.GetComponents<Component>();
                                    foreach (var comp in components)
                                    {
                                        if (comp != null)
                                        {
                                            var typeName = comp.GetIl2CppType().Name;
                                            if (typeName != "Transform" && typeName != "RectTransform")
                                            {
                                                LogDebug($"    -> Component: {typeName}");
                                            }
                                        }
                                    }
                                    
                                    if (found >= 10) break; // Limitiere Output
                                }
                            }
                            catch { }
                        }
                        
                        LogDebug($"  Total found: {found}");
                    }
                    catch (Exception e)
                    {
                        LogDebug($"  Error searching for {term}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                LogError("Error in deep analysis", e);
            }
            
            // Analysiere spezifische bekannte Manager
            try
            {
                LogWarning("\n=== LOOKING FOR KNOWN MANAGERS ===");
                
                // Suche nach Singleton-Pattern
                var singletonTypes = new[] { "NetworkManager", "GameManager", "ClanManager", "EventManager", "ChatManager" };
                
                foreach (var typeName in singletonTypes)
                {
                    try
                    {
                        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
                        foreach (var go in objects)
                        {
                            if (go.name.Contains(typeName))
                            {
                                LogDebug($"Found potential manager: {go.name}");
                                AnalyzeGameObjectSafe(go, 0, true);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                LogError("Error searching for managers", e);
            }
            
            LogWarning("=== DEEP ANALYSIS COMPLETE ===");
            LogDebug($"Full results saved to: {debugLogPath}", true);
        }
        
        private static void QuickSearch()
        {
            LogWarning("=== QUICK SEARCH (IL2CPP) ===");
            
            try
            {
                // Suche nach Event-UI
                LogDebug("\nSearching for Event UI elements...");
            //     var eventObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            //         .Where(go => go != null && (
            //             go.name.ToLower().Contains("event") ||
            //             go.name.ToLower().Contains("party") ||
            //             go.name.ToLower().Contains("skill") ||
            //             go.name.ToLower().Contains("lobby")
            //         ))
            //         .Take(20)
            //         .ToList();
                
            //     LogDebug($"Found {eventObjects.Count} event-related objects:");
            //     foreach (var go in eventObjects)
            //     {
            //         try
            //         {
            //             LogDebug($"  {go.name} (active: {go.activeInHierarchy})");
                        
            //             // Suche nach Text-Komponenten in Kindern
            //             var texts = go.GetComponentsInChildren<Text>();
            //             foreach (var text in texts)
            //             {
            //                 if (!string.IsNullOrEmpty(text.text))
            //                 {
            //                     LogDebug($"    Text: '{text.text}'");
            //                 }
            //             }
            //         }
            //         catch { }
            //     }
                
            //     // Suche nach Clan-UI
            //     LogDebug("\nSearching for Clan UI elements...");
            //     var clanObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            //         .Where(go => go != null && go.name.ToLower().Contains("clan"))
            //         .Take(20)
            //         .ToList();
                
            //     LogDebug($"Found {clanObjects.Count} clan-related objects:");
            //     foreach (var go in clanObjects)
            //     {
            //         try
            //         {
            //             LogDebug($"  {GetGameObjectPathSafe(go)} (active: {go.activeInHierarchy})");
            //         }
            //         catch { }
            //     }
            }
            catch (Exception e)
            {
                LogError("Error in quick search", e);
            }

			// AnalyzeGuildData();

			LogWarning("\n=== READING CLAN MEMBERS ===");
			// ReadClanMembersDetailed();
			TestOnlineDetectionDebug();
			TestOnlineDetectionFinal();
			// DebugMemberComponents();
            
            LogWarning("=== QUICK SEARCH COMPLETE ===");
            LogDebug($"Results saved to: {debugLogPath}", true);
        }

		// Ersetze die TestOnlineDetection Methode mit dieser Version:

// Debug-Version um zu sehen was passiert:

private static void TestOnlineDetectionDebug()
{
    LogWarning("=== TEST ONLINE DETECTION (DEBUG) ===");
    
    try
    {
        var guildPage = GameObject.Find("GameCanvas/PageCanvas/GuildPage");
        if (guildPage == null)
        {
            LogDebug("GuildPage not found");
            return;
        }
        
        var scrollContent = guildPage.transform.Find("Container/InClanView/Panels/GuildPanel/MemberListPanel/MemberContainer/Scroll View/Viewport/Content");
        if (scrollContent == null)
        {
            LogDebug("Member list not found");
            return;
        }
        
        LogDebug($"Found member list with {scrollContent.childCount} entries\n");
        
        var onlineMembers = new List<string>();
        var offlineMembers = new List<string>();
        
        // Test nur die ersten 5 Member für bessere Übersicht
        int testCount = Mathf.Min(5, scrollContent.childCount);
        
        for (int i = 0; i < testCount; i++)
        {
            try
            {
                var child = scrollContent.GetChild(i);
                if (child == null) 
                {
                    LogDebug($"[{i}] Child is null!");
                    continue;
                }
                
                string playerName = child.name;
                LogDebug($"\n[{i}] Processing: {playerName}");
                
                // Liste ALLE Komponenten auf
                var components = child.GetComponents<Component>();
                LogDebug($"  Total components: {components.Length}");
                
                bool foundProceduralImage = false;
                bool isOnline = false;
                
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        LogDebug("  - Found null component");
                        continue;
                    }
                    
                    var typeName = comp.GetIl2CppType().Name;
                    LogDebug($"  - Component: {typeName}");
                    
                    if (typeName == "ProceduralImage")
                    {
                        foundProceduralImage = true;
                        LogDebug("    -> This is ProceduralImage!");
                        
                        // Versuche verschiedene Casts
                        if (comp is Behaviour behaviour)
                        {
                            LogDebug($"    -> Cast to Behaviour successful");
                            LogDebug($"    -> enabled = {behaviour.enabled}");
                            isOnline = !behaviour.enabled;
                        }
                        else if (comp is MonoBehaviour monoBehaviour)
                        {
                            LogDebug($"    -> Cast to MonoBehaviour successful");
                            LogDebug($"    -> enabled = {monoBehaviour.enabled}");
                            isOnline = !monoBehaviour.enabled;
                        }
                        else
                        {
                            LogDebug($"    -> Could not cast to Behaviour or MonoBehaviour!");
                            
                            // Versuche über Il2CppSystem
                            try
                            {
                                var enabledProp = comp.GetIl2CppType().GetProperty("enabled");
                                if (enabledProp != null)
                                {
                                    var value = enabledProp.GetValue(comp);
                                    LogDebug($"    -> Got enabled via property: {value}");
                                    // Versuche den Wert zu konvertieren
                                    if (value != null)
                                    {
                                        var valueStr = value.ToString();
                                        LogDebug($"    -> enabled as string: {valueStr}");
                                        if (valueStr.ToLower() == "false")
                                        {
                                            isOnline = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                LogDebug($"    -> Error getting enabled property: {e.Message}");
                            }
                        }
                        
                        LogDebug($"    -> Result: Player is {(isOnline ? "ONLINE" : "OFFLINE")}");
                    }
                }
                
                if (!foundProceduralImage)
                {
                    LogDebug("  WARNING: No ProceduralImage found!");
                }
                
                // Clean name
                string cleanName = ExtractPlayerName(playerName);
                LogDebug($"  Clean name: {cleanName}");
                
                if (isOnline)
                {
                    onlineMembers.Add(cleanName);
                }
                else
                {
                    offlineMembers.Add(cleanName);
                }
            }
            catch (Exception e)
            {
                LogError($"Error processing member {i}", e);
            }
        }
        
        // Specific test for And0rk3
        LogWarning("\n=== SPECIFIC CHECK FOR And0rk3 ===");
        for (int i = 0; i < scrollContent.childCount; i++)
        {
            var child = scrollContent.GetChild(i);
            if (child != null && child.name.Contains("And0rk3"))
            {
                LogDebug($"Found And0rk3 at index {i}: {child.name}");
                
                var components = child.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp != null)
                    {
                        var typeName = comp.GetIl2CppType().Name;
                        
                        if (typeName == "ProceduralImage")
                        {
                            LogDebug("  ProceduralImage found!");
                            
                            // Teste alle möglichen Basisklassen
                            LogDebug($"    -> Is Behaviour: {comp is Behaviour}");
                            LogDebug($"    -> Is MonoBehaviour: {comp is MonoBehaviour}");
                            LogDebug($"    -> Is Graphic: {comp is Graphic}");
                            LogDebug($"    -> Is UIBehaviour: {comp is UIBehaviour}");
                            LogDebug($"    -> Is MaskableGraphic: {comp is MaskableGraphic}");
                            
                            // Wenn es ein Graphic ist
                            if (comp is Graphic graphic)
                            {
                                LogDebug($"    -> Graphic.enabled = {graphic.enabled}");
                                LogDebug($"    -> Graphic.color = {graphic.color}");
                            }
                            
                            // Wenn es ein MaskableGraphic ist (Image erbt davon)
                            if (comp is MaskableGraphic maskableGraphic)
                            {
                                LogDebug($"    -> MaskableGraphic.enabled = {maskableGraphic.enabled}");
                            }
                        }
                    }
                }
                break;
            }
        }
        
        LogWarning($"\n=== DEBUG SUMMARY ===");
        LogDebug($"Tested {testCount} members");
        LogDebug($"Online: {onlineMembers.Count}");
        LogDebug($"Offline: {offlineMembers.Count}");
    }
    catch (Exception e)
    {
        LogError("Error in TestOnlineDetectionDebug", e);
    }
}

// Finale Version die NUR ProceduralImage checkt:

private static void TestOnlineDetectionFinal()
{
    LogWarning("=== TEST ONLINE DETECTION (FINAL) ===");
    
    try
    {
        var guildPage = GameObject.Find("GameCanvas/PageCanvas/GuildPage");
        if (guildPage == null)
        {
            LogDebug("GuildPage not found - are you in the Clan view?");
            return;
        }
        
        var scrollContent = guildPage.transform.Find("Container/InClanView/Panels/GuildPanel/MemberListPanel/MemberContainer/Scroll View/Viewport/Content");
        if (scrollContent == null)
        {
            LogDebug("Member list not found");
            return;
        }
        
        LogDebug($"Found member list with {scrollContent.childCount} entries\n");
        
        var onlineMembers = new List<string>();
        var offlineMembers = new List<string>();
        
        int childCount = scrollContent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            try
            {
                var child = scrollContent.GetChild(i);
                if (child == null) continue;
                
                string playerName = child.name;
                bool isOnline = false;
                bool foundProceduralImage = false;
                
                // Get ALL components and find ProceduralImage specifically
                var components = child.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetIl2CppType().Name == "ProceduralImage")
                    {
                        foundProceduralImage = true;
                        
                        // ProceduralImage inherits from Behaviour
                        if (comp is Behaviour behaviour)
                        {
                            isOnline = !behaviour.enabled;  // disabled = online
                            LogDebug($"[{i}] {playerName}");
                            LogDebug($"  ProceduralImage.enabled = {behaviour.enabled}");
                            LogDebug($"  => Player is {(isOnline ? "ONLINE" : "OFFLINE")}");
                        }
                        break; // Found it, no need to check other components
                    }
                }
                
                if (!foundProceduralImage)
                {
                    LogDebug($"[{i}] {playerName} - WARNING: No ProceduralImage found!");
                    continue;
                }
                
                // Clean up the player name
                string cleanName = ExtractPlayerName(playerName);
                
                // Add to appropriate list
                if (isOnline)
                {
                    onlineMembers.Add(cleanName);
                }
                else
                {
                    offlineMembers.Add(cleanName);
                }
            }
            catch (Exception e)
            {
                LogDebug($"Error processing member {i}: {e.Message}");
            }
        }
        
        // Summary
        LogWarning($"\n=== SUMMARY ===");
        LogWarning($"Total members: {childCount}");
        
        LogWarning($"\nONLINE ({onlineMembers.Count}):");
        foreach (var member in onlineMembers)
        {
            LogDebug($"  ✓ {member}");
        }
        
        LogWarning($"\nOFFLINE ({offlineMembers.Count}):");
        foreach (var member in offlineMembers)
        {
            LogDebug($"  ✗ {member}");
        }
    }
    catch (Exception e)
    {
        LogError("Error in TestOnlineDetectionFinal", e);
    }
}

// Helper method to extract clean player name
private static string ExtractPlayerName(string rawName)
{
    // Remove rank number (e.g., "4. ")
    if (rawName.Contains(". "))
    {
        var parts = rawName.Split(new[] { ". " }, 2, StringSplitOptions.None);
        if (parts.Length > 1)
        {
            rawName = parts[1];
        }
    }
    
    // Remove role suffix (e.g., " - Anführer")
    if (rawName.Contains(" - Anführer"))
    {
        rawName = rawName.Replace(" - Anführer", "");
    }
    
    // Remove offline time (e.g., " - 12h")
    if (rawName.Contains(" - ") && rawName[rawName.Length - 1] == 'h')
    {
        var lastDash = rawName.LastIndexOf(" - ");
        if (lastDash > 0)
        {
            rawName = rawName.Substring(0, lastDash);
        }
    }
    
    return rawName.Trim();
}

private static void ReadClanMembersDetailed()
{
    LogWarning("=== READING CLAN MEMBERS (DETAILED) ===");
    
    try
    {
        // Find the GuildPage
        var guildPage = GameObject.Find("GameCanvas/PageCanvas/GuildPage");
        if (guildPage == null)
        {
            LogDebug("GuildPage not found - are you in the Clan view?");
            return;
        }
        
        // Find the member list
        var memberContainer = guildPage.transform.Find("Container/InClanView/Panels/GuildPanel/MemberListPanel/MemberContainer");
        if (memberContainer == null)
        {
            LogDebug("MemberContainer not found");
            return;
        }
        
        var scrollContent = memberContainer.Find("Scroll View/Viewport/Content");
        if (scrollContent == null)
        {
            LogDebug("Scroll content not found");
            return;
        }
        
        LogDebug($"Found member list with {scrollContent.childCount} entries");
        
        // Let's focus on your entry (And0rk3)
        int childCount = scrollContent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            try
            {
                var child = scrollContent.GetChild(i);
                if (child == null) continue;
                
                // Get the name first
                string playerName = null;
                var allComponents = child.GetComponentsInChildren<Component>();
                foreach (var comp in allComponents)
                {
                    if (comp != null && comp.GetIl2CppType().Name.Contains("TextMesh"))
                    {
                        try
                        {
                            var textProp = comp.GetIl2CppType().GetProperty("text");
                            if (textProp != null)
                            {
                                var textValue = textProp.GetValue(comp);
                                if (textValue != null)
                                {
                                    playerName = textValue.ToString();
                                }
                            }
                        }
                        catch { }
                    }
                }
                
                // Only detailed log for And0rk3
                if (playerName != null && playerName.Contains("And0rk3"))
                {
                    LogWarning($"\n=== DETAILED ANALYSIS FOR: {playerName} ===");
                    LogDebug($"GameObject name: {child.name}");
                    
                    // Log the full hierarchy
                    LogDebug("\nGameObject Hierarchy:");
                    LogGameObjectHierarchy(child, 0);
                    
                    // Log ALL components
                    LogDebug("\nALL Components in hierarchy:");
                    var allComps = child.GetComponentsInChildren<Component>();
                    int compIndex = 0;
                    foreach (var comp in allComps)
                    {
                        if (comp != null)
                        {
                            LogDebug($"  [{compIndex++}] {comp.GetIl2CppType().Name} on {comp.gameObject.name}");
                        }
                    }
                    
                    // Log ALL Image components and their colors
                    LogDebug("\nALL Image components:");
                    var images = child.GetComponentsInChildren<Image>();
                    int imgIndex = 0;
                    foreach (var img in images)
                    {
                        if (img != null)
                        {
                            LogDebug($"  Image[{imgIndex++}] on '{img.gameObject.name}':");
                            LogDebug($"    Color: {img.color} (R:{img.color.r:F3}, G:{img.color.g:F3}, B:{img.color.b:F3}, A:{img.color.a:F3})");
                            LogDebug($"    Enabled: {img.enabled}");
                            
                            // Check if it has a parent with color
                            if (img.transform.parent != null)
                            {
                                var parentImg = img.transform.parent.GetComponent<Image>();
                                if (parentImg != null)
                                {
                                    LogDebug($"    Parent has Image with color: {parentImg.color}");
                                }
                            }
                        }
                    }
                    
                    // Check Text color
                    LogDebug("\nText component colors:");
                    foreach (var comp in allComps)
                    {
                        if (comp != null && comp.GetIl2CppType().Name.Contains("TextMesh"))
                        {
                            try
                            {
                                // Try to get color property
                                var colorProp = comp.GetIl2CppType().GetProperty("color");
                                if (colorProp != null)
                                {
                                    var colorValue = colorProp.GetValue(comp);
                                    if (colorValue != null)
                                    {
                                        LogDebug($"  TextMesh color: {colorValue}");
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    
                    // Look for any CanvasRenderer components
                    LogDebug("\nCanvasRenderer components:");
                    var canvasRenderers = child.GetComponentsInChildren<CanvasRenderer>();
                    foreach (var cr in canvasRenderers)
                    {
                        if (cr != null)
                        {
                            try
                            {
                                var color = cr.GetColor();
                                LogDebug($"  CanvasRenderer on '{cr.gameObject.name}' color: {color}");
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogDebug($"Error processing child {i}: {e.Message}");
            }
        }
    }
    catch (Exception e)
    {
        LogError("Error in detailed reading", e);
    }
}

private static void LogGameObjectHierarchy(Transform transform, int depth)
{
    if (depth > 5) return; // Prevent too deep recursion
    
    string indent = new string(' ', depth * 2);
    LogDebug($"{indent}{transform.name}");
    
    for (int i = 0; i < transform.childCount; i++)
    {
        var child = transform.GetChild(i);
        if (child != null)
        {
            LogGameObjectHierarchy(child, depth + 1);
        }
    }
}
		private static bool IsColorSimilar(Color a, Color b, float tolerance = 0.05f) {
			return Mathf.Abs(a.r - b.r) < tolerance &&
				Mathf.Abs(a.g - b.g) < tolerance &&
				Mathf.Abs(a.b - b.b) < tolerance;
		}
		
		private static void AnalyzeGuildData() {
			LogWarning("=== ANALYZING GUILD/CLAN DATA ===");

			try {
				// 1. Finde den GuildManager
				LogDebug("\n--- Looking for GuildManager ---");
				var guildManagers = Resources.FindObjectsOfTypeAll<GameObject>()
					.Where(go => go.name == "GuildManager")
					.ToList();

				foreach (var gm in guildManagers) {
					LogDebug($"Found GuildManager: {gm.name} (active: {gm.activeInHierarchy})");
					var components = gm.GetComponents<Component>();
					foreach (var comp in components) {
						if (comp != null) {
							var typeName = comp.GetIl2CppType().Name;
							LogDebug($"  Component: {typeName}");

							// Versuche Felder zu lesen (vorsichtig bei IL2CPP)
							if (typeName == "GuildManager" || typeName == "GuildListener") {
								LogDebug($"  -> This is our target! Type: {typeName}");
							}
						}
					}
				}

				// 2. Finde die GuildPage und analysiere Member-Listen
				LogDebug("\n--- Looking for GuildPage Members ---");
				var guildPages = Resources.FindObjectsOfTypeAll<GameObject>()
					.Where(go => go.name == "GuildPage")
					.ToList();

				foreach (var page in guildPages) {
					LogDebug($"Found GuildPage: {GetGameObjectPathSafe(page)}");

					// Suche nach MembersScrollView oder ähnlichen Containern
					var allChildren = page.GetComponentsInChildren<Transform>(true);
					foreach (var child in allChildren) {
						var name = child.name.ToLower();
						if (name.Contains("member") || name.Contains("scroll") || name.Contains("list")) {
							LogDebug($"  Member container: {GetGameObjectPathSafe(child.gameObject)}");

							// Suche nach Member-Einträgen
							var memberEntries = child.GetComponentsInChildren<Transform>(true)
								.Where(t => t.name.Contains("Member") || t.name.Contains("Entry") || t.name.Contains("Player"))
								.Take(5);

							foreach (var entry in memberEntries) {
								LogDebug($"    Entry: {entry.name}");

								// Versuche Farben zu finden
								var images = entry.GetComponentsInChildren<Image>();
								foreach (var img in images) {
									if (img.color != Color.white) {
										LogDebug($"      Image color: {img.color} (R:{img.color.r}, G:{img.color.g}, B:{img.color.b})");
									}
								}

								// Suche nach Text (Namen)
								var texts = entry.GetComponentsInChildren<Text>();
								foreach (var text in texts) {
									if (!string.IsNullOrEmpty(text.text)) {
										LogDebug($"      Text: '{text.text}' Color: {text.color}");
									}
								}

								// TextMeshPro?
								var tmpComponents = entry.GetComponents<Component>();
								foreach (var comp in tmpComponents) {
									if (comp != null && comp.GetIl2CppType().Name.Contains("TextMesh")) {
										LogDebug($"      Found TextMesh component: {comp.GetIl2CppType().Name}");
									}
								}
							}
						}
					}
				}

				// 3. Suche nach Event-Party Views
				LogDebug("\n--- Looking for Event/Party Views ---");
				var partyViews = Resources.FindObjectsOfTypeAll<GameObject>()
					.Where(go => go.name.Contains("Party") || go.name.Contains("Event"))
					.Take(10)
					.ToList();

				foreach (var view in partyViews) {
					if (view.name.Contains("GuildEvent") || view.name.Contains("PartyView")) {
						LogDebug($"Event View: {GetGameObjectPathSafe(view)} (active: {view.activeInHierarchy})");

						// Komponenten analysieren
						var components = view.GetComponents<Component>();
						foreach (var comp in components) {
							if (comp != null) {
								var typeName = comp.GetIl2CppType().Name;
								if (typeName != "Transform" && typeName != "RectTransform") {
									LogDebug($"  -> Component: {typeName}");
								}
							}
						}
					}
				}

			} catch (Exception e) {
				LogError("Error analyzing guild data", e);
			}
		}
        
        private static void AnalyzeGameObjectSafe(GameObject go, int depth, bool detailed = false) {
			if (depth > 3) return;

			try {
				string indent = new string(' ', depth * 2);
				var components = go.GetComponents<Component>();

				var relevantComps = new List<string>();
				foreach (var comp in components) {
					try {
						if (comp != null) {
							var typeName = comp.GetIl2CppType().Name;
							if (typeName != "Transform" && typeName != "RectTransform" && typeName != "CanvasRenderer") {
								relevantComps.Add(typeName);
							}
						}
					} catch { }
				}

				if (relevantComps.Count > 0 || go.name.ToLower().Contains("player") ||
					go.name.ToLower().Contains("member") || go.name.ToLower().Contains("list")) {
					LogDebug($"{indent}{go.name} [{string.Join(", ", relevantComps)}]");

					// Text-Komponente Details
					try {
						var text = go.GetComponent<Text>();
						if (text != null && !string.IsNullOrEmpty(text.text)) {
							LogDebug($"{indent}  Text: '{text.text}'");
						}
					} catch { }

					if (detailed) {
						// Versuche mehr Details zu bekommen
						foreach (var comp in components) {
							try {
								if (comp != null) {
									var typeName = comp.GetIl2CppType().Name;
									LogDebug($"{indent}  Component: {typeName}");
								}
							} catch { }
						}
					}
				}

				// Rekursiv durch Kinder (vorsichtig bei IL2CPP)
				if (depth < 3) {
					for (int i = 0; i < go.transform.childCount; i++) {
						try {
							var child = go.transform.GetChild(i);
							if (child != null && child.gameObject != null) {
								AnalyzeGameObjectSafe(child.gameObject, depth + 1, detailed);
							}
						} catch { }
					}
				}
			} catch (Exception e) {
				LogDebug($"Error analyzing GameObject: {e.Message}");
			}
		}
        
        private static string GetGameObjectPathSafe(GameObject go)
        {
            try
            {
                string path = go.name;
                Transform parent = go.transform.parent;
                
                int maxDepth = 10; // Verhindere Endlosschleifen
                while (parent != null && maxDepth-- > 0)
                {
                    path = parent.name + "/" + path;
                    parent = parent.parent;
                }
                
                return path;
            }
            catch
            {
                return go?.name ?? "Unknown";
            }
        }
        
        // Patch für Event-Tracking
        [HarmonyPatch(typeof(GameObject), "SetActive")]
        public class SetActiveTrackingPatch
        {
            [HarmonyPostfix]
            static void Postfix(GameObject __instance, bool value)
            {
                try
                {
                    if (!logAllUIChanges.Value || !value) return;
                    
                    var name = __instance.name.ToLower();
                    if (name.Contains("event") || name.Contains("party") || name.Contains("skill") || 
                        name.Contains("clan") || name.Contains("lobby"))
                    {
                        var path = GetGameObjectPathSafe(__instance);
                        if (!loggedComponents.Contains(path))
                        {
                            loggedComponents.Add(path);
                            LogWarning($"[ACTIVATED] {path}");
                            eventLog.Add($"{DateTime.Now:HH:mm:ss} - Activated: {path}");
                        }
                    }
                }
                catch { }
            }
        }
        
        public static void OnGUI()
        {
            try
            {
                if (logAllUIChanges.Value && eventLog.Count > 0)
                {
                    // Zeige die letzten Events auf dem Bildschirm
                    GUI.Box(new Rect(10, 10, 400, 200), "Event Log (Last 10)");
                    
                    var lastEvents = eventLog.Skip(Math.Max(0, eventLog.Count - 10)).ToList();
                    for (int i = 0; i < lastEvents.Count; i++)
                    {
                        GUI.Label(new Rect(15, 30 + i * 18, 390, 18), lastEvents[i]);
                    }
                    
                    // Zeige Pfad zum Debug-Log
                    GUI.Label(new Rect(15, 180, 390, 18), $"Debug log: {Path.GetFileName(debugLogPath)}");
                }
            }
            catch { }
        }
    }
}