# Feature 018: Audit Logging

## Overview

This feature adds structured audit logging for control-plane events (policy apply, rollback, teardown, LKG revert). Logs are stored in JSON Lines format for easy parsing and analysis.

**Scope**: Control-plane events only. This is NOT per-connection traffic logging.

## Behavior

### Events Logged

| Event | Trigger | Details Captured |
|-------|---------|------------------|
| `apply-started` | Policy apply begins | Policy file (redacted) |
| `apply-finished` | Policy apply completes | Success: filters created/removed, rules skipped, policy version, total rules. Failure: error code, message |
| `rollback-started` | Filter rollback begins | Source |
| `rollback-finished` | Filter rollback completes | Success: filters removed. Failure: error code, message |
| `teardown-started` | WFP teardown begins | Source |
| `teardown-finished` | WFP teardown completes | Success: provider/sublayer removed. Failure: error code, message |
| `lkg-revert-started` | LKG revert begins | Source |
| `lkg-revert-finished` | LKG revert completes | Success: filters created/removed, rules skipped, policy version, total rules. Failure: error code, message |

### Log File Location

```
%ProgramData%\WfpTrafficControl\audit.log
```

Typically: `C:\ProgramData\WfpTrafficControl\audit.log`

### Log Format (JSON Lines)

Each line is a self-contained JSON object:

```json
{"ts":"2026-01-31T10:15:30.123Z","event":"apply-started","source":"cli","details":{"policyFile":"policy.json"}}
{"ts":"2026-01-31T10:15:30.456Z","event":"apply-finished","source":"cli","status":"success","details":{"filtersCreated":5,"filtersRemoved":2,"rulesSkipped":0,"policyVersion":"1.0.0","totalRules":5}}
```

### Entry Schema

| Field | Type | Description |
|-------|------|-------------|
| `ts` | string | ISO 8601 UTC timestamp |
| `event` | string | Event type (see Events Logged) |
| `source` | string? | Event source: `cli`, `hot-reload`, `startup` |
| `status` | string? | `success` or `failure` (only on *-finished events) |
| `errorCode` | string? | Error code (only on failure) |
| `errorMessage` | string? | Error message (only on failure) |
| `details` | object? | Additional event-specific details |

### Security: Path Redaction

Full file paths are redacted to filename only to avoid leaking directory structure:
- Input: `C:\Users\Admin\policies\my-policy.json`
- Logged: `my-policy.json`

## CLI Commands

### `wfpctl logs`

Show the last 20 audit log entries (default).

```bash
wfpctl logs
```

### `wfpctl logs --tail <N>`

Show the last N audit log entries.

```bash
wfpctl logs --tail 50
```

### `wfpctl logs --since <minutes>`

Show audit log entries from the last N minutes.

```bash
wfpctl logs --since 60    # Last hour
wfpctl logs --since 1440  # Last 24 hours
```

### Example Output

```
Audit Log Entries
  Log file: C:\ProgramData\WfpTrafficControl\audit.log
  Showing:  5 of 127 entries

2026-01-31 10:15:30 [OK] apply-finished
    Source: cli
    created=5, removed=2, rules=5, version=1.0.0

2026-01-31 10:15:30     apply-started
    Source: cli
    Policy: policy.json

2026-01-31 10:10:15 [OK] rollback-finished
    Source: cli
    removed=3

2026-01-31 10:10:15     rollback-started
    Source: cli

2026-01-31 10:05:00 [FAIL] apply-finished
    Source: cli
    Error: [VALIDATION_FAILED] Policy validation failed: Missing required field 'id'
```

## IPC Protocol

### audit-logs Request

```json
{
  "type": "audit-logs",
  "tail": 20
}
```

Or with time-based filtering:

```json
{
  "type": "audit-logs",
  "sinceMinutes": 60
}
```

### audit-logs Response

```json
{
  "ok": true,
  "entries": [
    {
      "timestamp": "2026-01-31T10:15:30.456Z",
      "event": "apply-finished",
      "source": "cli",
      "status": "success",
      "filtersCreated": 5,
      "filtersRemoved": 2,
      "rulesSkipped": 0,
      "totalRules": 5,
      "policyVersion": "1.0.0"
    }
  ],
  "count": 1,
  "totalCount": 127,
  "logPath": "C:\\ProgramData\\WfpTrafficControl\\audit.log"
}
```

## Implementation Details

### Files Created

| File | Description |
|------|-------------|
| `src/shared/Audit/AuditLogEntry.cs` | Log entry model with factory methods |
| `src/shared/Audit/AuditLogWriter.cs` | Thread-safe JSON Lines writer |
| `src/shared/Audit/AuditLogReader.cs` | Log reader with query support |
| `src/shared/Ipc/AuditLogMessages.cs` | IPC request/response types |

### Files Modified

