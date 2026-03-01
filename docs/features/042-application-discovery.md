# Feature 042: Application Discovery

## Overview

This feature adds application discovery capabilities to WFP Traffic Control, enabling users to discover installed applications on the system and get intelligent rule suggestions based on known application signatures. The feature helps users quickly configure firewall rules for common applications by identifying installed software and providing pre-configured rule templates.

## Components

### 1. Application Discovery Service (`src/ui/.../Services/ApplicationDiscoveryService.cs`)

#### IApplicationDiscoveryService Interface
- `DiscoverApplicationsAsync(CancellationToken)` - Scans system for installed applications
- `GetSuggestedRules(string applicationId)` - Gets rule suggestions for a known application
- `GetApplicationSignatures()` - Returns all known application signatures

#### DiscoveredApplication Class
- **Id**: Unique identifier from registry
- **Name**: Display name of the application
- **Publisher**: Software publisher
- **InstallPath**: Installation directory
- **ExecutablePath**: Path to main executable
- **Version**: Application version
- **InstallDate**: Installation date
- **CoverageStatus**: Current rule coverage (Unknown/Uncovered/PartiallyCovered/FullyCovered)
- **IsKnownApplication**: Whether the app matches a known signature
- **KnownApplicationId**: ID of matched signature
- **MatchingRules**: List of existing rules that cover this application
- **SuggestedRules**: List of suggested rules for this application

#### ApplicationCoverageStatus Enum
- `Unknown` - Coverage status not determined
- `Uncovered` - No existing rules cover this application
- `PartiallyCovered` - Some suggested rules are covered
- `FullyCovered` - All suggested rules are covered

#### SuggestedRule Class
- **Id**: Rule identifier
- **Description**: Human-readable description
- **Action**: "allow" or "block"
- **Direction**: "inbound" or "outbound"
- **Protocol**: "tcp", "udp", or "any"
- **RemoteIp**: Optional remote IP/CIDR
- **RemotePorts**: Optional list of ports
- **Comment**: Optional comment
- **Priority**: Suggestion priority (Low/Normal/High/Critical)

#### ApplicationSignature Class
- **Id**: Signature identifier
- **Name**: Application name
- **Publisher**: Expected publisher
- **ExecutablePatterns**: Patterns to match executable path
- **DefaultRules**: Pre-configured rules for this application
- **Description**: Optional description
- **Category**: Application category

### 2. Known Application Signatures

The service includes built-in signatures for common applications:

| Category | Applications |
|----------|-------------|
| Web Browser | Google Chrome, Mozilla Firefox, Microsoft Edge |
| Communication | Discord, Slack, Microsoft Teams, Zoom |
| Development | Visual Studio Code, Git, Docker Desktop |
| Cloud Storage | Dropbox, Microsoft OneDrive, Google Drive |
| Gaming | Steam |
| Media | Spotify, VLC media player |

Each signature includes appropriate default rules (e.g., browsers get HTTP/HTTPS rules, communication apps get voice UDP rules).

### 3. UI Components

#### ApplicationDiscoveryViewModel (`src/ui/.../ViewModels/ApplicationDiscoveryViewModel.cs`)

Features:
- Scan for installed applications
- Filter by search text, category, and coverage status
- Display statistics (total, covered, uncovered, known apps)
- Select application to view details and suggested rules
- Apply suggested rules to generate policy
- Copy application path to clipboard
- View all known application signatures

#### ApplicationDiscoveryView (`src/ui/.../Views/ApplicationDiscoveryView.xaml`)

Modal dialog with:
- **Header**:
  - Scan button
  - Statistics cards (Total, Covered, Uncovered, Known)

- **Filters**:
  - Search box (by name, publisher, or path)
  - Category filter dropdown
  - Coverage filter dropdown

- **Content Area**:
  - Left panel: Application list with icons and status indicators
  - Right panel: Selected application details
    - Name, Publisher, Version, Install Date
    - Executable path with copy button
    - Coverage status
    - Suggested rules list
    - Apply suggested rules button

- **Footer**:
  - Status message
  - Show Signatures button
  - Close button

### 4. Main Window Integration

- "Apps" button in header bar opens the Application Discovery dialog
- Located after the "Profiles" button

## Usage

### Discovering Applications

1. Click "Apps" in the header bar
2. Click "Scan for Applications"
3. Wait for the scan to complete
4. View discovered applications in the list

### Filtering Applications

- **Search**: Type in the search box to filter by name, publisher, or path
- **Category**: Select a category (Web Browser, Communication, etc.) to filter
- **Coverage**: Filter by rule coverage status (Uncovered, Partially Covered, etc.)

### Viewing Application Details

1. Click on an application in the list
2. View details in the right panel:
   - Basic information (name, publisher, version)
   - Executable path
   - Coverage status
   - Suggested firewall rules

### Applying Suggested Rules

1. Select an application with suggested rules
2. Click "Apply Suggested Rules"
3. Confirm the operation
4. Choose a location to save the policy file
5. Optionally apply the policy immediately

### Copying Application Path

1. Select an application
2. Click the "Copy" button next to the executable path
3. Use the path in manual rule creation

## Coverage Calculation

Coverage is calculated by comparing existing policy rules against suggested rules:

1. **Uncovered**: No existing rules match the application's executable
2. **Partially Covered**: Some but not all suggested rules are covered
3. **Fully Covered**: All suggested rules (or more) are covered
4. **Unknown**: No suggested rules to compare against

## Application Discovery Process

1. Scan Windows Registry for installed applications:
   - `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`
   - `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall`

2. Filter out system components and updates

3. Extract executable path from:
   - DisplayIcon registry value
   - InstallLocation + DisplayName.exe

4. Match against known application signatures by:
   - Executable path patterns
   - Application name

5. Calculate coverage based on current policy

## Testing

### Unit Tests (35 tests in `tests/UI/ApplicationDiscoveryTests.cs`)

- DiscoveredApplication default values and properties
- SuggestedRule default values and properties
- ApplicationSignature default values and properties
- ApplicationCoverageStatus enum values
- SuggestionPriority enum values
- IApplicationDiscoveryService methods
- ApplicationDiscoveryViewModel initial state
- Filter functionality (search, category, coverage)
- Scan functionality
- Apply suggested rules scenarios
- Real service integration tests

### Manual Testing

1. Open the UI application
2. Click "Apps" button in header
3. Click "Scan for Applications"
4. Verify discovered applications appear
5. Test filtering by search text
6. Test filtering by category
7. Test filtering by coverage status
8. Select an application to view details
9. Apply suggested rules and verify policy generation

## Error Handling

- Registry access failures are silently skipped
- Individual application errors don't stop the scan
- Service unavailable errors are shown in status
- Policy application errors are displayed to user

## Known Limitations

1. Only scans Windows Registry uninstall locations
2. May not find portable applications
3. Executable path detection is heuristic-based
4. Known signatures are built-in (not configurable)
5. Coverage calculation is based on process path matching only

## Future Enhancements

- Custom application signatures (user-defined)
- Import/export application signatures
- Scan additional locations (Start Menu, Program Files)
- Automatic signature updates
- Bulk rule application for multiple applications
- Integration with Windows Security Center
- Process monitoring for runtime application detection
