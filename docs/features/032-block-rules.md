# 032 - Block Rules View (Simplified)

## Overview

The Block Rules view provides a dedicated tab in the WfpTrafficControl UI to display block rules defined in the currently loaded policy. This is a **simplified implementation** that shows policy-defined block rules rather than actual blocked connection events.

## Important Limitations

This implementation shows **policy block rules**, not actual blocked connection events. A full blocked connection log would require:

1. Windows ETW (Event Tracing for Windows) integration
2. Real-time event subscription from WFP
3. Significantly more complex infrastructure

The current implementation serves as a useful tool to review what traffic your policy will block when matching rules are triggered.

## Features

### Block Rules Tab

A new "Block Rules" tab has been added to the main window, displaying:

- **Simplification Notice Banner**: A prominent yellow warning banner explaining that this view shows policy rules, not actual blocked events
- **Block Rules Table**: Lists all enabled block rules from the current policy with columns:
  - Rule ID (highlighted in red/error color)
  - Summary (human-readable description of what the rule blocks)
  - Direction (color-coded: blue=outbound, yellow=inbound, red=both)
  - Protocol (tcp, udp, any)
  - Process (if specified)
  - Remote (IP:port combination)
  - Priority
  - Enabled status
  - Comment

### Status Information

- Policy version displayed in status bar
- Count of block rules shown
- Clear messaging when no policy is loaded or when policy has no block rules

### User Actions

- **Refresh**: Reload block rules from the service
- **Copy Rule**: Copy selected rule details to clipboard

## IPC Protocol

### Request

```json
{
  "type": "block-rules"
}
```

### Response

```json
{
  "ok": true,
  "rules": [
    {
      "id": "block-telnet",
      "direction": "outbound",
      "protocol": "tcp",
      "process": null,
      "remoteIp": null,
      "remotePorts": "23",
      "localIp": null,
      "localPorts": null,
      "comment": "Block outbound Telnet",
      "priority": 100,
      "enabled": true,
      "summary": "Outbound TCP to port 23"
    }
  ],
  "count": 1,
  "policyVersion": "1.0",
  "policyLoaded": true
}
```

When no policy is loaded:

```json
{
  "ok": true,
  "rules": [],
  "count": 0,
  "policyLoaded": false
}
```

## Implementation Details

### Service Side

The service reads the last-known-good (LKG) policy and filters to only return rules where:
- `action` equals "block" (case-insensitive)
- `enabled` is true

The service generates a human-readable summary for each rule describing what traffic it blocks.

### UI Side

- `BlockRulesViewModel`: Manages state, loads rules, handles refresh and copy commands
- `BlockRulesView.xaml`: XAML layout with DataGrid, empty states, and loading indicator
- Registered in DI container and wired to MainWindow via MainViewModel

## Files Changed

### New Files

- `src/shared/Ipc/BlockRulesMessages.cs` - IPC request/response DTOs
- `src/ui/WfpTrafficControl.UI/ViewModels/BlockRulesViewModel.cs` - ViewModel
- `src/ui/WfpTrafficControl.UI/Views/BlockRulesView.xaml` - View
- `src/ui/WfpTrafficControl.UI/Views/BlockRulesView.xaml.cs` - View code-behind
- `tests/UI/BlockRulesViewModelTests.cs` - Unit tests

### Modified Files

- `src/shared/Ipc/IpcMessages.cs` - Added BlockRulesRequest.RequestType to parser
- `src/service/Ipc/PipeServer.cs` - Added ProcessBlockRulesRequest handler
- `src/ui/WfpTrafficControl.UI/Services/IServiceClient.cs` - Added GetBlockRulesAsync method
- `src/ui/WfpTrafficControl.UI/Services/ServiceClient.cs` - Implemented GetBlockRulesAsync
- `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` - Added BlockRulesViewModel property
- `src/ui/WfpTrafficControl.UI/MainWindow.xaml` - Added Block Rules tab
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - Registered BlockRulesViewModel
- `tests/UI/MockServiceClient.cs` - Added GetBlockRulesAsync mock

## Testing

### Manual Testing

1. Launch the UI application
2. Navigate to the "Block Rules" tab
3. Verify the yellow notice banner is visible
4. If no policy is loaded, verify appropriate empty state message
5. Apply a policy with block rules
6. Click Refresh and verify rules appear in the table
7. Select a rule and click "Copy Rule" to verify clipboard functionality

### Automated Tests

Unit tests verify:
- Initial state (loading, no policy)
- Successful load with rules
- Empty policy (no block rules)
- Service unavailable handling
- Copy command functionality
- SimplificationNotice property returns expected text

## Future Enhancements

To implement a true blocked connection log, the following would be required:

1. **ETW Integration**: Subscribe to WFP audit events using Windows Event Tracing
2. **Event Processing Service**: A background service to process and aggregate blocked connection events
3. **Storage**: A mechanism to store recent blocked connections (in-memory ring buffer or file-based)
4. **IPC Extension**: New request/response types for querying blocked connection events
5. **UI Updates**: Real-time updates, filtering by time range, process, destination, etc.

This is a significant undertaking and would be appropriate for a dedicated phase.
