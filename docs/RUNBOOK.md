# WfpTrafficControl Release Package Runbook

This document provides step-by-step instructions for installing, configuring, and operating WfpTrafficControl.

---

## What Is WfpTrafficControl?

WfpTrafficControl is a **Windows network firewall system** that allows administrators to control network traffic at the operating system level using Microsoft's Windows Filtering Platform (WFP). It operates similarly to Windows Firewall but with a policy-as-code approach: you define rules in JSON files, and the system enforces them at the kernel network stack.

### Implemented Capabilities

| Capability | Description |
|------------|-------------|
| **Policy-based filtering** | Define allow/block rules for TCP and UDP traffic using JSON policy files |
| **Process-level control** | Restrict network access per executable (e.g., block `telnet.exe` from any outbound connections) |
| **IP/CIDR filtering** | Allow or block traffic to/from specific IP addresses or ranges |
| **Port filtering** | Control access by port number, ranges, or lists (e.g., `"80,443"`, `"1024-65535"`) |
| **Bidirectional rules** | Apply rules to inbound, outbound, or both traffic directions |
| **Idempotent apply** | Re-applying the same policy makes no changes (infrastructure-as-code style) |
| **Transactional safety** | Policy changes are atomic—all filters update or none do |
| **Panic rollback** | Instantly remove all filters to restore connectivity |
| **Last-Known-Good (LKG)** | Automatically saves successful policies; revert anytime |
| **Fail-open design** | Service failures restore normal connectivity (no lockout risk) |
| **Audit logging** | All control-plane operations are logged for troubleshooting |

### Current Limitations

- **IPv4 only** — IPv6 is not yet supported
- **No ICMP filtering** — Only TCP, UDP, and "any" protocols
- **No deep packet inspection** — Layer 4 filtering only (no L7/application-layer rules)
- **Local administration only** — No remote management; CLI/UI must run on the same machine

---

## Prerequisites

| Requirement | Details |
|-------------|---------|
| **Operating System** | Windows 10, Windows 11, or Windows Server 2016+ |
| **Architecture** | x64 only |
| **Runtime** | .NET 8.0 Runtime (included in MSI installer; required for manual install) |
| **Privileges** | Administrator rights required for installation and operation |
| **Network** | WFP access requires LocalSystem service account (configured automatically) |

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

| Command | Description |
|---------|-------------|
| `wfpctl status` | Check service connection and WFP object status |
| `wfpctl validate <file>` | Validate policy JSON syntax (offline, no service needed) |
| `wfpctl apply <file>` | Apply a policy file to the service |
| `wfpctl rollback` | Remove all filters and revert to previous state |
| `wfpctl lkg show` | Show last-known-good policy info |
| `wfpctl lkg revert` | Revert to the last-known-good policy |
| `wfpctl watch set <file>` | Enable hot-reload: auto-apply when file changes |
| `wfpctl watch set` | Disable hot-reload (no file argument) |
| `wfpctl watch status` | Show current file watch status |
| `wfpctl logs --tail <N>` | Show last N audit log entries |
| `wfpctl logs --since <min>` | Show audit log entries from last N minutes |
| `wfpctl bootstrap` | Initialize WFP provider and sublayer (usually automatic) |
| `wfpctl teardown` | Remove all WFP objects (provider, sublayer, filters) |

**Note:** The `enable` and `disable` commands are reserved but not yet implemented. Use `apply` and `rollback` instead.

**Example workflow:**
```powershell
# 1. Check the service is running
wfpctl status

# 2. Validate your policy file
wfpctl validate my-policy.json

# 3. Apply the policy
wfpctl apply my-policy.json

# 4. Monitor logs
wfpctl logs --tail 20
```

---

### GUI Application (`bin\ui\`)

The WfpTrafficControl UI is a WPF desktop application providing a graphical interface for policy management. It requires administrator privileges and communicates with the service via named pipes.

**Key files:**
| File | Description |
|------|-------------|
| `WfpTrafficControl.UI.exe` | WPF application |
| `Shared.dll` | Shared library |

