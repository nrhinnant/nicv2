# Feature 030: System Tray Integration

## Overview

The system tray icon provides quick status visibility and common actions without opening the full GUI window. This is a standard expectation for Windows security software and allows the application to run unobtrusively in the background.

## Behavior

### Tray Icon States

The tray icon displays different colors based on the application state:

| State | Color | Description |
|-------|-------|-------------|
| Active | Green | Service connected with active filters |
| No Policy | Yellow/Amber | Service connected but no policy applied |
| Disconnected | Gray | Service is offline or not responding |
| Error | Red | An error has occurred (reserved for future use) |

### Tooltip Information

Hovering over the tray icon displays:
- Application name: "WFP Traffic Control"
- Current status (Active, No Policy, or Offline)
- Number of active filters (when connected)
- Service version (when connected)

### Context Menu

Right-clicking the tray icon shows:
- **Status: Connected/Offline** (display only)
- **Filters: N** (display only)
- ---
- **Show Window** - Restores and brings the main window to front
- **Refresh Status** - Triggers a status refresh from the service
- ---
- **Exit** - Closes the application completely

### Minimize to Tray

- Minimizing the window hides it and shows a balloon notification
- Closing the window (X button) also minimizes to tray instead of exiting
- Double-clicking the tray icon restores the window
- Use the "Exit" menu item to fully close the application

### Balloon Notifications

The tray icon shows notifications for:
- Application minimized to system tray
- Future: Policy applied/rolled back, blocked connections (configurable)

## Configuration

Currently, the tray icon behavior is not configurable. All features are enabled by default.

## Files Changed

| File | Description |
|------|-------------|
| `src/ui/WfpTrafficControl.UI/Services/ITrayIconService.cs` | Interface definition |
| `src/ui/WfpTrafficControl.UI/Services/TrayIconService.cs` | Implementation using System.Windows.Forms.NotifyIcon |
| `src/ui/WfpTrafficControl.UI/App.xaml.cs` | Integration and lifecycle management |
| `src/ui/WfpTrafficControl.UI/GlobalUsings.cs` | Namespace disambiguation for WPF + WinForms |
| `src/ui/WfpTrafficControl.UI/WfpTrafficControl.UI.csproj` | Added UseWindowsForms reference |

## Technical Notes

### Icon Generation

Icons are generated programmatically using System.Drawing to avoid external icon file dependencies. A simple colored circle is drawn:
- 16x16 pixels
- Anti-aliased circle fill
- Subtle border for visibility on both light and dark taskbars

### GDI Handle Management

The icon creation uses `Bitmap.GetHicon()` which returns a GDI icon handle. To prevent handle leaks:
1. The icon is cloned using `Icon.Clone()`
2. The original handle is destroyed using `DestroyIcon` P/Invoke
3. Icons are disposed when the state changes or the service is disposed

### WPF + WinForms Interop

Adding `<UseWindowsForms>true</UseWindowsForms>` to the project file causes namespace collisions between WPF and WinForms (e.g., `Application`, `UserControl`). This is resolved with global using aliases in `GlobalUsings.cs`:

```csharp
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
// etc.
```

## Testing

### Manual Testing

1. **Icon States**: Start the app, verify gray icon. Start service, verify green/yellow icon based on filter count.
2. **Tooltip**: Hover over icon, verify status information is accurate.
3. **Context Menu**: Right-click icon, verify all menu items work.
4. **Minimize to Tray**: Minimize window, verify it hides and notification appears.
5. **Close to Tray**: Close window with X button, verify it minimizes instead of exiting.
6. **Show Window**: Double-click icon or use menu, verify window restores.
7. **Exit**: Use Exit menu, verify application closes completely.

### Automated Tests

See `tests/UI/TrayIconServiceTests.cs` for unit tests covering:
- State transitions
- Icon visibility
- Event firing
- Proper disposal

## Known Limitations

1. Icons are simple colored circles - no detailed graphics
2. Balloon notifications use the basic Windows style (no custom UI)
3. No configuration options for disabling minimize-to-tray behavior
4. No notification settings (all notifications enabled)

## Future Enhancements

- Custom icon graphics
- Configurable balloon notifications
- Quick action: Apply last policy
- Quick action: View recent blocked connections
- Settings to control minimize-to-tray behavior
