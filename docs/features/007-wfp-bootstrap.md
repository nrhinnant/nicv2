# 007 - WFP Bootstrap (Provider & Sublayer Management)

## Overview

This feature implements the foundational WFP infrastructure required before any filters can be created. It provides:

1. **Bootstrap**: Creates our WFP provider and sublayer if they don't exist
2. **Teardown**: Removes our WFP provider and sublayer (panic rollback mechanism)

Both operations are **idempotent** — calling them multiple times has the same effect as calling once.

## WFP Objects Created

### Provider
| Property | Value |
|----------|-------|
| GUID | `7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A` |
| Name | `WfpTrafficControl Provider` |
| Description | `Traffic control provider for WfpTrafficControl service` |
| Flags | Non-persistent (removed on reboot or service stop) |

### Sublayer
| Property | Value |
|----------|-------|
| GUID | `B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C` |
| Name | `WfpTrafficControl Sublayer` |
| Description | `Sublayer containing all WfpTrafficControl filters` |
| Provider | Linked to our provider GUID |
| Weight | `0x8000` (32768 - middle of the range) |
| Flags | Non-persistent |

## CLI Commands

### `wfpctl bootstrap`

Ensures the WFP provider and sublayer exist. Creates them if they don't.

```
> wfpctl bootstrap
WFP bootstrap completed successfully
  Provider exists: True
  Sublayer exists: True
```

Exit codes:
- `0` - Success (objects created or already existed)
- `1` - Failure (see error message)

### `wfpctl teardown`

Removes the WFP provider and sublayer. This is the **panic rollback** mechanism.

```
> wfpctl teardown
WFP teardown completed successfully
  Provider removed: True
  Sublayer removed: True
```

Exit codes:
- `0` - Success (objects removed or didn't exist)
- `1` - Failure (see error message)

**Important**: Teardown will fail if filters still exist in the sublayer. Remove all filters first.

## Idempotency Guarantees

### Bootstrap
- If provider already exists: logs "skipping creation", returns success
- If sublayer already exists: logs "skipping creation", returns success
- If `FWP_E_ALREADY_EXISTS` returned (race condition): treated as success

### Teardown
- If provider doesn't exist: logs "skipping removal", returns success
- If sublayer doesn't exist: logs "skipping removal", returns success
- If `FWP_E_PROVIDER_NOT_FOUND` or `FWP_E_SUBLAYER_NOT_FOUND` (race condition): treated as success

## Transaction Semantics

All operations are wrapped in WFP transactions:

1. `FwpmTransactionBegin0` - Start transaction
2. Perform operations (create/delete provider and sublayer)
3. `FwpmTransactionCommit0` - Commit on success
4. `FwpmTransactionAbort0` - Abort on any failure

This ensures atomicity — either both objects are created/deleted, or neither is.

## Rollback Behavior

### Panic Rollback
The `teardown` command is designed as a panic rollback mechanism:
- Can be called at any time to remove all WFP objects created by this service
- Restores normal network connectivity by removing our provider and sublayer
- Safe to call even if objects don't exist (idempotent)

### Failure During Teardown
If teardown fails with `FWP_E_IN_USE`:
- Sublayer has filters that must be removed first
- Use future `wfpctl disable` or `wfpctl rollback` to remove filters
- Then retry teardown

## Architecture

### Components

```
CLI (wfpctl)                    Service                         WFP
     |                              |                             |
     |-- BootstrapRequest --------->|                             |
     |                              |-- FwpmEngineOpen0 --------->|
     |                              |-- FwpmTransactionBegin0 --->|
     |                              |-- FwpmProviderAdd0 -------->|
     |                              |-- FwpmSubLayerAdd0 -------->|
     |                              |-- FwpmTransactionCommit0 -->|
     |<-- BootstrapResponse --------|                             |
```

### Files

| File | Purpose |
|------|---------|
| `src/shared/Native/IWfpEngine.cs` | Interface for WFP operations (enables mocking) |
| `src/service/Wfp/WfpEngine.cs` | Implementation using WFP APIs |
| `src/shared/Native/NativeMethods.cs` | P/Invoke declarations for WFP APIs |
| `src/shared/Ipc/IpcMessages.cs` | `BootstrapRequest/Response`, `TeardownRequest/Response` |
| `src/service/Ipc/PipeServer.cs` | Handles bootstrap/teardown IPC requests |
| `src/cli/Program.cs` | `bootstrap` and `teardown` commands |

## IPC Protocol

### Bootstrap Request
```json
{ "type": "bootstrap" }
```

### Bootstrap Response
```json
{
  "ok": true,
  "providerExists": true,
  "sublayerExists": true
}
```

### Teardown Request
```json
{ "type": "teardown" }
```

### Teardown Response
```json
{
  "ok": true,
  "providerRemoved": true,
  "sublayerRemoved": true
}
```

## Error Handling

| Error Code | Meaning | Handling |
|------------|---------|----------|
| `FWP_E_ALREADY_EXISTS` | Object already exists | Treated as success (idempotent) |
| `FWP_E_PROVIDER_NOT_FOUND` | Provider doesn't exist | Treated as success for teardown |
| `FWP_E_SUBLAYER_NOT_FOUND` | Sublayer doesn't exist | Treated as success for teardown |
| `FWP_E_IN_USE` | Sublayer has filters | Fail with message to remove filters first |
| `ERROR_ACCESS_DENIED` | Not running as admin | Return access denied error |

## Testing

### Unit Tests
- `WfpBootstrapTests.cs` - Tests idempotency logic using mocked `IWfpEngine`

### Manual Testing (in VM)

1. **Bootstrap from clean state:**
   ```
   wfpctl bootstrap
   ```
   Verify: "WFP bootstrap completed successfully"

2. **Bootstrap again (idempotent):**
   ```
   wfpctl bootstrap
   ```
   Verify: Same success message, no errors

3. **Teardown:**
   ```
   wfpctl teardown
   ```
   Verify: "WFP teardown completed successfully"

4. **Teardown again (idempotent):**
   ```
   wfpctl teardown
   ```
   Verify: Same success message (ProviderRemoved/SublayerRemoved may be False)

5. **Verify WFP state with netsh:**
   ```
   netsh wfp show providers
   netsh wfp show sublayers
   ```
   Look for `WfpTrafficControl Provider` and `WfpTrafficControl Sublayer`

## Known Limitations

1. **Non-persistent objects**: Provider and sublayer are not marked as persistent, so they are removed when:
   - The WFP engine session is closed (service stops)
   - The system reboots

   This is intentional for safety — ensures clean state on restart.

2. **No automatic bootstrap on service start**: The service does not automatically create the provider/sublayer on startup. This is by design to require explicit user action.

3. **Sequential removal order**: Sublayer must be removed before provider. If sublayer removal fails (e.g., filters exist), provider removal is not attempted.

## Security Considerations

- Bootstrap/teardown require administrator privileges (enforced by service)
- Operations use GUIDs from `WfpConstants` — no user-provided input in WFP calls
- Transaction abort on failure prevents partial state
