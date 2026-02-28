# WfpTrafficControl Release Package Runbook

This document describes the components included in the WfpTrafficControl release package and provides step-by-step instructions for installation, configuration, and operation.

---

## Release Package Contents

```
\release\
├── bin\
│   ├── cli\                    # Command-line interface tool
│   ├── service\                # Windows service binaries
│   └── ui\                     # GUI application (WPF)
├── installer\
│   └── WfpTrafficControl-X.Y.Z.msi   # MSI installer
├── scripts\
│   ├── Install-Service.ps1     # Manual service installation
│   ├── Uninstall-Service.ps1   # Manual service removal
│   ├── Start-Service.ps1       # Start the service
│   └── Stop-Service.ps1        # Stop the service
└── docs\
    ├── RUNBOOK.md              # This file
    ├── EXECUTIVE_SUMMARY.md    # Project overview
    ├── 022-how-it-works.md     # Technical deep-dive
    ├── 023-troubleshooting.md  # Troubleshooting guide
    └── License.rtf             # License agreement
```

---

## Component Descriptions

### MSI Installer (`installer\WfpTrafficControl-X.Y.Z.msi`)

The recommended installation method. The MSI installer:
- Installs all binaries to `C:\Program Files\WfpTrafficControl\`
- Registers and configures the Windows service
- Adds the CLI to the system PATH
- Handles upgrades and uninstallation cleanly

**To install:**
```powershell
# Run as Administrator
msiexec /i "installer\WfpTrafficControl-X.Y.Z.msi"
```

**To uninstall:**
```powershell
# Run as Administrator
msiexec /x "installer\WfpTrafficControl-X.Y.Z.msi"
```

**Silent installation:**
```powershell
msiexec /i "installer\WfpTrafficControl-X.Y.Z.msi" /quiet /norestart
```

---

### Windows Service (`bin\service\`)

The WfpTrafficControl service runs as a Windows service under LocalSystem and manages WFP (Windows Filtering Platform) filters based on policy configuration.

**Key files:**
| File | Description |
|------|-------------|
| `WfpTrafficControl.Service.exe` | Main service executable |
| `Shared.dll` | Shared library (policy models, IPC, WFP interop) |
| `appsettings.json` | Service configuration (if present) |

**Service details:**
- **Service Name:** `WfpTrafficControl`
- **Display Name:** WFP Traffic Control Service
- **Startup Type:** Automatic
- **Account:** LocalSystem

**Manual installation (without MSI):**
```powershell
# Run as Administrator from the release folder
.\scripts\Install-Service.ps1
```

**Service management:**
```powershell
# Start the service
.\scripts\Start-Service.ps1
# or
net start WfpTrafficControl

# Stop the service
.\scripts\Stop-Service.ps1
# or
net stop WfpTrafficControl

# Check service status
sc query WfpTrafficControl
```

---

### Command-Line Interface (`bin\cli\`)

The `wfpctl` CLI provides administrative control over the WfpTrafficControl service.

**Key files:**
| File | Description |
|------|-------------|
| `wfpctl.exe` | CLI executable |
| `Shared.dll` | Shared library |

**Available commands:**

```powershell
# Check service status and connection
wfpctl status

# Validate a policy file (syntax check only)
wfpctl validate policy.json

# Apply a policy to the service
wfpctl apply policy.json

# Rollback to previous policy (or clear all filters)
wfpctl rollback

# Enable traffic control (apply current policy)
wfpctl enable

# Disable traffic control (remove all filters, allow all traffic)
wfpctl disable

# View recent audit logs
wfpctl logs --tail
```

**Example workflow:**
```powershell
# 1. Check the service is running
wfpctl status

# 2. Validate your policy file
wfpctl validate my-policy.json

# 3. Apply the policy
wfpctl apply my-policy.json

