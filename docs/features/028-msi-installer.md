# Feature 028: MSI Installer

## Overview

This feature adds a Windows Installer (MSI) package for deploying WfpTrafficControl to production environments. The installer handles:

- Service installation and configuration
- CLI deployment
- System PATH modification (optional)
- WFP cleanup on uninstall
- Version upgrades

## Prerequisites

- **WiX Toolset v5**: The installer uses WiX v5 SDK, installed automatically via NuGet
- **.NET 8.0 SDK**: Required to build the installer project
- **Administrator privileges**: Required to install the MSI

## Project Structure

```
installer/
  WfpTrafficControl.Installer/
    WfpTrafficControl.Installer.wixproj  # WiX v5 SDK project
    Package.wxs                          # Package metadata, features, upgrade logic
    Directories.wxs                      # Directory structure
    Components.wxs                       # File components
    ServiceInstall.wxs                   # Windows Service configuration
    CustomActions.wxs                    # WFP cleanup custom action
    License.rtf                          # License agreement text
```

## Building the Installer

### Using the Build Script (Recommended)

```powershell
# From repository root (as Administrator)
.\scripts\Build-Installer.ps1 -Version 1.0.0
```

This script:
1. Publishes the Service project (Release, win-x64)
2. Publishes the CLI project (Release)
3. Builds the WiX installer

Output: `installer\WfpTrafficControl.Installer\bin\Release\WfpTrafficControl-1.0.0.msi`

### Manual Build

```powershell
# Step 1: Publish Service
dotnet publish src\service\Service.csproj -c Release -r win-x64 --self-contained false -o src\service\bin\Release\net8.0-windows\win-x64\publish

# Step 2: Publish CLI
dotnet publish src\cli\Cli.csproj -c Release -o src\cli\bin\Release\net8.0\publish

# Step 3: Build Installer
dotnet build installer\WfpTrafficControl.Installer\WfpTrafficControl.Installer.wixproj -c Release -p:Version=1.0.0
```

## Installation

### Install

```powershell
# GUI installation
msiexec /i WfpTrafficControl-1.0.0.msi

# Silent installation
msiexec /i WfpTrafficControl-1.0.0.msi /qn

# Silent with logging
msiexec /i WfpTrafficControl-1.0.0.msi /qn /l*v install.log
```

### Install Location

Default: `C:\Program Files\WfpTrafficControl`

Custom location (GUI):
- The installer provides an installation directory selection dialog

Custom location (silent):
```powershell
msiexec /i WfpTrafficControl-1.0.0.msi /qn INSTALLFOLDER="D:\WfpTrafficControl"
```

## Features

### Core (Required)
Installs the Windows Service and CLI tool:
- `WfpTrafficControl.Service.exe` - Windows Service
- `wfpctl.exe` - Command-line tool
- `appsettings.json` - Service configuration
- `sample-policy.json` - Example policy file
- Supporting DLLs and runtime configuration

### Add to PATH (Optional)
Adds the installation directory to the system PATH, allowing `wfpctl` to be run from any command prompt.

Enabled by default. To disable during silent install:
```powershell
msiexec /i WfpTrafficControl-1.0.0.msi /qn ADDLOCAL=Core
```

## Service Configuration

The installer configures the Windows Service with:

| Setting | Value |
|---------|-------|
| Name | WfpTrafficControl |
| Display Name | WFP Traffic Control Service |
| Start Type | Manual (demand) |
| Account | LocalSystem |
| Failure Recovery | Restart at 5s, 10s, 30s |

## Upgrade Behavior

The installer supports major upgrades:

1. **Automatic upgrade detection**: Installing a newer version automatically removes the previous version
2. **WFP cleanup**: Before removal, runs `wfpctl teardown` to clean up WFP objects
3. **Data preservation**: User data in `%ProgramData%\WfpTrafficControl` is preserved
4. **Downgrade prevention**: Installing an older version over a newer version is blocked

## Uninstall

### Uninstall via GUI
1. Open "Add or Remove Programs" (Settings > Apps)
2. Find "WFP Traffic Control"
3. Click Uninstall

### Uninstall via Command Line
```powershell
# Using product code
msiexec /x {ProductCode} /qn

# Using MSI file
msiexec /x WfpTrafficControl-1.0.0.msi /qn
```

