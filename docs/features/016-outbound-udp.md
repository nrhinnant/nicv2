# 016 - Outbound UDP Rule Compilation

## Overview

This phase adds support for **outbound UDP rules**, extending the rule compiler to support `protocol: "udp"` for outbound direction. UDP rules use the same WFP ALE_AUTH_CONNECT_V4 layer as outbound TCP to filter outgoing UDP datagrams.

## Scope

### Supported

| Field | Values | Notes |
|-------|--------|-------|
| `direction` | `"outbound"` | UDP only supported for outbound |
| `protocol` | `"tcp"`, `"udp"` | Both protocols now supported for outbound |
| `action` | `"allow"`, `"block"` | Both actions supported |
| `remote.ip` | IPv4 address or CIDR | Matches destination IP |
| `remote.ports` | Single, range, or comma-separated | Matches destination port |
| `process` | Full Windows path | Matches the sending application |
| `enabled` | `true`, `false` | Disabled rules are skipped |
| `priority` | Integer | Higher priority = higher weight in WFP |

### Not Supported (Errors)

| Field | Values | Error |
|-------|--------|-------|
| `direction` | `"both"` | "Unsupported direction" |
| `direction` + `protocol` | `"inbound"` + `"udp"` | "Inbound UDP rules are not supported" |
| `protocol` | `"any"` | "Unsupported protocol" |
| `local.ip` | Any | "Local endpoint filters not supported" |
| `local.ports` | Any | "Local endpoint filters not supported" |
| `remote.ip` | IPv6 addresses | "IPv6 addresses not supported" |

## Behavior

### WFP Layer Selection

Both TCP and UDP outbound rules use the same WFP layer:

| Direction | Protocol | WFP Layer | Purpose |
|-----------|----------|-----------|---------|
| `outbound` | `tcp` | `FWPM_LAYER_ALE_AUTH_CONNECT_V4` | Filters outbound TCP connections |
| `outbound` | `udp` | `FWPM_LAYER_ALE_AUTH_CONNECT_V4` | Filters outbound UDP datagrams |
| `inbound` | `tcp` | `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4` | Filters inbound TCP accepts |
| `inbound` | `udp` | N/A (not supported) | Inbound UDP not supported |

### Protocol Byte Values

| Protocol | IANA Number | Constant |
|----------|-------------|----------|
| TCP | 6 | `WfpConstants.ProtocolTcp` |
| UDP | 17 | `WfpConstants.ProtocolUdp` |

### Filter Properties

Generated UDP filters have:
- **Display Name**: `WfpTrafficControl: {ruleId}` (with port index suffix if multiple)
- **Description**: `Compiled from rule '{ruleId}': {action} udp outbound to {ip}:{port}`
- **GUID**: Deterministic, derived from rule ID + port index (idempotent applies)
- **Weight**: Base weight (1000) + rule priority
- **Layer**: `FWPM_LAYER_ALE_AUTH_CONNECT_V4`
- **Protocol Condition**: `FWPM_CONDITION_IP_PROTOCOL = 17`

## CLI Usage

```bash
# Apply a policy with UDP rules
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
      "id": "block-google-dns-udp",
      "action": "block",
      "direction": "outbound",
      "protocol": "udp",
      "remote": {
        "ip": "8.8.8.8",
        "ports": "53"
      },
      "priority": 100,
      "enabled": true,
      "comment": "Block DNS queries to Google DNS over UDP"
    },
    {
      "id": "block-cloudflare-dns-udp",
      "action": "block",
      "direction": "outbound",
      "protocol": "udp",
      "remote": {
        "ip": "1.1.1.1",
        "ports": "53"
      },
      "priority": 100,
      "enabled": true,
      "comment": "Block DNS queries to Cloudflare DNS over UDP"
    },
    {
      "id": "allow-local-dns",
      "action": "allow",
      "direction": "outbound",
      "protocol": "udp",
      "remote": {
        "ip": "192.168.1.1",
        "ports": "53"
      },
      "priority": 200,
      "enabled": true,
      "comment": "Allow DNS queries to local router"
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

2. Create a test policy file `udp-test-policy.json`:
   ```json
   {
     "version": "1.0.0",
     "defaultAction": "allow",
     "updatedAt": "2024-01-15T12:00:00Z",
     "rules": [
       {
         "id": "block-google-dns",
         "action": "block",
         "direction": "outbound",
         "protocol": "udp",
         "remote": {
           "ip": "8.8.8.8",
           "ports": "53"
         },
         "priority": 100,
         "enabled": true
       }
     ]
   }
   ```

3. Test DNS before blocking:
   ```powershell
   # This should succeed
   Resolve-DnsName -Name example.com -Server 8.8.8.8 -Type A
   ```

4. Apply the policy:
   ```powershell
   wfpctl apply udp-test-policy.json
   ```

5. Test DNS after blocking:
   ```powershell
   # This should fail/timeout due to UDP block
   Resolve-DnsName -Name example.com -Server 8.8.8.8 -Type A -DnsOnly
   ```

6. Rollback:
   ```powershell
   wfpctl rollback
   ```

7. Verify DNS restored:
   ```powershell
   # This should succeed again
   Resolve-DnsName -Name example.com -Server 8.8.8.8 -Type A
   ```

### Verification Script

A PowerShell script is provided at `scripts/test-udp-block.ps1` for automated verification.

### Unit Tests

Run the UDP-specific unit tests:
```powershell
dotnet test --filter "FullyQualifiedName~Udp"
```

Run all RuleCompiler tests:
```powershell
dotnet test --filter "FullyQualifiedName~RuleCompiler"
```

## Reconciliation

The existing reconciliation mechanism handles UDP filters the same as TCP:

1. **Enumeration**: All filters in our sublayer are enumerated
2. **Diff Computation**: Comparison by FilterKey (GUID) only
3. **Atomic Apply**: Adds and removes are transactional

Mixed TCP/UDP policies are fully supported. The protocol byte is set correctly based on each rule's protocol field.

## Rollback Behavior

- `wfpctl rollback`: Removes **all** filters in our sublayer (TCP and UDP)
- `wfpctl teardown`: Full cleanup (requires rollback first if filters exist)
- Failed apply: Transaction aborted, WFP state unchanged

## Known Limitations

1. **Inbound UDP not supported**: Only outbound UDP is implemented
2. **direction="both" not supported**: Must create separate inbound and outbound rules
3. **protocol="any" not supported**: Must specify "tcp" or "udp" explicitly
4. **IPv6 not supported**: Only IPv4 addresses supported
5. **Local endpoint not supported**: Cannot filter by local IP/port
6. **No persistence**: Policy must be re-applied after service restart

## Files Changed

### Modified
- `src/shared/WfpConstants.cs` - Added `ProtocolUdp = 17` constant
- `src/shared/Policy/RuleCompiler.cs` - Added UDP protocol support for outbound, added `GetProtocolByte()` helper
- `tests/RuleCompilerTests.cs` - Added UDP-specific unit tests, updated multi-error test

### Created
- `docs/features/016-outbound-udp.md` - This documentation
- `scripts/test-udp-block.ps1` - Manual verification script

## Future Work

- Add inbound UDP support
- Add IPv6 support
- Add local endpoint filtering
- Add `direction="both"` support (compile to two filters)
- Add `protocol="any"` support (compile to TCP + UDP filters)
- Policy persistence and hot reload
