# 033 - Visual Rule Builder

## Overview

The Visual Rule Builder provides enhancements to the Policy Editor that make it easier to create and edit firewall rules without manually writing JSON. This feature adds process selection capabilities, inline validation, and a JSON preview to the existing rule editor.

## Features

### Process Selection

Two new buttons have been added next to the Process field:

1. **Browse Button (...)**: Opens a standard file dialog to browse for executable files.

2. **Pick Process Button**: Opens the Process Picker Dialog to select from currently running processes.

### Process Picker Dialog

A new dialog (`ProcessPickerDialog`) allows users to:

- View all running processes with their names, PIDs, and paths
- Search/filter processes by name or path
- Refresh the process list
- Double-click or select a process to use its executable path
- Only processes with accessible paths are shown (system processes without path access are filtered out)

### Inline Validation

The rule editor now provides real-time validation feedback for:

1. **IP/CIDR Fields** (Remote IP/CIDR, Local IP/CIDR):
   - Validates IP address format (IPv4 and IPv6)
   - Validates CIDR notation (prefix 0-128)
   - Shows red border and error message when invalid

2. **Port Fields** (Remote Ports, Local Ports):
   - Validates single ports (0-65535)
   - Validates port ranges (e.g., "80-443")
   - Validates comma-separated lists (e.g., "80,443,8080-9000")
   - Shows red border and error message when invalid

### JSON Preview

An expandable "JSON Preview" section at the bottom of the rule detail panel shows:

- The current rule serialized as JSON
- Updates in real-time as fields are edited
- Uses the same JSON format that will be saved to the policy file
- Read-only, monospace font for easy reading

## Implementation Details

### Files Created/Modified

**New Files:**
- `src/ui/WfpTrafficControl.UI/Views/ProcessPickerDialog.xaml` - Dialog XAML
- `src/ui/WfpTrafficControl.UI/Views/ProcessPickerDialog.xaml.cs` - Dialog code-behind and ViewModel

**Modified Files:**
- `src/ui/WfpTrafficControl.UI/ViewModels/PolicyEditorViewModel.cs`:
  - Added `BrowseProcessCommand` and `PickProcessCommand`
  - Extended `RuleViewModel` with:
    - `JsonPreview` property (generates JSON representation)
    - `RemoteIpError`, `LocalIpError` properties (IP validation)
    - `RemotePortsError`, `LocalPortsError` properties (port validation)
    - `HasValidationErrors` property
    - Static `JsonPreviewOptions` for cached serialization options

- `src/ui/WfpTrafficControl.UI/Views/PolicyEditorView.xaml`:
  - Added Browse and Pick Process buttons next to Process field
  - Added validation styling to IP and port fields
  - Added JSON Preview expander

### Validation Logic

**IP/CIDR Validation:**
```csharp
// Accepts: "192.168.1.1", "10.0.0.0/8", "::1", "fe80::/10"
// Rejects: "999.999.999.999", "10.0.0.1/999", "invalid"
```

**Port Validation:**
```csharp
// Accepts: "80", "80-443", "80,443,8080", "80,443,8080-9000"
// Rejects: "abc", "99999", "443-80" (start > end)
```

## Usage

### Creating a Rule with Process Path

1. Add a new rule or select an existing rule
2. Click the **Pick** button next to the Process field
3. In the Process Picker Dialog:
   - Use the search box to filter by process name or path
   - Select a process from the list
   - Click **Select** or double-click to use its path
4. The process path is populated in the Process field

### Validating Fields

1. Enter values in the IP/CIDR or Port fields
2. Invalid values show:
   - Red border around the field
   - Error message below the field
   - Tooltip with the error message

### Previewing JSON

1. Expand the "JSON Preview" section at the bottom of the rule details
2. View the JSON representation of the current rule
3. JSON updates automatically as you edit fields

## Security Considerations

- The Process Picker only shows processes the current user can access
- Process paths are read-only from the process list (no arbitrary code execution)
- User input is validated before being included in policy rules
- JSON preview is read-only

## Testing

### Manual Testing

1. Create a new policy and add a rule
2. Click **Browse** to test file selection
3. Click **Pick** to test process picker:
   - Verify process list loads
   - Verify search/filter works
   - Verify selection returns correct path
4. Test validation:
   - Enter invalid IP: "999.1.1.1" - should show error
   - Enter invalid port: "abc" - should show error
   - Enter valid values - errors should clear
5. Expand JSON Preview:
   - Verify JSON updates as you edit fields
   - Verify JSON matches rule configuration

### Automated Tests

Unit tests verify:
- IP/CIDR validation logic (valid and invalid cases)
- Port validation logic (single ports, ranges, lists)
- JSON preview generation

## Known Limitations

- Process Picker shows only processes with accessible paths (some system processes are hidden)
- IPv6 validation accepts the address format but the underlying WFP may have limitations
- Process path field does not validate that the file exists (path may not exist yet)
