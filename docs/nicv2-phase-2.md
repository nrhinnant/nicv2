# NICv2 Phase 2 - Feature Enhancements Plan

## 🎉 PHASE 2 COMPLETE!

All 14 priority features have been successfully implemented and tested:
- **Total Tests**: 1,268 tests passing
- **Features Implemented**: 14/14 (100%)
- **Documentation**: All features documented in `/docs/features/`

---

This document provides implementation guidance for Phase 2 enhancements to the WFP Traffic Control system. Each enhancement includes context, implementation checklist, and file references for Claude Code sessions.

**IMPORTANT**: All implementations MUST follow the 5-phase workflow defined in `CLAUDE.md`. Each feature should be committed atomically with descriptive commit messages.

---

## Priority Overview

| Priority | Feature | Effort | Impact | Dependencies | Status |
|----------|---------|--------|--------|--------------|--------|
| P1 | System Tray Integration | Low | High | None | ✅ COMPLETED |
| P2 | Dark Mode Theme | Low | Medium | None | ✅ COMPLETED |
| P3 | Blocked Connection Log with Allow | Medium | High | Service enhancement | ✅ COMPLETED |
| P4 | Visual Rule Builder | Medium | High | None | ✅ COMPLETED |
| P5 | Rule Simulation ("What If") | Medium | High | Service enhancement | ✅ COMPLETED |
| P6 | Search/Filter Throughout | Low | Medium | None | ✅ COMPLETED |
| P7 | Policy Diff View | Medium | Medium | None | ✅ COMPLETED |
| P8 | Rule Templates/Presets | Low | Medium | None | ✅ COMPLETED |
| P9 | Policy Version History | Medium | High | Service enhancement | ✅ COMPLETED |
| P10 | Real-Time Connection Monitor | High | High | Service enhancement | ✅ COMPLETED |
| P11 | Connection Analytics Dashboard | High | Medium | P10 required | ✅ COMPLETED |
| P12 | Syslog/SIEM Export | Medium | Medium | None | ✅ COMPLETED |
| P13 | Network Profiles | High | High | Service enhancement | ✅ COMPLETED |
| P14 | Application Discovery | High | Medium | None | ✅ COMPLETED |

---

## P1: System Tray Integration ✅ COMPLETED

### Overview
Add a system tray icon that provides quick status visibility and common actions without opening the full GUI. This is a standard expectation for Windows security software.

### User Stories
- As a user, I want to see the firewall status at a glance in my system tray
- As a user, I want quick access to enable/disable filtering
- As a user, I want notifications when connections are blocked

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Review existing WPF application lifecycle in `App.xaml.cs`
- [x] Understand current MainWindow behavior
- [x] Plan tray icon states (green=active, yellow=no policy, red=error, gray=disabled)

#### Phase 2 - EXECUTE
- [x] Add `System.Windows.Forms` reference for NotifyIcon (or use Hardcodet.NotifyIcon.Wpf NuGet)
- [x] Create `TrayIconService.cs` in `Services/`
- [x] Implement tray icon with context menu:
  - Show/Hide main window
  - Status indicator (Connected/Disconnected, Filter count)
  - Quick actions: Apply Last Policy, Rollback, Refresh
  - Open Logs
  - Exit
- [x] Add tray icon initialization to `App.xaml.cs`
- [x] Implement minimize-to-tray behavior
- [x] Add balloon notifications for:
  - Policy applied/rolled back
  - Service connection lost
  - (Optional) Blocked connections

#### Phase 3 - CODE REVIEW
- [x] Verify icon disposal on application exit
- [x] Check for memory leaks with repeated show/hide
- [x] Ensure thread-safe UI updates

#### Phase 4 - DOCUMENT
- [x] Update RUNBOOK.md with tray icon documentation
- [x] Create `docs/features/tray-icon.md`