# 4. Monitor logs
wfpctl logs --tail
```

---

### GUI Application (`bin\ui\`)

The WfpTrafficControl UI provides a graphical interface for managing policies and monitoring the service.

**Key files:**
| File | Description |
|------|-------------|
| `WfpTrafficControl.UI.exe` | WPF application |
| `Shared.dll` | Shared library |

**To launch:**
```powershell
.\bin\ui\WfpTrafficControl.UI.exe
```

**Features:**
- View service status
- Create and edit policies visually
- Apply policies with one click
- View real-time logs

**Note:** The UI requires the service to be running. It communicates with the service via named pipes.

---

### PowerShell Scripts (`scripts\`)

Helper scripts for manual service management (when not using the MSI installer).

| Script | Description |
|--------|-------------|
| `Install-Service.ps1` | Registers the Windows service |
| `Uninstall-Service.ps1` | Removes the Windows service and cleans up WFP artifacts |
| `Start-Service.ps1` | Starts the service |
| `Stop-Service.ps1` | Stops the service |

**All scripts must be run as Administrator.**

---

## Quick Start Guide

### Option A: MSI Installation (Recommended)

1. **Install:**
   ```powershell
   msiexec /i "installer\WfpTrafficControl-1.0.0.msi"
   ```

2. **Verify installation:**
   ```powershell
   wfpctl status
   ```

3. **Create a policy file** (e.g., `my-policy.json`):
   ```json
   {
     "version": "1.0",
     "defaultAction": "allow",
     "rules": [
       {
         "id": "block-telnet",
         "action": "block",
         "direction": "outbound",
         "protocol": "tcp",
         "remote": { "ports": [23] },
         "comment": "Block outbound Telnet"
       }
     ]
   }
   ```

4. **Apply the policy:**
   ```powershell
   wfpctl apply my-policy.json
   ```

### Option B: Manual Installation

1. **Copy binaries** to desired location (e.g., `C:\WfpTrafficControl\`)

2. **Install the service:**
   ```powershell
   cd C:\WfpTrafficControl
   .\scripts\Install-Service.ps1
   ```

3. **Start the service:**
   ```powershell
   .\scripts\Start-Service.ps1
   ```

4. **Use the CLI:**
   ```powershell
   .\bin\cli\wfpctl.exe status
   ```

---

## Policy File Format

Policies are JSON files defining traffic control rules.

```json
{
  "version": "1.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": [
    {
      "id": "unique-rule-id",
      "action": "allow|block",
      "direction": "inbound|outbound|both",
      "protocol": "tcp|udp|any",
      "process": "C:\\Path\\To\\app.exe",
      "local": {
        "ip": "192.168.1.0/24",
        "ports": [80, 443]
      },
      "remote": {
        "ip": "10.0.0.0/8",
        "ports": [1024, 65535]
      },
      "priority": 100,
      "enabled": true,
      "comment": "Description of the rule"
    }
  ]
}
```

**Field reference:**
| Field | Required | Description |
|-------|----------|-------------|
| `version` | Yes | Policy schema version (use "1.0") |
| `defaultAction` | Yes | "allow" or "block" when no rule matches |
| `rules` | Yes | Array of rule objects |
| `rules[].id` | Yes | Unique identifier for the rule |
| `rules[].action` | Yes | "allow" or "block" |
| `rules[].direction` | Yes | "inbound", "outbound", or "both" |
| `rules[].protocol` | No | "tcp", "udp", or "any" (default: "any") |
| `rules[].process` | No | Full path to executable |
| `rules[].local.ip` | No | Local IP or CIDR |
| `rules[].local.ports` | No | Array of local ports |
| `rules[].remote.ip` | No | Remote IP or CIDR |
| `rules[].remote.ports` | No | Array of remote ports |
| `rules[].priority` | No | Higher values = higher priority |
| `rules[].enabled` | No | true/false (default: true) |
| `rules[].comment` | No | Human-readable description |

---

## Safety Features

WfpTrafficControl includes multiple safety mechanisms:

1. **Fail-Open Design:** If the service crashes or stops unexpectedly, all WFP filters are removed, restoring normal network connectivity.

2. **Panic Rollback:** The service maintains emergency rollback capability to quickly remove all filters.

3. **Last-Known-Good Policy:** The service saves the last successfully applied policy and can revert to it.

4. **Transactional Updates:** Policy changes are applied atomically - either all filters update or none do.

5. **Default Allow:** By default, the system uses "allow" as the default action, only blocking explicitly specified traffic.

---

## Troubleshooting

### Service won't start

1. Check Windows Event Log:
   ```powershell
   Get-EventLog -LogName Application -Source "WfpTrafficControl*" -Newest 10
   ```

2. Verify service is installed:
   ```powershell
   Get-Service WfpTrafficControl
   ```

3. Check for port conflicts or permission issues in the event log.

### CLI can't connect to service

1. Verify service is running:
   ```powershell
   Get-Service WfpTrafficControl
   ```

2. Run CLI as Administrator (required for named pipe access).

3. Check the pipe exists:
   ```powershell
   Get-ChildItem \\.\pipe\ | Where-Object Name -like "*WfpTrafficControl*"
   ```

### Policy not being applied

1. Validate the policy file:
   ```powershell
   wfpctl validate policy.json
   ```

2. Check for JSON syntax errors.

3. Review service logs:
   ```powershell
   wfpctl logs --tail
   ```

### Network connectivity lost

1. **Emergency recovery:** Stop the service to remove all filters:
   ```powershell
   net stop WfpTrafficControl
   ```

2. If service won't stop, use rollback:
   ```powershell
   wfpctl rollback
   ```

3. As a last resort, reboot in Safe Mode (WFP filters are not loaded).

For detailed troubleshooting, see `023-troubleshooting.md`.

---

## Uninstallation

### MSI Uninstall
```powershell
msiexec /x "installer\WfpTrafficControl-X.Y.Z.msi"
```

### Manual Uninstall
```powershell
# Stop and remove the service
.\scripts\Uninstall-Service.ps1

# Delete the installation directory
Remove-Item -Recurse -Force "C:\WfpTrafficControl"
```

The uninstall process:
1. Stops the service
2. Removes all WFP filters, provider, and sublayer
3. Unregisters the Windows service
4. Removes installed files (MSI only)

### Verify Complete Uninstallation

After uninstalling, verify all components were removed:

```powershell
# Run from the scripts folder (if available) or download the script
.\Test-MsiUninstall.ps1
```

This verifies:
- Service is removed
- All files are deleted
- PATH entry is cleaned up
- WFP artifacts are removed
- Registry entries are cleaned up

---

## Support

- **Documentation:** See included docs folder
- **Issues:** https://github.com/anthropics/wfp-traffic-control/issues
- **Technical Details:** See `022-how-it-works.md`
