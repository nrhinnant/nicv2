# Feature 041: Network Profiles

## Overview

This feature adds network profile management to WFP Traffic Control, enabling automatic policy switching based on network conditions. Users can define profiles that match specific networks (by SSID, gateway, DNS suffix, etc.) and associate them with policy files. When the system detects a network change, it can automatically switch to the appropriate profile and apply the corresponding policy.

## Components

### 1. Network Profile Model (`src/shared/Ipc/NetworkProfileMessages.cs`)

#### NetworkProfile Class
- **Id**: Unique identifier for the profile
- **Name**: Display name
- **Description**: Optional description
- **PolicyPath**: Path to the policy file to apply when active
- **Conditions**: Matching conditions (ProfileConditions)
- **Priority**: Higher priority profiles are matched first (default: 100)
- **Enabled**: Whether the profile is active
- **IsDefault**: Whether this is the fallback profile

#### ProfileConditions Class
- **Ssids**: List of WiFi SSIDs to match
- **DnsSuffixes**: List of DNS suffixes to match
- **NetworkNames**: List of network names to match
- **Gateways**: List of gateway IP addresses to match
- **NetworkCategory**: Network category to match (Public/Private/Domain)
- **MatchAll**: If true, all conditions must match (AND logic); if false, any condition matches (OR logic)

#### CurrentNetworkInfo Class
- **NetworkName**: Current network name
- **Category**: Network category (Public/Private/Domain)
- **Ssid**: Connected WiFi SSID (if wireless)
- **DnsSuffix**: Current DNS suffix
- **Gateway**: Default gateway IP
- **IsConnected**: Connection status
- **AdapterName**: Network adapter name

### 2. IPC Messages

| Request | Response | Description |
|---------|----------|-------------|
| GetNetworkProfilesRequest | GetNetworkProfilesResponse | Get all profiles and active profile ID |
| SaveNetworkProfileRequest | SaveNetworkProfileResponse | Create or update a profile |
| DeleteNetworkProfileRequest | DeleteNetworkProfileResponse | Delete a profile by ID |
| GetCurrentNetworkRequest | GetCurrentNetworkResponse | Get current network info and matching profile |
| ActivateProfileRequest | ActivateProfileResponse | Manually activate a specific profile |
| SetAutoSwitchRequest | SetAutoSwitchResponse | Enable/disable automatic switching |
| GetAutoSwitchStatusRequest | GetAutoSwitchStatusResponse | Get auto-switch status and active profile |

### 3. UI Components

#### NetworkProfilesViewModel (`src/ui/.../ViewModels/NetworkProfilesViewModel.cs`)

Features:
- Load profiles from service
- Display current network information
- Toggle automatic profile switching
- Add/Edit/Delete profiles
- Activate profiles manually
- Copy current network values to profile conditions
- Browse for policy files

#### NetworkProfilesView (`src/ui/.../Views/NetworkProfilesView.xaml`)

Modal dialog with:
- **Left Panel**:
  - Current network info (name, SSID, category, gateway)
  - Refresh network button
  - Auto-switch toggle with description
  - Profiles list with selection
  - Add/Edit/Delete/Activate buttons

- **Right Panel**:
  - View mode: Selected profile details and conditions
  - Edit mode: Full profile editor with:
    - Basic info (name, description, policy path, priority, enabled, default)
    - Matching conditions (SSIDs, gateways, DNS suffixes, network names, category)
    - Match mode toggle (AND/OR logic)
    - "Copy Current" buttons to copy current network values

- **Footer**:
  - Active profile indicator
  - Status message
  - Close button

### 4. Service Client Integration

IServiceClient interface extended with:
- `GetNetworkProfilesAsync()`
- `SaveNetworkProfileAsync(profile)`
- `DeleteNetworkProfileAsync(profileId)`
- `GetCurrentNetworkAsync()`
- `ActivateProfileAsync(profileId)`
- `SetAutoSwitchAsync(enabled)`
- `GetAutoSwitchStatusAsync()`

### 5. Main Window Integration

- "Profiles" button in header bar opens the Network Profiles dialog
- Located next to the "Syslog" button

## Usage

### Creating a Profile

1. Click "Profiles" in the header bar
2. Click "Add Profile"
3. Enter a name and description
4. Select or browse for a policy file
5. Configure matching conditions:
   - Use "Copy Current" buttons to use current network values
   - Add SSIDs, gateways, DNS suffixes as needed
   - Choose match mode (any/all conditions)
6. Set priority (higher = matched first)
7. Click "Save Profile"

### Profile Matching Logic

When auto-switch is enabled:
1. Profiles are sorted by priority (highest first)
2. For each profile, conditions are evaluated:
   - If MatchAll is true: ALL conditions must match
   - If MatchAll is false: ANY condition matches
3. First matching profile is activated
4. If no profile matches, the default profile is used

### Manual Activation

1. Select a profile from the list
2. Click "Activate Selected Profile"
3. The profile becomes active and its policy is applied

### Auto-Switch

When enabled:
- System monitors network changes
- Automatically matches and activates profiles
- Applies the corresponding policy

When disabled:
- Profiles must be activated manually

## Example Profiles

### Home Network Profile
```json
{
  "name": "Home Network",
  "policyPath": "C:\\Policies\\home.json",
  "conditions": {
    "ssids": ["MyHomeWiFi"],
    "gateways": ["192.168.1.1"],
    "matchAll": false
  },
  "priority": 100
}
```

### Corporate Network Profile
```json
{
  "name": "Corporate",
  "policyPath": "C:\\Policies\\corporate.json",
  "conditions": {
    "dnsSuffixes": ["corp.company.com"],
    "networkCategory": "Domain",
    "matchAll": false
  },
  "priority": 200
}
```

### Public Network Profile (Default)
```json
{
  "name": "Public/Untrusted",
  "policyPath": "C:\\Policies\\strict.json",
  "conditions": {
    "networkCategory": "Public",
    "matchAll": false
  },
  "priority": 50,
  "isDefault": true
}
```

## Testing

### Unit Tests (57 tests in `tests/UI/NetworkProfilesTests.cs`)

- ViewModel initial state and defaults
- Profile loading and display
- Auto-switch toggling
- Profile selection and can-edit/can-delete logic
- Add/Edit/Save/Delete profile operations
- Activate profile functionality
- Copy current network values
- IPC message types and responses
- MockServiceClient profile operations

### Manual Testing

1. Open the UI application
2. Click "Profiles" button in header
3. View current network information
4. Create profiles with matching conditions
5. Test manual profile activation
6. Enable auto-switch and verify automatic switching

## Error Handling

- Service unavailable: Shows error message in status bar and dialog
- Invalid profile name: Validation error prevents save
- Delete confirmation: Requires user confirmation
- Default profile protection: Cannot delete the default profile
- Profile not found: Error when activating non-existent profile

## Known Limitations

1. Network detection relies on service-side implementation (not included in UI feature)
2. Auto-switch monitoring requires service to detect network changes
3. Policy application errors are handled by existing apply mechanism
4. No profile import/export functionality

## Future Enhancements

- Profile import/export (JSON)
- Profile templates for common scenarios
- Network change notifications in UI
- Profile scheduling (time-based activation)
- Profile groups/inheritance
- Conflict resolution UI for overlapping conditions
