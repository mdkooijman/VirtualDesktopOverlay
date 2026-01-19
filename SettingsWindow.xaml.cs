// ============================================
// SettingsWindow.xaml.cs - Settings UI
// ============================================
using System;
using System.Windows;

namespace VirtualDesktopOverlay
{
    public partial class SettingsWindow : Window
    {
        private AppSettings settings;
        public event EventHandler? SettingsChanged;

        public SettingsWindow(AppSettings currentSettings)
        {
            InitializeComponent();
            settings = currentSettings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            ThemeComboBox.SelectedIndex = settings.Theme switch
            {
                "Light" => 1,
                "Dark" => 0,
                _ => 0
            };

            OpacitySlider.Value = settings.Opacity * 100;
            FontSizeSlider.Value = settings.FontSize;
            AcrylicCheckBox.IsChecked = settings.AcrylicEffect;
            StartupCheckBox.IsChecked = settings.RunAtStartup;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            settings.Theme = ThemeComboBox.SelectedIndex == 1 ? "Light" : "Dark";
            settings.Opacity = OpacitySlider.Value / 100.0;
            settings.FontSize = (int)FontSizeSlider.Value;
            settings.AcrylicEffect = AcrylicCheckBox.IsChecked ?? false;
            settings.RunAtStartup = StartupCheckBox.IsChecked ?? false;

            settings.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ResetPositionButton_Click(object sender, RoutedEventArgs e)
        {
            // Use main window dimensions when available; fall back to original literals.
            var main = Application.Current?.MainWindow;
            double windowWidth = 300;
            double windowHeight = 50;

            if (main != null)
            {
                // Prefer rendered size (ActualWidth/ActualHeight); fall back to declared Width/Height.
                if (!double.IsNaN(main.ActualWidth) && main.ActualWidth > 0)
                    windowWidth = main.ActualWidth;
                else if (!double.IsNaN(main.Width) && main.Width > 0)
                    windowWidth = main.Width;

                if (!double.IsNaN(main.ActualHeight) && main.ActualHeight > 0)
                    windowHeight = main.ActualHeight;
                else if (!double.IsNaN(main.Height) && main.Height > 0)
                    windowHeight = main.Height;
            }

            settings.SetDefaultPosition(
                SystemParameters.PrimaryScreenWidth,
                SystemParameters.PrimaryScreenHeight,
                windowWidth,
                windowHeight
            );
            settings.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            MessageBox.Show("Position reset to default (lower-right corner)", "Position Reset",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

