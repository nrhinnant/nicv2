# 003 — Service Hosting

## Behavior

This milestone implements the Windows Service hosting infrastructure for the WfpTrafficControl service. The service runs as a standard Windows service with clean start/stop lifecycle management and logging to Windows Event Log.

### Service Characteristics

| Property | Value |
|----------|-------|
| Service Name | `WfpTrafficControl` |
| Display Name | `WFP Traffic Control Service` |
| Startup Type | Manual (demand) |
| Log Output | Windows Event Log (Application) |
| Recovery | Restart on failure (5s, 10s, 30s delays) |

### Lifecycle

1. **Start**: Service logs startup with version and timestamp, enters main loop
2. **Running**: Service idles (placeholder for future WFP policy controller)
3. **Stop**: Service logs shutdown, gracefully terminates

### Logging

Events are written to Windows Event Log under:
- **Log**: Application
- **Source**: WfpTrafficControl

Log levels:
- `Information` - Service start/stop events
- `Debug` - Internal state changes
- `Error` - Unhandled exceptions

To view logs:
```powershell
Get-EventLog -LogName Application -Source WfpTrafficControl -Newest 20
```

Or use Event Viewer: `eventvwr.msc` → Windows Logs → Application → filter by Source = "WfpTrafficControl"

## Configuration / Schema Changes

No configuration files are required for this milestone. The service runs with default settings.

Future milestones will add:
- Policy file configuration
- IPC endpoint configuration
- Logging verbosity settings

## How to Build

```powershell
# Build the service
dotnet build src/service/Service.csproj -c Release

# Or build entire solution
dotnet build
```

## How to Install

All scripts require **Administrator privileges**.

### Quick Start (Dev VM)

```powershell
# From repository root, run as Administrator:

# 1. Install the service
.\scripts\Install-Service.ps1

# 2. Start the service
.\scripts\Start-Service.ps1

# 3. Verify it's running
Get-Service WfpTrafficControl

# 4. Check logs
Get-EventLog -LogName Application -Source WfpTrafficControl -Newest 10

# 5. Stop and uninstall when done
.\scripts\Stop-Service.ps1
.\scripts\Uninstall-Service.ps1 -RemoveFiles
```

### Custom Install Path

```powershell
.\scripts\Install-Service.ps1 -InstallPath "D:\Services\WfpTrafficControl"
.\scripts\Uninstall-Service.ps1 -InstallPath "D:\Services\WfpTrafficControl" -RemoveFiles
```

## Scripts Reference

| Script | Purpose |
|--------|---------|
| `Install-Service.ps1` | Builds, publishes, and registers the service |
| `Uninstall-Service.ps1` | Stops and removes the service (optionally deletes files) |
| `Start-Service.ps1` | Starts the service |
| `Stop-Service.ps1` | Stops the service |

### Install-Service.ps1

Builds the service in Release mode and installs it.

```powershell
.\Install-Service.ps1 [-InstallPath <path>]
```

**Parameters:**
- `-InstallPath`: Installation directory (default: `C:\Program Files\WfpTrafficControl`)

**What it does:**
1. Builds and publishes service to install path
2. Creates Windows service via `sc.exe create`
3. Sets service description
4. Configures recovery options (restart on failure)

### Uninstall-Service.ps1

Removes the service and optionally deletes files.

```powershell
.\Uninstall-Service.ps1 [-RemoveFiles] [-InstallPath <path>]
```

**Parameters:**
- `-RemoveFiles`: Also delete the installation directory
- `-InstallPath`: Installation directory to remove (default: `C:\Program Files\WfpTrafficControl`)

**What it does:**
1. Stops service if running (waits up to 30 seconds)
2. Removes service registration via `sc.exe delete`
3. Optionally removes installation directory

## Rollback / Uninstall Behavior

The service can be completely removed using:

```powershell
.\scripts\Uninstall-Service.ps1 -RemoveFiles
```

This will:
1. Stop the service if running
2. Remove the Windows service registration
3. Delete all installed files

**No system modifications persist after uninstall.** The service does not modify:
- Registry (beyond service registration)
- WFP state (no filters created in this milestone)
- Network configuration
- Any other system settings

## Manual Operations

If scripts fail, you can use these manual commands:

```powershell
# Create service manually
sc.exe create WfpTrafficControl binPath= "C:\Program Files\WfpTrafficControl\WfpTrafficControl.Service.exe" start= demand displayname= "WFP Traffic Control Service"

# Start/stop
sc.exe start WfpTrafficControl
sc.exe stop WfpTrafficControl

# Query status
sc.exe query WfpTrafficControl

# Delete service
sc.exe delete WfpTrafficControl
```

## How to Run (Console Mode)

For debugging, the service can run in console mode without installing:

```powershell
dotnet run --project src/service/Service.csproj
```

Press Ctrl+C to stop. In console mode, logs appear in the terminal instead of Event Log.

## Smoke Test

A smoke test script validates the full service lifecycle:

```powershell
.\tests\Smoke-Test.ps1
```

See Phase 5 (Test Development) for details.

## Known Limitations

- Service has no functional logic yet (placeholder for WFP policy controller)
- No IPC endpoint (CLI cannot communicate with service)
- No policy file support
- EventLog source registration may require the first run to be elevated
- Service does not auto-start on boot (manual startup type)

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Hosting | 8.0.0 | Generic host infrastructure |
| Microsoft.Extensions.Hosting.WindowsServices | 8.0.0 | Windows Service support |
| Microsoft.Extensions.Logging.EventLog | 8.0.0 | EventLog logging provider |

## Security Considerations

- Service runs as LocalSystem by default (required for WFP operations)
- **Elevation check**: On startup, the service verifies it has Administrator privileges before any WFP operations or IPC server initialization. If not elevated, the service logs a CRITICAL error and exits immediately (exit code 1)
- All management scripts require Administrator privileges
- Service does not expose any network endpoints
- No external dependencies or network calls

## Troubleshooting

### Service exits with "must run with Administrator privileges" error

The service performs an elevation check on startup. If running without Administrator privileges:
- A CRITICAL error is logged: "Service must run with Administrator privileges to access Windows Filtering Platform"
- The service exits immediately with exit code 1
- No WFP operations or IPC server are started

**Solutions:**
1. When running as a Windows Service, ensure it runs as LocalSystem (default) or another Administrator account
2. When running in console mode, start PowerShell/cmd as Administrator:
   ```powershell
   # Right-click PowerShell → Run as Administrator
   dotnet run --project src/service/Service.csproj
   ```

### Service fails to start

1. Check Event Viewer for errors:
   ```powershell
   Get-EventLog -LogName Application -Source WfpTrafficControl -Newest 20
   ```

2. Verify the executable exists:
   ```powershell
   Test-Path "C:\Program Files\WfpTrafficControl\WfpTrafficControl.Service.exe"
   ```

3. Try running in console mode to see errors:
   ```powershell
   & "C:\Program Files\WfpTrafficControl\WfpTrafficControl.Service.exe"
   ```

### "Access Denied" errors

Ensure you're running PowerShell as Administrator.

### Service won't stop

If the service hangs during stop:
```powershell
# Force kill the process
taskkill /F /FI "SERVICES eq WfpTrafficControl"

# Then delete the service
sc.exe delete WfpTrafficControl
```

### EventLog source not found

On first run, Windows may need to create the EventLog source. Run the service once as Administrator, or manually register:

```powershell
New-EventLog -LogName Application -Source WfpTrafficControl
```
