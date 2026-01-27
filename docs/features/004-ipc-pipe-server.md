# Feature 004: IPC Pipe Server

## Overview

The IPC Pipe Server enables communication between the CLI (`wfpctl`) and the WfpTrafficControl service using Windows Named Pipes. This provides a secure, low-latency IPC mechanism for sending commands to the service and receiving responses.

## Protocol Specification

### Transport Layer

- **Pipe Name**: `\\.\pipe\WfpTrafficControl` (defined in `WfpConstants.PipeName`)
- **Pipe Direction**: Bidirectional (InOut)
- **Transmission Mode**: Byte stream
- **Connection Model**: Sequential (one client at a time)

### Message Framing

Messages use a length-prefixed framing protocol:

```
[4 bytes: message length (little-endian int32)][N bytes: UTF-8 JSON payload]
```

- **Maximum message size**: 64 KB
- **Read timeout**: 30 seconds

### Request Format

All requests are JSON objects with a required `type` field:

```json
{
  "type": "<request_type>"
}
```

### Response Format

All responses are JSON objects with these common fields:

```json
{
  "ok": true|false,
  "error": "error message if ok=false"
}
```

## Supported Requests

### Ping Request

Checks if the service is running and returns basic information.

**Request:**
```json
{
  "type": "ping"
}
```

**Response (success):**
```json
{
  "ok": true,
  "serviceVersion": "1.0.0",
  "time": "2024-01-15T10:30:00.0000000Z"
}
```

**Response (error):**
```json
{
  "ok": false,
  "error": "Access denied. Administrator privileges required."
}
```

## Security

### Authorization Model

The pipe server enforces strict authorization on all connections:

1. **Who can connect**: Only local administrators or the LocalSystem account
2. **How it's checked**: Uses Windows impersonation to get the client's identity, then checks group membership

### Authorization Flow

```
Client connects to pipe
    |
    v
Server gets client identity via impersonation
    |
    v
Check: Is client LocalSystem (SID S-1-5-18)?
    |--- Yes --> Authorized
    |
    v
Check: Is client in BUILTIN\Administrators group?
    |--- Yes --> Authorized
    |--- No ---> Reject with "Access denied"
```

### Implementation Details

The authorization uses `NamedPipeServerStream.RunAsClient()` to temporarily impersonate the connected client, then:

1. Gets `WindowsIdentity.GetCurrent()` (which returns the impersonated identity)
2. Creates a `WindowsPrincipal` from that identity
3. Checks `principal.IsInRole(WellKnownSidType.BuiltinAdministratorsSid)`

This approach works correctly even with UAC (User Account Control) because:
- If the client runs elevated (e.g., "Run as Administrator"), their token includes the Administrators group
- If the client runs non-elevated, even if the user is an administrator, the filtered token won't include the Administrators group

### Security Considerations

| Threat | Mitigation |
|--------|------------|
| Unauthorized access | Authorization check before processing any request |
| Message tampering | Named pipes are local-only; kernel enforces security |
| Denial of service (large messages) | Maximum message size enforced (64 KB) |
| Denial of service (slow clients) | Read timeout (30 seconds) |
| Information disclosure | Error messages don't leak internal paths or data |

## Error Handling

### Client Errors

| Error | Response |
|-------|----------|
| Not an administrator | `{"ok":false,"error":"Access denied. Administrator privileges required."}` |
| Invalid JSON | `{"ok":false,"error":"Invalid JSON: <details>"}` |
| Missing `type` field | `{"ok":false,"error":"Missing 'type' field in request."}` |
| Unknown request type | `{"ok":false,"error":"Unknown request type: <type>"}` |
| Read timeout | `{"ok":false,"error":"Request timeout"}` |

### Server Errors

Server-side errors are logged and return a generic error response:
```json
{
  "ok": false,
  "error": "Internal error: <message>"
}
```

## Usage

### Running the Service (for testing)

From the repository root:

```powershell
# Build and run in console mode (for debugging)
cd src\service
dotnet run
```

### Connecting to the Pipe (test client example)

```csharp
using System.IO.Pipes;
using System.Text;

// Connect to the pipe
using var client = new NamedPipeClientStream(".", "WfpTrafficControl", PipeDirection.InOut);
await client.ConnectAsync();

// Send a ping request
var request = """{"type":"ping"}""";
var requestBytes = Encoding.UTF8.GetBytes(request);
var lengthBytes = BitConverter.GetBytes(requestBytes.Length);

await client.WriteAsync(lengthBytes);
await client.WriteAsync(requestBytes);
await client.FlushAsync();

// Read the response
var responseLengthBytes = new byte[4];
await client.ReadAsync(responseLengthBytes);
var responseLength = BitConverter.ToInt32(responseLengthBytes);

var responseBytes = new byte[responseLength];
await client.ReadAsync(responseBytes);
var response = Encoding.UTF8.GetString(responseBytes);

Console.WriteLine(response);
```

## Files

| File | Purpose |
|------|---------|
| `src/shared/Ipc/IpcMessages.cs` | Request/response models and JSON parsing |
| `src/service/Ipc/PipeServer.cs` | Named pipe server implementation |
| `src/service/Worker.cs` | Service lifecycle (starts/stops pipe server) |
| `src/shared/WfpConstants.cs` | Pipe name constant |

## Rollback/Uninstall

This feature has no persistent state. When the service stops:
1. The pipe server stops accepting new connections
2. Any active connection is completed or timed out
3. The pipe is closed

No cleanup is required on uninstall.

## Known Limitations

1. **Single connection at a time**: The server processes one client at a time (sequential). Concurrent clients will queue and be processed in order.

2. **No streaming**: Each request/response is a complete message. Streaming or long-polling is not supported.

3. **Local only**: Named pipes with this configuration only accept local connections. Remote connections are not supported (and not needed for this project).

## Future Enhancements (not in scope for this feature)

- Add more request types (status, apply, rollback, etc.)
- Add request correlation IDs for logging
- Add metrics/telemetry for IPC operations
