# Virtual Desktop Overlay - Execution Flow Analysis

## Table of Contents
1. [Application Startup Flow](#application-startup-flow)
2. [Main Window Initialization](#main-window-initialization)
3. [Main Loop (Timer)](#main-loop-timer)
4. [Event Handlers](#event-handlers)

---

## Application Startup Flow

### Entry Point: App.xaml.cs - OnStartup

```csharp
protected override void OnStartup(StartupEventArgs e)
```

**Line-by-line execution:**

1. `base.OnStartup(e);`
   - Calls the base WPF Application startup logic
   - Initializes the application framework

2. `if (!CheckDotNetVersion())`
   - Calls the version check method
   - Returns `false` if .NET version is below 10

3. **If version check fails:**
   - `MessageBox.Show(...)`
     - Displays error dialog to user
     - Informs about .NET 10 requirement
     - Waits for user to click OK

4. `Process.Start(new ProcessStartInfo {...})`
   - Creates new process start info
   - Sets `FileName` to .NET 10 download URL
   - Sets `UseShellExecute = true` to open in default browser
   - Opens browser to Microsoft download page
   - Wrapped in try-catch in case browser fails

5. **If browser fails to open:**
   - Shows secondary MessageBox with URL text
   - User can manually copy the URL

6. `this.Shutdown();`
   - Terminates the application
   - No window is created

7. **If version check passes:**
   - OnStartup completes normally
   - WPF framework continues to load MainWindow.xaml
   - Application proceeds to MainWindow initialization

### Version Check Method

```csharp
private bool CheckDotNetVersion()
```

1. `var version = Environment.Version;`
   - Gets the current runtime version (e.g., Version 10.0.x)

2. `return version.Major >= 10;`
   - Compares major version number
   - Returns `true` if 10 or higher
   - Returns `false` for .NET 9, 8, 7, etc.

3. **Entire method wrapped in try-catch:**
   - If any exception occurs, returns `false`
   - Assumes incompatible version on error

---

## Main Window Initialization

### MainWindow Constructor

```csharp
public MainWindow()
```

**Line-by-line execution:**

1. `InitializeComponent();`
   - Loads and parses MainWindow.xaml
   - Creates all UI elements (Border, TextBlock, ContextMenu)
   - Sets up data bindings
   - Applies initial styles

2. `_settings = AppSettings.Load();`
   - Calls static Load method on AppSettings class
   - Attempts to read `settings.json` from executable directory
   - If file exists: deserializes JSON into AppSettings object
   - If file doesn't exist or error: creates new AppSettings with defaults

3. `ApplySettings();`
   - Calls method to apply loaded settings to window

### ApplySettings Method

```csharp
private void ApplySettings()
```

1. **Position & Size:**
   ```csharp
   if (_settings.WindowLeft == 0 && _settings.WindowTop == 0)
   ```
   - Checks if this is first run (position not set)
   - If first run:
     - Gets primary screen dimensions
     - Calculates default position (lower-right corner)
     - Sets position 20px from right, 60px from bottom
     - Updates settings object

2. `this.Left = _settings.WindowLeft;`
   - Sets window X position

3. `this.Top = _settings.WindowTop;`
   - Sets window Y position

4. `this.Width = _settings.WindowWidth;`
   - Sets window width (default 300)

5. `this.Height = _settings.WindowHeight;`
   - Sets window height (default 50)

6. **Theme Application:**
   ```csharp
   string effectiveTheme = AppSettings.GetEffectiveTheme(_settings.Theme);
   ```
   - Resolves theme: if "Auto", reads Windows registry
   - Registry path: `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`
   - Reads `AppsUseLightTheme` value (1 = Light, 0 = Dark)
   - Returns "Light" or "Dark" string

7. `if (effectiveTheme == "Light") {...} else {...}`
   - Sets MainBorder.Background based on theme
   - Light: `new SolidColorBrush(Color.FromArgb(204, 240, 240, 240))` (light gray, 80% opacity)
   - Dark: `new SolidColorBrush(Color.FromArgb(204, 30, 30, 30))` (dark gray, 80% opacity)

8. `MainBorder.Background.Opacity = _settings.Opacity;`
   - Applies user's chosen opacity (0.0 to 1.0)

9. `DesktopText.Foreground = ...`
   - Sets text color based on theme
   - Light: Black
   - Dark: White

10. `DesktopText.FontSize = _settings.FontSize;`
    - Sets font size (default 18)

11. `DesktopText.FontFamily = new FontFamily(_settings.FontFamily);`
    - Sets font family (default "Segoe UI")

12. **Acrylic Effect:**
    ```csharp
    if (_settings.AcrylicEffect)
    ```
    - If enabled:
      - Enables blur-behind window effect
      - Sets window background to transparent
      - Uses Win32 API via WindowsDesktop.dll
    - If disabled:
      - Disables blur effect
      - Uses solid color background

### UpdateDesktopName Method (Initial Call)

```csharp
private void UpdateDesktopName()
```

**First execution during initialization:**

1. `var info = VirtualDesktopHelper.GetCurrentDesktopInfo();`
   - Calls static helper method
   - Opens registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops`
   - Reads `CurrentVirtualDesktop` (16-byte GUID)
   - Reads `VirtualDesktopIDs` (array of all desktop GUIDs)
   - Calculates desktop count (array length / 16)
   - Finds matching GUID to determine current index
   - Attempts to read custom name from registry
   - Returns DesktopInfo object with Name and Index

2. `if (_currentDesktop != info.Name)`
   - Compares with previous desktop name (initially null)
   - First run: always true

3. `_currentDesktop = info.Name;`
   - Stores current desktop name

4. `DesktopText.Text = info.Name;`
   - Updates TextBlock content
   - Displays on screen (e.g., "Desktop 1" or custom name)

### Timer Setup

```csharp
_updateTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromMilliseconds(500)
};
_updateTimer.Tick += UpdateTimer_Tick;
_updateTimer.Start();
```

1. `new DispatcherTimer`
   - Creates UI thread timer (runs on Dispatcher thread)

2. `Interval = TimeSpan.FromMilliseconds(500)`
   - Sets timer to fire every 500ms (0.5 seconds)

3. `_updateTimer.Tick += UpdateTimer_Tick;`
   - Subscribes event handler

4. `_updateTimer.Start();`
   - Begins timer execution
   - First tick will occur in 500ms

### Final Initialization

```csharp
UpdateDesktopName();
```
- Makes initial call to display desktop name
- Ensures window shows data immediately on startup

---

## Main Loop (Timer)

### UpdateTimer_Tick Event

```csharp
private void UpdateTimer_Tick(object? sender, EventArgs e)
```

**This runs every 500ms continuously while app is running:**

1. `var info = VirtualDesktopHelper.GetCurrentDesktopInfo();`
   - Opens Windows Registry
   - Reads current virtual desktop GUID
   - Reads all desktop GUIDs
   - Compares to find current desktop index
   - Attempts to read custom desktop name
   - Returns DesktopInfo with Name and Index

2. `if (_currentDesktop != info.Name)`
   - Compares new desktop name with stored name
   - Detects if user switched virtual desktops
   - Most of the time this is `false` (no change)

3. **If desktop changed:**
   - `_currentDesktop = info.Name;`
     - Updates stored desktop name
   
   - `DesktopText.Text = info.Name;`
     - Updates UI to show new desktop name
     - User sees the change immediately

**Timer continues running until application closes**

---

## Event Handlers

### 1. MainBorder_MouseLeftButtonDown

**Triggered when:** User clicks and holds left mouse button on the overlay window

```csharp
private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
```

1. `this.DragMove();`
   - Built-in WPF method
   - Allows user to drag the window
   - Window follows mouse cursor until button released
   - **Note:** Window position NOT saved yet (happens on close)

---

### 2. SettingsMenuItem_Click

**Triggered when:** User right-clicks overlay and selects "Settings" from context menu

```csharp
private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
```

1. `_updateTimer.Stop();`
   - Pauses the 500ms update timer
   - Prevents registry reads while settings dialog is open

2. `var settingsWindow = new SettingsWindow(_settings);`
   - Creates new SettingsWindow instance
   - Passes current settings object as parameter
   - Settings window constructor copies settings to temporary object

3. `if (settingsWindow.ShowDialog() == true)`
   - Opens settings window as modal dialog
   - Blocks main window until settings closed
   - Returns `true` if user clicked Save
   - Returns `false` if user clicked Cancel or closed window

4. **If user clicked Save:**

   a. `_settings = settingsWindow.UpdatedSettings;`
      - Retrieves modified settings from dialog
      - Overwrites current settings object

   b. `_settings.Save();`
      - Serializes settings to JSON
      - Writes to `settings.json` file
      - Updates Windows startup registry if needed
      - **Registry update logic:**
        - If RunAtStartup = true: Adds registry key with exe path
        - If RunAtStartup = false: Removes registry key
        - Path: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
        - Key name: "VirtualDesktopOverlay"

   c. `ApplySettings();`
      - Re-applies all settings to main window
      - Updates theme, opacity, font, size, etc.
      - Window updates visually immediately

   d. `UpdateDesktopName();`
      - Refreshes desktop display
      - Ensures text renders correctly with new font settings

5. `_updateTimer.Start();`
   - Resumes the 500ms update timer
   - Continues monitoring desktop changes

---

### 3. ExitMenuItem_Click

**Triggered when:** User right-clicks overlay and selects "Exit" from context menu

```csharp
private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
```

1. `Application.Current.Shutdown();`
   - Triggers application shutdown sequence
   - Calls MainWindow_Closing event
   - Then exits application

---

### 4. MainWindow_Closing

**Triggered when:** 
- User closes window via Exit menu
- User presses Alt+F4
- Application.Shutdown() is called
- Windows is shutting down

```csharp
private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
```

1. `_settings.WindowLeft = this.Left;`
   - Saves current window X position

2. `_settings.WindowTop = this.Top;`
   - Saves current window Y position

3. `_settings.WindowWidth = this.Width;`
   - Saves current window width

4. `_settings.WindowHeight = this.Height;`
   - Saves current window height

5. `_settings.Save();`
   - Serializes all settings to JSON
   - Writes to `settings.json`
   - Position/size will be restored on next launch

**After this event completes:**
- Timer stops automatically
- Window closes
- Application process terminates

---

## Settings Window Event Handlers

### SettingsWindow Constructor

```csharp
public SettingsWindow(AppSettings currentSettings)
```

1. `InitializeComponent();`
   - Loads SettingsWindow.xaml
   - Creates all UI controls

2. `_originalSettings = currentSettings;`
   - Stores reference to main window's settings

3. `_tempSettings = new AppSettings { ... };`
   - Creates temporary copy of all settings
   - Used for live preview without committing changes

4. `this.DataContext = _tempSettings;`
   - Sets data context for WPF bindings
   - All UI controls bind to _tempSettings properties

5. `LoadAvailableFonts();`
   - Enumerates system fonts
   - Populates FontFamilyComboBox
   - Calls `Fonts.SystemFontFamilies`
   - Sorts alphabetically
   - Selects current font

6. `OpacitySlider.Value = _tempSettings.Opacity;`
   - Sets slider position to current opacity

7. `FontSizeSlider.Value = _tempSettings.FontSize;`
   - Sets slider position to current font size

8. `ThemeComboBox.SelectedItem = ...`
   - Finds matching ComboBoxItem for current theme
   - Selects "Light", "Dark", or "Auto"

9. `AcrylicCheckBox.IsChecked = _tempSettings.AcrylicEffect;`
   - Sets checkbox state

10. `StartupCheckBox.IsChecked = AppSettings.IsSetToRunAtStartup();`
    - Reads actual registry state
    - Checks if startup entry exists
    - Sets checkbox accordingly

---

### SaveButton_Click

**Triggered when:** User clicks "Save" button in settings dialog

```csharp
private void SaveButton_Click(object sender, RoutedEventArgs e)
```

1. `UpdatedSettings = _tempSettings;`
   - Stores temporary settings to public property
   - Main window will read this property

2. `this.DialogResult = true;`
   - Sets dialog result to true
   - Causes ShowDialog() to return true
   - Closes the settings window

**Control returns to MainWindow SettingsMenuItem_Click handler**

---

### CancelButton_Click

**Triggered when:** User clicks "Cancel" button in settings dialog

```csharp
private void CancelButton_Click(object sender, RoutedEventArgs e)
```

1. `this.DialogResult = false;`
   - Sets dialog result to false
   - Causes ShowDialog() to return false
   - Closes the settings window
   - All changes in _tempSettings are discarded

**Control returns to MainWindow SettingsMenuItem_Click handler**

---

### OpacitySlider_ValueChanged

**Triggered when:** User moves opacity slider

```csharp
private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
```

1. `if (_tempSettings != null)`
   - Checks if initialization complete
   - Prevents errors during window load

2. `_tempSettings.Opacity = e.NewValue;`
   - Updates temporary settings
   - Value between 0.0 and 1.0

**Note:** Preview updates happen through data binding automatically

---

### FontSizeSlider_ValueChanged

**Triggered when:** User moves font size slider

```csharp
private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
```

1. `if (_tempSettings != null)`
   - Safety check

2. `_tempSettings.FontSize = (int)e.NewValue;`
   - Converts double to int
   - Updates font size (8-48 range)

---

### ThemeComboBox_SelectionChanged

**Triggered when:** User selects different theme option

```csharp
private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
```

1. `if (_tempSettings != null && ThemeComboBox.SelectedItem is ComboBoxItem item)`
   - Safety checks

2. `_tempSettings.Theme = item.Content.ToString() ?? "Dark";`
   - Gets selected item text ("Light", "Dark", or "Auto")
   - Updates temporary settings

---

### FontFamilyComboBox_SelectionChanged

**Triggered when:** User selects different font

```csharp
private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
```

1. `if (_tempSettings != null && FontFamilyComboBox.SelectedItem is FontFamily font)`
   - Safety checks

2. `_tempSettings.FontFamily = font.Source;`
   - Gets font family name string
   - Updates temporary settings

---

### AcrylicCheckBox_Changed

**Triggered when:** User toggles acrylic effect checkbox

```csharp
private void AcrylicCheckBox_Changed(object sender, RoutedEventArgs e)
```

1. `if (_tempSettings != null)`
   - Safety check

2. `_tempSettings.AcrylicEffect = AcrylicCheckBox.IsChecked == true;`
   - Updates boolean setting

---

### StartupCheckBox_Changed

**Triggered when:** User toggles startup checkbox

```csharp
private void StartupCheckBox_Changed(object sender, RoutedEventArgs e)
```

1. `if (_tempSettings != null)`
   - Safety check

2. `_tempSettings.RunAtStartup = StartupCheckBox.IsChecked == true;`
   - Updates boolean setting
   - Actual registry update happens on Save

---

## Summary of Execution Flow

### Startup Sequence
1. App.xaml.cs OnStartup → Check .NET version
2. MainWindow constructor → Load settings
3. ApplySettings → Configure window appearance
4. UpdateDesktopName → Show initial desktop
5. Start timer → Begin monitoring loop

### Main Loop
- Every 500ms: Check registry for desktop changes
- Update display if desktop switched

### User Interactions
- Drag window → Move position (saved on close)
- Right-click → Show context menu
- Settings → Open dialog, edit, save/cancel
- Exit → Save position and close

### On Close
- Save window position and size
- Write settings.json
- Terminate application