**To launch:**
```powershell
.\bin\ui\WfpTrafficControl.UI.exe
```

**The UI has three main tabs:**

#### Dashboard Tab
- **System Status Cards** — View service connection state, active filter count, current policy version, and LKG status at a glance
- **Quick Actions** — One-click buttons to apply a policy file, rollback all filters, or revert to the last known good policy
- **Hot Reload Section** — Enable/disable file watching for automatic policy reload when the policy file changes (useful for development)
- **Recent Activity** — Table showing recent audit log entries (apply, rollback, revert operations with timestamps and filter counts)

#### Policy Editor Tab
- **File Operations** — Create new policies, open existing JSON files, save, or save-as
- **Template Library** — Quick-start templates for common scenarios (e.g., "Block Telemetry", "Development Lockdown")
- **Rules List** — Visual list of all rules with enable/disable checkboxes, action badges (allow/block), and summary info
- **Rule Detail Editor** — Form-based editing of all rule fields (ID, action, direction, protocol, process path, IP/CIDR, ports, priority, comments)
- **Validation Bar** — Real-time validation feedback as you edit
- **Keyboard Shortcuts** — Ctrl+N (new), Ctrl+O (open), Ctrl+S (save), Ctrl+D (duplicate rule), Delete (remove rule), Alt+Up/Down (reorder)

#### Logs Tab
- **Filter Options** — View last N entries or entries from the last N minutes
- **Full Audit Log Table** — All log fields including timestamp, event type, source, status, policy version, filters created/removed, and error messages
- **Export to CSV** — Export displayed entries for troubleshooting or archival
- **Status Bar** — Shows entry count and log file path

**Note:** The UI requires the service to be running. If the service is offline, the Dashboard will show "Offline" status and most operations will be disabled.

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

1. **Install** (run as Administrator):
   ```powershell
   msiexec /i "installer\WfpTrafficControl-1.0.0.msi"
   ```

2. **Verify installation:**
   ```powershell
   wfpctl status
   ```
   You should see "Service connected" and filter count information.

3. **Create a policy file** (e.g., `my-policy.json`):
   ```json
   {
     "version": "1.0",
     "defaultAction": "allow",
     "updatedAt": "2024-01-15T10:30:00Z",
     "rules": [
       {
         "id": "block-telnet",
         "action": "block",
         "direction": "outbound",
         "protocol": "tcp",
         "remote": { "ports": "23" },
         "comment": "Block outbound Telnet"
       }
     ]
   }
   ```

4. **Apply the policy:**
   ```powershell
   wfpctl apply my-policy.json
   ```

5. **Verify filters are active:**
   ```powershell
   wfpctl status
   ```

### Option B: Manual Installation

1. **Copy binaries** to desired location (e.g., `C:\WfpTrafficControl\`)

2. **Install the service** (run as Administrator):
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

### Option C: Using the GUI

1. **Install** using Option A or B above

2. **Launch the UI** (run as Administrator):
   ```powershell
   .\bin\ui\WfpTrafficControl.UI.exe
   ```

3. **Check connection** — The status bar should show "Connected" with a green indicator

4. **Create a policy:**
   - Go to the **Policy Editor** tab
   - Click **New Policy** or select a template from the Quick Start Templates section
   - Add rules using the **+** button in the rules list
   - Fill in rule details in the right panel

5. **Apply the policy:**
   - Click **Validate** to check for errors
   - Click **Apply to Service** to enforce the policy

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
        "ports": "80,443"
      },
      "remote": {
        "ip": "10.0.0.0/8",
        "ports": "1024-65535"
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
| `rules[].local.ports` | No | Port specification: "80", "80-443", or "80,443,8080" |
| `rules[].remote.ip` | No | Remote IP or CIDR |
| `rules[].remote.ports` | No | Port specification: "80", "80-443", or "80,443,8080" |
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
   wfpctl logs --tail 20
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
- **Technical Details:** See `022-how-it-works.md`
- **Troubleshooting:** See `023-troubleshooting.md`
