# Feature 040: Syslog/SIEM Export

## Overview

This feature adds syslog export capabilities to WFP Traffic Control, allowing audit events and connection logs to be forwarded to external logging servers and SIEM systems. The implementation supports multiple transport protocols (UDP, TCP, TLS) and message formats (RFC 5424 Syslog, CEF, JSON).

## Components

### 1. Syslog Configuration Model (`src/shared/Ipc/SyslogMessages.cs`)

- **SyslogProtocol enum**: UDP, TCP, TLS transport options
- **SyslogFormat enum**: RFC 5424 (standard syslog), CEF (Common Event Format for SIEM), JSON
- **SyslogConfig class**: Configuration with:
  - `Enabled`: Toggle syslog export on/off
  - `Host`: Server hostname or IP
  - `Port`: Server port (default: 514)
  - `Protocol`: Transport protocol
  - `Format`: Message format
  - `Facility`: Syslog facility code (default: 16/local0)
  - `AppName`: Application identifier in logs
  - `VerifyCertificate`: TLS certificate validation toggle

### 2. Syslog Exporter (`src/shared/Logging/SyslogExporter.cs`)

Thread-safe syslog client implementation:

- **Transport Support**:
  - UDP: Connectionless, fire-and-forget
  - TCP: Persistent connection with newline message delimiter
  - TLS: Encrypted TCP with optional certificate verification

- **Message Formats**:
  - **RFC 5424**: Standard syslog with structured data
  - **CEF**: ArcSight-compatible Common Event Format
  - **JSON**: Machine-readable JSON with syslog priority header

- **Event Fields**: Timestamp, severity, event type, message, source, status, policy version, filter counts, process path, remote IP/port, direction, protocol, rule ID, and extensible additional data

### 3. IPC Messages

- **GetSyslogConfigRequest/Response**: Retrieve current configuration
- **SetSyslogConfigRequest/Response**: Update configuration
- **TestSyslogRequest/Response**: Send test message and measure RTT

### 4. UI Components

#### SyslogSettingsViewModel (`src/ui/.../ViewModels/SyslogSettingsViewModel.cs`)

- Load/save configuration from service
- Test connection functionality (saves pending changes first)
- Reset to defaults
- Change tracking for unsaved modifications
- Available protocols, formats, and syslog facilities

#### SyslogSettingsView (`src/ui/.../Views/SyslogSettingsView.xaml`)

Modal dialog with:
- Enable/disable toggle
- Server configuration (host, port, protocol)
- Message format selection
- Facility code dropdown with standard codes
- App name customization
- TLS certificate verification option (shown only for TLS protocol)
- Test connection button with result display
- Save/Cancel buttons

### 5. Service Client Integration

IServiceClient interface extended with:
- `GetSyslogConfigAsync()`
- `SetSyslogConfigAsync(config)`
- `TestSyslogAsync()`

### 6. Main Window Integration

- "Syslog" button in header bar opens settings dialog
- Accessible from any tab

## Message Format Examples

### RFC 5424 (Syslog)
```
<134>1 2024-01-15T10:30:00.000Z HOSTNAME WfpTrafficControl - apply-finished [wfp@0 eventType="apply-finished" source="cli" status="success" policyVersion="1.0.0" filtersCreated="5"] Policy applied successfully
```

### CEF (SIEM)
```
<134>CEF:0|WfpTrafficControl|WFP Traffic Control|1.0|apply-finished|Policy applied successfully|3|rt=2024-01-15T10:30:00.000Z dhost=HOSTNAME src=cli outcome=success cs1=1.0.0 cs1Label=PolicyVersion cn1=5 cn1Label=FiltersCreated
```

### JSON
```
<134>{"timestamp":"2024-01-15T10:30:00.000Z","severity":"informational","eventType":"apply-finished","message":"Policy applied successfully","hostname":"HOSTNAME","appName":"WfpTrafficControl","source":"cli","status":"success","policyVersion":"1.0.0","filtersCreated":5}
```

## Configuration

### Default Values

| Setting | Default Value |
|---------|---------------|
| Enabled | false |
| Host | localhost |
| Port | 514 |
| Protocol | UDP |
| Format | RFC 5424 |
| Facility | 16 (local0) |
| AppName | WfpTrafficControl |
| VerifyCertificate | true |

### Standard Syslog Facility Codes

- 0: kernel
- 1: user
- 2: mail
- 3: daemon
- 4: auth
- 5: syslog
- 16-23: local0-local7

## Testing

### Unit Tests (50 tests in `tests/UI/SyslogSettingsTests.cs`)

- ViewModel initial state and defaults
- Available protocols, formats, and facilities
- Property change tracking
- Load/save configuration operations
- Test connection functionality
- Reset to defaults
- IPC message types and responses
- SyslogExporter behavior

### Manual Testing

1. Open the UI application
2. Click "Syslog" button in header
3. Enable syslog export
4. Configure server settings (e.g., localhost:514, UDP)
5. Click "Test Connection" to verify connectivity
6. Click "Save" to apply settings

## Error Handling

- Connection failures return descriptive error messages
- TCP/TLS connections are automatically reconnected on failure
- Test connection saves pending changes before testing
- Invalid configurations are rejected by the service

## Known Limitations

1. No batching/buffering of messages (each event is sent immediately)
2. No automatic retry with backoff for failed sends
3. TLS certificate verification bypasses all checks when disabled (security warning)
4. No rate limiting on the client side

## Future Enhancements

- Message queuing with async send
- Configurable retry policy
- Filter events by type/severity before export
- Custom message templates
- Multiple syslog destinations
