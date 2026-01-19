// ============================================
// AppSettings.cs - Settings Management
// ============================================
using System;
using System.IO;
using System.Text.Json;

namespace VirtualDesktopOverlay
{
    public class AppSettings
    {
        public double WindowLeft { get; set; }
        public double WindowTop { get; set; }
        public string Theme { get; set; } = "Dark";
        public double Opacity { get; set; } = 0.8;
        public int FontSize { get; set; } = 18;
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
    }
}

