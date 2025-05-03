using HarmonyLib;
using IdlePlus.Attributes;
using IdlePlus.Utilities;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace IdlePlus.Patches.Raids {
    [HarmonyPatch]
    public class RaidDebugPatch {
        
        [InitializeOnce]
        private static void Initialize() {
            IdleLog.Info("RaidDebugPatch initialized");
            
            // Logge alle Klassen, die "Raid" oder "Citadel" im Namen haben
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies) {
                try {
                    var assemblyName = assembly.GetName().Name;
                    IdleLog.Info($"Checking assembly: {assemblyName}");
                    
                    foreach (var type in assembly.GetTypes()) {
                        if (type.Name.Contains("Raid") || type.Name.Contains("Citadel")) {
                            IdleLog.Info($"Found raid-related class: {type.FullName} in {assemblyName}");
                            
                            // Wenn es sich um den RaidCitadelBattleManager handelt, logge alle Methoden
                            if (type.Name == "RaidCitadelBattleManager") {
                                IdleLog.Info($"Found RaidCitadelBattleManager in {assemblyName}");
                                LogMethodsAndFields(type);
                                TryPatchMethods(type);
                            }
                        }
                    }
                } catch (Exception ex) {
                    // Manche Assemblies könnten Probleme verursachen, überspringen
                    IdleLog.Error($"Error checking assembly {assembly.GetName().Name}: {ex.Message}");
                }
            }
        }
        
        private static void LogMethodsAndFields(Type type) {
            IdleLog.Info($"Methods in {type.Name}:");
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (method.Name.Contains("Raid") || method.Name.Contains("Phase") || 
                    method.Name.Contains("Start") || method.Name.Contains("End")) {
                    IdleLog.Info($"  - {method.Name}");
                }
            }
            
            IdleLog.Info($"Fields in {type.Name}:");
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (field.Name.Contains("duration") || field.Name.Contains("time") || 
                    field.Name.Contains("phase") || field.Name.Contains("preparation")) {
                    IdleLog.Info($"  - {field.Name}: {field.FieldType}");
                }
            }
        }
        
        private static void TryPatchMethods(Type type) {
            try {
                var harmony = new Harmony("com.idleplus.raidpatch");
                
                // Versuche wichtige Methoden zu patchen
                string[] methodNames = {
                    "StartRaid", "EndRaid", "OnCitadelPhaseChanged",
                    "Start", "Update", "OnEnable"
                };
                
                foreach (var methodName in methodNames) {
                    try {
                        var method = type.GetMethod(methodName, 
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                        if (method != null) {
                            var postfix = typeof(RaidDebugPatch).GetMethod(nameof(GenericPostfix), 
                                BindingFlags.Static | BindingFlags.NonPublic);
                            
                            harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                            IdleLog.Info($"Successfully patched method: {methodName}");
                        }
                    } catch (Exception ex) {
                        IdleLog.Error($"Failed to patch method {methodName}: {ex.Message}");
                    }
                }
            } catch (Exception ex) {
                IdleLog.Error($"Error in TryPatchMethods: {ex.Message}");
            }
        }
        
        private static void GenericPostfix(object __instance, MethodBase __originalMethod, params object[] __args) {
            try {
				return;
			    if (__originalMethod.Name == "Update") {
            		return;
        		}

                IdleLog.Info($"Method called: {__originalMethod.DeclaringType.Name}.{__originalMethod.Name} with {__args.Length} args");
                
                // Log arguments
                for (int i = 0; i < __args.Length; i++) {
                    var argValue = __args[i]?.ToString() ?? "null";
                    IdleLog.Info($"  Arg {i}: {argValue}");
                }
                
                // Wenn es sich um eine Phase-Änderung handelt
                if (__originalMethod.Name == "OnCitadelPhaseChanged" || 
                    __originalMethod.Name.Contains("Phase")) {
                    IdleLog.Info("  This appears to be a phase change event!");
                }
                
                // Wenn es sich um den Start eines Raids handelt
                if (__originalMethod.Name == "StartRaid" || 
                    (__originalMethod.Name == "Start" && __instance.GetType().Name.Contains("Citadel"))) {
                    IdleLog.Info("  This appears to be a raid start event!");
                    
                    // Versuche die Vorbereitungszeit zu finden und zu loggen
                    var prepField = __instance.GetType().GetField("_preparationDuration", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prepField != null) {
                        var prepValue = prepField.GetValue(__instance);
                        IdleLog.Info($"  Preparation duration: {prepValue}");
                    }
                }
            } catch (Exception ex) {
                IdleLog.Error($"Error in generic postfix: {ex.Message}");
            }
        }
    }
}