// ============================================
// MainWindow.xaml.cs - Main Overlay Window
// ============================================
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace VirtualDesktopOverlay
{
    public partial class MainWindow : Window
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int GWL_EXSTYLE = -20;
        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTTRANSPARENT = -1;

        private bool isUnlocked = false;
        private bool isHidden = false;
        private Point dragOffset;
        private DispatcherTimer updateTimer;
        private DispatcherTimer mouseCheckTimer;
        private AppSettings settings;
        private WinForms.NotifyIcon? trayIcon;
        private bool isMouseOver = false;
        private HwndSource? hwndSource;

        // Minimal addition: keep a reference to the "Verplaatsen" menu item so we can update it on open
        private WinForms.ToolStripMenuItem? moveMenuItem;
        // Minimal addition: keep a reference to the "Verbergen" menu item so we can update it on open
        private WinForms.ToolStripMenuItem? hideMenuItem;

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
            if (!File.Exists(AppSettings.ConfigPath))
            {
                settings.Save(); // This creates the physical file for the first time
            }

            // Sync RunAtStartup setting with actual registry state on load
            settings.RunAtStartup = AppSettings.IsSetToRunAtStartup();

            // Apply saved size (device-independent units) before layout/default-position logic
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;

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

            //SetClickThrough(true);

            // Update desktop name periodically
            updateTimer = new DispatcherTimer();
            updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

            // Mouse check timer (only runs when overlay is hidden)
            mouseCheckTimer = new DispatcherTimer();
            mouseCheckTimer.Interval = TimeSpan.FromMilliseconds(100);
            mouseCheckTimer.Tick += MouseCheckTimer_Tick;

            UpdateDesktopName();
            CreateTrayIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Add hook for mouse hover detection
            hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);

            // Pin to all desktops first
            PinToAllDesktops();

            // Set click-through mode
            Dispatcher.BeginInvoke(new Action(() => {
                SetClickThrough(true);

                // Start a polling timer to check mouse position
                // (WM_NCHITTEST isn't reliable with transparent windows)
                var pollTimer = new DispatcherTimer();
                pollTimer.Interval = TimeSpan.FromMilliseconds(100);
                pollTimer.Tick += (s, ev) => {
                    if (!isUnlocked)
                    {
                        CheckMousePosition();
                    }
                };
                pollTimer.Start();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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

                // Add WS_EX_TOOLWINDOW while preserving other flags
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                // Only add TOOLWINDOW if not already set
                if ((extendedStyle & WS_EX_TOOLWINDOW) == 0)
                {
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
                    System.Diagnostics.Debug.WriteLine($"PinToAllDesktops: Added WS_EX_TOOLWINDOW, style now 0x{GetWindowLong(hwnd, GWL_EXSTYLE):X}");
                }
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

            var currentStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            int newStyle;

            if (clickThrough)
            {
                // Enable click-through: add TRANSPARENT and ensure LAYERED
                newStyle = currentStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED;
                this.Cursor = Cursors.Arrow;
            }
            else
            {
                // Disable click-through: remove TRANSPARENT but keep LAYERED
                newStyle = (currentStyle & ~WS_EX_TRANSPARENT) | WS_EX_LAYERED;
                this.Cursor = Cursors.SizeAll;
            }

            // Force the style change
            SetWindowLong(hwnd, GWL_EXSTYLE, newStyle);

            // Verify it worked and retry if needed
            var verifyStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (verifyStyle != newStyle)
            {
                System.Diagnostics.Debug.WriteLine($"SetClickThrough: Style mismatch! Expected 0x{newStyle:X}, got 0x{verifyStyle:X}. Retrying...");
                SetWindowLong(hwnd, GWL_EXSTYLE, newStyle);
            }

            System.Diagnostics.Debug.WriteLine($"SetClickThrough({clickThrough}): 0x{currentStyle:X} → 0x{newStyle:X}");
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST)
            {
                System.Diagnostics.Debug.WriteLine($"WndProc: WM_NCHITTEST received, isUnlocked={isUnlocked}");
            }

            // WM_NCHITTEST - Windows asking "what is at this position?"
            if (msg == WM_NCHITTEST && !isUnlocked)
            {
                // Get mouse position from lParam
                int x = (short)(lParam.ToInt32() & 0xFFFF);
                int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

                // Get window bounds
                var topLeft = PointToScreen(new Point(0, 0));
                var bottomRight = PointToScreen(new Point(this.ActualWidth, this.ActualHeight));

                // Check if mouse is within window
                bool mouseInBounds = x >= topLeft.X && x <= bottomRight.X &&
                                     y >= topLeft.Y && y <= bottomRight.Y;

                if (mouseInBounds && !isMouseOver)
                {
                    // Mouse entered
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"WndProc: Mouse entered at ({x},{y})");
#endif
                    isMouseOver = true;
                    Dispatcher.BeginInvoke(() => UpdateOverlayAppearance());
                    
                    // Start timer to check when mouse leaves
                    mouseCheckTimer.Start();
                }
                else if (!mouseInBounds && isMouseOver)
                {
                    // Mouse left (detected by WM_NCHITTEST while still visible)
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"WndProc: Mouse left at ({x},{y})");
#endif
                    isMouseOver = false;
                    mouseCheckTimer.Stop();
                    Dispatcher.BeginInvoke(() => UpdateOverlayAppearance());
                }

                // Always return transparent so clicks pass through
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }

        private void MouseCheckTimer_Tick(object? sender, EventArgs e)
        {
            // Stop timer if unlocked or mouse is not over
            if (isUnlocked || !isMouseOver)
            {
                mouseCheckTimer.Stop();
                return;
            }

            try
            {
                // Get current mouse position
                var mousePos = System.Windows.Forms.Cursor.Position;
                
                // Get window bounds
                var topLeft = PointToScreen(new Point(0, 0));
                var bottomRight = PointToScreen(new Point(this.ActualWidth, this.ActualHeight));

                // Check if mouse is still within bounds
                bool mouseInBounds = mousePos.X >= topLeft.X && mousePos.X <= bottomRight.X &&
                                     mousePos.Y >= topLeft.Y && mousePos.Y <= bottomRight.Y;

                if (!mouseInBounds)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"MouseCheckTimer: Mouse left at ({mousePos.X},{mousePos.Y})");
#endif
                    isMouseOver = false;
                    UpdateOverlayAppearance();
                    mouseCheckTimer.Stop();
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        private void CreateTrayIcon()
        {
            trayIcon = new WinForms.NotifyIcon();
            //trayIcon.Icon = System.Drawing.SystemIcons.Application;
            // Get the path to your EXE
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

            // Extract the icon using the Drawing library
            trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            trayIcon.Text = "Virtual Desktop Overlay";
            trayIcon.Visible = true;

            var contextMenu = new WinForms.ContextMenuStrip();

            // create the hide/display item and keep a reference
            hideMenuItem = new WinForms.ToolStripMenuItem("Verbergen");
            hideMenuItem.Click += (s, e) => ToggleHide();
            contextMenu.Items.Add(hideMenuItem);

            // create the move/lock item and keep a reference
            moveMenuItem = new WinForms.ToolStripMenuItem("Verplaatsen");
            moveMenuItem.Click += (s, e) => ToggleLock();
            contextMenu.Items.Add(moveMenuItem);

            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("Instellingen", null, (s, e) => OpenSettings());
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add("Afsluiten", null, (s, e) => ExitApplication());

            // Update menu items each time the menu opens so they reflect current state
            contextMenu.Opening += (s, e) =>
            {
                if (moveMenuItem != null)
                {
                    moveMenuItem.Checked = isUnlocked;
                    // leave label as-is or change if you prefer different wording
                    // moveMenuItem.Text = isUnlocked ? "Ontgrendelen" : "Verplaatsen";
                }

                if (hideMenuItem != null)
                {
                    // Checked = hidden, label shows the action to take next
                    hideMenuItem.Checked = isHidden;
                    //hideMenuItem.Text = isHidden ? "Weergeven" : "Verbergen";
                }
            };

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(settings);
            bool saved = false;

            // live preview (does NOT mutate persisted settings)
            settingsWindow.PreviewChanged += (s, ev) =>
            {
                ApplyPreview(ev.Preview);
            };

            // Save pressed inside SettingsWindow: the settings instance is updated and persisted there.
            settingsWindow.SettingsChanged += (s, e) =>
            {
                // Apply saved settings (settings object now contains persisted values)
                ApplySettings();

                // Apply saved size
                this.Width = settings.WindowWidth;
                this.Height = settings.WindowHeight;

                settings.Save();
                saved = true;
            };

            settingsWindow.Closed += (s, e) =>
            {
                if (!saved)
                {
                    // revert visual changes (reapply persisted settings)
                    ApplySettings();
                    // reapply persisted size
                    this.Width = settings.WindowWidth;
                    this.Height = settings.WindowHeight;
                }
            };

            settingsWindow.Show();
        }

        // New helper: apply a preview (does not persist)
        private void ApplyPreview(AppSettings preview)
        {
            var background = preview.Theme switch
            {
                "Light" => Color.FromArgb((byte)(preview.Opacity * 255), 240, 240, 240),
                "Dark" => Color.FromArgb((byte)(preview.Opacity * 255), 0, 0, 0),
                _ => Color.FromArgb((byte)(preview.Opacity * 255), 0, 0, 0)
            };

            OverlayBorder.Background = new SolidColorBrush(background);

            DesktopNameText.FontSize = preview.FontSize;
            DesktopNameText.Foreground = preview.Theme == "Light"
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.White);

            try
            {
                DesktopNameText.FontFamily = new FontFamily(preview.FontFamily ?? DesktopNameText.FontFamily.Source);
            }
            catch
            {
                // ignore invalid font names
            }

            // preview size change
            this.Width = preview.WindowWidth;
            this.Height = preview.WindowHeight;
        }

        // also update ApplySettings() to set FontFamily when applying real settings:
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

            // Apply font family
            try
            {
                DesktopNameText.FontFamily = new FontFamily(settings.FontFamily ?? DesktopNameText.FontFamily.Source);
            }
            catch
            {
                // ignore invalid
            }
        }

        private void UpdateOverlayAppearance()
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"UpdateOverlayAppearance: isUnlocked={isUnlocked}, isMouseOver={isMouseOver}");
#endif
            
            if (isUnlocked)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("  -> Unlocked mode");
