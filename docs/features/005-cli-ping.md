# 005 — CLI Status/Ping Command

## Overview

This feature implements the `wfpctl status` command (also available as `wfpctl ping`) that checks if the WfpTrafficControl service is running and displays basic service information.

## Behavior

### Success Case

When the service is running and accessible:

```
> wfpctl status
WfpTrafficControl Service is running
  Version: 1.0.0
  Time:    2025-01-27T12:34:56.789Z
```

Exit code: `0`

### Failure Cases

**Service not running:**
```
> wfpctl status
Error: Cannot connect to WfpTrafficControl service. Is the service running?
```
Exit code: `1`

**Access denied (not running as Administrator):**
```
> wfpctl status
Error: Access denied. Run the CLI as Administrator.
```
Exit code: `1`

**Service error:**
```
> wfpctl status
Error: <error message from service>
```
Exit code: `1`

## Usage Examples

```powershell
# Check if service is running
wfpctl status

# Alternative command (alias)
wfpctl ping

# Show help
wfpctl --help
wfpctl -h

# Show version
wfpctl --version
wfpctl -v
```

## Implementation Details

### Components

1. **PipeClient** (`src/cli/PipeClient.cs`)
   - Connects to the service via named pipe (`\\.\pipe\WfpTrafficControl`)
   - Implements length-prefix framing protocol (4-byte LE length + JSON body)
   - Handles timeouts: 5s for connect, 30s for read
   - Returns `Result<T>` for all operations

2. **CLI Program** (`src/cli/Program.cs`)
   - Parses command-line arguments
   - Routes to appropriate command handler
   - Displays formatted output to user

3. **CliRequestSerializer** (`src/cli/PipeClient.cs`)
   - Helper for serializing requests to JSON and wire format
   - Used by unit tests to verify serialization

### Wire Protocol

The CLI uses the same IPC protocol as documented in [004-ipc-pipe-server.md](./004-ipc-pipe-server.md):

**Request (PingRequest):**
```json
{"type":"ping"}
```

**Response (PingResponse):**
```json
{"ok":true,"serviceVersion":"1.0.0","time":"2025-01-27T12:34:56.789Z"}
```

### Error Handling

| Scenario | Error Code | User Message |
|----------|------------|--------------|
| Service not running | `SERVICE_UNAVAILABLE` | Cannot connect to WfpTrafficControl service. Is the service running? |
| Access denied | `ACCESS_DENIED` | Access denied. Run the CLI as Administrator. |
| Connection timeout | `SERVICE_UNAVAILABLE` | Cannot connect to WfpTrafficControl service. Is the service running? |
| Read timeout | `SERVICE_UNAVAILABLE` | Request timed out waiting for service response. |
| Service error | varies | Error message from service |

## Testing

### Unit Tests

Run unit tests:
```powershell
dotnet test --filter "CliRequestSerializerTests"
```

Tests cover:
- Ping request serialization to JSON
- Ping request serialization to wire format (length-prefix)
- Wire format structure validation

### Manual Integration Test

1. **Start the service** (requires Administrator):
   ```powershell
   # From project root
   dotnet run --project src/service
   ```

2. **Run CLI command** (in separate Administrator terminal):
   ```powershell
   dotnet run --project src/cli -- status
   ```

3. **Expected output:**
   ```
   WfpTrafficControl Service is running
     Version: 1.0.0
     Time:    <current UTC time>
   ```

4. **Test failure case** (stop the service first):
   ```powershell
   dotnet run --project src/cli -- status
   ```
   Expected: Error message about service not running.

## Configuration

No configuration required. The CLI uses the pipe name defined in `WfpConstants.PipeName`.

## Rollback/Uninstall

This feature is purely additive to the CLI. No rollback needed. Uninstalling the CLI removes this functionality.

## Known Limitations

1. **No retry logic**: If the service is busy, the CLI will fail immediately rather than retry.
2. **Single connection**: Each command creates a new pipe connection (no connection pooling).
3. **Administrator required**: The CLI must run as Administrator to communicate with the service (enforced by pipe server).

## Dependencies

- `WfpTrafficControl.Shared` - IPC message types, constants, Result type
- .NET 8.0 - NamedPipeClientStream, System.Text.Json

## Files Changed

| File | Change |
|------|--------|
| `src/cli/PipeClient.cs` | New - Pipe client implementation |
| `src/cli/Program.cs` | Modified - Command parsing and status command |
| `src/shared/Result.cs` | Modified - Added new error codes |
| `tests/CliSerializationTests.cs` | New - Unit tests for CLI serialization |
| `docs/features/005-cli-ping.md` | New - This documentation |
