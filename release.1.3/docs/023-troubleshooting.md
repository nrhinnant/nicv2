# 023 — Troubleshooting Guide

## Overview

This guide covers common failure scenarios, diagnostic procedures, and recovery steps for the WFP Traffic Control system.

**Target audience:** Operators diagnosing issues in development or production environments.

---

## Quick Diagnostic Checklist

When something isn't working, run through this checklist:

```powershell
# 1. Is the service running?
sc query WfpTrafficControl

# 2. Is BFE (Base Filtering Engine) running?
sc query BFE

# 3. Can CLI connect to service?
wfpctl ping

# 4. Are WFP objects present?
wfpctl status

# 5. Review recent audit logs
wfpctl logs --tail 20

# 6. Check Windows Event Log
eventvwr
# Navigate to: Windows Logs → Application
# Filter by source: WfpTrafficControl
```

If any of these fail, proceed to the detailed sections below.

---

## Common Failure Scenarios

### 1. Service Failures

#### Service won't start

**Symptoms:**
```powershell
PS> sc start WfpTrafficControl
[SC] StartService FAILED 1053:
The service did not respond to the start or control request in a timely manner.
```

**Possible causes:**

| Cause | Check | Fix |
|-------|-------|-----|
| BFE not running | `sc query BFE` | `sc start BFE` |
| Service binary missing | Check `%ProgramFiles%\WfpTrafficControl\Service.exe` | Reinstall service |
| Insufficient privileges | Event Viewer: Access Denied errors | Service must run as LocalSystem |
| Corrupted LKG on auto-apply | Audit log shows LKG errors | Delete `%ProgramData%\WfpTrafficControl\lkg-policy.json` and restart |
| Port/resource conflict | Event Viewer: Pipe creation errors | Check if another process is using the named pipe |

**Recovery steps:**
```powershell
# 1. Ensure BFE is running
sc start BFE

# 2. Delete corrupted LKG if present
del C:\ProgramData\WfpTrafficControl\lkg-policy.json

# 3. Attempt to start service
sc start WfpTrafficControl

# 4. If still failing, check Event Viewer for specific error
eventvwr
```

#### Service starts but CLI can't connect

**Symptoms:**
```powershell
PS> wfpctl ping
Error: Could not connect to service: All pipe instances are busy
```

**Possible causes:**

| Cause | Check | Fix |
|-------|-------|-----|
| Service still starting | Wait 5-10 seconds | Service may be initializing |
| Pipe not created | Event Viewer: Pipe creation errors | Restart service |
| Permission denied | CLI not running as admin | Run PowerShell/CMD as Administrator |
| IPC timeout | Service is hung | Restart service: `sc stop WfpTrafficControl && sc start WfpTrafficControl` |

**Recovery steps:**
```powershell
# 1. Verify service is running (not starting/stopping)
sc query WfpTrafficControl
# Look for: STATE: 4 RUNNING

# 2. Verify you're running as admin
whoami /groups | findstr "S-1-16-12288"
# Should show "Mandatory Label\High Mandatory Level"

# 3. If not admin, restart PowerShell as admin
# Right-click PowerShell → Run as Administrator

# 4. Try ping again
wfpctl ping
```

---

### 2. BFE (Base Filtering Engine) Issues

#### BFE not running

**Symptoms:**
- Service fails to start with WFP-related errors in Event Viewer
- WFP API calls fail with `ERROR_SERVICE_NOT_ACTIVE` (1062)

**Diagnosis:**
```powershell
sc query BFE
# Look for: STATE: 1 STOPPED
```

**Recovery:**
```powershell
# Start BFE
sc start BFE

# Verify it's running
sc query BFE

# BFE should start automatically (Automatic startup)
# If not, configure it:
sc config BFE start= auto

# Restart WfpTrafficControl service
sc start WfpTrafficControl
```

**Root cause:** BFE is the kernel-mode Windows Firewall service that manages WFP. Our service requires BFE to be running to make any WFP API calls.

#### BFE access denied

**Symptoms:**
- WFP operations fail with `ERROR_ACCESS_DENIED` (5)
- Event Viewer shows access denied errors

**Diagnosis:**
```powershell
# Check service account
sc qc WfpTrafficControl
# Look for: SERVICE_START_NAME: LocalSystem
```

**Recovery:**
```powershell
# If service is not running as LocalSystem, reconfigure:
sc config WfpTrafficControl obj= LocalSystem

# Restart service
sc stop WfpTrafficControl
sc start WfpTrafficControl
```

