# Real-Time Connection Monitor (P10)

## Overview

The Real-Time Connection Monitor provides a live view of all active TCP and UDP network connections on the system. It displays connection details including local and remote endpoints, connection state, and the process owning each connection.

## Features

### Connection Display

- **Protocol**: TCP or UDP
- **State**: Connection state (ESTABLISHED, LISTEN, TIME_WAIT, etc. for TCP; * for UDP)
- **Local Endpoint**: Local IP address and port
- **Remote Endpoint**: Remote IP address and port
- **Process**: Process name (e.g., chrome.exe)
- **PID**: Process ID
- **Path**: Full path to the executable

### Auto-Refresh

- **Configurable Interval**: 1, 2, 3, 5, or 10 seconds
- **Toggle Control**: Enable/disable auto-refresh
- **Manual Refresh**: Refresh button for on-demand updates

### Filtering

- **Search**: Filter by any field (IP, port, process name, path, etc.)
- **Protocol Filter**: Show all, TCP only, or UDP only
- **State Filter**: Filter by connection state (all, ESTABLISHED, LISTEN, TIME_WAIT, CLOSE_WAIT)
- **Clear Filters**: Reset all filters with one click

### UI Features

- **Virtualized DataGrid**: Efficient rendering for thousands of connections
- **Color-Coded States**: Visual distinction between connection states
- **Copy Details**: Copy selected connection details to clipboard
- **Connection Count**: Shows filtered vs total connection count

## Implementation Details

### Architecture

The Connection Monitor uses the IP Helper API (iphlpapi.dll) to retrieve connection information:

1. **GetExtendedTcpTable**: Retrieves TCP connection table with process ownership
2. **GetExtendedUdpTable**: Retrieves UDP listener table with process ownership

### IPC Messages

New IPC message type:

**get-connections**: Get active network connections
- Request: `{ "type": "get-connections", "includeTcp": true, "includeUdp": true }`
- Response: `{ "ok": true, "connections": [...], "count": 100, "timestamp": "..." }`

### Process Information

- Process names and paths are retrieved using `Process.GetProcessById()`
- A 30-second cache prevents excessive process queries
- System processes (PID 0 and 4) are handled specially

## Files

### New Files

- `src/shared/Native/IpHelperApi.cs` - P/Invoke wrappers for IP Helper API
- `src/shared/Ipc/ConnectionMessages.cs` - IPC message types for connections
- `src/ui/WfpTrafficControl.UI/ViewModels/ConnectionMonitorViewModel.cs` - ViewModel
- `src/ui/WfpTrafficControl.UI/Views/ConnectionMonitorView.xaml(.cs)` - View
- `tests/UI/ConnectionMonitorTests.cs` - Unit tests
- `docs/features/038-connection-monitor.md` - This documentation

### Modified Files

- `src/shared/Ipc/IpcMessages.cs` - Added GetConnectionsRequest parsing
- `src/service/Ipc/PipeServer.cs` - Added GetConnections handler
- `src/ui/WfpTrafficControl.UI/Services/IServiceClient.cs` - Added method signature
- `src/ui/WfpTrafficControl.UI/Services/ServiceClient.cs` - Implemented method
- `tests/UI/MockServiceClient.cs` - Added mock implementation
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - Registered ViewModel
- `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` - Added ViewModel property
- `src/ui/WfpTrafficControl.UI/MainWindow.xaml` - Added Connections tab

## Usage

### Accessing the Connection Monitor

1. Open the WFP Traffic Control UI
2. Click the "Connections" tab in the navigation bar
3. Click "Refresh" to load current connections

### Filtering Connections

1. **Protocol**: Select TCP, UDP, or All from the protocol dropdown
2. **State**: Select a specific state or All from the state dropdown
3. **Search**: Type in the search box to filter by any field
4. **Clear**: Click "Clear Filters" to reset all filters

### Auto-Refresh

1. Check the "Auto-refresh" checkbox
2. Select the desired interval (1-10 seconds)
3. Connections will update automatically

### Copying Connection Details

1. Select a connection in the grid
2. Click "Copy" or right-click and copy
3. Connection details are copied to clipboard in a readable format

## Performance Considerations

- Uses virtualization for efficient rendering of large connection lists
- Process information is cached to reduce system calls
- Auto-refresh can be disabled to reduce CPU usage
- Filter operations are performed on the client side

## Known Limitations

- Only IPv4 connections are shown (IPv6 support planned for future)
- UDP "connections" are actually listeners (UDP is connectionless)
- Process paths may not be available for some system processes
- Connections created and closed between refreshes may be missed

## Testing

Run the connection monitor tests:
```bash
dotnet test tests/Tests.csproj --filter "FullyQualifiedName~ConnectionMonitorTests"
```

## Security Notes

- Connection enumeration requires administrator privileges
- Process path retrieval may fail for protected processes
- No network data is captured, only connection metadata
