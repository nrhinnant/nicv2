# 014 — LKG Persistence and Fail-Open Behavior

## Summary

This feature implements Last Known Good (LKG) policy persistence with fail-open behavior. When a policy is successfully applied, it is saved as the LKG. On service startup, the LKG can optionally be auto-applied. If the LKG is missing or corrupt, the service starts without any policy (fail-open).

## Motivation

Without LKG persistence:
- After a reboot, the service starts with no policy applied
- Users must manually re-apply their policy after every restart
- There's no way to recover a previously working policy

With LKG persistence:
- The last successfully applied policy is saved to disk
- On service restart, the policy can be automatically restored
- CLI commands allow viewing and reverting to the saved LKG
- If anything goes wrong, the service fails open (no policy = all traffic allowed)

## Behavior

### LKG Save (After Apply)

When a policy is successfully applied via `wfpctl apply`:

1. The policy JSON is saved to `%ProgramData%\WfpTrafficControl\lkg-policy.json`
2. A SHA256 checksum is computed and stored with the policy
3. Metadata (timestamp, source path) is included
4. Atomic write pattern prevents corruption (write to temp, then rename)
5. LKG save failure is non-fatal (logged as warning, apply still succeeds)

### Auto-Apply on Startup

When the service starts (if `AutoApplyLkgOnStartup = true`):

1. Check if LKG file exists
2. If missing: log info, start with no policy (fail-open)
3. If corrupt: log warning, start with no policy (fail-open)
4. If valid: load, compile, and apply the LKG policy
5. If apply fails: log warning, start with no policy (fail-open)

**Important:** Auto-apply is disabled by default (`AutoApplyLkgOnStartup = false`). To enable it, change the constant in `WfpConstants.cs`.

### CLI Commands

#### `wfpctl lkg show`

Displays information about the stored LKG policy:

```
LKG (Last Known Good) Policy
  Path: C:\ProgramData\WfpTrafficControl\lkg-policy.json

  Status:  Valid
  Version: 1.0.0
  Rules:   3
  Saved:   2024-01-15T10:30:00.0000000Z
  Source:  C:\policies\my-policy.json

Use 'wfpctl lkg revert' to apply this policy.
```

If no LKG exists:
```
LKG (Last Known Good) Policy
  Path: C:\ProgramData\WfpTrafficControl\lkg-policy.json

  Status: No LKG policy saved

Apply a policy with 'wfpctl apply <file>' to save an LKG.
```

If LKG is corrupt:
```
LKG (Last Known Good) Policy
  Path: C:\ProgramData\WfpTrafficControl\lkg-policy.json

  Status: CORRUPT
  Error:  LKG checksum mismatch

Apply a new policy to replace the corrupt LKG.
```

#### `wfpctl lkg revert`

Applies the stored LKG policy:

```
Reverting to LKG policy...

LKG policy reverted successfully!
  Policy version:  1.0.0
  Total rules:     3
  Filters created: 3
  Filters removed: 0
  Rules skipped:   0

Use 'wfpctl rollback' to remove all filters.
```

## Implementation Details

### Storage Format

The LKG file is a JSON wrapper containing:

```json
{
  "checksum": "a1b2c3d4...",
  "policyJson": "{ ... actual policy JSON ... }",
  "savedAt": "2024-01-15T10:30:00Z",
  "sourcePath": "C:\\policies\\my-policy.json"
}
```

### Integrity Verification

- SHA256 checksum of the policy JSON
- Verified on every load
- Checksum mismatch = treat as corrupt (fail-open)

### New Types

**LkgStore** (`src/shared/Lkg/LkgStore.cs`)

Static class providing:
- `Save(policyJson, sourcePath)` - Save policy as LKG
- `Load()` - Load LKG with integrity verification
- `Exists()` - Check if LKG file exists
- `Delete()` - Remove LKG file
- `GetMetadata()` - Get LKG info without full load

**LkgLoadResult** - Result of loading LKG (success with policy, not found, or failed with error)

**LkgMetadata** - Lightweight metadata about LKG file

### IPC Messages

| Message Type | Description |
|--------------|-------------|
| `LkgShowRequest` | Request to show LKG info |
| `LkgShowResponse` | Response with LKG metadata |
| `LkgRevertRequest` | Request to apply LKG policy |
| `LkgRevertResponse` | Response with apply results |

## Configuration

| Constant | Default | Description |
|----------|---------|-------------|
| `AutoApplyLkgOnStartup` | `false` | Enable auto-apply of LKG on service start |
| `DataDirectoryName` | `WfpTrafficControl` | Directory under ProgramData |
| `LkgPolicyFileName` | `lkg-policy.json` | LKG file name |

## Testing

### Unit Tests

Test the LKG store in `tests/LkgStoreTests.cs`:

| Scenario | Expected |
|----------|----------|
| Save valid policy | Creates LKG file with checksum |
| Load valid LKG | Returns policy with metadata |
| Load missing LKG | Returns NotFound |
| Load corrupt LKG (bad checksum) | Returns Failed |
| Load corrupt LKG (invalid JSON) | Returns Failed |
| Load corrupt LKG (invalid policy) | Returns Failed |
| Atomic write (crash during save) | Original LKG preserved |

### Manual Validation (VM)

1. Apply a policy:
   ```
   wfpctl apply policy.json
   ```
   Verify LKG is created at `%ProgramData%\WfpTrafficControl\lkg-policy.json`

2. Check LKG status:
   ```
   wfpctl lkg show
   ```
   Should show valid LKG with correct metadata

3. Rollback and revert:
   ```
   wfpctl rollback
   wfpctl lkg revert
   ```
   Should restore the policy from LKG

4. Test corrupt handling:
   - Manually edit the LKG file to corrupt it
   - Run `wfpctl lkg show` - should show CORRUPT status
   - Run `wfpctl lkg revert` - should fail with error
   - Restart service - should start successfully (fail-open)

5. Test missing LKG:
   - Delete the LKG file
   - Restart service - should start successfully (fail-open)
   - Run `wfpctl lkg show` - should show "No LKG policy saved"

## Rollback

- `wfpctl rollback` removes all filters (unchanged behavior)
- `wfpctl lkg revert` applies the stored LKG (can be used after rollback)
- Deleting the LKG file does not affect running policy

## Known Limitations

1. **No automatic LKG update on manual changes**: If WFP filters are modified outside of the service (e.g., via PowerShell), the LKG is not updated.

2. **Single LKG only**: Only one LKG is stored. No history or rollback to previous LKGs.

3. **No LKG encryption**: The LKG file is readable by anyone with file access. Policy rules are not encrypted.

4. **Auto-apply disabled by default**: Must modify source code constant to enable.

## Security Considerations

1. **File access**: LKG stored in ProgramData, typically ACL'd for admin-only write access
2. **Checksum verification**: Prevents loading tampered/corrupted files
3. **Policy validation**: Full validation before applying (same as normal apply)
4. **Fail-open**: No connectivity loss on any error condition

## Files Changed

| File | Change |
|------|--------|
| `src/shared/WfpConstants.cs` | Added LKG path constants |
| `src/shared/Lkg/LkgStore.cs` | Created - LKG persistence |
| `src/shared/Ipc/IpcMessages.cs` | Added LKG request/response types |
| `src/service/Ipc/PipeServer.cs` | Added LKG handlers + LKG save after apply |
| `src/service/Worker.cs` | Added auto-apply LKG on startup |
| `src/cli/Program.cs` | Added `lkg show` and `lkg revert` commands |
| `tests/LkgStoreTests.cs` | Created - unit tests |
| `docs/features/014-lkg-and-failopen.md` | Created - this document |