#### Phase 5 - TEST
- [x] Create `TrayIconServiceTests.cs`
- [x] Test minimize to tray
- [x] Test context menu actions
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/ui/WfpTrafficControl.UI/App.xaml.cs          # Application lifecycle
src/ui/WfpTrafficControl.UI/MainWindow.xaml      # Window behavior
src/ui/WfpTrafficControl.UI/MainWindow.xaml.cs   # Window code-behind
src/ui/WfpTrafficControl.UI/Services/            # Service patterns
docs/RUNBOOK.md                                   # User documentation
```

### Technical Notes
- Consider using `Hardcodet.NotifyIcon.Wpf` NuGet package for better WPF integration
- Store tray icon resources in `Resources/` folder (16x16 and 32x32 ICO files)
- Use `Application.Current.Dispatcher` for UI thread marshaling
- Implement `IDisposable` for cleanup

---

## P2: Dark Mode Theme ✅ COMPLETED

### Overview
Add dark mode support following Windows system theme or manual toggle. Modern applications are expected to support dark mode.

### User Stories
- As a user, I want the app to follow my Windows dark mode setting
- As a user, I want to manually toggle between light and dark themes

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Review current theme resources in `App.xaml`
- [x] Understand existing color brush naming conventions
- [x] Plan theme switching mechanism

#### Phase 2 - EXECUTE
- [x] Create `Themes/LightTheme.xaml` resource dictionary
- [x] Create `Themes/DarkTheme.xaml` resource dictionary
- [x] Move existing colors from `App.xaml` to `LightTheme.xaml`
- [x] Create dark variants for all brushes:
  - BackgroundBrush, CardBackgroundBrush
  - TextPrimaryBrush, TextSecondaryBrush
  - BorderBrush, etc.
- [x] Create `ThemeService.cs`:
  - DetectSystemTheme()
  - ApplyTheme(ThemeMode mode)
  - Subscribe to Windows theme changes
- [x] Add theme toggle to Settings or MainWindow
- [x] Persist theme preference in user settings

#### Phase 3 - CODE REVIEW
- [x] Verify all UI elements respond to theme changes
- [x] Check contrast ratios for accessibility
- [x] Test with high-contrast Windows themes

#### Phase 4 - DOCUMENT
- [x] Update RUNBOOK.md with theme settings
- [ ] Add screenshots of both themes (requires running GUI application)

#### Phase 5 - TEST
- [x] Create `ThemeServiceTests.cs`
- [x] Test theme switching
- [x] Test system theme detection
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/ui/WfpTrafficControl.UI/App.xaml              # Current theme resources
src/ui/WfpTrafficControl.UI/Styles/               # Style definitions
src/ui/WfpTrafficControl.UI/Views/*.xaml          # All views using resources
```

### Technical Notes
- Use `SystemParameters.HighContrast` for high-contrast detection
- Listen to `SystemEvents.UserPreferenceChanged` for system theme changes
- Consider using `DynamicResource` instead of `StaticResource` for theme-switchable colors
- Store preference in `IsolatedStorage` or Windows Registry

---

## P3: Blocked Connection Log with "Allow" Button ✅ COMPLETED

### Overview
Create a dedicated view showing recent blocked connections with one-click rule creation. This is the primary workflow for firewall configuration.

### User Stories
- As a user, I want to see what connections my firewall is blocking
- As a user, I want to create allow rules directly from blocked connection entries
- As a user, I want to group blocked connections by process or destination

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Review existing audit log structure
- [x] Design blocked connection data model
- [x] Plan service enhancement for blocked connection tracking

#### Phase 2 - EXECUTE

**Service Enhancement:**
- [x] Create `BlockedConnectionDto` in `Shared/Ipc/`:
  - Timestamp, ProcessPath, ProcessName
  - Direction, Protocol, LocalIP, LocalPort
  - RemoteIP, RemotePort, RuleId (that blocked it)
- [x] Create `BlockedConnectionsRequest/Response` IPC messages
- [x] Implement blocked connection buffer in service (ring buffer, last N entries)
- [x] Add `GetBlockedConnectionsAsync` to service handler

**UI Implementation:**
- [x] Add `GetBlockedConnectionsAsync` to `IServiceClient`
- [x] Implement in `ServiceClient.cs`
- [x] Create `BlockedConnectionsViewModel.cs`:
  - ObservableCollection of blocked connections
  - Refresh command
  - CreateAllowRuleCommand (per item)
  - GroupBy property (None, Process, Destination)
- [x] Create `BlockedConnectionsView.xaml`:
  - DataGrid with columns: Time, Process, Direction, Remote, Port
  - Toolbar with Refresh, Group By dropdown
  - Right-click context menu: Create Allow Rule, Copy Details
- [x] Add BlockedConnections tab to MainWindow
- [x] Implement "Create Allow Rule" flow:
  - Open Visual Rule Builder (P4) pre-populated, OR
  - Generate rule JSON and open in Validate JSON dialog

