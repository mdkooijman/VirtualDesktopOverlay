// ============================================
// SettingsWindow.xaml.cs - Settings UI
// ============================================
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace VirtualDesktopOverlay
{
    public partial class SettingsWindow : Window
    {
        private AppSettings settings;
        public event EventHandler? SettingsChanged;

        // sensible bounds
        private const int MinWidth = 100, MaxWidth = 2000;
        private const int MinHeight = 20, MaxHeight = 1000;
        private const int MinFont = 10, MaxFont = 72;

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
            // font now integer control
            FontSizeTextBox.Text = ((int)settings.FontSize).ToString(CultureInfo.InvariantCulture);

            WidthTextBox.Text = ((int)settings.WindowWidth).ToString(CultureInfo.InvariantCulture);
            HeightTextBox.Text = ((int)settings.WindowHeight).ToString(CultureInfo.InvariantCulture);

            AcrylicCheckBox.IsChecked = settings.AcrylicEffect;
            StartupCheckBox.IsChecked = settings.RunAtStartup;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            settings.Theme = ThemeComboBox.SelectedIndex == 1 ? "Light" : "Dark";
            settings.Opacity = OpacitySlider.Value / 100.0;
            settings.AcrylicEffect = AcrylicCheckBox.IsChecked ?? false;
            settings.RunAtStartup = StartupCheckBox.IsChecked ?? false;

            // parse integer font size
            if (!int.TryParse(FontSizeTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fs))
            {
                MessageBox.Show("Invalid font size.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            settings.FontSize = (int)Math.Max(MinFont, Math.Min(MaxFont, fs));

            // parse integer width/height
            if (!int.TryParse(WidthTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int w))
            {
                MessageBox.Show("Invalid width value.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(HeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int h))
            {
                MessageBox.Show("Invalid height value.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            settings.WindowWidth = Math.Max(MinWidth, Math.Min(MaxWidth, w));
            settings.WindowHeight = Math.Max(MinHeight, Math.Min(MaxHeight, h));

            settings.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();

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

        // Numeric up/down logic

        private void WidthUpButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(WidthTextBox, +1, MinWidth, MaxWidth);
        private void WidthDownButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(WidthTextBox, -1, MinWidth, MaxWidth);

        private void HeightUpButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(HeightTextBox, +1, MinHeight, MaxHeight);
        private void HeightDownButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(HeightTextBox, -1, MinHeight, MaxHeight);

        private void FontSizeUpButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(FontSizeTextBox, +1, MinFont, MaxFont);
        private void FontSizeDownButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(FontSizeTextBox, -1, MinFont, MaxFont);

        private void ChangeIntegerValue(System.Windows.Controls.TextBox tb, int delta, int min, int max)
        {
            if (!int.TryParse(tb.Text, out int value))
                value = Math.Max(min, 0);

            value = Math.Max(min, Math.Min(max, value + delta));
            tb.Text = value.ToString(CultureInfo.InvariantCulture);
        }

        // allow only digits, allow basic editing keys via PreviewKeyDown
        private void Integer_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void Integer_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // block space; navigation and editing keys are allowed
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        // Mouse wheel modifies focused numeric textbox
        private void Numeric_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                int delta = e.Delta > 0 ? +1 : -1;
                int min = MinWidth, max = MaxWidth;

                if (tb == WidthTextBox) { min = MinWidth; max = MaxWidth; }
                else if (tb == HeightTextBox) { min = MinHeight; max = MaxHeight; }
                else if (tb == FontSizeTextBox) { min = MinFont; max = MaxFont; }

                ChangeIntegerValue(tb, delta, min, max);
                e.Handled = true;
            }
        }
    }
}

