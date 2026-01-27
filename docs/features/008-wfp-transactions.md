# 008 - WFP Transaction Wrapper

## Overview

This feature introduces a RAII-style transaction wrapper (`WfpTransaction`) that simplifies WFP transaction management with automatic abort-on-dispose semantics. The wrapper ensures that transactions are properly cleaned up even when exceptions occur.

## Problem Statement

The previous implementation used manual transaction tracking with a `transactionActive` boolean flag:

```csharp
// Old pattern - error-prone
bool transactionActive = true;
try
{
    // ... do work ...
    CommitTransaction(engineHandle);
    transactionActive = false;
}
catch
{
    if (transactionActive) AbortTransaction(engineHandle);
    throw;
}
```

Issues with this approach:
- Easy to forget to set `transactionActive = false` after commit
- Duplicated tracking logic across multiple methods
- Risk of leaving transactions open on early returns

## Solution

The `WfpTransaction` wrapper provides automatic cleanup:

```csharp
// New pattern - safe and simple
var txResult = WfpTransaction.Begin(engineHandle);
if (txResult.IsFailure) return Result.Failure(txResult.Error);

using var transaction = txResult.Value;

// ... do work ...

var commitResult = transaction.Commit();
if (commitResult.IsFailure) return commitResult;

// Transaction auto-aborts on dispose if not committed
```

## Components

### WfpTransaction Class

| Member | Description |
|--------|-------------|
| `Begin(engineHandle, nativeTransaction?)` | Factory method that starts a new transaction |
| `Commit()` | Explicitly commits the transaction |
| `Dispose()` | Aborts the transaction if not committed |
| `IsCommitted` | True if `Commit()` succeeded |
| `IsDisposed` | True if transaction has been disposed |
| `LastErrorCode` | Last error code from a failed operation (0 if no error) |

### IWfpNativeTransaction Interface

Abstracts the P/Invoke calls for testability:

```csharp
public interface IWfpNativeTransaction
{
    uint Begin(IntPtr engineHandle);
    uint Commit(IntPtr engineHandle);
    uint Abort(IntPtr engineHandle);
}
```

### WfpNativeTransaction Class

Default implementation using actual P/Invoke calls:

```csharp
internal sealed class WfpNativeTransaction : IWfpNativeTransaction
{
    public static readonly WfpNativeTransaction Instance = new();
    // ... calls NativeMethods.FwpmTransaction* functions
}
```

## Transaction Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│                    WfpTransaction Lifecycle                      │
└─────────────────────────────────────────────────────────────────┘

Begin() ──────► Transaction Active
                     │
         ┌──────────┴──────────┐
         │                     │
    Commit()              Dispose()
         │              (without commit)
         ▼                     │
   IsCommitted=true            ▼
         │              Abort() called
         │                     │
         ▼                     ▼
   Dispose()             IsDisposed=true
   (no-op)
         │
         ▼
   IsDisposed=true
```

## Error Handling

### Begin Failure
If `FwpmTransactionBegin0` fails, `Begin()` returns a failed `Result<WfpTransaction>` with the translated error.

### Commit Failure
If `FwpmTransactionCommit0` fails:
1. Windows automatically aborts the transaction
2. `IsDisposed` is set to `true`
3. `IsCommitted` remains `false`
4. `Dispose()` will not attempt to abort again

### Abort Failure
If `FwpmTransactionAbort0` fails during dispose:
- The error is logged as a warning
- No exception is thrown (safe in dispose context)

## Files

| File | Purpose |
|------|---------|
| `src/shared/Native/WfpTransaction.cs` | Transaction wrapper and interfaces |
| `src/service/Wfp/WfpEngine.cs` | Updated to use WfpTransaction |
| `tests/WfpTransactionTests.cs` | Unit tests with mock implementation |

## Usage in WfpEngine

### EnsureProviderAndSublayerExist

```csharp
_logger.LogDebug("Beginning WFP transaction");
var txResult = WfpTransaction.Begin(engineHandle);
if (txResult.IsFailure)
{
    _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
    return Result.Failure(txResult.Error);
}

using var transaction = txResult.Value;

// Step 1: Ensure provider exists
var providerResult = EnsureProviderExistsInternal(engineHandle);
if (providerResult.IsFailure)
{
    return providerResult; // Transaction aborted by dispose
}

// Step 2: Ensure sublayer exists
var sublayerResult = EnsureSublayerExistsInternal(engineHandle);
if (sublayerResult.IsFailure)
{
    return sublayerResult; // Transaction aborted by dispose
}

// Commit transaction
var commitResult = transaction.Commit();
if (commitResult.IsFailure) return commitResult;

return Result.Success();
```

### RemoveProviderAndSublayer

Same pattern - early returns automatically trigger abort via dispose.

## Testing

### Unit Tests

The `WfpTransactionTests.cs` file contains comprehensive tests:

| Test Category | Coverage |
|---------------|----------|
| Begin Tests | Valid handle, zero handle, native failure |
| Commit Tests | Success, already committed, native failure, after dispose |
| Dispose Tests | Without commit (aborts), after commit (no-op), double dispose |
| Using Pattern | With commit, without commit, with exception |
| State Properties | IsCommitted, IsDisposed tracking |

### Running Tests

```bash
cd tests
dotnet test --filter "FullyQualifiedName~WfpTransactionTests"
```

### Mock Implementation

Tests use `MockNativeTransaction` to verify behavior without requiring admin rights:

```csharp
private class MockNativeTransaction : IWfpNativeTransaction
{
    public int BeginCallCount { get; private set; }
    public int CommitCallCount { get; private set; }
    public int AbortCallCount { get; private set; }

    public uint BeginReturnValue { get; set; } = 0; // SUCCESS
    public uint CommitReturnValue { get; set; } = 0;
    public uint AbortReturnValue { get; set; } = 0;

    // ... implementation tracks calls and returns configured values
}
```

## Rollback Behavior

### Normal Operation
1. Transaction begins
2. Work is performed
3. `Commit()` is called
4. `Dispose()` is called (no-op since committed)

### Failure During Work
1. Transaction begins
2. Error occurs during work
3. Method returns early
4. `Dispose()` is called by `using` block
5. Transaction is aborted automatically

### Exception
1. Transaction begins
2. Exception thrown
3. `using` block's `finally` calls `Dispose()`
4. Transaction is aborted automatically
5. Exception propagates

## Known Limitations

1. **Single transaction per session**: WFP only allows one active transaction per engine session. The wrapper does not enforce this - the caller must ensure proper sequencing.

2. **No nested transactions**: WFP does not support nested transactions. Attempting to begin a transaction while one is active will fail.

3. **Thread affinity**: The transaction is tied to the engine handle and should be used on the same thread that opened the session.

## Security Considerations

- No user input flows into transaction operations
- Error codes are translated through `WfpErrorTranslator` with safe error messages
- Abort failures are logged but do not expose internal state

## Performance

The wrapper adds minimal overhead:
- One object allocation per transaction
- No additional P/Invoke calls beyond the required begin/commit/abort
- Dispose pattern is compiler-optimized

## Future Enhancements

1. **Transaction timeout support**: Could expose the `txnWaitTimeoutInMSec` parameter from `FWPM_SESSION0`
2. **Transaction ID tracking**: Could expose the transaction ID for debugging
3. **Async support**: Could add async versions for long-running transactions
