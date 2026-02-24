# Feature 027: UI Phase 1 MVP

## Overview

This feature implements a WPF desktop application providing a graphical user interface for the WFP Traffic Control system. The UI enables users to manage firewall policies, monitor system status, and perform operations without requiring CLI knowledge.

## Architecture

### Technology Stack

- **Framework**: WPF (.NET 8.0-windows)
- **MVVM Pattern**: CommunityToolkit.Mvvm (source generators)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **IPC**: Named pipe client (same protocol as CLI)

### Project Structure

```
src/ui/WfpTrafficControl.UI/
├── WfpTrafficControl.UI.csproj    # Project file
├── app.manifest                    # Admin elevation requirement
├── App.xaml / App.xaml.cs         # Application entry with DI
├── MainWindow.xaml / .cs          # Main shell with navigation
├── Resources/
│   └── Styles.xaml                # Shared styles and colors
├── Views/
│   ├── DashboardView.xaml         # System status and quick actions
│   └── PolicyEditorView.xaml      # Rule management
├── ViewModels/
│   ├── MainViewModel.cs           # Shell state and navigation
│   ├── DashboardViewModel.cs      # Dashboard logic
│   └── PolicyEditorViewModel.cs   # Policy editing logic
├── Services/
│   ├── IServiceClient.cs          # IPC interface
│   ├── ServiceClient.cs           # Named pipe implementation
│   ├── IDialogService.cs          # Dialog interface
│   └── DialogService.cs           # WPF dialog implementation
└── Converters/
    ├── BoolToVisibilityConverter.cs
    ├── InverseBoolConverter.cs
    └── InverseBoolToVisibilityConverter.cs
```

## Features

### Dashboard Screen

The dashboard provides at-a-glance system status:

- **Service Status**: Online/Offline indicator with version
- **Active Filters**: Count of installed WFP filters
- **Policy Status**: Currently applied policy version
- **LKG Status**: Availability of Last Known Good backup

**Quick Actions:**
- **Apply Policy**: Open file picker, preview, and apply
- **Rollback**: Remove all filters (with confirmation)
- **Revert to LKG**: Restore last known good policy

**Recent Activity:**
- Displays last 5 audit log entries
- Shows event type, status, and details

### Policy Editor Screen

Visual editor for firewall rules:

- **Policy Metadata**: Version, default action
- **Rule List**: Sortable list with enable/disable checkboxes
- **Rule Details Panel**: Edit all rule properties
  - ID, Action (allow/block)
  - Direction (inbound/outbound/both)
  - Protocol (tcp/udp/any)
  - Process path
  - Remote IP/CIDR and ports
  - Priority, enabled state, comment

**Operations:**
- New/Open/Save/Save As policy files
- Add/Delete/Reorder rules
- Validate policy (via service)
- Apply to service

### IPC Communication

The UI uses the same named pipe protocol as the CLI:

```
Pipe: \\.\pipe\WfpTrafficControl
Protocol: Length-prefixed JSON (4-byte LE + UTF-8)
Timeout: 5s connect, 30s read/write
```

**Supported Operations:**
- `PingRequest/Response` - Service health check
- `ApplyRequest/Response` - Apply policy file
- `RollbackRequest/Response` - Remove all filters
- `LkgShowRequest/Response` - Get LKG info
- `LkgRevertRequest/Response` - Restore LKG
- `AuditLogsRequest/Response` - Query audit log
- `ValidateRequest/Response` - Validate policy JSON

## Security

### Elevation Requirement

The application requires administrator privileges via manifest:

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

### Confirmation Dialogs

Destructive operations require explicit confirmation:
- Apply Policy: Shows file path, warns about rule changes
- Rollback: Warns all filters will be removed
- Revert to LKG: Shows LKG policy details

## Usage

### Building

```powershell
cd src/ui/WfpTrafficControl.UI
dotnet build
```

### Running

```powershell
# Run from project directory (requires elevation)
dotnet run

# Or run the built executable
bin\Debug\net8.0-windows\WfpTrafficControl.UI.exe
```

### Quick Start

1. Launch the application (will prompt for elevation)
2. Dashboard shows current service status
3. Click "Apply Policy..." to apply a policy file
4. Or go to Policy Editor to create/edit policies

### Creating a Policy

1. Go to **Policy Editor** tab
2. Click **New** to create empty policy
3. Click **+** to add rules
4. Configure each rule (action, direction, protocol, etc.)
5. Click **Validate** to check for errors
6. Click **Save** to save to file
7. Click **Apply to Service** to activate

### Emergency Recovery

If a policy causes connectivity issues:

1. Open Dashboard
2. Click **Rollback** to remove all filters
3. Or click **Revert to LKG** to restore last working policy

## Configuration

No additional configuration required. The UI uses the same service and constants as the CLI.

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.2.2 | MVVM source generators |
| Microsoft.Extensions.DependencyInjection | 8.0.0 | DI container |
| WfpTrafficControl.Shared | (project) | Shared models and IPC |

## Known Limitations

1. **Single instance**: No mutex prevents multiple UI instances
2. **No auto-refresh**: Status must be manually refreshed
3. **No process browser**: Process path must be typed manually
4. **Basic styling**: Default WPF appearance (enhanced in Phase 2)

## Testing

### Manual Testing Checklist

| Test | Steps | Expected |
|------|-------|----------|
| Service offline | Stop service, launch UI | Shows "Offline" status |
| Apply valid policy | Load JSON, click Apply | Success message, filter count updated |
| Apply invalid policy | Load bad JSON | Validation errors shown |
| Rollback | Click Rollback, confirm | Filters removed, success message |
| LKG revert | Click Revert to LKG | LKG restored, success message |
| Create policy | New → Add rules → Save | Policy saved to file |
| Edit rule | Select rule, change fields | Rule updated in list |

### Integration Test

```powershell
# 1. Start service
net start WfpTrafficControl

# 2. Launch UI
.\bin\Debug\net8.0-windows\WfpTrafficControl.UI.exe

# 3. Verify dashboard shows "Online"
# 4. Apply a test policy
# 5. Verify filter count updates
# 6. Rollback and verify filters removed
```

## Future Enhancements (Phase 2+)

- Modern theme (MaterialDesign or ModernWpf)
- Audit log viewer screen
- Settings screen
- Hot reload configuration
- Process path browser
- Rule templates
- Export/import formats
- System tray integration

## Files Changed

New files:
- `src/ui/WfpTrafficControl.UI/` (entire project)

No existing files modified.

## Rollback

To remove the UI:
1. Delete `src/ui/` directory
2. No service or CLI changes required
