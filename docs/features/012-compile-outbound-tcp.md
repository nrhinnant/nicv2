# 012 - Compile Outbound TCP Rules

## Overview

This phase implements rule compilation for **outbound TCP rules only**, converting policy rules into WFP filter definitions that can be applied to the Windows Filtering Platform.

## Scope Limitations (Phase 12)

This implementation supports a **limited subset** of the policy model:

### Supported

| Field | Values | Notes |
|-------|--------|-------|
| `direction` | `"outbound"` | Only outbound rules supported |
| `protocol` | `"tcp"` | Only TCP protocol supported |
| `action` | `"allow"`, `"block"` | Both actions supported |
| `remote.ip` | IPv4 address or CIDR | e.g., `"192.168.1.1"`, `"10.0.0.0/8"` |
| `remote.ports` | Single, range, or comma-separated | e.g., `"443"`, `"80-443"`, `"80,443,8080"` |
| `process` | Full Windows path | e.g., `"C:\Windows\System32\notepad.exe"` |
| `enabled` | `true`, `false` | Disabled rules are skipped |
| `priority` | Integer | Higher priority = higher weight in WFP |

### Not Supported (Errors)

| Field | Values | Error |
|-------|--------|-------|
| `direction` | `"inbound"`, `"both"` | "Unsupported direction" |
| `protocol` | `"udp"`, `"any"` | "Unsupported protocol" |
| `local.ip` | Any | "Local endpoint filters not supported" |
| `local.ports` | Any | "Local endpoint filters not supported" |
| `remote.ip` | IPv6 addresses | "IPv6 addresses not supported" |

## Behavior

### Policy Application Flow

1. **Validate**: Policy is validated against schema (Phase 11 validation)
2. **Compile**: Each enabled rule is compiled to one or more WFP filters
3. **Replace**: All existing filters in our sublayer are removed
4. **Apply**: New filters are added in a transaction
5. **Report**: Results returned with counts and warnings

### Rule to Filter Mapping

- **Single port**: One rule = One filter
- **Port range**: One rule = One filter (uses WFP range matching)
- **Comma-separated ports**: One rule = Multiple filters (one per port/range)

Example:
```json
{
  "id": "block-web",
  "action": "block",
  "direction": "outbound",
  "protocol": "tcp",
  "remote": { "ports": "80,443,8080-8090" }
}
```
Compiles to **3 filters**: port 80, port 443, ports 8080-8090 (range).

### Process Matching

When the `process` field is specified:
- Must be a full Windows path (e.g., `C:\Program Files\App\app.exe`)
- Uses `FwpmGetAppIdFromFileName0` to convert to WFP-compatible device path
- If path resolution fails, the process condition is skipped (warning logged)

### Filter Properties

Generated filters have:
- **Display Name**: `WfpTrafficControl: {ruleId}` (with port index suffix if multiple)
- **Description**: Human-readable summary of the rule
- **GUID**: Deterministic, derived from rule ID + port index (idempotent applies)
- **Weight**: Base weight (1000) + rule priority
- **Layer**: ALE_AUTH_CONNECT_V4 (outbound connection authorization)

## CLI Usage

```bash
# Apply a policy file
wfpctl apply policy.json

# Validate first (no service needed)
wfpctl validate policy.json

# Remove all applied filters
wfpctl rollback
```

### Example Output

```
Applying policy: C:\policies\example.json

Policy applied successfully!
  Policy version:  1.0.0
  Total rules:     10
  Filters created: 15
  Filters removed: 5
  Rules skipped:   2

Warnings:
  - Rule 'disabled-rule' is disabled, skipping
  - Rule 'ipv6-rule' is disabled, skipping

Use 'wfpctl rollback' to remove all filters.
```

