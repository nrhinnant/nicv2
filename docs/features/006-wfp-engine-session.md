# 006 - WFP Engine Session Wrapper

## Overview

This feature provides a safe, managed wrapper around the Windows Filtering Platform (WFP) engine session APIs. An engine session is required before any WFP management operations (creating providers, sublayers, filters) can be performed.

## Components

### Files Added

| File | Purpose |
|------|---------|
| `src/shared/Native/NativeMethods.cs` | P/Invoke declarations for `FwpmEngineOpen0` and `FwpmEngineClose0` |
| `src/shared/Native/WfpEngineHandle.cs` | SafeHandle wrapper ensuring proper handle cleanup |
| `src/shared/Native/WfpErrorTranslator.cs` | Translates Win32/WFP error codes to `Error` objects |
| `src/shared/Native/WfpSession.cs` | High-level API for opening/closing engine sessions |

### Files Modified

| File | Change |
|------|--------|
| `src/shared/Shared.csproj` | Added `AllowUnsafeBlocks` for LibraryImport source generator |

## API Reference

### WfpSession.OpenEngine()

Opens a session to the local WFP filter engine.

```csharp
public static Result<WfpEngineHandle> OpenEngine()
```

**Returns:** A `Result<WfpEngineHandle>` containing the engine handle on success, or an error on failure.

**Requirements:** Administrator privileges. Returns `AccessDenied` error if not elevated.

**Usage:**
```csharp
var result = WfpSession.OpenEngine();
if (result.IsFailure)
{
    Console.WriteLine($"Failed to open engine: {result.Error}");
    return;
}

using (var handle = result.Value)
{
    // Use the handle for WFP operations...
}
// Handle is automatically closed when disposed
```

### WfpSession.CloseEngine(WfpEngineHandle handle)

Explicitly closes a WFP engine session with error reporting.

```csharp
public static Result CloseEngine(WfpEngineHandle handle)
```

**Note:** Normally use `Dispose()` or a `using` statement instead. This method is for cases where you need to check the close result explicitly.

### WfpSession.CanOpenEngine()

Pre-flight check to verify WFP operations are possible.

```csharp
public static bool CanOpenEngine()
```

**Returns:** `true` if an engine session can be opened; `false` otherwise.

### WfpEngineHandle

A `SafeHandle` derivative that wraps the native WFP engine handle.

**Key behaviors:**
- Automatically closes the handle via `FwpmEngineClose0` when disposed or finalized
- Derives from `SafeHandleZeroOrMinusOneIsInvalid` (zero and -1 are invalid)
- Thread-safe disposal through `SafeHandle` infrastructure

### WfpErrorTranslator

Translates Win32 and WFP-specific error codes into `Error` objects.

**Handled error codes:**
| Code | Error Type | Description |
|------|------------|-------------|
| `ERROR_ACCESS_DENIED` (5) | AccessDenied | Missing admin privileges |
| `ERROR_INVALID_PARAMETER` (87) | InvalidArgument | Bad parameter to WFP API |
| `ERROR_NOT_FOUND` (1168) | NotFound | Object not found |
| `FWP_E_ALREADY_EXISTS` | WfpError | WFP object already exists |
| `FWP_E_IN_USE` | WfpError | Object is in use |
| `FWP_E_PROVIDER_NOT_FOUND` | NotFound | Provider not found |
| `FWP_E_SUBLAYER_NOT_FOUND` | NotFound | Sublayer not found |
| `FWP_E_FILTER_NOT_FOUND` | NotFound | Filter not found |
| `FWP_E_NOT_FOUND` | NotFound | Generic WFP not found |
| `FWP_E_SESSION_ABORTED` | WfpError | Session was aborted |
| `FWP_E_INVALID_PARAMETER` | InvalidArgument | Invalid WFP parameter |
| Other | WfpError | Falls back to Win32 error message |

## Design Decisions

### Why SafeHandle?

- **Reliability**: Handles are released even if exceptions occur
- **Finalization guarantee**: Runtime ensures cleanup even if `Dispose()` is not called
- **Thread safety**: Built-in reference counting prevents premature closure
- **CER support**: Critical finalizer runs even in low-resource situations

### Why LibraryImport over DllImport?

- **Source generation**: No runtime marshaling overhead
- **Trimming friendly**: Compatible with AOT compilation
- **Type safety**: Compile-time verification of marshaling attributes

### Why return Result<T> instead of throwing?

- **Explicit error handling**: Callers must acknowledge potential failures
- **Consistent with project patterns**: Matches existing `Result<T>` usage
- **No exception overhead**: Faster in failure-heavy scenarios

## Testing

### Unit Tests

Unit tests for error translation are in `tests/WfpErrorTranslatorTests.cs`. These test error code mapping without requiring actual WFP calls.

```bash
dotnet test --filter "FullyQualifiedName~WfpErrorTranslator"
```

### Manual Validation (VM)

1. **Non-elevated test** (should fail with AccessDenied):
   ```csharp
   var result = WfpSession.OpenEngine();
   // result.IsFailure == true
   // result.Error.Code == "ACCESS_DENIED"
   ```

2. **Elevated test** (should succeed):
   ```powershell
   # Run as Administrator
   dotnet run --project src/cli
   # Or write a test app that calls WfpSession.OpenEngine()
   ```

3. **Handle leak verification**:
   - Open many sessions in a loop
   - Verify handles are released (use Process Explorer or handle count monitoring)

## Rollback / Uninstall

This component does not create any persistent WFP objects. Engine sessions are client-side state only.

- **Uninstall**: No action required
- **Rollback**: Not applicable

## Known Limitations

1. **Local engine only**: `serverName` is always null. Remote WFP management is not supported.
2. **Default session settings**: Uses default `FWPM_SESSION0` (no custom flags or timeout). Future milestones may add dynamic session support.
3. **No session reuse**: Each `OpenEngine()` call creates a new session. Connection pooling is not implemented.

## Future Enhancements

- Session configuration options (dynamic sessions, custom timeouts)
- Transaction support (`FwpmTransactionBegin0`, `FwpmTransactionCommit0`, `FwpmTransactionAbort0`)
- Provider/sublayer/filter management APIs (subsequent milestones)

## References

- [FwpmEngineOpen0 (Microsoft Docs)](https://docs.microsoft.com/en-us/windows/win32/api/fwpmu/nf-fwpmu-fwpmengineopen0)
- [FwpmEngineClose0 (Microsoft Docs)](https://docs.microsoft.com/en-us/windows/win32/api/fwpmu/nf-fwpmu-fwpmengineclose0)
- [SafeHandle Class (Microsoft Docs)](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle)
- [LibraryImportAttribute (Microsoft Docs)](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.libraryimportattribute)