**Root cause:** WFP APIs require administrator or LocalSystem privileges. The service must run as LocalSystem to manage WFP objects.

---

### 3. Policy Errors

#### Policy validation fails

**Symptoms:**
```powershell
PS> wfpctl apply policy.json
Error: Policy validation failed with 3 error(s):
  - rules[0].id: Rule ID is required
  - rules[1] (id='my-rule').protocol: Invalid protocol 'icmp'. Must be one of: tcp, udp, any
```

**Common validation errors:**

| Error | Cause | Fix |
|-------|-------|-----|
| `Rule ID is required` | Missing `id` field | Add unique `id` to each rule |
| `Duplicate rule ID` | Multiple rules with same `id` | Make rule IDs unique |
| `Invalid protocol` | Protocol not `tcp`, `udp`, or `any` | Use supported protocol |
| `Invalid direction` | Direction not `inbound`, `outbound`, or `both` | Use supported direction |
| `Invalid port specification` | Malformed port string | Use format: `80`, `80-443`, or `80,443,8080` |
| `Invalid IP address or CIDR` | Malformed IP or CIDR | Use format: `192.168.1.1` or `10.0.0.0/8` |
| `Process path too long` | Path > 260 characters | Shorten path |
| `Invalid characters in rule ID` | Non-alphanumeric/dash/underscore | Use only `a-z`, `A-Z`, `0-9`, `-`, `_` |

**Recovery:**
```powershell
# 1. Validate policy before applying
wfpctl validate policy.json

# 2. Fix errors in policy file

# 3. Validate again until clean
wfpctl validate policy.json

# 4. Apply
wfpctl apply policy.json
```

**Tip:** Use `wfpctl validate` to check policy syntax without requiring the service to be running or making any WFP changes.

#### Policy compilation fails

**Symptoms:**
```powershell
PS> wfpctl apply policy.json
Error: Policy compilation failed with 2 error(s):
  - Rule 'inbound-rule': Unsupported direction: 'inbound' with protocol 'udp'
  - Rule 'ipv6-rule': IPv6 addresses not supported
```

**Common compilation errors:**

| Error | Cause | Fix / Workaround |
|-------|-------|------------------|
| `Unsupported direction: 'inbound'` (legacy) | Inbound not yet implemented in early phases | Upgrade to phase 15+ or use outbound rules only |
| `IPv6 addresses not supported` | IPv6 not yet implemented | Use IPv4 addresses only |
| `Local endpoint filters not supported` | Local IP/port filtering not yet implemented | Remove `local` field or use workaround filters |
| `Unsupported protocol: 'icmp'` | ICMP not supported | Use `tcp`, `udp`, or `any` |

**Recovery:**
```powershell
# 1. Review compilation error message
wfpctl apply policy.json

# 2. Check feature support in current version
# See docs/features/ for implemented features

# 3. Adjust policy to use supported features

# 4. Re-apply
wfpctl apply policy.json
```

**Note:** Compilation errors indicate the policy is valid JSON/schema but contains features not yet implemented. Check the error message and feature docs to understand limitations.

#### Apply fails mid-transaction

**Symptoms:**
- Apply command returns error
- Audit log shows `apply-finished` with status `failure`
- Error message indicates WFP API failure

**Example errors:**

| Error | Cause | Fix |
|-------|-------|-----|
| `FWP_E_DUPLICATE_KEYMOD` | Filter GUID conflict | Likely a bug; report issue. Workaround: `wfpctl rollback` then re-apply |
| `FWP_E_PROVIDER_CONTEXT_MISMATCH` | Sublayer missing or corrupted | `wfpctl teardown`, `wfpctl bootstrap`, re-apply |
| `ERROR_NOT_ENOUGH_MEMORY` | Too many filters | Reduce policy size or increase system resources |
| `FWP_E_NET_EVENTS_DISABLED` | BFE issue | Restart BFE: `sc stop BFE && sc start BFE` |

**Recovery (generic):**
```powershell
# 1. Transaction abort ensures no partial state
# Current filters are unchanged

# 2. Check audit log for details
wfpctl logs --tail 5

# 3. If persistent, try rollback and re-bootstrap
wfpctl rollback
wfpctl teardown
wfpctl bootstrap

# 4. Re-apply policy
wfpctl apply policy.json
```

**Guarantee:** If apply fails, the transaction is aborted. Previous filters remain unchanged (atomicity).

---

### 4. WFP State Issues

#### Filters not blocking/allowing traffic

**Symptoms:**
- Policy applied successfully (no errors)
- But traffic is not being blocked/allowed as expected

**Diagnosis steps:**