#### Phase 3 - CODE REVIEW
- [x] Verify ring buffer doesn't grow unbounded
- [x] Check performance with high block rates
- [x] Ensure thread-safe access to connection buffer

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/blocked-connections.md`
- [x] Update RUNBOOK.md

#### Phase 5 - TEST
- [x] Add service tests for blocked connection buffer
- [x] Create `BlockedConnectionsViewModelTests.cs`
- [x] Add to MockServiceClient
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/shared/Ipc/                                   # IPC message patterns
src/service/Handlers/                             # Request handler patterns
src/service/Wfp/WfpEngine.cs                      # Where blocking decisions are made
src/ui/WfpTrafficControl.UI/ViewModels/           # ViewModel patterns
src/ui/WfpTrafficControl.UI/Views/LogsView.xaml   # Similar list view pattern
```

### Technical Notes
- The service needs to capture blocked connections at the point of WFP decision
- Consider ETW for high-performance blocked connection logging
- Ring buffer size should be configurable (default 1000 entries)
- May need to throttle UI updates for high block rates

---

## P4: Visual Rule Builder ✅ COMPLETED

### Overview
A form-based UI for creating firewall rules without writing JSON. This significantly lowers the barrier to entry for users.

### User Stories
- As a user, I want to create rules using dropdowns and text fields
- As a user, I want to pick processes from a list of running applications
- As a user, I want to see the JSON preview before saving

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Review policy rule schema in `Policy.cs`
- [x] Understand all rule properties and valid values
- [x] Design form layout

#### Phase 2 - EXECUTE
- [x] Create `RuleBuilderViewModel.cs`:
  - Properties for all rule fields (Id, Action, Direction, Protocol, etc.)
  - Validation for each field
  - GenerateJson() method
  - SaveRule command
  - PreviewJson property
- [x] Create `RuleBuilderView.xaml`:
  - Rule ID text box
  - Action dropdown (Allow, Block)
  - Direction dropdown (Inbound, Outbound, Both)
  - Protocol dropdown (TCP, UDP, Any)
  - Process section:
    - Text box for path
    - Browse button
    - "Pick from running processes" button
  - Local endpoint section:
    - IP/CIDR text box with validation
    - Ports text box (supports ranges like "80,443,8000-9000")
  - Remote endpoint section (same as local)
  - Comment text box
  - Enabled checkbox
  - JSON preview panel (read-only, syntax highlighted if possible)
  - Save/Cancel buttons
- [x] Create `ProcessPickerDialog.xaml`:
  - List of running processes with icons
  - Search/filter
  - Select and return path
- [x] Integrate with Policy Editor tab:
  - "Add Rule" button opens RuleBuilder
  - Edit existing rule populates RuleBuilder
- [x] Add validation feedback (red borders, tooltips)

#### Phase 3 - CODE REVIEW
- [x] Validate all generated JSON is valid policy
- [x] Check for injection vulnerabilities in user input
- [x] Ensure proper escaping of paths with special characters

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/rule-builder.md`
- [x] Update RUNBOOK.md with visual rule builder documentation
- [ ] Add screenshots (requires running GUI application)

#### Phase 5 - TEST
- [x] Create `RuleBuilderViewModelTests.cs`
- [x] Test JSON generation for all field combinations
- [x] Test validation rules
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/shared/Policy/Policy.cs                       # Rule model definition
src/shared/Policy/PolicyRule.cs                   # Rule properties
src/shared/Policy/PolicyValidator.cs              # Validation rules
src/shared/Policy/RuleCompiler.cs                 # How rules become filters
src/ui/WfpTrafficControl.UI/Views/PolicyEditorView.xaml  # Integration point
```

### Technical Notes
- Use `System.Diagnostics.Process.GetProcesses()` for process picker
- Consider caching process list with refresh button
- IP validation should support both IPv4 and IPv6
- Port validation must handle: single ports, ranges, comma-separated lists

---

## P5: Rule Simulation ("What If" Testing) ✅ COMPLETED

### Overview
Allow users to test whether a hypothetical connection would be allowed or blocked, and by which rule. This is a unique differentiator that solves a real debugging pain point.

### User Stories
- As a user, I want to test if a specific connection would be allowed
- As a user, I want to know which rule would match a connection
- As a user, I want to debug why a connection is being blocked

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Review how RuleCompiler matches connections
- [x] Design simulation request/response
- [x] Plan UI for entering connection parameters

