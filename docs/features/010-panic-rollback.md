# 010 - Panic Rollback (RemoveAllFiltersInOurSublayer)

## Summary

Implements a robust panic rollback mechanism that enumerates all WFP filters in our sublayer and deletes them atomically in a transaction. This ensures the system can always recover to a clean state without connectivity loss.

## Behavior

### `wfpctl rollback` Command

The rollback command removes all filters from our sublayer while preserving the provider and sublayer infrastructure.

```bash
# Remove all filters (keeps provider/sublayer)
wfpctl rollback

# Output on success:
# Panic rollback completed successfully
#   Filters removed: 3
#   Provider and sublayer kept intact
#
# Use 'wfpctl teardown' to also remove provider and sublayer.
```

### API: `IWfpEngine.RemoveAllFilters()`

```csharp
/// <summary>
/// Removes all filters in our sublayer using enumeration.
/// </summary>
/// <returns>
/// Result<int> containing the number of filters removed on success.
/// On failure, returns an error without partial cleanup.
/// </returns>
Result<int> RemoveAllFilters();
```

**Properties:**
- **Idempotent**: Calling when no filters exist returns `Success(0)`
- **Atomic**: All deletions happen in a single transaction
- **Safe**: Provider and sublayer are preserved
- **Discoverable**: Enumerates filters dynamically (not hardcoded GUIDs)

## Implementation Details

### Two-Phase Approach

**Phase 1: Enumeration (outside transaction)**
1. Open WFP engine session
2. Create filter enumeration handle with `FwpmFilterCreateEnumHandle0`
3. Enumerate all filters in batches of 100 using `FwpmFilterEnum0`
4. For each filter, check if `subLayerKey` matches our sublayer GUID
5. Collect filter IDs for deletion
6. Destroy enumeration handle with `FwpmFilterDestroyEnumHandle0`

**Phase 2: Deletion (in transaction)**
1. Begin WFP transaction
2. Delete each filter by ID using `FwpmFilterDeleteById0`
3. Handle `FWP_E_FILTER_NOT_FOUND` gracefully (race condition)
4. Commit transaction on success, abort on failure

### WFP APIs Used

| API | Purpose |
|-----|---------|
| `FwpmFilterCreateEnumHandle0` | Create enumeration handle |
| `FwpmFilterEnum0` | Enumerate filters in batches |
| `FwpmFilterDestroyEnumHandle0` | Clean up enumeration handle |
| `FwpmFilterDeleteById0` | Delete filter by runtime ID |
| `FwpmFreeMemory0` | Free memory from enumeration |

### Sublayer Scoping

The `FWPM_FILTER_ENUM_TEMPLATE0` structure doesn't support sublayer filtering directly. The implementation:
1. Enumerates all filters (no template filter)
2. Checks each filter's `subLayerKey` field against `WfpConstants.SublayerGuid`
3. Only collects filters that match our sublayer

## Rollback vs Teardown

| Aspect | `wfpctl rollback` | `wfpctl teardown` |
|--------|------------------|------------------|
| Filters | Removed | Must be removed first (or will fail) |
| Sublayer | Preserved | Removed |
| Provider | Preserved | Removed |
| Use case | Quick recovery | Full uninstall |

**Typical workflow for full cleanup:**
```bash
wfpctl rollback  # Remove all filters
wfpctl teardown  # Remove provider and sublayer
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| No filters exist | Returns `Success(0)` |
| Enumeration fails | Returns error, no changes made |
| Delete fails mid-transaction | Transaction aborts, no partial cleanup |
| Filter removed during enumeration | `FWP_E_FILTER_NOT_FOUND` ignored, continues |
| Engine open fails | Returns error immediately |

## IPC Protocol

**Request:**
```json
{ "type": "rollback" }
```

**Success Response:**
```json
{
  "ok": true,
  "filtersRemoved": 3
}
```

**Failure Response:**
```json
{
  "ok": false,
  "error": "Failed to enumerate filters"
}
```

## Testing

### Unit Tests

Located in `tests/PanicRollbackTests.cs`:

- `RemoveAllFilters_WhenNoFilters_ReturnsZero` - Idempotency
- `RemoveAllFilters_WithFilters_RemovesAllAndReturnsCount` - Basic removal
- `RemoveAllFilters_OnEnumerationFailure_ReturnsError` - Error handling
- `RemoveAllFilters_OnDeletionFailure_ReturnsError` - Transaction abort
- `RemoveAllFilters_MultipleCalls_IsIdempotent` - Repeated calls

### Manual Testing (in VM)

1. Start service: `dotnet run --project src/service`
2. Add filters: `wfpctl demo-block enable`
3. Verify filter exists: `wfpctl demo-block status`
4. Run rollback: `wfpctl rollback`
5. Verify no filters: `wfpctl demo-block status`
6. Verify sublayer exists: `wfpctl status` (should show bootstrap info)

### Integration Test Script

```powershell
# scripts/test-rollback.ps1
Write-Host "Testing panic rollback..."

# Setup
wfpctl bootstrap
wfpctl demo-block enable

# Verify filter is active
$status = wfpctl demo-block status
if ($status -notmatch "Active: True") {
    Write-Error "Filter not active"
    exit 1
}

# Test rollback
wfpctl rollback

# Verify filter is gone
$status = wfpctl demo-block status
if ($status -notmatch "Active: False") {
    Write-Error "Filter still active after rollback"
    exit 1
}

# Verify sublayer still exists
$status = wfpctl status
if ($status -notmatch "running") {
    Write-Error "Service not running"
    exit 1
}

Write-Host "Rollback test passed!"
```

## Known Limitations

1. **Large filter sets**: Enumeration is done in batches of 100, but very large filter sets may take time
2. **Race conditions**: Filters added during rollback won't be removed (run rollback again)
3. **No selective rollback**: Removes ALL filters in our sublayer, not specific ones

## Security Considerations

- Rollback requires admin privileges (enforced by named pipe authorization)
- Only removes filters in our sublayer (identified by GUID)
- Cannot affect other providers' filters
- Transaction ensures atomic cleanup (no partial state)

## Related Documentation

- [007-wfp-bootstrap.md](007-wfp-bootstrap.md) - Provider/sublayer creation
- [008-wfp-transactions.md](008-wfp-transactions.md) - Transaction handling
- [009-demo-block-rule.md](009-demo-block-rule.md) - Demo filter implementation
