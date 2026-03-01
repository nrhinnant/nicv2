# Feature 031: Dark Mode Theme

## Overview

The application supports light and dark themes that can be toggled at runtime. The theme follows Windows system preferences by default, but users can override this with an explicit light or dark mode selection.

## Behavior

### Theme Modes

| Mode | Description |
|------|-------------|
| System | Automatically follows Windows system theme setting |
| Light | Forces light theme regardless of system setting |
| Dark | Forces dark theme regardless of system setting |

### Theme Toggle

A theme toggle button is available in the main window header, next to the Refresh button. The button text shows the theme that clicking will switch to:
- When in light mode: shows "Dark"
- When in dark mode: shows "Light"

### System Theme Detection

The application detects the Windows system theme by reading the registry key:
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme
```

- Value `0` = Windows is in dark mode
- Value `1` = Windows is in light mode

When the user preference is set to "System" mode, the application automatically updates when Windows theme changes.

### Theme Persistence

The selected theme mode is persisted to:
```
%LocalAppData%\WfpTrafficControl\theme.txt
```

The file contains a single line with the theme mode name: `System`, `Light`, or `Dark`.

## Color Definitions

### Light Theme Colors

| Color Key | Hex Value | Purpose |
|-----------|-----------|---------|
| PrimaryBrush | #0078D4 | Accent color for buttons and highlights |
| BackgroundBrush | #F3F3F3 | Main window background |
| CardBackgroundBrush | #FFFFFF | Card and panel backgrounds |
| TextPrimaryBrush | #1A1A1A | Primary text color |
| TextSecondaryBrush | #666666 | Secondary/muted text |

### Dark Theme Colors

| Color Key | Hex Value | Purpose |
|-----------|-----------|---------|
| PrimaryBrush | #60CDFF | Accent color (brighter for dark mode) |
| BackgroundBrush | #202020 | Main window background |
| CardBackgroundBrush | #2D2D2D | Card and panel backgrounds |
| TextPrimaryBrush | #FFFFFF | Primary text color |
| TextSecondaryBrush | #A0A0A0 | Secondary/muted text |

## Files Changed

| File | Description |
|------|-------------|
| `src/ui/WfpTrafficControl.UI/Services/IThemeService.cs` | Interface with ThemeMode enum and event args |
| `src/ui/WfpTrafficControl.UI/Services/ThemeService.cs` | Implementation with registry-based detection |
| `src/ui/WfpTrafficControl.UI/Themes/LightTheme.xaml` | Light theme color definitions |
| `src/ui/WfpTrafficControl.UI/Themes/DarkTheme.xaml` | Dark theme color definitions |
| `src/ui/WfpTrafficControl.UI/Resources/Styles.xaml` | Updated to use DynamicResource |
| `src/ui/WfpTrafficControl.UI/App.xaml` | Loads theme dictionary |
| `src/ui/WfpTrafficControl.UI/App.xaml.cs` | Initializes theme service |
| `src/ui/WfpTrafficControl.UI/MainWindow.xaml` | Added theme toggle button |
| `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` | Added toggle command |
| `src/ui/WfpTrafficControl.UI/Views/*.xaml` | Updated to DynamicResource |

## Technical Notes

### DynamicResource vs StaticResource

All color/brush references in XAML use `DynamicResource` instead of `StaticResource`. This allows the colors to update at runtime when the theme changes without restarting the application.

### Theme Dictionary Swapping

When the theme changes, the `ThemeService`:
1. Creates a new `ResourceDictionary` from the appropriate theme file
2. Finds and removes the current theme dictionary from `Application.Resources.MergedDictionaries`
3. Inserts the new theme dictionary at position 0 (before Styles.xaml)

### System Theme Change Detection

The service subscribes to `Microsoft.Win32.SystemEvents.UserPreferenceChanged` to detect when Windows theme changes. When a change is detected and the user is in "System" mode, the theme is automatically reapplied.

## Testing

### Manual Testing

1. **Toggle Theme**: Click the theme button in the header, verify all UI elements update colors
2. **System Mode**: Set to System mode, change Windows theme, verify app follows
3. **Persistence**: Toggle theme, restart app, verify theme is restored
4. **All Views**: Navigate to Dashboard, Policy Editor, and Logs tabs to verify theming

### Automated Tests

See `tests/UI/ThemeServiceTests.cs` for unit tests covering:
- Theme mode switching
- System theme detection
- Theme persistence
- Event firing

## Known Limitations

1. Some hardcoded colors remain (e.g., validation warning backgrounds)
2. DataGrid alternating row colors have fixed values
3. Loading overlay backgrounds are semi-transparent white
4. No animated transitions when theme changes

## Future Enhancements

- Add theme selection to settings/preferences dialog
- Animated theme transitions
- Custom accent color selection
- High contrast theme support
- Per-monitor DPI-aware theme assets
