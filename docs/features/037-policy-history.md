# Policy Version History (P9)

## Overview

Policy Version History provides automatic tracking of all policy applications, enabling users to view previous versions and quickly revert to any past state. Every time a policy is applied (via CLI, UI, or watch mode), it is automatically saved to the history store.

## Features

### Automatic History Tracking

- **Automatic Save**: Every successful policy apply operation saves the policy to history
- **Metadata Recording**: Captures timestamp, version, rule count, source, filters created/removed
- **Maximum Entries**: Keeps up to 100 history entries (configurable)
- **Persistent Storage**: History is stored in `%ProgramData%\WfpTrafficControl\History\`

### History View

Access via the "History" button in the main window toolbar:

- **Chronological List**: Most recent entries first
- **Entry Details**: Shows version, timestamp, rule count, source, filter changes
- **Quick Revert**: One-click revert to any previous version
- **Policy Preview**: View the full policy JSON content for any entry

### Entry Information

Each history entry records:
- **Entry ID**: Unique identifier based on timestamp
- **Applied At**: When the policy was applied (UTC)
- **Policy Version**: Version string from the policy file
- **Rule Count**: Number of rules in the policy
- **Source**: Where the apply came from (CLI, UI, Watch, LKG)
- **Source Path**: Original policy file path (if applicable)
- **Filters Created**: Number of WFP filters created
- **Filters Removed**: Number of WFP filters removed

## Implementation Details

### Storage Structure

History is stored in `%ProgramData%\WfpTrafficControl\History\`:
```
History/
  history-index.json    # Index file with entry metadata
  policy-YYYYMMDD-HHmmss-fff.json  # Individual policy files
```

### IPC Messages

New IPC message types:

1. **policy-history**: Get list of history entries
   - Request: `{ "type": "policy-history", "limit": 50 }`
   - Response: `{ "ok": true, "entries": [...], "totalCount": 100 }`

2. **policy-history-revert**: Revert to a specific version
   - Request: `{ "type": "policy-history-revert", "entryId": "20250301-120000-001" }`
   - Response: `{ "ok": true, "filtersCreated": 5, "filtersRemoved": 2, ... }`

3. **policy-history-get**: Get a specific policy from history
   - Request: `{ "type": "policy-history-get", "entryId": "20250301-120000-001" }`
   - Response: `{ "ok": true, "entry": {...}, "policyJson": "..." }`

### Service Integration

History saving is integrated into the apply workflow:
1. Policy validation
2. Policy compilation
3. WFP filter application
4. LKG save (existing)
5. **History save (new)** - non-fatal if fails

## Files

### New Files
- `src/shared/History/PolicyHistoryStore.cs` - Storage logic
- `src/shared/Ipc/PolicyHistoryMessages.cs` - IPC message types
- `src/ui/WfpTrafficControl.UI/ViewModels/PolicyHistoryViewModel.cs` - UI ViewModel
- `src/ui/WfpTrafficControl.UI/Views/PolicyHistoryView.xaml(.cs)` - UI View
- `tests/UI/PolicyHistoryTests.cs` - Unit tests
- `docs/features/037-policy-history.md` - Documentation

### Modified Files
- `src/shared/Ipc/IpcMessages.cs` - Added history message parsing
- `src/service/Ipc/PipeServer.cs` - Added history handlers and save on apply
- `src/ui/WfpTrafficControl.UI/Services/IServiceClient.cs` - Added history methods
- `src/ui/WfpTrafficControl.UI/Services/ServiceClient.cs` - Implemented history methods
- `tests/UI/MockServiceClient.cs` - Added mock implementations
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - Registered ViewModel
- `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` - Added History command
- `src/ui/WfpTrafficControl.UI/MainWindow.xaml` - Added History button

## Usage

### Viewing History

1. Click the "History" button in the main toolbar
2. The history list shows all previous policy applications
3. Click an entry to select it
4. View entry details in the panel below the list

### Reverting to a Previous Version

1. Open the History view
2. Select the entry you want to revert to
3. Click "Revert to This Version"
4. Confirm the revert operation
5. The policy is applied immediately

### Viewing Policy Content

1. Select a history entry
2. Click "View Policy"
3. The full policy JSON is displayed in a dialog

## Known Limitations

- History is local to the machine (not synchronized)
- Maximum 100 entries before oldest are pruned
- History save failures are non-fatal (logged as warnings)
- No search/filter capability within history (yet)
- Policy content preview truncates very large policies

## Testing

Run the policy history tests:
```bash
dotnet test tests/Tests.csproj --filter "FullyQualifiedName~PolicyHistoryTests"
```