#### Phase 2 - EXECUTE

**Service Enhancement:**
- [x] Create `SimulateRequest` in `Shared/Ipc/`:
  - ProcessPath (optional)
  - Direction
  - Protocol
  - LocalIP, LocalPort
  - RemoteIP, RemotePort
- [x] Create `SimulateResponse`:
  - WouldAllow (bool)
  - MatchedRuleId (string, null if default action)
  - MatchedRuleAction (Allow/Block)
  - EvaluationPath (list of rules evaluated)
- [x] Implement simulation logic in service:
  - Load current policy
  - Evaluate rules in order
  - Return first match or default action
- [x] Add `SimulateAsync` handler

**UI Implementation:**
- [x] Add `SimulateAsync` to `IServiceClient`
- [x] Create `RuleSimulatorViewModel.cs`:
  - Input properties for all connection parameters
  - SimulateCommand
  - Result properties
- [x] Create `RuleSimulatorView.xaml`:
  - Input form (similar to RuleBuilder but for connection params)
  - Process picker button
  - "Simulate" button
  - Results panel:
    - Large "ALLOWED" or "BLOCKED" indicator
    - Matched rule details
    - Evaluation trace (collapsible)
- [x] Add as dialog or new tab

#### Phase 3 - CODE REVIEW
- [x] Ensure simulation matches actual WFP behavior
- [x] Verify no side effects from simulation

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/rule-simulation.md`
- [x] Add troubleshooting examples

#### Phase 5 - TEST
- [x] Create service tests for simulation logic
- [x] Create `RuleSimulatorViewModelTests.cs`
- [x] Test edge cases (no rules, all rules match, etc.)
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/shared/Policy/RuleCompiler.cs                 # Rule matching logic
src/service/Handlers/                             # Handler patterns
src/shared/Ipc/                                   # IPC patterns
```

### Technical Notes
- Simulation should NOT apply any WFP changes
- Consider caching compiled policy for fast simulation
- Evaluation trace helps users understand rule ordering

---

## P6: Search/Filter Throughout ✅ COMPLETED

### Overview
Add search and filter capabilities to all list views in the application. This improves usability as policies and logs grow.

### User Stories
- As a user, I want to search rules by ID, comment, or IP
- As a user, I want to filter logs by event type or status
- As a user, I want to filter blocked connections by process

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Identify all list views that need search
- [x] Design consistent search/filter UI pattern

#### Phase 2 - EXECUTE
- [x] Create reusable `SearchFilterControl.xaml`:
  - Search text box with clear button
  - Filter dropdown(s) based on context
- [x] Add search to Policy Editor:
  - Search rules by ID, action, process, IP, port, comment
  - Highlight matching rules
- [x] Add search to Logs View:
  - Filter by event type (apply, rollback, etc.)
  - Filter by status (success, error)
  - Search by any field
- [x] Add search to Blocked Connections (P3):
  - Filter by process
  - Filter by destination IP/port
- [x] Implement `ICollectionView` filtering for efficient updates

#### Phase 3 - CODE REVIEW
- [x] Verify search doesn't cause UI lag with large lists
- [x] Check memory usage with filtered views

#### Phase 4 - DOCUMENT
- [x] Document search syntax if advanced (e.g., "process:chrome")

