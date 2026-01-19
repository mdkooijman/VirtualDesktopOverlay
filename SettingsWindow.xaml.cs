// ============================================
// SettingsWindow.xaml.cs - Settings UI
// ============================================
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace VirtualDesktopOverlay
{
    // EventArgs wrapper for preview data (EventHandler<T> requires T : EventArgs)
    public class AppSettingsPreviewEventArgs : EventArgs
    {
        public AppSettings Preview { get; }
        public AppSettingsPreviewEventArgs(AppSettings preview) => Preview = preview;
    }

    public partial class SettingsWindow : Window
    {
        private AppSettings settings;
        public event EventHandler? SettingsChanged;

        // Live preview event (raises AppSettingsPreviewEventArgs)
        public event EventHandler<AppSettingsPreviewEventArgs>? PreviewChanged;

        // sensible bounds (renamed to avoid hiding)
        private const int MinOverlayWidth = 100, MaxOverlayWidth = 2000;
        private const int MinOverlayHeight = 20, MaxOverlayHeight = 1000;
        private const int MinFontSize = 10, MaxFontSize = 72;

        // Guard to avoid reacting to control events during construction
        private bool _isInitialized = false;

        public SettingsWindow(AppSettings currentSettings)
        {
            // ensure settings available during InitializeComponent
            settings = currentSettings;

            InitializeComponent();

            PopulateFontList();
            LoadSettings();

            // now it's safe to respond to control events
            _isInitialized = true;
        }

        private void PopulateFontList()
        {
            FontComboBox.ItemsSource = Fonts.SystemFontFamilies
                .OrderBy(f => f.Source)
                .Select(f => f.Source)
                .ToList();
        }

        private void LoadSettings()
        {
            // Map persisted theme to ComboBox index: Auto=0, Dark=1, Light=2
            ThemeComboBox.SelectedIndex = settings.Theme switch
            {
                "Auto" => 0,
                "Dark" => 1,
                "Light" => 2,
                _ => 0
            };

            OpacitySlider.Value = settings.Opacity * 100;
            // update label to show number before '%'
            OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";

            FontSizeTextBox.Text = ((int)settings.FontSize).ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(settings.FontFamily) && FontComboBox.Items.Contains(settings.FontFamily))
                FontComboBox.SelectedItem = settings.FontFamily;
            else
                FontComboBox.SelectedIndex = FontComboBox.Items.Contains("Segoe UI") ? FontComboBox.Items.IndexOf("Segoe UI") : 0;

            WidthTextBox.Text = ((int)settings.WindowWidth).ToString(CultureInfo.InvariantCulture);
            HeightTextBox.Text = ((int)settings.WindowHeight).ToString(CultureInfo.InvariantCulture);

            AcrylicCheckBox.IsChecked = settings.AcrylicEffect;
            StartupCheckBox.IsChecked = settings.RunAtStartup;

            // Apply theme to settings window UI using the user's selection (Auto uses system resolution)
            ApplyWindowTheme(settings.Theme);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Map ComboBox index to stored theme string
            settings.Theme = ThemeComboBox.SelectedIndex switch
            {
                0 => "Auto",
                1 => "Dark",
                2 => "Light",
                _ => "Auto"
            };

            settings.Opacity = OpacitySlider.Value / 100.0;
            settings.AcrylicEffect = AcrylicCheckBox.IsChecked ?? false;
            settings.RunAtStartup = StartupCheckBox.IsChecked ?? false;

            if (!int.TryParse(FontSizeTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fs))
            {
                MessageBox.Show("Invalid font size.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            settings.FontSize = (int)Math.Max(MinFontSize, Math.Min(MaxFontSize, fs));

            if (FontComboBox.SelectedItem is string ff) settings.FontFamily = ff;

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

            settings.WindowWidth = Math.Max(MinOverlayWidth, Math.Min(MaxOverlayWidth, w));
            settings.WindowHeight = Math.Max(MinOverlayHeight, Math.Min(MaxOverlayHeight, h));

            settings.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private void ResetPositionButton_Click(object sender, RoutedEventArgs e)
        {
            var main = Application.Current?.MainWindow;
            double windowWidth = 300;
            double windowHeight = 50;

            if (main != null)
            {
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

        // live preview wiring
        private void Control_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) RaisePreview();
        }
        private void Control_ValueChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isInitialized) RaisePreview();
        }
        private void Control_ValueChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isInitialized) RaisePreview();
        }
        private void Control_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitialized) RaisePreview();
        }

        private string SelectedThemeString()
        {
            return ThemeComboBox?.SelectedIndex switch
            {
                0 => "Auto",
                1 => "Dark",
                2 => "Light",
                _ => "Auto"
            };
        }

        private void RaisePreview()
        {
            // ensure fully initialized
            if (!_isInitialized) return;

            // update opacity label from slider value (safe if control exists)
            if (OpacityLabel != null && OpacitySlider != null)
                OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";

            // Safely read control text (controls may be null during InitializeComponent)
            string widthText = WidthTextBox?.Text ?? string.Empty;
            string heightText = HeightTextBox?.Text ?? string.Empty;
            string fontSizeText = FontSizeTextBox?.Text ?? string.Empty;
            string fontFamily = FontComboBox?.SelectedItem as string ?? settings.FontFamily;

            int w = int.TryParse(widthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int wp)
                ? Math.Max(MinOverlayWidth, Math.Min(MaxOverlayWidth, wp))
                : (int)settings.WindowWidth;

            int h = int.TryParse(heightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hp)
                ? Math.Max(MinOverlayHeight, Math.Min(MaxOverlayHeight, hp))
                : (int)settings.WindowHeight;

            int fs = int.TryParse(fontSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fsp)
                ? Math.Max(MinFontSize, Math.Min(MaxFontSize, fsp))
                : settings.FontSize;

            var preview = new AppSettings
            {
                Theme = SelectedThemeString(),
                Opacity = (OpacitySlider != null) ? (OpacitySlider.Value / 100.0) : settings.Opacity,
                FontSize = fs,
                FontFamily = fontFamily,
                WindowWidth = w,
                WindowHeight = h,
                AcrylicEffect = AcrylicCheckBox?.IsChecked ?? settings.AcrylicEffect,
                RunAtStartup = StartupCheckBox?.IsChecked ?? settings.RunAtStartup
            };

            // apply theme to settings window using selected theme (Auto => system resolution)
            ApplyWindowTheme(preview.Theme);

            PreviewChanged?.Invoke(this, new AppSettingsPreviewEventArgs(preview));
        }

        private void ApplyWindowTheme(string selectedTheme)
        {
            if (RootGrid == null) return;

            // Resolve Auto -> actual system theme, otherwise keep explicit choice
            string effectiveTheme = AppSettings.GetEffectiveTheme(selectedTheme);

            Brush bg, fg, tbBg, comboBorder;
            if (string.Equals(effectiveTheme, "Light", StringComparison.OrdinalIgnoreCase))
            {
                bg = Brushes.White;
                fg = Brushes.Black;
                tbBg = Brushes.White;
                comboBorder = Brushes.Gray;
            }
            else
            {
                bg = new SolidColorBrush(Color.FromRgb(34, 34, 34));
                fg = Brushes.White;
                tbBg = new SolidColorBrush(Color.FromRgb(48, 48, 48));
                comboBorder = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            }

            // Window + root
            this.Background = bg;
            RootGrid.Background = bg;
            this.Foreground = fg;

            // Per-control explicit colors (closed ComboBox, textboxes, checkboxes)
            if (OpacityLabel != null) OpacityLabel.Foreground = fg;

            ThemeComboBox.Foreground = fg;
            ThemeComboBox.Background = tbBg;
            ThemeComboBox.BorderBrush = comboBorder;

            FontComboBox.Foreground = fg;
            FontComboBox.Background = tbBg;
            FontComboBox.BorderBrush = comboBorder;

            FontSizeTextBox.Background = tbBg;
            FontSizeTextBox.Foreground = fg;
            WidthTextBox.Background = tbBg;
            WidthTextBox.Foreground = fg;
            HeightTextBox.Background = tbBg;
            HeightTextBox.Foreground = fg;
            AcrylicCheckBox.Foreground = fg;
            StartupCheckBox.Foreground = fg;

            // Inject/override system brush keys locally so control templates (selected value area, popup, etc.)
            // pick up the correct colors inside this window.
            this.Resources[SystemColors.ControlTextBrushKey] = fg;
            this.Resources[SystemColors.ControlBrushKey] = tbBg;
            this.Resources[SystemColors.WindowBrushKey] = bg;
            this.Resources[SystemColors.WindowTextBrushKey] = fg;

            // Highlight (selection) brushes - critical for selected item background in dropdown and button focus visuals
            Brush highlightBrush;
            Brush highlightTextBrush;
            if (string.Equals(effectiveTheme, "Light", StringComparison.OrdinalIgnoreCase))
            {
                highlightBrush = SystemColors.HighlightBrush;
                highlightTextBrush = SystemColors.HighlightTextBrush;
            }
            else
            {
                highlightBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)); // dark selection bg
                highlightTextBrush = Brushes.White; // selection text on dark
            }

            this.Resources[SystemColors.HighlightBrushKey] = highlightBrush;
            this.Resources[SystemColors.HighlightTextBrushKey] = highlightTextBrush;

            // Also ensure ComboBoxItem uses the same foreground for popup items (in case the style wasn't set)
            this.Resources[typeof(System.Windows.Controls.ComboBoxItem)] = new Style(typeof(System.Windows.Controls.ComboBoxItem))
            {
                Setters =
                {
                    new Setter(System.Windows.Controls.Control.ForegroundProperty, fg),
                    new Setter(System.Windows.Controls.Control.BackgroundProperty, tbBg)
                },
                Triggers =
                {
                    new Trigger
                    {
                        Property = System.Windows.Controls.Primitives.Selector.IsSelectedProperty,
                        Value = true,
                        Setters =
                        {
                            new Setter(System.Windows.Controls.Control.BackgroundProperty, highlightBrush),
                            new Setter(System.Windows.Controls.Control.ForegroundProperty, highlightTextBrush)
                        }
                    }
                }
            };
        }

        // Numeric up/down logic (unchanged)...
        private void WidthUpButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(WidthTextBox, +1, MinOverlayWidth, MaxOverlayWidth);
        private void WidthDownButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(WidthTextBox, -1, MinOverlayWidth, MaxOverlayWidth);

        private void HeightUpButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(HeightTextBox, +1, MinOverlayHeight, MaxOverlayHeight);
        private void HeightDownButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(HeightTextBox, -1, MinOverlayHeight, MaxOverlayHeight);

        private void FontSizeUpButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(FontSizeTextBox, +1, MinFontSize, MaxFontSize);
        private void FontSizeDownButton_Click(object sender, RoutedEventArgs e) => ChangeIntegerValue(FontSizeTextBox, -1, MinFontSize, MaxFontSize);

        private void ChangeIntegerValue(System.Windows.Controls.TextBox tb, int delta, int min, int max)
        {
            if (!int.TryParse(tb.Text, out int value))
                value = Math.Max(min, 0);

            value = Math.Max(min, Math.Min(max, value + delta));
            tb.Text = value.ToString(CultureInfo.InvariantCulture);
            RaisePreview();
        }

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

        private void Numeric_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                int delta = e.Delta > 0 ? +1 : -1;
                int min = MinOverlayWidth, max = MaxOverlayWidth;

                if (tb == WidthTextBox) { min = MinOverlayWidth; max = MaxOverlayWidth; }
                else if (tb == HeightTextBox) { min = MinOverlayHeight; max = MaxOverlayHeight; }
                else if (tb == FontSizeTextBox) { min = MinFontSize; max = MaxFontSize; }

                ChangeIntegerValue(tb, delta, min, max);
                e.Handled = true;
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int attrValue, int attrSize);

        // call after window is created
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            TryEnableDarkTitleBar();
        }

        private void TryEnableDarkTitleBar()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int useDark = 1;
                const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // works on modern Windows 10/11
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            }
            catch
            {
                // Ignore: DWM attribute may not be available on older systems
            }
        }
    }
}

