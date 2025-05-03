using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;
using IdlePlus.Utilities;

namespace IdlePlus.Utilities {
    /// <summary>
    /// Audio-Alert-System zum Abspielen von Sounds in IdlePlus
    /// </summary>
    public static class AudioAlertSystem {
        private static bool _initialized = false;
        
        /// <summary>
        /// Initialisiert das Audio-System
        /// </summary>
        public static void Initialize() {
            if (_initialized) return;
            
            try {
                string soundsDir = Path.Combine(BepInEx.Paths.PluginPath, "IdlePlus", "Sounds");
                
                // Stelle sicher, dass das Sound-Verzeichnis existiert
                if (!Directory.Exists(soundsDir)) {
                    Directory.CreateDirectory(soundsDir);
                    IdleLog.Info($"Sound-Verzeichnis erstellt: {soundsDir}");
                }
                
                // Extrahiere alle eingebetteten Sound-Ressourcen
                ExtractSoundResources(soundsDir);
                
                _initialized = true;
                IdleLog.Info("AudioAlertSystem erfolgreich initialisiert (MediaPlayer wird verwendet)");
            } catch (Exception ex) {
                IdleLog.Error($"Fehler bei der Initialisierung des AudioAlertSystems: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Extrahiert alle Sound-Ressourcen aus der Assembly
        /// </summary>
        private static void ExtractSoundResources(string destinationDir) {
            try {
                // Aktuelle Assembly abrufen
                var assembly = Assembly.GetExecutingAssembly();
                
                // Liste alle verfügbaren Ressourcen auf
                var allResources = assembly.GetManifestResourceNames();
                IdleLog.Info($"Gefundene Ressourcen in der Assembly: {allResources.Length}");
                
                // Durchsuche alle Ressourcen nach Sound-Dateien
                foreach (var res in allResources) {
                    IdleLog.Info($"Verfügbare Ressource: {res}");
                    
                    // Extrahiere MP3-Dateien
                    if (res.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) {
                        // Extrahiere den Dateinamen aus dem Ressourcennamen
                        string fileName = GetFileNameFromResourceName(res);
                        string outputPath = Path.Combine(destinationDir, fileName);
                        
                        // Prüfe, ob die Datei bereits existiert
                        if (!File.Exists(outputPath)) {
                            ExtractResourceToFile(assembly, res, outputPath);
                            IdleLog.Info($"Sound-Datei extrahiert: {outputPath}");
                        } else {
                            IdleLog.Info($"Sound-Datei existiert bereits: {outputPath}");
                        }
                    }
                }
            } catch (Exception ex) {
                IdleLog.Error($"Fehler beim Extrahieren der Sound-Ressourcen: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Extrahiert eine Ressource in eine Datei
        /// </summary>
        private static void ExtractResourceToFile(Assembly assembly, string resourceName, string outputPath) {
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName)) {
                if (resourceStream == null) {
                    IdleLog.Error($"Ressource-Stream konnte nicht geöffnet werden: {resourceName}");
                    return;
                }
                
                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write)) {
                    // Ressource in die Datei kopieren
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = resourceStream.Read(buffer, 0, buffer.Length)) > 0) {
                        fileStream.Write(buffer, 0, bytesRead);
                    }
                }
            }
        }
        
        /// <summary>
        /// Extrahiert den Dateinamen aus dem Ressourcennamen
        /// </summary>
        private static string GetFileNameFromResourceName(string resourceName) {
            // Ressourcennamen haben oft das Format "Namespace.Ordner.Dateiname.Erweiterung"
            // Wir extrahieren nur den letzten Teil
            int lastDot = resourceName.LastIndexOf('.');
            if (lastDot < 0) return resourceName;
            
            string extension = resourceName.Substring(lastDot);
            
            int secondLastDot = resourceName.LastIndexOf('.', lastDot - 1);
            if (secondLastDot < 0) return resourceName;
            
            string fileName = resourceName.Substring(secondLastDot + 1);
            return fileName; // Enthält z.B. "notification.mp3"
        }
        
        /// <summary>
        /// Spielt einen Sound ab. Unterstützt MP3 und WAV.
        /// </summary>
        public static void PlaySound(string soundName) {
            if (!_initialized) Initialize();
            
            string soundsDir = Path.Combine(BepInEx.Paths.PluginPath, "IdlePlus", "Sounds");
            
            // Prüfe mehrere mögliche Dateiformate
            string mp3Path = Path.Combine(soundsDir, $"{soundName}.mp3");
            string wavPath = Path.Combine(soundsDir, $"{soundName}.wav");
            
            if (File.Exists(mp3Path)) {
                IdleLog.Info($"MP3-Datei gefunden: {mp3Path}");
                MediaPlayer.PlaySound(mp3Path);
            } else if (File.Exists(wavPath)) {
                IdleLog.Info($"WAV-Datei gefunden: {wavPath}");
                MediaPlayer.PlaySound(wavPath);
            } else {
                IdleLog.Error($"Sound-Datei nicht gefunden: {soundName}");
                MediaPlayer.PlaySystemSound(MediaPlayer.SoundType.Warning);
            }
        }
        
        /// <summary>
        /// Spielt den Start-Sound ab
        /// </summary>
        public static void PlayStartSound() {
            PlaySound("start");
        }
        
        /// <summary>
        /// Spielt den End-Sound ab
        /// </summary>
        public static void PlayEndSound() {
            PlaySound("end");
        }
        