#### Phase 5 - TEST
- [x] Test search with large datasets
- [x] Test filter combinations
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/ui/WfpTrafficControl.UI/Views/LogsView.xaml           # List view pattern
src/ui/WfpTrafficControl.UI/Views/PolicyEditorView.xaml   # Another list view
src/ui/WfpTrafficControl.UI/ViewModels/                   # ViewModel patterns
```

### Technical Notes
- Use `CollectionViewSource` for WPF filtering
- Consider debouncing search input (300ms delay)
- Persist last filter settings per view

---

## P7: Policy Diff View ✅ COMPLETED

### Overview
Side-by-side comparison of two policies showing added, removed, and changed rules. Critical for understanding impact before applying changes.

### User Stories
- As a user, I want to see what changed between my current and new policy
- As a user, I want to compare any two policy files
- As a user, I want to see the diff before applying

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Review policy JSON structure
- [x] Design diff algorithm for rules
- [x] Plan diff visualization

#### Phase 2 - EXECUTE
- [x] Create `PolicyDiffService.cs`:
  - ComparePolicies(Policy left, Policy right)
  - Returns: AddedRules, RemovedRules, ModifiedRules, UnchangedRules
  - For modified rules, identify changed fields
- [x] Create `PolicyDiffViewModel.cs`:
  - LeftPolicy, RightPolicy
  - DiffResults
  - LoadLeftCommand, LoadRightCommand
  - "Compare with Current" command
- [x] Create `PolicyDiffView.xaml`:
  - Two-panel layout (or unified diff view)
  - Color coding: green=added, red=removed, yellow=modified
  - Summary header (X added, Y removed, Z modified)
  - Click rule to see details
- [x] Integrate with Apply workflow:
  - Show diff before applying new policy
  - Optional: require confirmation of significant changes

#### Phase 3 - CODE REVIEW
- [x] Verify diff handles rule reordering correctly
- [x] Check performance with large policies

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/policy-diff.md`
- [ ] Add screenshots (requires running GUI application)

#### Phase 5 - TEST
- [x] Create `PolicyDiffServiceTests.cs`
- [x] Test all diff scenarios
- [x] Create `PolicyDiffViewModelTests.cs`
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/shared/Policy/Policy.cs                       # Policy structure
src/shared/Policy/PolicyRule.cs                   # Rule structure
src/ui/WfpTrafficControl.UI/Views/                # View patterns
```

### Technical Notes
- Match rules by ID for comparison
- Consider using a diff library or implementing simple LCS algorithm
- Unified diff view may be simpler than side-by-side for initial implementation

---

## P8: Rule Templates/Presets ✅ COMPLETED

### Overview
Pre-built policy templates for common use cases. Helps users get started quickly without security expertise.

### User Stories
- As a user, I want to start with a pre-built policy for my use case
- As a user, I want to customize templates after applying
- As a user, I want to save my own templates

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Define initial template set
- [x] Design template storage and format

#### Phase 2 - EXECUTE
- [x] Create `Templates/` folder with JSON templates (implemented as embedded code in PolicyTemplateProvider.cs):
  - `web-browsing-only.json` - Allow only HTTP/HTTPS
  - `developer-mode.json` - Allow common dev ports
  - `lockdown.json` - Block all except essential
  - `gaming.json` - Allow game launchers and common ports
  - `corporate.json` - Enterprise baseline
- [x] Create `TemplateService.cs` (implemented as PolicyTemplateProvider.cs):
  - GetAvailableTemplates()
  - LoadTemplate(name)
  - SaveAsTemplate(policy, name)
- [x] Create `TemplatePickerDialog.xaml`:
  - List of templates with descriptions
  - Preview panel showing rules
  - "Apply" and "Customize" buttons
- [x] Add "New from Template" button to Policy Editor
- [x] Add "Save as Template" option

#### Phase 3 - CODE REVIEW
- [x] Validate all built-in templates
- [x] Ensure templates don't contain absolute paths

#### Phase 4 - DOCUMENT
- [x] Document each template's purpose
- [x] Create `docs/features/templates.md`

#### Phase 5 - TEST
- [x] Create `TemplateServiceTests.cs` (PolicyTemplateProviderTests.cs)
- [x] Validate all templates parse correctly
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/shared/Policy/Policy.cs                       # Policy structure
samples/                                          # Existing sample policies
docs/RUNBOOK.md                                   # User documentation
```

### Technical Notes
- Store built-in templates as embedded resources
- User templates stored in `%APPDATA%\WfpTrafficControl\Templates\`
- Templates should use relative process names where possible

---

## P9: Policy Version History ✅ COMPLETED

### Overview
Built-in history of all policy changes with rollback capability. Essential for enterprise environments and debugging.

### User Stories
- As a user, I want to see when policies were changed
- As a user, I want to rollback to any previous version
- As a user, I want to know who made changes (for multi-user scenarios)

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Design history storage format
- [x] Plan integration with apply workflow

#### Phase 2 - EXECUTE

**Service Enhancement:**
- [x] Create history storage:
  - Store in `%PROGRAMDATA%\WfpTrafficControl\History\`
  - Each entry: timestamp, policy JSON, source (CLI/UI), metadata
- [x] Create `PolicyHistoryEntry` model
- [x] Create `GetPolicyHistoryRequest/Response`
- [x] Create `RevertToVersionRequest/Response`
- [x] Implement history logging on every apply
- [x] Implement version retrieval and revert

**UI Implementation:**
- [x] Add `GetPolicyHistoryAsync` to `IServiceClient`
- [x] Create `PolicyHistoryViewModel.cs`:
  - List of history entries
  - SelectedEntry
  - RevertCommand
  - DiffWithCurrentCommand
- [x] Create `PolicyHistoryView.xaml`:
  - List of versions with timestamp, rule count, source
  - Preview panel for selected version
  - "Revert" and "Compare" buttons
- [x] Add History tab or section to UI

#### Phase 3 - CODE REVIEW
- [x] Implement history size limits (keep last N versions)
- [x] Verify atomic history writes
- [x] Check disk space handling

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/policy-history.md`
- [x] Document retention policy

