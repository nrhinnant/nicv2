# Feature 019: IPC Security Hardening

## Summary

This feature hardens the IPC (Inter-Process Communication) channel between the CLI and the Windows Service by adding:

1. **OS-level access control** via named pipe security ACLs
2. **Request size limits** to prevent resource exhaustion
3. **Read timeouts** to prevent hanging connections
4. **Protocol versioning** to safely handle version mismatches

## Behavior

### PipeSecurity ACLs

The named pipe is now created with explicit security ACLs that restrict access at the OS level:

| Principal | Permission |
|-----------|------------|
| BUILTIN\Administrators | Full Control |
| NT AUTHORITY\SYSTEM | Full Control |

This provides defense-in-depth beyond the application-level authorization check that was already in place.

**Effect**: Non-administrator users will receive "Access Denied" from the OS before even connecting to the pipe.

### Request Size Limits

All IPC messages are validated against a maximum size limit:

| Parameter | Value | Constant |
|-----------|-------|----------|
| Max Message Size | 64 KB | `WfpConstants.IpcMaxMessageSize` |

Requests exceeding this limit are rejected with a clear error message.

### Timeouts

| Operation | Timeout | Constant |
|-----------|---------|----------|
| Client Connect | 5 seconds | `WfpConstants.IpcConnectTimeoutMs` |
| Read/Write | 30 seconds | `WfpConstants.IpcReadTimeoutMs` |

Timeouts prevent hung connections from consuming server resources indefinitely.

### Protocol Versioning

All IPC requests now include an optional `protocolVersion` field:

```json
{
  "type": "ping",
  "protocolVersion": 1
}
```

All IPC responses include the server's protocol version:

```json
{
  "ok": true,
  "protocolVersion": 1,
  "serviceVersion": "1.0.0"
}
```

**Version validation rules:**

| Client Version | Server Behavior |
|----------------|-----------------|
| 0 (not sent) | Accept (backward compatibility), log debug message |
| Within supported range | Accept |
| Below minimum | Reject with `ProtocolVersionMismatch` error |
| Above maximum | Reject with `ProtocolVersionMismatch` error |

**Current version constants:**

| Constant | Value |
|----------|-------|
| `IpcProtocolVersion` | 1 |
| `IpcMinProtocolVersion` | 1 |

## Configuration

No additional configuration is required. All security features are enabled by default.

## Wire Protocol

The IPC wire protocol uses length-prefixed messages:

```
[4 bytes: message length (little-endian int32)]
[N bytes: JSON message body (UTF-8)]
```

### Request Format

```json
{
  "type": "request-type",
  "protocolVersion": 1,
  ... other fields ...
}
```

### Response Format

```json
{
  "ok": true|false,
  "protocolVersion": 1,
  "error": "error message if ok=false",
  ... other fields ...
}
```

## Error Responses

New error responses added for security validation:

| Error | Condition |
|-------|-----------|
| `Access denied. Administrator privileges required.` | Non-admin client |
| `Protocol version mismatch. Client version: X, supported range: Y-Z. Please update the CLI.` | Incompatible protocol version |
| `Request too large: X bytes exceeds maximum of Y bytes.` | Message exceeds size limit |
| `Request timed out.` | Read timeout exceeded |

## Files Changed

| File | Change |
|------|--------|
| `src/shared/WfpConstants.cs` | Added IPC constants |
| `src/shared/Ipc/IpcMessages.cs` | Added protocol version to base classes, error factories |
| `src/service/Ipc/PipeServer.cs` | Added PipeSecurity ACL, version validation |
| `src/cli/PipeClient.cs` | Sends protocol version with requests |

## Testing

### Manual Testing

1. **Test as Administrator:**
   ```powershell
   # Run as admin - should work
   wfpctl ping
   ```

2. **Test as non-Administrator:**
   ```powershell
   # Run as standard user - should fail with access denied
   wfpctl ping
   ```

3. **Test protocol version:**
   - Older CLI without version: Should work (backward compatibility)
   - Current CLI with version: Should work
   - Manipulated request with version 999: Should fail with version mismatch

### Unit Tests

Run the IPC security tests:

```powershell
dotnet test --filter "FullyQualifiedName~IpcSecurity"
```

## Rollback

These changes are additive security hardening. To rollback:

1. Remove PipeSecurity from pipe creation
2. Remove protocol version validation
3. Rebuild and redeploy

No WFP artifacts are affected by this feature.

## Known Limitations

1. **Backward compatibility mode**: Clients that don't send a protocol version are still accepted. This will be deprecated in a future version.

2. **Single connection at a time**: The pipe server still handles one connection at a time. This is intentional for simplicity but could be a bottleneck under high load.

## Security Considerations

1. **ACL vs Application AuthZ**: The pipe ACL provides OS-level protection, but the application still performs its own authorization check via impersonation. Both layers are maintained for defense-in-depth.

2. **Version rejection**: Rejected versions still receive a response with the server's version information. This is intentional to help users diagnose version mismatches.

3. **Size limits**: The 64 KB limit is generous for policy files but prevents obvious abuse. Larger policies should be rejected at the policy validation layer.

4. **TOCTOU (Time-of-Check-Time-of-Use) Protection**: The `ProcessApplyRequest()` method reads policy files using a single atomic `File.ReadAllBytes()` call. This eliminates the race window that would exist if file existence, size, and content were checked separately. The file size is validated from the byte array length after the read, ensuring the same data that was read is what gets validated and applied.
