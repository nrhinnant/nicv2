# 009 - Demo Block Rule

## Problem Statement

We need a simple, safe way to verify that the WFP filtering infrastructure works end-to-end before implementing the full policy system. This demo rule blocks a specific external endpoint to demonstrate that:

1. Filters can be added to our sublayer
2. Outbound connection blocking works
3. Filters can be safely removed
4. Rollback mechanisms function correctly

## Solution Overview

Implement a hardcoded block rule that targets outbound TCP connections to **1.1.1.1:443** (Cloudflare's DNS-over-HTTPS endpoint). This target was chosen because:

- It's external (won't affect local system operation)
- It's reliably reachable for testing
- Blocking port 443 to a single IP won't break general connectivity
- It's easy to verify with common tools (curl, PowerShell)

## Blocked Traffic Tuple

| Field | Value |
|-------|-------|
| Direction | Outbound |
| Protocol | TCP (6) |
| Remote IP | 1.1.1.1 |
| Remote Port | 443 |
| WFP Layer | FWPM_LAYER_ALE_AUTH_CONNECT_V4 |

## CLI Commands

### Enable Demo Block Filter

```powershell
wfpctl demo-block enable
```

**What it does:**
1. Ensures provider and sublayer exist (calls bootstrap automatically)
2. Adds a WFP filter blocking TCP to 1.1.1.1:443
3. Reports success/failure

**Expected output:**
```
Demo block filter enabled successfully
  Filter active: True
  Blocking: TCP to 1.1.1.1:443 (Cloudflare)

To test: curl -v --connect-timeout 5 https://1.1.1.1 (should fail)
```

### Disable Demo Block Filter

```powershell
wfpctl demo-block disable
```

**What it does:**
1. Removes the demo block filter by its GUID
2. Idempotent (succeeds even if filter doesn't exist)

**Expected output:**
```
Demo block filter disabled successfully
  Filter removed: True

To test: curl -v https://1.1.1.1 (should succeed)
```

### Check Demo Block Status

```powershell
wfpctl demo-block status
```

**Expected output (when enabled):**
```
Demo Block Filter Status
  Active: True
  Blocking: TCP 1.1.1.1:443 (Cloudflare)
```

**Expected output (when disabled):**
```
Demo Block Filter Status
  Active: False
```

### Rollback All Filters

```powershell
wfpctl rollback
```

**What it does:**
1. Removes all filters from our sublayer (currently just the demo filter)
2. Keeps provider and sublayer intact
3. Idempotent (succeeds even if no filters exist)

**Expected output:**
```
Rollback completed successfully
  Filters removed: True
  Provider and sublayer kept intact
```

## Verification Steps

### Test 1: Verify Block Works

```powershell
# 1. Enable the block
wfpctl demo-block enable

# 2. Try to connect (should fail/timeout)
curl -v --connect-timeout 5 https://1.1.1.1

# Expected: Connection timeout or refused
# In PowerShell:
Test-NetConnection -ComputerName 1.1.1.1 -Port 443
# Expected: TcpTestSucceeded: False
```

### Test 2: Verify Disable Works

```powershell
# 1. Disable the block
wfpctl demo-block disable

# 2. Try to connect (should succeed)
curl -v https://1.1.1.1

# Expected: HTTP response from Cloudflare
# In PowerShell:
Test-NetConnection -ComputerName 1.1.1.1 -Port 443
# Expected: TcpTestSucceeded: True
```

### Test 3: Verify Rollback

```powershell
# 1. Enable the block
wfpctl demo-block enable

# 2. Rollback all filters
wfpctl rollback

# 3. Verify status shows inactive
wfpctl demo-block status

# 4. Verify connection works
Test-NetConnection -ComputerName 1.1.1.1 -Port 443
# Expected: TcpTestSucceeded: True
```

## Implementation Details

### IPC Messages

| Request Type | Response Type | Purpose |
|--------------|---------------|---------|
| `demo-block-enable` | `DemoBlockEnableResponse` | Add filter |
| `demo-block-disable` | `DemoBlockDisableResponse` | Remove filter |
| `demo-block-status` | `DemoBlockStatusResponse` | Check if active |
| `rollback` | `RollbackResponse` | Remove all filters |

### WFP Objects Created

| Object | GUID | Description |
|--------|------|-------------|
| Filter | `D3E4F5A6-7B8C-4D9E-0F1A-2B3C4D5E6F7A` | Demo block filter |

The filter is added to our sublayer (`B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C`) and tagged with our provider (`7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A`).

### Filter Conditions

Three conditions are ANDed together:

1. **Protocol = TCP (6)**: `FWPM_CONDITION_IP_PROTOCOL` = `FWP_UINT8(6)`
2. **Remote IP = 1.1.1.1**: `FWPM_CONDITION_IP_REMOTE_ADDRESS` = `FWP_V4_ADDR_MASK(1.1.1.1/32)`
3. **Remote Port = 443**: `FWPM_CONDITION_IP_REMOTE_PORT` = `FWP_UINT16(443)`

### Safety Mechanisms

1. **Idempotent operations**: Enable/disable can be called multiple times safely
2. **Transactional**: Filter add/remove operations use WFP transactions with auto-rollback on failure
3. **Targeted blocking**: Only blocks one specific IP:port combination
4. **Easy removal**: Multiple ways to remove the filter:
   - `wfpctl demo-block disable` - removes just this filter
   - `wfpctl rollback` - removes all our filters
   - `wfpctl teardown` - removes everything (provider, sublayer, filters)

## Files Changed

| File | Changes |
|------|---------|
| `src/shared/Native/NativeMethods.cs` | Added FwpmFilterAdd0, FwpmFilterDeleteById0, FwpmFilterGetByKey0, filter structures |
| `src/shared/WfpConstants.cs` | Added DemoBlockFilterGuid, DemoBlockFilterName, demo constants |
| `src/shared/Native/IWfpEngine.cs` | Added AddDemoBlockFilter, RemoveDemoBlockFilter, DemoBlockFilterExists, RemoveAllFilters |
| `src/service/Wfp/WfpEngine.cs` | Implemented demo block filter methods |
| `src/shared/Ipc/IpcMessages.cs` | Added DemoBlockEnable/Disable/Status and Rollback request/response types |
| `src/service/Ipc/PipeServer.cs` | Added handlers for new IPC messages |
| `src/cli/Program.cs` | Added demo-block and rollback commands |
| `tests/WfpBootstrapTests.cs` | Updated MockWfpEngine with new interface methods |

## Known Limitations

1. **IPv4 only**: The demo filter only blocks IPv4 connections. IPv6 connections to the same target would not be blocked.
2. **Single target**: This is a hardcoded demo. A real policy system would support configurable rules.
3. **No persistence**: The filter is not marked as persistent, so it won't survive system reboot. The service would need to re-apply it on startup.
4. **Filter enumeration**: `rollback` currently only removes the demo filter. A production implementation would enumerate all filters in our sublayer.

## Future Enhancements

1. Add IPv6 support with `FWPM_LAYER_ALE_AUTH_CONNECT_V6`
2. Implement proper policy model with configurable rules
3. Add filter enumeration for comprehensive rollback
4. Consider persistent filters for survival across reboots
5. Add audit logging for filter matches (requires callout driver or ETW subscription)