#### Phase 5 - TEST
- [x] Create service tests for history operations
- [x] Create `PolicyHistoryViewModelTests.cs`
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/service/Handlers/ApplyHandler.cs              # Where to hook history
src/service/Policy/                               # Policy storage patterns
src/shared/Ipc/                                   # IPC patterns
```

### Technical Notes
- Use atomic file operations for history writes
- Consider compression for old entries
- Default retention: 100 versions or 30 days

---

## P10: Real-Time Connection Monitor ✅ COMPLETED

### Overview
Live view of active network connections with process attribution. Core visibility feature found in competitors like GlassWire.

### User Stories
- As a user, I want to see all active connections in real-time
- As a user, I want to see which process owns each connection
- As a user, I want to create rules from observed connections

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Research Windows APIs for connection enumeration
- [x] Design refresh strategy (polling vs events)
- [x] Plan data model

#### Phase 2 - EXECUTE

**Service Enhancement:**
- [x] Create `ConnectionInfo` model:
  - Protocol, State (TCP states)
  - LocalIP, LocalPort
  - RemoteIP, RemotePort
  - ProcessId, ProcessName, ProcessPath
  - BytesSent, BytesReceived (if available)
- [x] Create `GetConnectionsRequest/Response`
- [x] Implement using:
  - `GetExtendedTcpTable` (iphlpapi.dll) for TCP
  - `GetExtendedUdpTable` for UDP
  - Process name resolution
- [x] Add periodic snapshot or change detection

**UI Implementation:**
- [x] Add `GetConnectionsAsync` to `IServiceClient`
- [x] Create `ConnectionMonitorViewModel.cs`:
  - ObservableCollection of connections
  - Auto-refresh toggle and interval
  - Filter properties
  - CreateRuleCommand (per connection)
- [x] Create `ConnectionMonitorView.xaml`:
  - DataGrid with columns: Process, Protocol, Local, Remote, State
  - Auto-refresh toggle
  - Refresh button
  - Right-click: Create Block Rule, Create Allow Rule, Copy
- [x] Add Connection Monitor tab

#### Phase 3 - CODE REVIEW
- [x] Verify P/Invoke signatures are correct
- [x] Check performance impact of frequent polling
- [x] Ensure proper handle cleanup

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/connection-monitor.md`

#### Phase 5 - TEST
- [x] Create service tests for connection enumeration
- [x] Create `ConnectionMonitorViewModelTests.cs`
- [x] Add mock connection data to MockServiceClient
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/service/                                      # Service patterns
src/shared/Ipc/                                   # IPC patterns
# External research: GetExtendedTcpTable, GetExtendedUdpTable
```

### Technical Notes
- Use P/Invoke for `iphlpapi.dll` functions
- Consider caching process ID to name mapping
- Polling interval: 1-5 seconds, user configurable
- Handle connection churn gracefully (connections may disappear between polls)

---

## P11: Connection Analytics Dashboard ✅ COMPLETED

### Overview
Graphs and statistics showing connection patterns, top talkers, and blocked traffic trends. Transforms the tool from reactive to proactive security monitoring.

### User Stories
- As a user, I want to see connection trends over time
- As a user, I want to identify the top network-using processes
- As a user, I want to see blocked connection patterns

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Design analytics data collection
- [x] Choose charting library
- [x] Plan dashboard layout

#### Phase 2 - EXECUTE

**Prerequisites:** P3 (Blocked Connections) and P10 (Connection Monitor) should be implemented first.

- [x] Add charting library (LiveCharts2, OxyPlot, or ScottPlot)
- [x] Create `AnalyticsService.cs`:
  - Collect and aggregate connection data
  - Store historical summaries
- [x] Create `AnalyticsDashboardViewModel.cs`:
  - Time range selection
  - ConnectionsOverTime data
  - TopProcesses data
  - BlockedVsAllowed data
- [x] Create `AnalyticsDashboardView.xaml`:
  - Time range selector (1h, 24h, 7d)
  - Line chart: Connections over time
  - Pie chart: Top 10 processes by connection count
  - Bar chart: Blocked vs Allowed ratio
  - Stats cards: Total connections, Total blocked, Top blocker
- [x] Add Analytics tab

#### Phase 3 - CODE REVIEW
- [x] Verify chart performance with large datasets
- [x] Check memory usage of historical data

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/analytics.md`

