// ============================================
// AppSettings.cs - Settings Management
// ============================================
using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace VirtualDesktopOverlay
{
    public class AppSettings
    {
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }

        // Persisted window size (device-independent pixels)
        public double WindowWidth { get; set; } = 300;
        public double WindowHeight { get; set; } = 50;

        public string Theme { get; set; } = "Dark"; // "Light", "Dark" or "Auto"
        public double Opacity { get; set; } = 0.8;
        public int FontSize { get; set; } = 18;

        // Persist chosen font family
        public string FontFamily { get; set; } = "Segoe UI";

        public bool AcrylicEffect { get; set; } = false;
        public bool RunAtStartup { get; set; } = false;

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VirtualDesktopOverlay",
            "settings.json"
        );

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        public void SetDefaultPosition(double screenWidth, double screenHeight, double windowWidth, double windowHeight)
        {
            // Lower-right corner, above taskbar
            WindowLeft = screenWidth - windowWidth - 20;
            WindowTop = screenHeight - windowHeight - 60; 
        }

        // Resolve theme: if "Auto" -> query system, otherwise return "Light" or "Dark"
        public static string GetEffectiveTheme(string theme)
        {
            if (string.Equals(theme, "Auto", StringComparison.OrdinalIgnoreCase))
                return GetSystemTheme();
            return string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        }

        // Read the Windows registry setting that controls app theme (AppsUseLightTheme)
        private static string GetSystemTheme()
        {
            try
            {
                // HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme
                const string key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
                var value = Registry.GetValue(key, "AppsUseLightTheme", null);
                if (value is int intVal)
                {
                    return intVal == 1 ? "Light" : "Dark";
                }
            }
            catch { }
            // default fallback
            return "Dark";
        }
    }
}

