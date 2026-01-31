# 015 - Inbound TCP Rule Compilation

## Overview

This phase adds support for **inbound TCP rules**, extending the rule compiler to support the `direction: "inbound"` setting. Inbound rules use the WFP ALE_AUTH_RECV_ACCEPT_V4 layer to filter incoming TCP connection accepts.

## Scope

### Supported

| Field | Values | Notes |
|-------|--------|-------|
| `direction` | `"inbound"`, `"outbound"` | Both now supported |
| `protocol` | `"tcp"` | Only TCP protocol supported |
| `action` | `"allow"`, `"block"` | Both actions supported |
| `remote.ip` | IPv4 address or CIDR | Matches the **connecting client's** IP |
| `remote.ports` | Single, range, or comma-separated | Matches the **connecting client's** port |
| `process` | Full Windows path | Matches the **listening application** accepting the connection |
| `enabled` | `true`, `false` | Disabled rules are skipped |
| `priority` | Integer | Higher priority = higher weight in WFP |

### Not Supported (Errors)

| Field | Values | Error |
|-------|--------|-------|
| `direction` | `"both"` | "Unsupported direction" |
| `protocol` | `"udp"`, `"any"` | "Unsupported protocol" |
| `local.ip` | Any | "Local endpoint filters not supported" |
| `local.ports` | Any | "Local endpoint filters not supported" |
| `remote.ip` | IPv6 addresses | "IPv6 addresses not supported" |

## Behavior

### WFP Layer Selection

The compiler now sets a `Direction` property on each compiled filter:

| Direction | WFP Layer | Purpose |
|-----------|-----------|---------|
| `outbound` | `FWPM_LAYER_ALE_AUTH_CONNECT_V4` | Filters outbound connection attempts |
| `inbound` | `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4` | Filters inbound connection accepts |

### Semantic Clarification

For **inbound rules**:
- `remote.ip` matches the **connecting client's IP address**
- `remote.ports` matches the **connecting client's source port**
- `process` matches the **listening application** that is accepting the connection

This is consistent with the firewall perspective where "remote" always refers to the other party in the connection.

### Process Matching for Inbound

The `FWPM_CONDITION_ALE_APP_ID` condition is available at the ALE_AUTH_RECV_ACCEPT_V4 layer. When specified:
- Must be a full Windows path (e.g., `C:\Program Files\MyApp\server.exe`)
- Matches the listening process that is accepting the inbound connection
- If path resolution fails, the process condition is skipped (warning logged)

### Filter Properties

Generated inbound filters have:
- **Display Name**: `WfpTrafficControl: {ruleId}` (with port index suffix if multiple)
- **Description**: `Compiled from rule '{ruleId}': {action} tcp inbound to {ip}:{port}`
- **GUID**: Deterministic, derived from rule ID + port index (idempotent applies)
- **Weight**: Base weight (1000) + rule priority
- **Layer**: `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4`

## CLI Usage

```bash
# Apply a policy with inbound rules
wfpctl apply policy.json

# Validate policy (no service needed)
wfpctl validate policy.json

# Remove all applied filters
wfpctl rollback
```

## Example Policy

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T12:00:00Z",
  "rules": [
    {
      "id": "block-external-ssh",
      "action": "block",
      "direction": "inbound",
      "protocol": "tcp",
      "remote": {
        "ip": "0.0.0.0/0"
      },
      "process": "C:\\Windows\\System32\\OpenSSH\\sshd.exe",
      "priority": 100,
      "enabled": true,
      "comment": "Block all inbound SSH connections"
    },
    {
      "id": "allow-local-web",
      "action": "allow",
      "direction": "inbound",
      "protocol": "tcp",
      "remote": {
        "ip": "192.168.1.0/24",
        "ports": "80,443"
      },
      "priority": 200,
      "enabled": true,
      "comment": "Allow inbound HTTP/HTTPS from local network"
    },
    {
      "id": "block-outbound-telemetry",
      "action": "block",
      "direction": "outbound",
      "protocol": "tcp",
      "remote": {
        "ip": "104.46.162.224/28",
        "ports": "443"
      },
      "priority": 50,
      "enabled": true,
      "comment": "Block outbound telemetry"
    }
  ]
}
```

## How to Test

### Manual Testing in VM

1. Start the service:
   ```powershell
   sc start WfpTrafficControl
   ```

2. Create a test policy file `inbound-test-policy.json`:
   ```json
   {
     "version": "1.0.0",
     "defaultAction": "allow",
     "updatedAt": "2024-01-15T12:00:00Z",
     "rules": [
       {
         "id": "block-inbound-8080",
         "action": "block",
         "direction": "inbound",
         "protocol": "tcp",
         "remote": { "ports": "8080" },
         "priority": 100,
         "enabled": true
       }
     ]
   }
   ```

3. Apply the policy:
   ```powershell
   wfpctl apply inbound-test-policy.json
   ```

4. Start a simple listener:
   ```powershell
   # Start a TCP listener on port 8080
   $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, 8080)
   $listener.Start()
   Write-Host "Listening on port 8080..."
   ```

5. Test connection (from another terminal or machine):
   ```powershell
   # This should fail/timeout due to the inbound block rule
   Test-NetConnection -ComputerName localhost -Port 8080
   ```

6. Rollback:
   ```powershell
   wfpctl rollback
   ```

7. Verify connectivity restored:
   ```powershell
   # This should succeed now
   Test-NetConnection -ComputerName localhost -Port 8080
   ```

### Unit Tests

Run the inbound-specific unit tests:
```powershell
dotnet test --filter "FullyQualifiedName~Inbound"
```

Run all RuleCompiler tests:
```powershell
dotnet test --filter "FullyQualifiedName~RuleCompiler"
```

## Reconciliation

The existing reconciliation mechanism handles inbound filters the same as outbound:

1. **Enumeration**: All filters in our sublayer are enumerated (regardless of layer)
2. **Diff Computation**: Comparison by FilterKey (GUID) only
3. **Atomic Apply**: Adds and removes are transactional

Mixed inbound/outbound policies are fully supported. Each filter is applied to its appropriate layer based on the `Direction` property.

## Rollback Behavior

- `wfpctl rollback`: Removes **all** filters in our sublayer (both inbound and outbound)
- `wfpctl teardown`: Full cleanup (requires rollback first if filters exist)
- Failed apply: Transaction aborted, WFP state unchanged

## Known Limitations

1. **direction="both" not supported**: Must create separate inbound and outbound rules
2. **UDP not supported**: Only TCP protocol supported
3. **IPv6 not supported**: Only IPv4 addresses supported
4. **Local endpoint not supported**: Cannot filter by local IP/port
5. **No persistence**: Policy must be re-applied after service restart

## Files Changed

### Modified
- `src/shared/Native/NativeMethods.cs` - Added `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4` GUID
- `src/shared/Policy/RuleCompiler.cs` - Added `Direction` property to `CompiledFilter`, updated validation to allow inbound
- `src/service/Wfp/WfpEngine.cs` - Select WFP layer based on direction
- `tests/RuleCompilerTests.cs` - Added inbound-specific unit tests

### Created
- `docs/features/015-inbound-tcp.md` - This documentation

## Future Work

- Add UDP protocol support
- Add IPv6 support
- Add local endpoint filtering
- Add `direction="both"` support (compile to two filters)
- Policy persistence and hot reload