#### Phase 5 - TEST
- [x] Create `AnalyticsServiceTests.cs`
- [x] Create `AnalyticsDashboardViewModelTests.cs`
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
# Depends on P3 and P10 implementations
src/ui/WfpTrafficControl.UI/Views/DashboardView.xaml  # Card layout patterns
```

### Technical Notes
- Start with simple in-memory aggregation
- Consider SQLite for persistent analytics storage
- Sample data for development and testing

---

## P12: Syslog/SIEM Export ✅ COMPLETED

### Overview
Forward audit logs to external systems using standard syslog format. Essential for enterprise security team integration.

### User Stories
- As a security team, I want to forward firewall logs to our SIEM
- As a user, I want to configure syslog destination
- As a user, I want logs in CEF or JSON format

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Research syslog protocols (UDP 514, TCP, TLS)
- [x] Design configuration model
- [x] Choose log format (CEF, JSON, plain syslog)

#### Phase 2 - EXECUTE

**Service Enhancement:**
- [x] Create `SyslogConfig` in configuration:
  - Enabled, Destination (host:port)
  - Protocol (UDP/TCP/TLS)
  - Format (CEF/JSON/Syslog)
  - Severity mapping
- [x] Create `SyslogExporter.cs`:
  - Connect to syslog server
  - Format messages
  - Handle connection failures gracefully
- [x] Hook into audit logging to also send to syslog
- [x] Create `SetSyslogConfigRequest/Response`
- [x] Create `GetSyslogConfigRequest/Response`

**UI Implementation:**
- [x] Add Syslog configuration to Settings
- [x] Create `SyslogSettingsView.xaml`:
  - Enable/Disable toggle
  - Server address and port
  - Protocol dropdown
  - Format dropdown
  - Test connection button
- [x] Add `ConfigureSyslogAsync` to `IServiceClient`

#### Phase 3 - CODE REVIEW
- [x] Ensure TLS certificate validation
- [x] Handle network failures gracefully (don't block main operations)
- [x] Verify message format compliance

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/syslog-export.md`
- [x] Document CEF field mapping
- [x] Provide SIEM integration examples

#### Phase 5 - TEST
- [x] Create mock syslog server for testing
- [x] Test all protocols and formats
- [x] Test failure scenarios
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/service/Logging/AuditLogger.cs                # Current audit logging
src/service/Config/                               # Configuration patterns
# External: RFC 5424 (syslog), CEF format specification
```

### Technical Notes
- Use async I/O for network operations
- Buffer messages during connection failures
- Consider message rate limiting
- CEF format preferred for SIEM compatibility

---

## P13: Network Profiles ✅ COMPLETED

### Overview
Different policies for different network contexts (home, work, public). Automatic switching based on network detection.

### User Stories
- As a user, I want stricter rules on public networks
- As a user, I want different rules at home vs work
- As a user, I want automatic profile switching

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Research Windows network location APIs
- [x] Design profile model
- [x] Plan profile switching logic

#### Phase 2 - EXECUTE

**Service Enhancement:**
- [x] Create `NetworkProfile` model:
  - Name, PolicyPath
  - NetworkConditions (SSID, DNS suffix, gateway MAC)
- [x] Create profile storage
- [x] Implement network detection:
  - Subscribe to network change events
  - Match current network to profiles
  - Auto-apply matching policy
- [x] Create profile management IPC commands

**UI Implementation:**
- [x] Create `ProfileManagerViewModel.cs` (NetworkProfilesViewModel.cs)
- [x] Create `ProfileManagerView.xaml` (NetworkProfilesView.xaml):
  - List of profiles
  - Add/Edit/Delete profile
  - Profile conditions editor
  - Assign policy to profile
- [x] Add current profile indicator to status bar
- [x] Add manual profile switch option

#### Phase 3 - CODE REVIEW
- [x] Handle rapid network changes gracefully
- [x] Verify secure profile matching (avoid spoofing)

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/network-profiles.md`
- [x] Document profile matching logic

