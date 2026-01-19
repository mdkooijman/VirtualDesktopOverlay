// ============================================
// MainWindow.xaml.cs - Main Overlay Window
// ============================================
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace VirtualDesktopOverlay
{
    public partial class MainWindow : Window
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int GWL_EXSTYLE = -20;

        private bool isUnlocked = false;
        private Point dragOffset;
        private DispatcherTimer updateTimer;
        private AppSettings settings;
        private WinForms.NotifyIcon? trayIcon;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        // COM Interfaces for Virtual Desktop pinning
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        private interface IVirtualDesktopManager
        {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);
            [PreserveSig]
            int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
            [PreserveSig]
            int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
        }

        [ComImport]
        [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        private class VirtualDesktopManager { }

        public MainWindow()
        {
            InitializeComponent();

            settings = AppSettings.Load();
            ApplySettings();

            if (settings.WindowLeft == 0 && settings.WindowTop == 0)
            {
                settings.SetDefaultPosition(
                    SystemParameters.PrimaryScreenWidth,
                    SystemParameters.PrimaryScreenHeight,
                    this.Width,
                    this.Height
                );
            }

            this.Left = settings.WindowLeft;
            this.Top = settings.WindowTop;

            SetClickThrough(true);

            // Update desktop name periodically
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            UpdateDesktopName();
            CreateTrayIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            PinToAllDesktops();
        }

        private void PinToAllDesktops()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;

                // Try to pin window to all virtual desktops
                var desktopManager = (IVirtualDesktopManager)new VirtualDesktopManager();

                // Get current desktop ID
                desktopManager.GetWindowDesktopId(hwnd, out Guid currentDesktop);

                // Note: Full pinning requires additional COM interfaces not in standard IVirtualDesktopManager
                // This sets up the window, and we'll use WS_EX_TOOLWINDOW to help it appear on all desktops
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
            }
            catch (Exception ex)
            {
                // Pinning may not work on all Windows versions
                System.Diagnostics.Debug.WriteLine($"Virtual Desktop pinning failed: {ex.Message}");
            }
        }

        private void SetClickThrough(bool clickThrough)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            if (clickThrough)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                this.Cursor = Cursors.Arrow;
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void CreateTrayIcon()
        {
            trayIcon = new WinForms.NotifyIcon();
            trayIcon.Icon = System.Drawing.SystemIcons.Application;
            trayIcon.Text = "Virtual Desktop Overlay";
            trayIcon.Visible = true;

            var contextMenu = new WinForms.ContextMenuStrip();

            contextMenu.Items.Add("Settings", null, (s, e) => OpenSettings());
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("Lock Overlay", null, (s, e) => ToggleLock());
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(settings);
            settingsWindow.SettingsChanged += (s, e) =>
            {
                ApplySettings();
                settings.Save();
            };
            settingsWindow.Show();
        }

        private void ToggleLock()
        {
            isUnlocked = !isUnlocked;
            SetClickThrough(!isUnlocked);
            UpdateOverlayAppearance();
        }

        private void ApplySettings()
        {
            // Apply theme
            var background = settings.Theme switch
            {
                "Light" => Color.FromArgb((byte)(settings.Opacity * 255), 240, 240, 240),
                "Dark" => Color.FromArgb((byte)(settings.Opacity * 255), 0, 0, 0),
                _ => Color.FromArgb((byte)(settings.Opacity * 255), 0, 0, 0)
            };

            OverlayBorder.Background = new SolidColorBrush(background);

            // Apply font size
            DesktopNameText.FontSize = settings.FontSize;

            // Apply text color based on theme
            DesktopNameText.Foreground = settings.Theme == "Light"
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);
        }

        private void UpdateOverlayAppearance()
        {
            if (isUnlocked)
            {
                OverlayBorder.Background = new SolidColorBrush(
                    Color.FromArgb(100, 0, 120, 215));
            }
            else
            {
                ApplySettings();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                if (!isUnlocked)
                {
                    isUnlocked = true;
                    SetClickThrough(false);
                    UpdateOverlayAppearance();
                }
            }
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (isUnlocked &&
                !((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                  (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift))
            {
                isUnlocked = false;
                SetClickThrough(true);
                UpdateOverlayAppearance();
                SavePosition();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isUnlocked)
            {
                dragOffset = e.GetPosition(this);
                this.CaptureMouse();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isUnlocked && this.IsMouseCaptured)
            {
                // PointToScreen returns device (physical) pixels on high-DPI displays.
                // Window.Left/Top are in device-independent units (DIPs). Convert screen point to DIPs
                // before assigning to Left/Top to avoid the window jumping off-screen on scaled displays.
                Point screenPoint = PointToScreen(e.GetPosition(this));
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    // transform from device pixels to DIPs
                    Matrix transform = source.CompositionTarget.TransformFromDevice;
                    Point dipPoint = transform.Transform(screenPoint);
                    this.Left = dipPoint.X - dragOffset.X;
                    this.Top = dipPoint.Y - dragOffset.Y;
                }
                else
                {
                    // Fallback if no presentation source (rare)
                    this.Left = screenPoint.X - dragOffset.X;
                    this.Top = screenPoint.Y - dragOffset.Y;
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.IsMouseCaptured)
            {
                this.ReleaseMouseCapture();
                SavePosition();
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateDesktopName();
        }

        private void UpdateDesktopName()
        {
            try
            {
                var desktopInfo = VirtualDesktopHelper.GetCurrentDesktopInfo();
                DesktopNameText.Text = desktopInfo.Name;
            }
            catch
            {
                DesktopNameText.Text = "Desktop 1";
            }
        }

        private void SavePosition()
        {
            settings.WindowLeft = this.Left;
            settings.WindowTop = this.Top;
            settings.Save();
        }

        private void ExitApplication()
        {
            trayIcon?.Dispose();
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            trayIcon?.Dispose();
            updateTimer?.Stop();
        }
    }
}

