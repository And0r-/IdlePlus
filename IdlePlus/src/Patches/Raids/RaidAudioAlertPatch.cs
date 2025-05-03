using HarmonyLib;
using IdlePlus.Attributes;
using IdlePlus.Settings;
using IdlePlus.Utilities;
using System;
using UnityEngine;

namespace IdlePlus.Patches.Raids {
    [HarmonyPatch]
    public class RaidAudioAlertPatch {
        // Zeitvariablen für das Tracking
        private static bool _isRaidActive = false;
        private static float _preparationEndTime = 0f;
        private static bool _endAlertPlayed = false;
        
        // Konstanten
        private static readonly float PREPARATION_DURATION = 120f; // 120 Sekunden Vorbereitungsphase
        private static readonly float ALERT_SECONDS_BEFORE_END = 5f; // 5 Sekunden vor Ende der Phase
        
        // Initialisierung und Start der periodischen Überprüfung
        [InitializeOnce]
        private static void Initialize() {
            IdleLog.Info("RaidAudioAlertPatch initialisiert");
            
            // Audio-System initialisieren
            AudioAlertSystem.Initialize();
            
            // Starte periodische Überprüfung des Raid-Timers
            IdleTasks.Repeat(0.5f, 0.5f, CheckRaidTimer);
        }
        
        // Finde und patche die RaidCitadelBattleManager-Klasse
        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase TargetMethod() {
            try {
                // Suche nach der OnRaidPhaseStarted-Methode in RaidCitadelBattleManager
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    try {
                        foreach (var type in assembly.GetTypes()) {
                            if (type.Name == "RaidCitadelBattleManager") {
                                var method = type.GetMethod("OnRaidPhaseStarted", 
                                    System.Reflection.BindingFlags.Instance | 
                                    System.Reflection.BindingFlags.Public | 
                                    System.Reflection.BindingFlags.NonPublic);
                                
                                if (method != null) {
                                    IdleLog.Info("OnRaidPhaseStarted Methode in RaidCitadelBattleManager gefunden");
                                    return method;
                                }
                            }
                        }
                    } catch (Exception) {
                        // Ignoriere Fehler bei der Suche in Assemblies
                    }
                }
                
                IdleLog.Error("Konnte OnRaidPhaseStarted Methode nicht finden");
                return null;
                
            } catch (Exception ex) {
                IdleLog.Error($"Fehler beim Finden der Zielmethode: {ex.Message}");
                return null;
            }
        }
        
        // Patche die OnRaidPhaseStarted-Methode
        [HarmonyPostfix]
        private static void OnRaidPhaseStarted(object __instance, object phase) {
            if (!ModSettings.Features.RaidAudioAlerts.Value) return;
            
            try {
                // Konvertiere Phase zu String
                string phaseStr = phase?.ToString() ?? "unbekannt";
                IdleLog.Info($"Raid-Phase gestartet: {phaseStr}");
                
                // Überprüfe, ob es sich um die InitialPreparation-Phase handelt
                if (phaseStr == "InitialPreparation") {
                    IdleLog.Info("Raid mit Vorbereitungsphase gestartet");
                    _isRaidActive = true;
                    _endAlertPlayed = false;
                    _preparationEndTime = Time.time + PREPARATION_DURATION;
                    
                    // Spiele Start-Sound ab
                    AudioAlertSystem.PlayNotificationSound();
                    IdleLog.Info($"Vorbereitungsphase-End-Alert für {PREPARATION_DURATION - ALERT_SECONDS_BEFORE_END} Sekunden ab jetzt geplant");
                } 
                // Keine Sound-Ausgabe bei Battle-Phase oder anderen Phasen
            } catch (Exception ex) {
                IdleLog.Error($"Fehler im OnRaidPhaseStarted Postfix: {ex.Message}");
            }
        }
        
        // Überprüfe den Raid-Timer regelmäßig
        private static void CheckRaidTimer(IdleTasks.IdleTask task) {
            try {
                if (!_isRaidActive || !ModSettings.Features.RaidAudioAlerts.Value) return;
                
                float timeRemaining = _preparationEndTime - Time.time;
                
                // Spiele Alert ab, wenn das Ende der Vorbereitungsphase naht
                if (timeRemaining <= ALERT_SECONDS_BEFORE_END && !_endAlertPlayed) {
                    IdleLog.Info($"Spiele Vorbereitungsphase-End-Alert ab - noch {timeRemaining:F1} Sekunden verbleibend");
                    AudioAlertSystem.PlayNotificationSound();
                    _endAlertPlayed = true;
                }
                
                // Setze Tracking zurück, wenn die Vorbereitungsphase definitiv vorbei ist
                if (timeRemaining < -1f) {
                    _isRaidActive = false;
                }
            } catch (Exception ex) {
                IdleLog.Error($"Fehler bei der Raid-Timer-Überprüfung: {ex.Message}");
            }
        }
    }
}