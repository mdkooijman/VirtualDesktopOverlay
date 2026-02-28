# Virtual Desktop Overlay - Functionality Summary

## Overview
Virtual Desktop Overlay is a lightweight WPF application that displays the current Windows virtual desktop name and number in a customizable overlay window on your screen.

## Core Features

### 1. Virtual Desktop Display
- Shows the current virtual desktop name (if custom name is set) or number (e.g., "Desktop 2")
- Updates in real-time when switching between virtual desktops
- Reads desktop information from Windows Registry
- Supports custom desktop names set through Windows settings

### 2. Window Behavior
- **Always on Top**: Overlay stays above other windows
- **Click-Through**: Window doesn't steal focus or interrupt workflow
- **Draggable**: Can be repositioned anywhere on screen
- **Persistent Position**: Remembers position between sessions
- **Resizable**: Window size is adjustable and saved

### 3. Appearance Customization

#### Theme Options
- **Light Theme**: Light background with dark text
- **Dark Theme**: Dark background with light text
- **Auto Theme**: Automatically follows Windows system theme

#### Visual Settings
- **Opacity**: Adjustable transparency (0.0 to 1.0)
- **Font Size**: Customizable text size (8-48 px)
- **Font Family**: Choose from available system fonts
- **Acrylic Effect**: Optional Windows 10/11 blur effect (modern glass-like appearance)

### 4. Settings Management
- Settings accessible via right-click context menu
- All settings saved to portable JSON file (`settings.json`)
- Settings file stored next to executable (portable application)
- Includes window position, size, theme, opacity, font settings

### 5. System Integration
- **Run at Startup**: Optional Windows startup integration
- Uses Windows Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)
- Can be toggled on/off from settings
- Automatically adds/removes registry entry

### 6. Runtime Requirements
- Requires .NET 10 Desktop Runtime or later
- Shows error dialog with download link if runtime is missing
- Automatically redirects to Microsoft download page

## User Interface

### Main Window
- Displays current desktop name/number
- Borderless, rounded corners
- Shadow effect for better visibility
- Right-click for context menu

### Context Menu Options
1. **Settings**: Opens settings dialog
2. **Exit**: Closes the application

### Settings Window
- Modal dialog with all customization options
- Live preview of changes
- Organized sections:
  - Window position and size
  - Theme selection (Light/Dark/Auto)
  - Opacity slider
  - Font size adjustment
  - Font family dropdown
  - Acrylic effect toggle
  - Startup configuration

## Technical Details

### Registry Access
- Reads virtual desktop information from:
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops`
  - Checks multiple paths for custom desktop names
- Writes startup configuration to:
  - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

### Update Mechanism
- Uses `DispatcherTimer` with 500ms interval
- Continuously polls registry for desktop changes
- Updates display when desktop switch is detected

### File Structure
- Standalone executable
- `settings.json` created on first run
- No installation required (portable)

## Supported Platforms
- Windows 10 (with virtual desktop support)
- Windows 11
- Requires .NET 10 Desktop Runtime

## Default Settings
- **Position**: Lower-right corner (20px from right, 60px from bottom)
- **Size**: 300x50 pixels
- **Theme**: Dark
- **Opacity**: 0.8 (80%)
- **Font**: Segoe UI, 18px
- **Acrylic**: Disabled
- **Startup**: Disabled
