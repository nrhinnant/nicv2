# 013 — Idempotent Reconciliation

## Summary

This feature implements idempotent policy application. When the same policy is applied multiple times, only the necessary changes are made to WFP filters — filters that already exist are left untouched, new filters are added, and obsolete filters are removed.

## Motivation

The previous `ApplyFilters` implementation used a "delete all, add all" approach:
1. Enumerate all filters in our sublayer
2. Delete all of them
3. Create all new filters

This approach has drawbacks:
- **Not idempotent**: Re-applying the same policy deletes and recreates all filters
- **Noisy logs**: Every apply shows filters being removed and added
- **Unnecessary churn**: WFP state changes even when policy is unchanged
- **Brief enforcement gap**: During the delete-all phase, no filters are active

The reconciliation approach fixes all of these issues.

## Behavior

### Idempotent Apply

When `ApplyFilters` is called:

1. **Enumerate current state**: Get all filters currently in our sublayer with their GUIDs
2. **Compute diff**: Compare desired filters (from compiled policy) with current filters
3. **Apply diff atomically**:
   - Delete filters that are in current but not in desired
   - Add filters that are in desired but not in current
   - Leave unchanged filters untouched
4. **Early exit**: If no changes needed, skip the transaction entirely

### Diff Computation

Filters are compared by their GUID key (`FilterKey`). The GUID is deterministically generated from the rule ID and port index (for rules with multiple port ranges), ensuring:
- Same rule always produces the same filter GUID
- Different rules always produce different filter GUIDs

### Apply Result

The `ApplyResult` now includes three counts:
- `FiltersCreated`: Number of new filters added
- `FiltersRemoved`: Number of obsolete filters deleted
- `FiltersUnchanged`: Number of filters left untouched
- `TotalActive`: Convenience property = `FiltersUnchanged + FiltersCreated`

## Implementation Details

### New Types

**ExistingFilter** (`src/shared/Policy/FilterDiff.cs`)
```csharp
public sealed record ExistingFilter
{
    public Guid FilterKey { get; init; }
    public ulong FilterId { get; init; }
    public string? DisplayName { get; init; }
}
```

**FilterDiff** (`src/shared/Policy/FilterDiff.cs`)
```csharp
public sealed class FilterDiff
{
    public List<CompiledFilter> ToAdd { get; }
    public List<Guid> ToRemove { get; }
    public int Unchanged { get; set; }
    public bool IsEmpty => ToAdd.Count == 0 && ToRemove.Count == 0;
}
```

### Modified Methods

**EnumerateFiltersInOurSublayer** (`src/service/Wfp/WfpEngine.cs`)
- Now returns `List<ExistingFilter>` instead of `List<ulong>`
- Extracts both filter GUID (`filterKey`) and runtime ID (`filterId`)

**ApplyFilters** (`src/service/Wfp/WfpEngine.cs`)
- Uses `FilterDiffComputer.ComputeDiff()` to compute changes
- Only modifies filters that need changing
- Returns early if no changes needed (true idempotency)

## Testing

### Unit Tests

Test the diff logic in `tests/FilterDiffTests.cs`:

| Scenario | Expected |
|----------|----------|
| Empty desired, empty current | Empty diff |
| Filters to add (desired not in current) | ToAdd populated |
| Filters to remove (current not in desired) | ToRemove populated |
| Mixed add/remove | Both lists populated |
| Same filters in both | Unchanged = count, IsEmpty = true |
| Partial overlap | Add new, remove obsolete, count unchanged |

### Manual Validation (VM)

1. Apply a policy with 2 rules:
   ```
   wfpctl apply policy-v1.json
   ```
   Output should show: `2 created, 0 removed, 0 unchanged`

2. Apply the same policy again:
   ```
   wfpctl apply policy-v1.json
   ```
   Output should show: `0 created, 0 removed, 2 unchanged`

3. Apply a policy with 1 rule removed:
   ```
   wfpctl apply policy-v2.json
   ```
   Output should show: `0 created, 1 removed, 1 unchanged`

4. Apply a policy with 1 rule added:
   ```
   wfpctl apply policy-v3.json
   ```
   Output should show: `1 created, 0 removed, 1 unchanged`

## Rollback

Rollback behavior is unchanged:
- `wfpctl rollback` still uses `RemoveAllFilters()` which enumerates and deletes all filters
- Provider and sublayer remain intact after rollback

## Known Limitations

1. **Filter content changes not detected**: If a rule's conditions change but keep the same ID, the filter won't be updated. The GUID is based on rule ID, not rule content. This is intentional — changing conditions should use a new rule ID or be handled by a future "update" flow.

2. **No partial failure recovery**: If adding a new filter fails mid-transaction, the entire transaction is aborted. All changes are rolled back atomically.

## Files Changed

| File | Change |
|------|--------|
| `src/shared/Policy/FilterDiff.cs` | Created — diff types and computation |
| `src/shared/Native/IWfpEngine.cs` | Modified — added `FiltersUnchanged` to `ApplyResult` |
| `src/service/Wfp/WfpEngine.cs` | Modified — reconciliation in `ApplyFilters` |
| `tests/FilterDiffTests.cs` | Created — unit tests for diff logic |
| `docs/features/013-idempotent-reconcile.md` | Created — this document |