### Uninstall Process
1. **WFP Cleanup**: Runs `wfpctl teardown` to remove WFP provider, sublayer, and filters
2. **Service Removal**: Stops and removes the Windows Service
3. **File Removal**: Deletes installed files from Program Files
4. **PATH Cleanup**: Removes installation directory from system PATH (if added)

**Important**: The `%ProgramData%\WfpTrafficControl` directory (containing LKG policy and audit logs) is NOT removed during uninstall. Delete manually if needed.

## WFP Cleanup on Uninstall

The installer includes a custom action that runs `wfpctl teardown` before the service is stopped during uninstall or upgrade.

**Fail-Open Behavior**: If WFP cleanup fails (e.g., service not responding), the uninstall continues anyway. This prevents orphaned installations but may leave WFP objects behind.

**Manual Cleanup** (if needed):
```powershell
# After manual uninstall, remove WFP objects
wfpctl teardown

# Or use PowerShell if wfpctl is unavailable
# (Requires WFP expertise to enumerate and remove objects by GUID)
```

## Code Signing

The MSI is **not signed by default**. For production deployment:

1. Obtain a code signing certificate
2. Sign the MSI after building:
   ```powershell
   signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com WfpTrafficControl-1.0.0.msi
   ```
3. For enterprise deployment (SCCM/Intune), a signed MSI is strongly recommended

## Known Limitations

1. **No rollback on install failure**: If the service fails to start after installation, manual cleanup may be required
2. **No self-update**: The service cannot update itself; requires external orchestration
3. **x64 only**: No x86 or ARM64 builds (could be added if needed)

## Troubleshooting

### Installation fails with "Administrator privileges required"
Run the installer as Administrator or use `msiexec` from an elevated command prompt.

### Service fails to start after installation
1. Check Windows Event Viewer for service errors
2. Ensure .NET 8.0 Runtime is installed
3. Verify the service executable exists in the installation directory

### WFP cleanup warning during uninstall
If you see a warning about WFP cleanup failing, WFP objects may remain. Use `wfpctl teardown` manually or check if the service was running.

### PATH not updated after installation
1. Restart any open command prompts
2. Check if the "Add to PATH" feature was selected
3. Verify the PATH entry: `echo %PATH%`

## Testing

### Automated Uninstall Verification

Use the `Test-MsiUninstall.ps1` script to verify complete removal of all components:

```powershell
# After uninstalling, verify complete removal
.\scripts\Test-MsiUninstall.ps1

# Verify clean state before installation
.\scripts\Test-MsiUninstall.ps1 -Mode PreInstall

# Verify installation completed correctly
.\scripts\Test-MsiUninstall.ps1 -Mode PostInstall -Detailed
```

The test script verifies:
- **Service**: Windows service is removed and not running
- **Registry**: Service registry entries are cleaned up
- **Files**: Installation folder and all files are deleted
- **PATH**: System PATH no longer contains installation directory
- **WFP Artifacts**: Provider, sublayer, and filters are removed
- **Named Pipe**: IPC pipe is no longer active
- **MSI Registration**: Product is removed from Windows Installer database

### Manual Test Checklist (in a VM with snapshot)

#### Installation Tests
- [ ] Fresh install succeeds
- [ ] Service appears in Services (services.msc)
- [ ] Service starts without errors
- [ ] `wfpctl status` works from any directory (PATH feature)
- [ ] Sample policy file is installed
- [ ] Named pipe is accessible

#### Upgrade Tests
- [ ] Upgrade from previous version succeeds
- [ ] User data in ProgramData is preserved
- [ ] Service restarts after upgrade
- [ ] Old version files are removed

#### Uninstall Tests
- [ ] Uninstall via GUI succeeds
- [ ] Uninstall via command line succeeds
- [ ] Service is stopped and removed
- [ ] All files are deleted from Program Files
- [ ] PATH entry is removed
- [ ] WFP artifacts are cleaned up
- [ ] Re-install after uninstall works

#### Edge Cases
- [ ] Uninstall while service is running
- [ ] Uninstall with active WFP filters
- [ ] Silent install with custom path
- [ ] Cancel during installation (rollback)