## Example Policy

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T12:00:00Z",
  "rules": [
    {
      "id": "block-cloudflare-dns",
      "action": "block",
      "direction": "outbound",
      "protocol": "tcp",
      "remote": {
        "ip": "1.1.1.1",
        "ports": "443"
      },
      "priority": 100,
      "enabled": true,
      "comment": "Block HTTPS to Cloudflare DNS"
    },
    {
      "id": "block-private-network",
      "action": "block",
      "direction": "outbound",
      "protocol": "tcp",
      "remote": {
        "ip": "10.0.0.0/8"
      },
      "priority": 50,
      "enabled": true,
      "comment": "Block all TCP to 10.x.x.x private range"
    },
    {
      "id": "allow-chrome-https",
      "action": "allow",
      "direction": "outbound",
      "protocol": "tcp",
      "remote": {
        "ports": "443"
      },
      "process": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
      "priority": 200,
      "enabled": true,
      "comment": "Allow Chrome to access HTTPS"
    }
  ]
}
```

## Service IPC

### Request

```json
{
  "type": "apply",
  "policyPath": "C:\\policies\\example.json"
}
```

### Response (Success)

```json
{
  "ok": true,
  "filtersCreated": 15,
  "filtersRemoved": 5,
  "rulesSkipped": 2,
  "policyVersion": "1.0.0",
  "totalRules": 10,
  "warnings": [
    "Rule 'disabled-rule' is disabled, skipping"
  ]
}
```

### Response (Compilation Error)

```json
{
  "ok": false,
  "error": "Policy compilation failed with 2 error(s)",
  "compilationErrors": [
    { "ruleId": "inbound-rule", "message": "Unsupported direction: 'inbound'" },
    { "ruleId": "udp-rule", "message": "Unsupported protocol: 'udp'" }
  ],
  "warnings": []
}
```

## Rollback Behavior

- `wfpctl rollback`: Removes all filters in our sublayer, keeping provider/sublayer intact
- `wfpctl teardown`: Full cleanup (requires rollback first if filters exist)
- Failed apply: Transaction aborted, WFP state unchanged

## How to Test

### Manual Testing in VM

1. Start the service:
   ```powershell
   sc start WfpTrafficControl
   ```

2. Create a test policy file `test-policy.json`:
   ```json
   {
     "version": "1.0.0",
     "defaultAction": "allow",
     "updatedAt": "2024-01-15T12:00:00Z",
     "rules": [
       {
         "id": "block-1111",
         "action": "block",
         "direction": "outbound",
         "protocol": "tcp",
         "remote": { "ip": "1.1.1.1", "ports": "443" },
         "priority": 100,
         "enabled": true
       }
     ]
   }
   ```

3. Validate the policy:
   ```powershell
   wfpctl validate test-policy.json
   ```

4. Apply the policy:
   ```powershell
   wfpctl apply test-policy.json
   ```

5. Test blocking:
   ```powershell
   curl -v --connect-timeout 5 https://1.1.1.1
   # Should timeout/fail
   ```

6. Rollback:
   ```powershell
   wfpctl rollback
   ```

7. Verify connectivity restored:
   ```powershell
   curl -v https://1.1.1.1
   # Should succeed
   ```

### Unit Tests

Run the RuleCompiler unit tests:
```powershell
dotnet test --filter "FullyQualifiedName~RuleCompiler"
```

## Known Limitations

1. **IPv6 not supported**: Only IPv4 addresses/CIDR supported
2. **Inbound not supported**: Only outbound rules supported
3. **UDP not supported**: Only TCP protocol supported
4. **Local endpoint not supported**: Cannot filter by local IP/port
5. **Process path resolution**: If the executable doesn't exist at apply time, the process condition is skipped
6. **No persistence**: Policy must be re-applied after service restart

## Files Changed

### Created
- `src/shared/Policy/RuleCompiler.cs` - Rule compilation logic
- `src/shared/Ipc/ApplyMessages.cs` - IPC message types for apply
- `docs/features/012-compile-outbound-tcp.md` - This documentation

### Modified
- `src/shared/Native/NativeMethods.cs` - Added FWP_MATCH_RANGE, FWP_RANGE0, FWPM_CONDITION_ALE_APP_ID
- `src/shared/Native/IWfpEngine.cs` - Added ApplyFilters method
- `src/service/Wfp/WfpEngine.cs` - Implemented ApplyFilters
- `src/shared/Ipc/IpcMessages.cs` - Added ApplyRequest parsing
- `src/service/Ipc/PipeServer.cs` - Added apply request handler
- `src/cli/Program.cs` - Implemented apply command
- `tests/*.cs` - Updated mock implementations

## Future Work

- Phase 13+: Add inbound rule support
- Phase 13+: Add UDP protocol support
- Phase 13+: Add IPv6 support
- Phase 13+: Add local endpoint filtering
- Phase 13+: Policy persistence and hot reload