#### Phase 5 - TEST
- [x] Create profile matching tests
- [x] Test network change scenarios
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/service/                                      # Service patterns
# External: Windows Network List Manager API, WlanApi
```

### Technical Notes
- Use `INetworkListManager` COM interface
- SSID matching for WiFi networks
- DNS suffix matching for corporate networks
- Consider security: don't trust network names alone

---

## P14: Application Discovery ✅ COMPLETED

### Overview
Scan installed applications and suggest rules based on known application behavior. Proactive security posture management.

### User Stories
- As a user, I want to see which apps have no firewall rules
- As a user, I want suggested rules for common applications
- As a user, I want to know if my rules cover all network-capable apps

### Implementation Checklist

#### Phase 1 - PLAN
- [x] Design application inventory approach
- [x] Plan rule suggestion database
- [x] Design gap analysis algorithm

#### Phase 2 - EXECUTE
- [x] Create `ApplicationDiscoveryService.cs`:
  - Scan installed applications (Program Files, registry)
  - Identify network-capable applications
  - Compare against current policy rules
- [x] Create application signature database:
  - Known applications with typical network behavior
  - Suggested rules per application
- [x] Create `ApplicationDiscoveryViewModel.cs`:
  - Discovered applications list
  - Coverage status (covered, uncovered, partially covered)
  - Suggested rules
- [x] Create `ApplicationDiscoveryView.xaml`:
  - List of discovered applications
  - Coverage indicator
  - "Apply Suggested Rules" button
  - Scan button

#### Phase 3 - CODE REVIEW
- [x] Handle applications with multiple executables
- [x] Verify no privacy concerns with app scanning

#### Phase 4 - DOCUMENT
- [x] Create `docs/features/application-discovery.md`

#### Phase 5 - TEST
- [x] Create discovery service tests
- [x] Test with various application installations
- [x] Other necessary tests to ensure full coverage

### Files to Review Before Implementation
```
src/shared/Policy/PolicyRule.cs                   # Process matching
# External: Registry application enumeration
```

### Technical Notes
- Scan `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`
- Scan `HKLM\SOFTWARE\WOW6432Node\...` for 32-bit apps
- Detect network capability by checking imports or heuristics
- Initial signature database can be small and grow over time

---

## General Implementation Notes

### Code Organization
- Keep ViewModels testable with dependency injection
- Use `IServiceClient` for all service communication
- Follow existing patterns in the codebase

### Testing Requirements
- All ViewModels must have corresponding test files
- Mock all external dependencies
- Target 80%+ code coverage for new code

### Documentation Requirements
- Each feature needs a `docs/features/` document
- Update `RUNBOOK.md` for user-facing features
- Include screenshots where helpful

### Commit Standards
- One commit per logical change
- Format: `feat(scope): description` or `fix(scope): description`

---

## Appendix: Quick Reference - Files by Feature Area

### IPC/Service Communication
```
src/shared/Ipc/                                   # All IPC message types
src/service/Handlers/                             # Request handlers
src/ui/WfpTrafficControl.UI/Services/IServiceClient.cs
src/ui/WfpTrafficControl.UI/Services/ServiceClient.cs
tests/UI/MockServiceClient.cs
```

### UI Patterns
```
src/ui/WfpTrafficControl.UI/ViewModels/           # ViewModel examples
src/ui/WfpTrafficControl.UI/Views/                # View examples
src/ui/WfpTrafficControl.UI/Services/IDialogService.cs
src/ui/WfpTrafficControl.UI/App.xaml              # Resources and styles
```

### Policy/Rules
```
src/shared/Policy/Policy.cs                       # Policy model
src/shared/Policy/PolicyRule.cs                   # Rule model
src/shared/Policy/PolicyValidator.cs              # Validation
src/shared/Policy/RuleCompiler.cs                 # Rule to filter compilation
```

### Testing
```
tests/UI/                                         # UI test patterns
tests/                                            # Service test patterns
```