#endif
                // Unlocked mode - show blue highlight (no animation)
                StopOpacityAnimation();
                OverlayBorder.Opacity = 1.0;
                OverlayBorder.Background = new SolidColorBrush(
                    Color.FromArgb(100, 0, 120, 215));
            }
            else if (isMouseOver)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("  -> Mouse over - fading out");
#endif
                // Mouse over - fade to completely transparent
                AnimateOpacity(0.0, 200);
            }
            else
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine("  -> Normal mode - fading in");
#endif
                // Normal mode - fade back to visible and apply settings
                StopOpacityAnimation();
                ApplySettings();
                AnimateOpacity(1.0, 200);
            }
        }

        private void AnimateOpacity(double targetOpacity, int durationMs)
        {
            var animation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            
            OverlayBorder.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        private void StopOpacityAnimation()
        {
            OverlayBorder.BeginAnimation(UIElement.OpacityProperty, null);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                if (!isUnlocked)
                {
                    isUnlocked = true;
                    isMouseOver = false; // Reset mouse over state
                    mouseCheckTimer.Stop(); // Stop the timer
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
                
                // Check if mouse is currently over the overlay FIRST
                CheckMousePosition();
                
                // Update appearance BEFORE setting click-through
                // (to avoid WndProc race condition)
                UpdateOverlayAppearance();
                
                // Now enable click-through (this triggers WndProc)
                SetClickThrough(true);
                
                SavePosition();
            }
        }

        private void CheckMousePosition()
        {
            try
            {
                // Get current mouse position
                var mousePos = System.Windows.Forms.Cursor.Position;

                // Get window bounds
                var topLeft = PointToScreen(new Point(0, 0));
                var bottomRight = PointToScreen(new Point(this.ActualWidth, this.ActualHeight));

                // Check if mouse is within bounds
                bool mouseInBounds = mousePos.X >= topLeft.X && mousePos.X <= bottomRight.X &&
                                     mousePos.Y >= topLeft.Y && mousePos.Y <= bottomRight.Y;

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"CheckMousePosition: Mouse at ({mousePos.X},{mousePos.Y}), bounds ({topLeft.X},{topLeft.Y})-({bottomRight.X},{bottomRight.Y}), inBounds={mouseInBounds}");
#endif

                // Track if state changed
                bool stateChanged = false;

                if (mouseInBounds && !isMouseOver)
                {
                    // Mouse just entered - start hiding
                    isMouseOver = true;
                    mouseCheckTimer.Start();
                    stateChanged = true;
                }
                else if (!mouseInBounds && isMouseOver)
                {
                    // Mouse just left - stop hiding
                    isMouseOver = false;
                    mouseCheckTimer.Stop();
                    stateChanged = true;
                }

                // Update appearance if state changed
                if (stateChanged)
                {
                    UpdateOverlayAppearance();
                }
            }
            catch
            {
                // Fallback: assume mouse not over
                if (isMouseOver)
                {
                    isMouseOver = false;
                    mouseCheckTimer.Stop();
                    UpdateOverlayAppearance();
                }
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

            // persist current size as well
            settings.WindowWidth = this.Width;
            settings.WindowHeight = this.Height;

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
            
            // Remove window message hook
            hwndSource?.RemoveHook(WndProc);

            trayIcon?.Dispose();
            updateTimer?.Stop();
            mouseCheckTimer?.Stop();
        }

        private void ToggleLock()
        {
            isUnlocked = !isUnlocked;
            SetClickThrough(!isUnlocked);
            UpdateOverlayAppearance();
        }

        private void ToggleLock(object? sender, RoutedEventArgs e) => ToggleLock();

        private void ToggleHide()
        {
            isHidden = !isHidden;

            if (isHidden)
            {
                // stop timers to reduce work while hidden
                updateTimer?.Stop();
                mouseCheckTimer?.Stop();

                // release any mouse capture and hide the window
                if (this.IsMouseCaptured) this.ReleaseMouseCapture();
                this.Hide();
            }
            else
            {
                // show the window and restore state
                this.Show();
                ApplySettings();
                UpdateDesktopName();

                // restart periodic updates
                updateTimer?.Start();

                // restore click-through (normal behavior)
                SetClickThrough(true);
                UpdateOverlayAppearance();
            }

            // update menu item immediately (in case menu remains open)
            if (hideMenuItem != null)
            {
                hideMenuItem.Checked = isHidden;
                hideMenuItem.Text = isHidden ? "Weergeven" : "Verbergen";
            }
        }
    }
}