        /// <summary>
        /// Spielt die Benachrichtigungs-Sound ab
        /// </summary>
        public static void PlayNotificationSound() {
            PlaySound("notification");
        }
        
        /// <summary>
        /// Windows Media Player für Sound-Wiedergabe
        /// </summary>
        private static class MediaPlayer {
            // Sound-Typen für System-Sounds
            public enum SoundType {
                Default = 0,
                Information = 1,
                Warning = 2,
                Error = 3,
                Question = 4
            }
            
            // Windows API für System-Sounds (MessageBeep)
            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool MessageBeep(uint uType);
            
            // Windows API für mciSendString (für MP3 und andere Formate)
            [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern int mciSendString(string command, System.Text.StringBuilder returnValue, 
                                                   int returnLength, IntPtr hwndCallback);
            
            // Sound-Typen für MessageBeep
            private const uint MB_OK = 0x00000000; // Default
            private const uint MB_ICONINFORMATION = 0x00000040; // Information
            private const uint MB_ICONWARNING = 0x00000030; // Warning
            private const uint MB_ICONERROR = 0x00000010; // Error
            private const uint MB_ICONQUESTION = 0x00000020; // Question
            
            // Zähler für eindeutige Geräte-IDs
            private static int deviceCounter = 0;
            
            /// <summary>
            /// Spielt eine Sound-Datei ab (MP3, WAV, etc.)
            /// </summary>
            public static void PlaySound(string filePath) {
                try {
                    IdleLog.Info($"Versuche Sound abzuspielen: {filePath}");
                    
                    // Erstelle einen eindeutigen Gerätenamen
                    string deviceId = $"IdlePlus_{deviceCounter++}";
                    
                    // Öffne die Datei
                    int result = mciSendString($"open \"{filePath}\" type mpegvideo alias {deviceId}", null, 0, IntPtr.Zero);
                    if (result != 0) {
                        string errorMessage = GetMciErrorMessage(result);
                        IdleLog.Error($"Fehler beim Öffnen der Datei: {errorMessage}");
                        PlaySystemSound(SoundType.Warning);
                        return;
                    }
                    
                    // Spiele die Datei asynchron ab
                    result = mciSendString($"play {deviceId} notify", null, 0, IntPtr.Zero);
                    if (result != 0) {
                        string errorMessage = GetMciErrorMessage(result);
                        IdleLog.Error($"Fehler beim Abspielen der Datei: {errorMessage}");
                        
                        // Versuche das Gerät zu schließen
                        mciSendString($"close {deviceId}", null, 0, IntPtr.Zero);
                        
                        PlaySystemSound(SoundType.Warning);
                        return;
                    }
                    
                    // Verzögert schließen des Geräts, um sicherzustellen, dass der Sound abgespielt wird
                    System.Threading.Tasks.Task.Run(async () => {
                        try {
                            // Gib dem Sound Zeit zum Abspielen (5 Sekunden sollten mehr als genug sein)
                            await System.Threading.Tasks.Task.Delay(5000);
                            mciSendString($"close {deviceId}", null, 0, IntPtr.Zero);
                        } catch (Exception ex) {
                            IdleLog.Error($"Fehler beim Schließen des Geräts: {ex.Message}");
                        }
                    });
                    
                    IdleLog.Info("Sound erfolgreich abgespielt");
                } catch (Exception ex) {
                    IdleLog.Error($"Fehler beim Abspielen des Sounds: {ex.Message}");
                    // Fallback auf System-Sound
                    PlaySystemSound(SoundType.Warning);
                }
            }
            
            /// <summary>
            /// Hilfs-Methode, um MCI-Fehlermeldungen zu erhalten
            /// </summary>
            private static string GetMciErrorMessage(int errorCode) {
                System.Text.StringBuilder errorMessage = new System.Text.StringBuilder(256);
                int result = mciGetErrorString(errorCode, errorMessage, errorMessage.Capacity);
                return result != 0 ? errorMessage.ToString() : $"Unbekannter MCI-Fehler: {errorCode}";
            }
            
            [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern int mciGetErrorString(int errorCode, System.Text.StringBuilder errorText, int errorTextSize);
            
            /// <summary>
            /// Spielt einen System-Sound ab
            /// </summary>
            public static void PlaySystemSound(SoundType type) {
                try {
                    uint soundType;
                    
                    switch (type) {
                        case SoundType.Information:
                            soundType = MB_ICONINFORMATION;
                            break;
                        case SoundType.Warning:
                            soundType = MB_ICONWARNING;
                            break;
                        case SoundType.Error:
                            soundType = MB_ICONERROR;
                            break;
                        case SoundType.Question:
                            soundType = MB_ICONQUESTION;
                            break;
                        default:
                            soundType = MB_OK;
                            break;
                    }
                    
                    bool success = MessageBeep(soundType);
                    
                    if (!success) {
                        int errorCode = Marshal.GetLastWin32Error();
                        IdleLog.Error($"Fehler beim Abspielen des System-Sounds, Fehlercode: {errorCode}");
                    } else {
                        IdleLog.Info("System-Sound erfolgreich abgespielt");
                    }
                } catch (Exception ex) {
                    IdleLog.Error($"Fehler beim Abspielen des System-Sounds: {ex.Message}");
                }
            }
        }
    }
}