1. **Verify filters are active:**
   ```powershell
   wfpctl status
   # Look for filter count > 0
   ```

2. **Check filter details via netsh:**
   ```powershell
   netsh wfp show filters
   # Look for filters with displayName containing "WfpTrafficControl"
   ```

3. **Verify filter conditions match intent:**
   - Check remote IP/port
   - Check process path (if specified)
   - Check direction (inbound vs outbound)
   - Check action (block vs allow)

4. **Check filter weight:**
   - Higher weight = higher priority
   - Default weight: 1000 + rule priority
   - Windows Firewall filters may have higher weight and take precedence

5. **Check for conflicting filters:**
   ```powershell
   netsh wfp show filters
   # Look for other filters in same layer with higher weight
   ```

**Common causes:**

| Cause | Fix |
|-------|-----|
| **Another filter has higher priority** | Increase rule `priority` field in policy, or disable conflicting provider |
| **Process path doesn't match** | Use full path (`C:\Program Files\...`), check case sensitivity |
| **IP/port mismatch** | Verify remote IP/port in policy matches actual connection |
| **Wrong direction** | Check if traffic is inbound or outbound; update policy |
| **Rule disabled** | Verify `enabled: true` in policy JSON |
| **Filter not compiled** | Check audit log for skip warnings |

**Advanced debugging:**
```powershell
# Enable WFP tracing (use in test VM only, large logs)
netsh wfp set options netevents=on

# Reproduce the connection

# View trace
netsh wfp show netevents

# Disable tracing
netsh wfp set options netevents=off
```

#### Filters stuck after service crash

**Symptoms:**
- Service crashed or was forcibly terminated
- Filters remain active in WFP
- Cannot modify or remove filters via WfpTrafficControl

**Diagnosis:**
```powershell
# Check if filters exist
netsh wfp show filters | findstr "WfpTrafficControl"
```

**Recovery:**
```powershell
# 1. Restart service (if not running)
sc start WfpTrafficControl

# 2. Remove all filters via service
wfpctl rollback

# 3. If service is unrecoverable, use PowerShell to remove manually:
# (Advanced: requires WFP PowerShell module or custom script)
# See "Manual WFP Cleanup" section below
```

**Why this happens:** WFP filters are managed by BFE (kernel), not by our service process. If our service crashes, filters remain active until explicitly removed.

**Prevention:** Always use `wfpctl rollback` before stopping the service if you want to remove filters.

---

### 5. Permission Issues

#### CLI reports "Access Denied"

**Symptoms:**
```powershell
PS> wfpctl apply policy.json
Error: Access denied. Administrator privileges required.
```

**Cause:** CLI is not running as Administrator.

**Fix:**
```powershell
# Close current PowerShell/CMD window

# Open new PowerShell/CMD as Administrator:
# Right-click PowerShell icon → "Run as Administrator"

# Verify admin privileges:
whoami /groups | findstr "S-1-16-12288"
# Should show "High Mandatory Level"

# Retry command
wfpctl apply policy.json
```

**Note:** All `wfpctl` commands except `validate` require administrator privileges (enforced by named pipe ACL and service authorization check).

#### Service reports "Access Denied" for WFP APIs

**Symptoms:**
- Service starts but WFP operations fail
- Event Viewer shows `ERROR_ACCESS_DENIED` (5) for WFP API calls

**Diagnosis:**
```powershell
sc qc WfpTrafficControl
# Look for: SERVICE_START_NAME: LocalSystem
```

**Fix:**
```powershell
# Reconfigure service to run as LocalSystem
sc config WfpTrafficControl obj= LocalSystem

# Restart service
sc stop WfpTrafficControl
sc start WfpTrafficControl
```

---

## Recovery Procedures

### Complete System Reset

If the system is in an unknown or corrupted state:

```powershell
# 1. Stop the service
sc stop WfpTrafficControl

# 2. Remove all filters (if service is responsive)
wfpctl rollback

# 3. Remove provider and sublayer
wfpctl teardown

# 4. Delete LKG and audit log (optional, for clean slate)
del C:\ProgramData\WfpTrafficControl\lkg-policy.json
del C:\ProgramData\WfpTrafficControl\audit.log

# 5. Start service
sc start WfpTrafficControl

# 6. Re-bootstrap WFP objects
wfpctl bootstrap

# 7. Apply policy
wfpctl apply C:\path\to\policy.json
```

### Emergency Connectivity Restore

If policy has blocked critical connectivity:

```powershell
# Immediate: Remove all filters
wfpctl rollback

# Verify connectivity restored
ping google.com

# If rollback fails (service unresponsive), restart service:
sc stop WfpTrafficControl
sc start WfpTrafficControl
wfpctl rollback

# If still failing, reboot machine (WFP filters are not persistent across reboots)
shutdown /r /t 0
```

**Important:** WFP filters created by our service are **non-persistent** (not set with `FWPM_FILTER_FLAG_PERSISTENT`). A reboot will clear all filters.

### LKG Recovery

Restore the last known good policy:

```powershell
# 1. Check LKG status
wfpctl lkg show

# 2. If LKG exists and is valid, revert
wfpctl lkg revert

# 3. If LKG is corrupt, delete it
wfpctl rollback
del C:\ProgramData\WfpTrafficControl\lkg-policy.json

# 4. Apply a new known-good policy
wfpctl apply C:\path\to\policy.json
```

### Manual WFP Cleanup (Advanced)

If the service is completely unresponsive or uninstalled but filters remain:

**Using PowerShell with WFP APIs:**

```powershell
# This requires WFP interop in PowerShell
# Option 1: Use netsh (no per-filter control)
netsh wfp show filters > C:\wfp-dump.txt
# Manually review for "WfpTrafficControl" filters
# Note their Filter ID

# Option 2: Remove our provider (cascades to sublayer and filters)
# WARNING: This removes ALL our WFP objects
# Use C++ or C# tool to call:
# FwpmEngineOpen0, FwpmProviderDeleteByKey0(our GUID), FwpmEngineClose0

# Option 3: Reboot (filters are non-persistent)
shutdown /r /t 0
```

**Using a custom cleanup script:**

We recommend creating a PowerShell script that uses P/Invoke to call WFP APIs:
- `FwpmEngineOpen0`
- `FwpmProviderDeleteByKey0` (our provider GUID: see [WfpConstants.cs](../../src/shared/WfpConstants.cs))
- `FwpmEngineClose0`

See `scripts/` directory for examples (if available).

---

## Diagnostic Tools

### wfpctl Commands

| Command | Purpose | Requires Service |
|---------|---------|------------------|
| `wfpctl ping` | Test service connectivity | Yes |
| `wfpctl status` | Show WFP object status | Yes |
| `wfpctl validate <file>` | Validate policy syntax | No (offline) |
| `wfpctl logs --tail N` | Show recent audit log entries | Yes |
| `wfpctl logs --since M` | Show audit log from last M minutes | Yes |
| `wfpctl lkg show` | Show LKG policy info | Yes |
| `wfpctl lkg revert` | Apply LKG policy | Yes |
| `wfpctl watch status` | Show file watch status | Yes |

### Windows Built-in Tools

#### Service Control Manager (sc.exe)

```powershell
# Query service status
sc query WfpTrafficControl

# Query BFE status
sc query BFE

# Start/stop service
sc start WfpTrafficControl
sc stop WfpTrafficControl

# Check service configuration
sc qc WfpTrafficControl
```

#### Event Viewer (eventvwr)

**Location:** Windows Logs → Application

**Filter by Source:** WfpTrafficControl

**What to look for:**
- Errors during service startup
- WFP API failures with error codes
- LKG auto-apply status
- Pipe server errors

#### netsh wfp

```powershell
# Show all WFP filters (large output)
netsh wfp show filters

# Show our filters only (via display name)
netsh wfp show filters | findstr "WfpTrafficControl"

# Show all providers
netsh wfp show providers

# Show all sublayers
netsh wfp show sublayers

# Enable WFP event tracing (test VM only)
netsh wfp set options netevents=on
netsh wfp show netevents
netsh wfp set options netevents=off
```

**Warning:** `netsh wfp show netevents` generates large logs. Use in test VMs only.

### Log File Locations

| Log | Location | Purpose |
|-----|----------|---------|
| Audit log | `%ProgramData%\WfpTrafficControl\audit.log` | Control-plane events (apply, rollback, teardown) |
| LKG policy | `%ProgramData%\WfpTrafficControl\lkg-policy.json` | Last known good policy |
| Service logs | Event Viewer → Application → WfpTrafficControl | Service startup, errors, WFP API failures |
| Windows Firewall logs (optional) | `%SystemRoot%\System32\LogFiles\Firewall\pfirewall.log` | Per-connection logs (if enabled) |

### Audit Log Analysis

The audit log is JSON Lines format. Use PowerShell to parse:

```powershell
# Read all entries
Get-Content C:\ProgramData\WfpTrafficControl\audit.log | ConvertFrom-Json

# Filter failures only
Get-Content C:\ProgramData\WfpTrafficControl\audit.log | ConvertFrom-Json | Where-Object { $_.status -eq "failure" }

# Count events by type
Get-Content C:\ProgramData\WfpTrafficControl\audit.log | ConvertFrom-Json | Group-Object event | Sort-Object Count -Descending
```

**Common patterns to look for:**
- Repeated `apply-started` without `apply-finished`: Service may be crashing during apply
- `apply-finished` with `status=failure` and error: Shows exact failure reason
- `rollback-finished` with `filtersRemoved=0`: No filters were active (expected after rollback)

---

## Known Limitations

These are **not bugs**, but design constraints to be aware of:

1. **IPv6 not supported:** Only IPv4 addresses and CIDR ranges supported
2. **ICMP not supported:** Only TCP, UDP, and "any" protocols supported
3. **One connection at a time:** Named pipe handles one CLI connection at a time (sequential)
4. **No log rotation:** Audit log grows unbounded (manual cleanup required)
5. **No per-connection logs:** Only control-plane events logged (apply, rollback, etc.), not per-connection traffic
6. **Process path must exist at apply time:** If executable doesn't exist, process condition is skipped (warning logged)
7. **No persistent filters:** Filters are cleared on reboot (by design for safety)
8. **No automatic LKG apply by default:** Must enable `AutoApplyLkgOnStartup` in config

See individual feature docs for details on current implementation scope.

---

## Getting More Help

### Check Feature Documentation

See [docs/features/](../../docs/features/) for detailed feature documentation:
- [000-project-overview.md](000-project-overview.md) - Project goals and architecture
- [011-policy-schema-v1.md](011-policy-schema-v1.md) - Policy JSON schema
- [022-how-it-works.md](022-how-it-works.md) - Architecture and flow diagrams

### Check Audit Logs

The audit log is your best source for understanding what the service is doing:

```powershell
wfpctl logs --tail 50
```

### Enable Verbose Logging (Development)

If debugging in a development environment, increase service log verbosity:

Edit `appsettings.json` in service directory:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "WfpTrafficControl": "Trace"
    }
  }
}
```

Restart service to apply.

**Warning:** Verbose logging generates large Event Viewer entries. Use in test environments only.

### Report Issues

If you encounter a bug or unexpected behavior:
1. Collect audit log: `wfpctl logs --tail 100 > audit.txt`
2. Collect service logs from Event Viewer
3. Note exact error message and steps to reproduce
4. Report via project issue tracker (if available)

---

## Quick Reference: Error Codes

Common WFP error codes returned by the service:

| Error Code | Win32 | Meaning | Fix |
|------------|-------|---------|-----|
| `FWP_E_ALREADY_EXISTS` | 0x80320009 | Object already exists | Usually harmless; ignore or use idempotent operations |
| `FWP_E_IN_USE` | 0x80320018 | Object is in use (e.g., sublayer has filters) | Remove filters first: `wfpctl rollback` |
| `FWP_E_FILTER_NOT_FOUND` | 0x80320003 | Filter not found | Expected during enumeration race conditions; safe to ignore |
| `FWP_E_PROVIDER_NOT_FOUND` | 0x80320004 | Provider not found | Run `wfpctl bootstrap` |
| `ERROR_ACCESS_DENIED` | 5 | Access denied | Ensure admin privileges and service runs as LocalSystem |
| `ERROR_SERVICE_NOT_ACTIVE` | 1062 | BFE not running | Start BFE: `sc start BFE` |

For complete error code list, see [WfpErrorTranslator.cs](../../src/shared/Native/WfpErrorTranslator.cs).

---

## Summary

**Most common issues and fixes:**

| Symptom | Most Likely Cause | Quick Fix |
|---------|------------------|-----------|
| CLI can't connect | Not running as admin | Re-run PowerShell as Administrator |
| Service won't start | BFE not running | `sc start BFE` |
| Policy validation fails | Invalid JSON schema | `wfpctl validate policy.json` and fix errors |
| Filters not blocking traffic | Higher priority filter conflicts | Increase rule `priority` or check with `netsh wfp show filters` |
| Connectivity broken | Bad policy applied | `wfpctl rollback` |

**Emergency recovery:**
```powershell
wfpctl rollback  # Immediate: remove all filters
```

**Complete reset:**
```powershell
sc stop WfpTrafficControl
wfpctl teardown
del C:\ProgramData\WfpTrafficControl\*
sc start WfpTrafficControl
wfpctl bootstrap
```

**When in doubt, reboot:** Filters are non-persistent and will be cleared on reboot.
