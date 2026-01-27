# 002 — Shared Constants and Result Type

## Behavior

This milestone establishes a single source of truth for all WFP-related identifiers and a standardized error handling pattern used across the project.

### Constants Defined

| Constant | Value | Purpose |
|----------|-------|---------|
| `ProviderGuid` | `7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A` | Identifies our WFP provider in the system |
| `SublayerGuid` | `B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C` | Identifies our WFP sublayer containing all filters |
| `ProviderName` | "WfpTrafficControl Provider" | Display name visible in WFP diagnostic tools |
| `SublayerName` | "WfpTrafficControl Sublayer" | Display name for our sublayer |
| `ServiceName` | "WfpTrafficControl" | Windows service name for SCM registration |
| `PipeName` | "WfpTrafficControl" | Named pipe name for CLI-to-service IPC |
| `PipeFullPath` | `\\.\pipe\WfpTrafficControl` | Full pipe path for connections |

### Why GUIDs Matter

WFP uses GUIDs to identify objects (providers, sublayers, filters). These GUIDs must:
- Be **unique** to avoid conflicts with other software
- Be **stable** — never change after deployment
- Be **consistent** across all components (service, CLI, tests)

If GUIDs were changed after WFP objects were created, the old objects would become orphaned and require manual cleanup using `netsh wfp` commands.

### Result Type

The `Result<T>` type provides explicit error handling without exceptions for expected failure cases:

```csharp
// Success case
Result<int> success = 42;  // Implicit conversion

// Failure case
Result<int> failure = new Error(ErrorCodes.InvalidArgument, "Value must be positive");

// Pattern matching
var message = result.Match(
    onSuccess: value => $"Got {value}",
    onFailure: error => $"Error: {error.Message}"
);
```

### Standard Error Codes

| Code | Usage |
|------|-------|
| `UNKNOWN` | Unexpected errors |
| `INVALID_ARGUMENT` | Bad input parameters |
| `NOT_FOUND` | Resource not found |
| `PERMISSION_DENIED` | Authorization failed |
| `INVALID_POLICY` | Policy validation failed |
| `WFP_ERROR` | WFP API call failed |
| `SERVICE_ERROR` | Service-level error |
| `IPC_ERROR` | CLI-service communication failed |

## Configuration / Schema Changes

None. This milestone adds code only; no configuration files or policy schemas.

## How to Build

```bash
dotnet build
```

## How to Run Tests

```bash
dotnet test
```

Tests verify:
- GUIDs are non-empty
- GUIDs have expected values (stability check)
- Result type behaves correctly for success/failure cases

## Rollback / Uninstall Behavior

Not applicable — no runtime artifacts. This is compile-time only.

## Files Changed

| File | Change |
|------|--------|
| `src/shared/WfpConstants.cs` | **New** — All constants and GUIDs |
| `src/shared/Result.cs` | **New** — Result/Error types |
| `src/service/Program.cs` | Uses `WfpConstants.ServiceName` |
| `src/service/Worker.cs` | Uses constants in logging |
| `src/cli/Program.cs` | Uses `WfpConstants.ProjectName` |
| `tests/SanityTests.cs` | Updated with GUID stability tests |

## Known Limitations

- GUIDs are hardcoded; no mechanism to regenerate if collision detected
- Error codes are strings; consider enum if type safety becomes important
- No localization for error messages

## Dependencies

No new package dependencies added.
