# 024 - IPC Rate Limiting

## Overview

This feature adds rate limiting to the IPC pipe server to prevent Denial of Service (DoS) attacks from clients spamming the service with rapid requests.

## Behavior

The rate limiter uses a **token bucket algorithm** to control request rates:

- Each client (identified by Windows username) gets a bucket of tokens
- Default limit: **10 requests per 10-second window**
- When a request arrives, one token is consumed
- If no tokens remain, the request is rejected with a rate limit error
- Tokens reset when the window expires

### Request Flow

1. Client connects to named pipe
2. Request is read from pipe
3. Client is authorized (admin check)
4. Protocol version is validated
5. **Rate limit is checked** (NEW)
6. Request is processed
7. Response is sent

### Rate Limit Response

When rate limited, the client receives:
```json
{
  "ok": false,
  "error": "Rate limit exceeded. Please wait before retrying.",
  "protocolVersion": 1
}
```

A warning is logged:
```
Rate limit exceeded for client: DOMAIN\username
```

## Configuration

Currently hardcoded (future prompt will add config file support):

| Setting | Default | Description |
|---------|---------|-------------|
| MaxTokens | 10 | Maximum requests per window |
| WindowSeconds | 10 | Window duration in seconds |

## Implementation Details

### RateLimiter Class

Location: `src/service/Ipc/RateLimiter.cs`

- Thread-safe implementation using `lock`
- Uses `Stopwatch.GetTimestamp()` for monotonic time (immune to system clock changes)
- Automatic cleanup of expired client entries (every 100 calls)
- Per-client tracking by Windows username

### Integration

Location: `src/service/Ipc/PipeServer.cs`

The rate limiter is:
- Created once per PipeServer instance
- Shared across all connections
- Tracks clients by their Windows impersonation username
- Checked after authorization, before request processing

## Testing

### Manual Testing

1. Start the service
2. Run rapid CLI commands:
   ```powershell
   # PowerShell - send 20 rapid requests
   1..20 | ForEach-Object { wfpctl ping }
   ```
3. Observe that first 10 succeed, remaining get rate limit error

### Unit Tests

Location: `tests/RateLimiterTests.cs`

Tests cover:
- Basic token consumption
- Token exhaustion (rate limiting)
- Window reset after expiration
- Configurable limits
- Thread safety
- Client isolation

## Rollback

To disable rate limiting:
1. Remove the rate limit check from `HandleConnectionAsync` in PipeServer.cs
2. Remove the `_rateLimiter` field and constructor parameter
3. Rebuild the service

No persistent state or WFP changes are involved.

## Known Limitations

1. **Per-username tracking**: Rate limits are tracked per Windows username, not per process. Multiple CLI instances from the same user share the limit.

2. **Memory usage**: Client state is kept in memory. Cleanup runs every 100 calls, removing entries older than 2x the window duration.

3. **No burst allowance**: The current implementation does not support "burst" tokens beyond the window limit.

4. **Hardcoded limits**: Configuration via file/registry will be added in a future prompt.

## Security Considerations

- Rate limiter runs AFTER authorization, so only authenticated admins can trigger it
- Uses Windows impersonation username for client identity (secure, not client-provided)
- Prevents resource exhaustion attacks from malicious admin sessions
- Does not affect legitimate use cases (10 requests per 10 seconds is generous for CLI usage)
