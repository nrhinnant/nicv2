# Feature 017: Hot Reload (File Watching)

## Overview

This feature adds automatic policy reloading when the watched policy file changes. The service monitors a specified policy file using `FileSystemWatcher` and automatically reapplies the policy when modifications are detected.

## Behavior

### File Watching Flow

1. User enables watching via `wfpctl watch set <path>`
2. Service validates the path and reads the policy file
3. Policy is validated, compiled, and applied immediately
4. `FileSystemWatcher` monitors the file for changes
5. When a change is detected, a debounce timer starts (default: 1000ms)
6. After debounce period with no new changes, policy is reapplied
7. On validation/apply failure, the last applied policy is kept (fail-open)

### Debounce Logic

The debounce mechanism prevents rapid reapplies during file edits:

- When a file change event occurs, a timer starts
- If another change occurs before the timer expires, the timer resets
- Only when the timer expires without new changes does the apply happen
- Default debounce: 1000ms (configurable in `appsettings.json`)
- Range: 100ms to 30000ms

### Fail-Open Behavior

If validation or apply fails:
- The last successfully applied policy remains in effect
- The error is logged
- Error statistics are tracked (visible via `wfpctl watch status`)
- File watching continues

## CLI Commands

### `wfpctl watch set <file>`

Enable file watching on a policy file.

```bash
# Enable watching
wfpctl watch set C:\policies\my-policy.json

# Disable watching (no path)
wfpctl watch set
```

**Response fields:**
- `watching`: Whether watching is now active
- `policyPath`: The path being watched
- `initialApplySuccess`: Whether the initial apply succeeded
- `warning`: Warning message if initial apply failed

### `wfpctl watch status`

Show current file watch status.

```bash
wfpctl watch status
```

**Output includes:**
- `Active`: Whether watching is enabled
- `Watching`: The watched file path
- `Debounce`: Debounce interval in milliseconds
- `Applies`: Number of successful applies since watching started
- `Errors`: Number of failed applies since watching started
- `Last apply`: Timestamp of last successful apply
- `Last Error`: Details of the most recent error (if any)

## Configuration

### appsettings.json

```json
{
  "WfpTrafficControl": {
    "AutoApplyLkgOnStartup": false,
    "FileWatch": {
      "DebounceMs": 1000
    }
  }
}
```

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| `DebounceMs` | 1000 | 100-30000 | Milliseconds to wait after last change before applying |

## IPC Protocol

### watch-set Request

```json
{
  "type": "watch-set",
  "policyPath": "C:\\path\\to\\policy.json"
}
```

To disable watching, send with `policyPath` as null or empty:

```json
{
  "type": "watch-set",
  "policyPath": null
}
```

### watch-set Response

```json
{
  "ok": true,
  "watching": true,
  "policyPath": "C:\\path\\to\\policy.json",
  "initialApplySuccess": true,
  "warning": null
}
```

### watch-status Request

```json
{
  "type": "watch-status"
}
```

### watch-status Response

```json
{
  "ok": true,
  "watching": true,
  "policyPath": "C:\\path\\to\\policy.json",
  "debounceMs": 1000,
  "lastApplyTime": "2025-01-15T10:30:00.000Z",
  "lastError": null,
  "lastErrorTime": null,
  "applyCount": 5,
  "errorCount": 0
}
```

## Implementation Details

### Files Created

- `src/service/FileWatcher.cs` - PolicyFileWatcher class with debounce logic
- `src/shared/Ipc/WatchMessages.cs` - IPC message types for watch commands

### Files Modified

- `src/service/Worker.cs` - Initializes and manages PolicyFileWatcher
- `src/service/Ipc/PipeServer.cs` - Handles watch-set and watch-status requests
- `src/service/appsettings.json` - Added FileWatch configuration section
- `src/shared/Ipc/IpcMessages.cs` - Added parsing for watch messages
- `src/cli/Program.cs` - Added watch command handlers

### Key Classes

**PolicyFileWatcher** (`src/service/FileWatcher.cs`)
- Manages `FileSystemWatcher` lifecycle
- Implements debounce logic
- Tracks statistics (apply count, error count, timestamps)
- Handles file read retries (for locked files)

## Rollback/Uninstall

- Stop watching: `wfpctl watch set` (no path)
- Service restart clears watch state
- Filters remain in WFP until explicitly removed via `rollback`

## Security Considerations

- Path traversal protection: Paths containing `..` are rejected
- Paths must be absolute
- Only local administrators can execute watch commands (same as all IPC commands)
- File read retries mitigate locked file issues

## Known Limitations

1. **No persistence**: Watch state is not persisted across service restarts. The operator must re-enable watching after a restart.

2. **Single file only**: Only one file can be watched at a time. Enabling watch on a new file disables the previous watch.

3. **No recursive watching**: Only the specified file is watched, not related files or directories.

4. **Rename handling**: If the watched file is renamed, watching may stop. Re-enable with `watch set` if needed.

## Testing

### Manual Testing

1. Start the service
2. Create a test policy file
3. Enable watching: `wfpctl watch set <path>`
4. Verify status: `wfpctl watch status`
5. Modify the policy file
6. Wait for debounce period
7. Verify new policy applied: `wfpctl lkg show` (LKG is updated on each apply)
8. Check status for apply count increment

### Test Script

See `scripts/Test-HotReload.ps1` for automated testing.

### Unit Tests

See `tests/DebounceTests.cs` for debounce logic unit tests.

## Example Usage

```powershell
# Start service
net start WfpTrafficControl

# Bootstrap WFP infrastructure
wfpctl bootstrap

# Create initial policy
$policy = @{
    version = "1.0.0"
    defaultAction = "allow"
    updatedAt = (Get-Date).ToString("o")
    rules = @(
        @{
            id = "block-telemetry"
            action = "block"
            direction = "outbound"
            protocol = "tcp"
            remote = @{ ip = "104.46.162.224/32" }
            priority = 100
            enabled = $true
        }
    )
} | ConvertTo-Json -Depth 10
$policy | Out-File -FilePath "C:\policies\firewall.json" -Encoding utf8

# Enable watching
wfpctl watch set "C:\policies\firewall.json"

# Modify policy (add new rule)
# ... edit file ...

# Policy automatically reapplied after debounce

# Check status
wfpctl watch status

# Disable watching
wfpctl watch set
```
