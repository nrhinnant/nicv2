# Feature 020: WFP Mocking / IWfpInterop Interface

## Status
Implemented (Phase 19)

## Summary
Introduced `IWfpInterop` interface to abstract low-level WFP P/Invoke operations, enabling unit testing of `WfpEngine` without actual WFP access.

## Problem Statement
The original `WfpEngine` directly called `NativeMethods` (P/Invoke) for all WFP operations. This made it impossible to unit test the reconciliation logic, rollback behavior, or other high-level engine operations without running as administrator on Windows with real WFP access.

## Solution

### Interface Extraction Pattern
Extracted a new interface `IWfpInterop` that encapsulates all low-level WFP operations:

```csharp
public interface IWfpInterop
{
    // Engine session
    Result<WfpEngineHandle> OpenEngine();

    // Provider operations
    Result<bool> ProviderExists(IntPtr engineHandle);
    Result AddProvider(IntPtr engineHandle);
    Result DeleteProvider(IntPtr engineHandle);

    // Sublayer operations
    Result<bool> SublayerExists(IntPtr engineHandle);
    Result AddSublayer(IntPtr engineHandle);
    Result DeleteSublayer(IntPtr engineHandle);

    // Filter operations
    Result<List<ExistingFilter>> EnumerateFiltersInSublayer(IntPtr engineHandle);
    Result<ulong> AddFilter(IntPtr engineHandle, CompiledFilter filter);
    Result DeleteFilterByKey(IntPtr engineHandle, Guid filterKey);
    Result DeleteFilterById(IntPtr engineHandle, ulong filterId);
    Result<bool> FilterExists(IntPtr engineHandle, Guid filterKey);
}
```

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `IWfpInterop` | `src/shared/Native/IWfpInterop.cs` | Interface contract for WFP operations |
| `WfpInterop` | `src/service/Wfp/WfpInterop.cs` | Production implementation using NativeMethods |
| `WfpEngine` | `src/service/Wfp/WfpEngine.cs` | Uses IWfpInterop (injected via constructor) |
| `FakeWfpInterop` | `tests/FakeWfpInterop.cs` | In-memory fake for unit testing |

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         WfpEngine                           │
│  - Policy reconciliation                                    │
│  - Transaction management (via WfpTransaction)              │
│  - Demo filter operations                                   │
│  - Rollback logic                                          │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ uses
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        IWfpInterop                          │
│  - OpenEngine()                                            │
│  - Provider/Sublayer CRUD                                  │
│  - Filter enumeration, add, delete                         │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              │                               │
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────┐
│       WfpInterop        │     │     FakeWfpInterop      │
│    (Production)         │     │    (Unit Testing)       │
│ Wraps NativeMethods     │     │ In-memory simulation    │
└─────────────────────────┘     └─────────────────────────┘
              │
              │ P/Invoke
              ▼
┌─────────────────────────┐
│     NativeMethods       │
│   fwpuclnt.dll (WFP)    │
└─────────────────────────┘
```

## Files Changed

### Created
- `src/shared/Native/IWfpInterop.cs` - Interface definition
- `src/service/Wfp/WfpInterop.cs` - Production implementation
- `tests/FakeWfpInterop.cs` - Fake implementation for testing
- `tests/WfpEngineReconcileTests.cs` - Unit tests for reconciliation

### Modified
- `src/service/Wfp/WfpEngine.cs` - Refactored to use IWfpInterop
- `src/service/Worker.cs` - Creates WfpInterop and injects into WfpEngine
- `tests/Tests.csproj` - Added service project reference

## Usage

### Production (Worker.cs)
```csharp
var wfpInterop = new WfpInterop(_loggerFactory.CreateLogger<WfpInterop>());
_wfpEngine = new WfpEngine(_loggerFactory.CreateLogger<WfpEngine>(), wfpInterop);
```

### Testing
```csharp
var fake = new FakeWfpInterop();
var engine = new WfpEngine(NullLogger<WfpEngine>.Instance, fake);

// Setup fake state
fake.AddExistingFilter(someFilterKey, 1);

// Test reconciliation
var result = engine.ApplyFilters(newFilters);

// Assert
Assert.Equal(expectedAdded, result.Value.FiltersCreated);
Assert.Equal(expectedRemoved, result.Value.FiltersRemoved);
```

## Testing Strategy

### Unit Tests (with FakeWfpInterop)
- **Reconcile edge cases**: Empty to non-empty, non-empty to empty, partial overlap
- **Rollback behavior**: Verify RemoveAllFilters deletes all filters
- **Idempotency**: Applying same policy twice produces no changes
- **Error handling**: Simulate WFP failures, verify transaction abort

### Integration Tests (real WFP)
- Run in Windows VM with administrator privileges
- Verify actual WFP objects are created/deleted
- Test with real network traffic

## Behavior Preserved

This refactoring preserves all existing behavior:
- Idempotent reconciliation (diff-based apply)
- Atomic transactions (all-or-nothing)
- Panic rollback (RemoveAllFilters)
- Provider/sublayer lifecycle
- Demo block filter operations

## Rollback/Uninstall

No new WFP objects are created by this change. The refactoring is purely structural.

To rollback code changes: revert the commits and rebuild.

## Known Limitations

1. **FakeWfpInterop doesn't simulate all WFP behavior**: It's a simplified in-memory model. Some edge cases (like transaction conflicts) may not be caught.

2. **WfpEngineHandle in tests**: The fake returns a mock handle. Code that uses `DangerousGetHandle()` will get `IntPtr.Zero` unless specifically configured.

## Test Coverage

| Test Category | Tests Added |
|--------------|-------------|
| Reconcile empty→filters | ✓ |
| Reconcile filters→empty | ✓ |
| Reconcile partial overlap | ✓ |
| Reconcile no change (idempotent) | ✓ |
| RemoveAllFilters with filters | ✓ |
| RemoveAllFilters empty (idempotent) | ✓ |
| RemoveAllFilters enumeration failure | ✓ |
| RemoveAllFilters delete failure | ✓ |

## Security Considerations

- No new attack surface introduced
- Interface still requires admin privileges for production use
- Fake implementation is test-only (not shipped in production builds)