| File | Changes |
|------|---------|
| `src/shared/WfpConstants.cs` | Added `AuditLogFileName` constant and `GetAuditLogPath()` method |
| `src/shared/Ipc/IpcMessages.cs` | Added `AuditLogsRequest` to parser |
| `src/service/Ipc/PipeServer.cs` | Added audit logging to control-plane handlers; added `ProcessAuditLogsRequest` |
| `src/cli/Program.cs` | Added `logs` command with `--tail` and `--since` options |

### Key Classes

**AuditLogEntry** (`src/shared/Audit/AuditLogEntry.cs`)
- Immutable log entry model
- Factory methods for each event type
- JSON serialization to single line

**AuditLogWriter** (`src/shared/Audit/AuditLogWriter.cs`)
- Thread-safe file append
- Automatic directory creation
- ACL protection for append-only access (when running as LocalSystem)
- Graceful error handling (never crashes service)

**AuditLogReader** (`src/shared/Audit/AuditLogReader.cs`)
- Reads logs with shared access (works while service writes)
- `ReadTail(N)`: Last N entries
- `ReadSince(minutes)`: Time-based filtering
- Returns entries newest-first

## Rollback/Uninstall

Audit logs are informational only and do not affect WFP state. No rollback is needed.

To clear audit history:
```bash
del C:\ProgramData\WfpTrafficControl\audit.log
```

## Security Considerations

1. **Path Redaction**: Full file paths are redacted to filename only
2. **No Secrets**: Control-plane logging contains no sensitive data (no IPs, ports, or credentials)
3. **Admin-Only Access**: IPC commands require local administrator privileges
4. **Fail-Safe**: Audit log write failures do not crash the service or fail the operation
5. **ACL Protection**: Append-only ACLs prevent log tampering (see below)

### ACL Protection (Append-Only)

When running as LocalSystem (the service account), the audit log file is protected with restrictive ACLs:

| Principal | Rights | Purpose |
|-----------|--------|---------|
| LocalSystem | Full Control | Service needs complete access for writing and log rotation |
| Administrators | Read + AppendData | Can view logs and add entries, but cannot modify or delete existing entries |
| Users | No access | Unprivileged users cannot access audit logs |

**Behavior:**
- ACLs are applied on the first write to the audit log file
- Inheritance from the parent directory is disabled
- If ACL application fails, logging continues without protection (defense in depth, non-fatal)
- ACLs are only applied when running as LocalSystem (skipped during development/testing)

**Verification:**
```powershell
icacls "%ProgramData%\WfpTrafficControl\audit.log"
```

Expected output (when service is running as LocalSystem):
```
C:\ProgramData\WfpTrafficControl\audit.log
    NT AUTHORITY\SYSTEM:(F)
    BUILTIN\Administrators:(R,AD,S)
Successfully processed 1 files; Failed processing 0 files
```

Legend: `F`=Full, `R`=Read, `AD`=AppendData, `S`=Synchronize

**Limitations:**
- LocalSystem still has Full Control and can delete/modify logs (necessary for rotation)
- Administrators can take ownership and reset ACLs (Windows design limitation)
- Protection only applies when running as LocalSystem

## Known Limitations

1. **No Log Rotation**: Log file grows unbounded. Manual rotation or deletion required.
2. **No Streaming**: `--tail` is one-shot, not continuous like Unix `tail -f`.
3. **In-Memory Reads**: For very large log files, reads load all lines into memory.

## Future Enhancements (Not In Scope)

- Log rotation with max file size
- Streaming/live tail mode
- ETW provider for Windows Event Viewer integration
- Per-connection traffic logging (requires callout driver)

## Testing

### Manual Testing

1. Start the service
2. Bootstrap WFP: `wfpctl bootstrap`
3. Apply a policy: `wfpctl apply test-policy.json`
4. Check logs: `wfpctl logs --tail 5`
5. Verify apply-started and apply-finished entries appear
6. Trigger a failure (invalid policy) and verify error is logged
7. Rollback: `wfpctl rollback`
8. Verify rollback events in logs

### Unit Tests

See `tests/WfpTrafficControl.Tests/Audit/AuditLogTests.cs` for:
- Log entry serialization/deserialization
- Path redaction (no full paths in output)
- Writer thread safety
- Reader query logic

## Example Usage

```powershell
# Start service
net start WfpTrafficControl

# Bootstrap and apply policy
wfpctl bootstrap
wfpctl apply C:\policies\firewall.json

# View recent audit events
wfpctl logs

# View more history
wfpctl logs --tail 100

# View last hour
wfpctl logs --since 60

# Trigger rollback and view events
wfpctl rollback
wfpctl logs --tail 4

# Direct log file access (for external tools)
Get-Content C:\ProgramData\WfpTrafficControl\audit.log | ConvertFrom-Json
```
