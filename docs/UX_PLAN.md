# UX Plan — WFP Traffic Control UI

> **Document Version:** 1.0.0
> **Created:** 2026-02-23
> **Status:** Proposed
> **Target Audience:** Development team, stakeholders

---

## Executive Summary

This document outlines the development plan for a configurable graphical user interface (GUI) for the WFP Traffic Control firewall system. The UI will complement the existing CLI (`wfpctl`) by providing an intuitive visual interface for policy management, real-time monitoring, and system administration.

**Key objectives:**
- Provide visual policy creation and management without requiring JSON knowledge
- Surface system status and audit logs in an accessible dashboard
- Maintain all safety guarantees (rollback, fail-open, LKG recovery) through the UI
- Target Windows 10/11 desktop environments

---

## 1. Current State Analysis

### 1.1 Existing System Capabilities

| Capability | CLI Command | UI Equivalent Needed |
|------------|-------------|---------------------|
| Service health check | `wfpctl status` | Dashboard status indicator |
| Policy validation | `wfpctl validate <file>` | Real-time validation feedback |
| Policy application | `wfpctl apply <file>` | Apply button with preview |
| Emergency rollback | `wfpctl rollback` | One-click panic button |
| LKG recovery | `wfpctl lkg revert` | Recovery wizard |
| Hot reload toggle | `wfpctl watch set/status` | Toggle switch with status |
| Audit log viewing | `wfpctl logs --tail N` | Filterable log viewer |
| WFP bootstrap | `wfpctl bootstrap` | Setup wizard |
| WFP teardown | `wfpctl teardown` | Cleanup wizard with warnings |
| Demo filter testing | `wfpctl demo-block *` | Testing/demo panel |

### 1.2 Data Contracts (From Service IPC)

The UI will communicate with the existing service via the same named pipe IPC protocol. Key request/response pairs:

- **PingRequest/Response** — Health + version info
- **ApplyRequest/Response** — Policy application with filter counts
- **ValidateRequest/Response** — Schema validation results
- **RollbackRequest/Response** — Filter removal confirmation
- **LkgShowRequest/Response** — Current LKG policy retrieval
- **LkgRevertRequest/Response** — Recovery execution
- **WatchRequest/Response** — Hot reload configuration
- **AuditLogsRequest/Response** — Log query results

### 1.3 Policy Model

```json
{
  "version": "1.0.0",
  "defaultAction": "allow",
  "updatedAt": "2024-01-15T10:30:00Z",
  "rules": [
    {
      "id": "unique-rule-id",
      "action": "allow|block",
      "direction": "inbound|outbound|both",
      "protocol": "tcp|udp|any",
      "process": "C:\\path\\to\\process.exe",
      "local": { "ip": "CIDR", "ports": "80,443,8080-8090" },
      "remote": { "ip": "CIDR", "ports": "port-spec" },
      "priority": 100,
      "enabled": true,
      "comment": "description"
    }
  ]
}
```

---

## 2. Technology Recommendation

### 2.1 Framework Selection

| Option | Pros | Cons | Recommendation |
|--------|------|------|----------------|
| **WPF (.NET 8)** | Mature, extensive controls, same runtime as service, strong data binding | Older appearance without custom styling | **Recommended** |
| **WinUI 3** | Modern Fluent design, future-proof | Packaging complexity (MSIX), less mature | Consider for v2 |
| **MAUI** | Cross-platform potential | Overkill for Windows-only tool | Not recommended |
| **Electron/Web** | Familiar web tech | Heavy runtime, security concerns | Not recommended |

**Selected:** WPF with .NET 8 and MaterialDesign or ModernWpf theme for contemporary appearance.

### 2.2 Rationale

1. **Runtime alignment** — Service and CLI already use .NET 8; UI shares assemblies
2. **IPC reuse** — Direct reuse of `WfpTrafficControl.Shared` models and serialization
3. **Admin-only context** — No need for sandboxing; WPF runs elevated naturally
4. **Maturity** — Extensive ecosystem for data grids, charting, and MVVM support

### 2.3 Architecture Pattern

**MVVM (Model-View-ViewModel)** with:
- **CommunityToolkit.Mvvm** for source generators (ObservableProperty, RelayCommand)
- **Dependency Injection** via Microsoft.Extensions.DependencyInjection
- **IPC Client abstraction** injected into ViewModels

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│      Views      │◄───│   ViewModels     │◄───│    Services     │
│  (XAML/WPF)     │    │  (State/Logic)   │    │   (IPC Client)  │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                       │
                                                       ▼
                                               Named Pipe IPC
                                                       │
                                                       ▼
                                               WFP Service
```

---

## 3. UI Structure & Screens

### 3.1 Application Shell

```
┌────────────────────────────────────────────────────────────────┐
│  WFP Traffic Control                               ─ □ ✕       │
├────────────────────────────────────────────────────────────────┤
│  [Dashboard] [Policy Editor] [Audit Logs] [Settings]          │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│                      < Content Area >                          │
│                                                                │
├────────────────────────────────────────────────────────────────┤
│  Status: ● Connected   Filters: 12 active   Last apply: 5m    │
└────────────────────────────────────────────────────────────────┘
```

**Navigation:** Tab-based or sidebar navigation (4-5 primary screens)

### 3.2 Screen Breakdown

#### Screen 1: Dashboard

**Purpose:** At-a-glance system status and quick actions

```
┌─────────────────────────────────────────────────────────────┐
│  DASHBOARD                                                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │   Service   │  │   Filters   │  │    Policy   │         │
│  │   ● Online  │  │     12      │  │   v1.0.0    │         │
│  │   v1.2.3    │  │   active    │  │   applied   │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  Hot Reload │  │     LKG     │  │   Sublayer  │         │
│  │   ○ Off     │  │  Available  │  │   Present   │         │
│  │             │  │  (backup)   │  │             │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                             │
│  ───────────────── Quick Actions ─────────────────         │
│                                                             │
│  [ Apply Policy... ] [ Rollback ] [ Revert to LKG ]        │
│                                                             │
│  ───────────────── Recent Activity ───────────────         │
│                                                             │
│  • 10:30 AM  Policy applied (5 rules, 12 filters)          │
│  • 10:15 AM  Service started                               │
│  • Yesterday  Rollback executed                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Components:**
- Status cards (service, filters, policy version)
- Hot reload toggle with watched file indicator
- Quick action buttons (Apply, Rollback, Revert)
- Recent activity feed (last 5 audit events)

#### Screen 2: Policy Editor

**Purpose:** Create, edit, and manage firewall rules visually

```
┌─────────────────────────────────────────────────────────────┐
│  POLICY EDITOR                      [ Load ] [ Save ] [+]  │
├─────────────────────────────────────────────────────────────┤
│  Policy: my-policy.json   Version: 1.0.0   Default: Allow  │
├────────┬────────────────────────────────────────────────────┤
│ Rules  │                 Rule Details                       │
├────────┼────────────────────────────────────────────────────┤
│        │  ID:        block-telemetry                       │
│ ☑ Rule1│  Action:    [Block ▼]                             │
│ ☑ Rule2│  Direction: [Outbound ▼]                          │
│ ☐ Rule3│  Protocol:  [TCP ▼]                               │
│        │                                                    │
│ [+ Add]│  Process:   [ Browse... ] C:\path\app.exe         │
│        │                                                    │
│        │  Remote IP:   [ 1.2.3.0/24    ]                   │
│        │  Remote Ports:[ 443,8443      ]                   │
│        │                                                    │
│        │  Priority:  [ 100        ]                        │
│        │  Comment:   [ Block telemetry endpoints ]         │
│        │                                                    │
│        │  ───────────────────────────────────────          │
│        │  Validation: ✓ Rule is valid                      │
└────────┴────────────────────────────────────────────────────┘
│                                                             │
│  [ Validate Policy ] [ Preview Filters ] [ Apply to Service]│
└─────────────────────────────────────────────────────────────┘
```

**Components:**
- Rule list (left panel) with checkboxes for enable/disable
- Rule detail form (right panel) with:
  - Action dropdown (Allow/Block)
  - Direction dropdown (Inbound/Outbound)
  - Protocol dropdown (TCP/UDP/Any)
  - Process path picker with browse button
  - Remote IP/CIDR input with validation
  - Remote ports input with range support (e.g., "80,443,8000-9000")
  - Priority number input
  - Comment text field
- Real-time validation feedback per field
- Toolbar: Load file, Save file, Add rule
- Footer: Validate all, Preview compiled filters, Apply

**Validation Features:**
- Inline field validation (red borders, error tooltips)
- Policy-level validation summary
- Duplicate rule ID detection
- Port range syntax checking
- CIDR format validation

#### Screen 3: Audit Logs

**Purpose:** View and search historical operations

```
┌─────────────────────────────────────────────────────────────┐
│  AUDIT LOGS                                                 │
├─────────────────────────────────────────────────────────────┤
│  Filter: [ All events    ▼]  Since: [Last 24 hours ▼]      │
│  Search: [                                        ] [🔍]    │
├─────────────────────────────────────────────────────────────┤
│  Timestamp          │ Event            │ Status  │ Details  │
├─────────────────────┼──────────────────┼─────────┼──────────┤
│  2026-02-23 10:30   │ apply-finished   │ success │ 5 rules  │
│  2026-02-23 10:30   │ apply-started    │ —       │ v1.0.0   │
│  2026-02-23 10:15   │ rollback-finished│ success │ 0 remain │
│  2026-02-23 09:45   │ apply-finished   │ failed  │ [view]   │
│  ...                │                  │         │          │
├─────────────────────────────────────────────────────────────┤
│  ─────────────────── Selected Entry ───────────────────    │
│                                                             │
│  {                                                          │
│    "ts": "2026-02-23T10:30:00.123Z",                       │
│    "event": "apply-finished",                              │
│    "status": "success",                                    │
│    "details": {                                            │
│      "filtersCreated": 12,                                 │
│      "filtersRemoved": 0,                                  │
│      "policyVersion": "1.0.0"                              │
│    }                                                        │
│  }                                                          │
│                                                             │
└─────────────────────────────────────────────────────────────┘
│  Showing 50 of 234 entries              [ Export CSV ]      │
└─────────────────────────────────────────────────────────────┘
```

**Components:**
- Filter dropdowns (event type, time range)
- Search box (text search across entries)
- Log table with columns: Timestamp, Event, Status, Summary
- Detail panel showing full JSON for selected entry
- Export functionality (CSV/JSON)

#### Screen 4: Settings

**Purpose:** Configure UI preferences and service options

```
┌─────────────────────────────────────────────────────────────┐
│  SETTINGS                                                   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ─────────────────── Connection ───────────────────        │
│                                                             │
│  Service Status:    ● Connected (v1.2.3)                   │
│  Pipe Name:         WfpTrafficControl (default)            │
│  Connection Timeout: [ 5 ] seconds                         │
│                                                             │
│  ─────────────────── Hot Reload ───────────────────        │
│                                                             │
│  Enable Hot Reload: [ ○ ]                                  │
│  Watched File:      [ Browse... ] C:\policies\policy.json  │
│                                                             │
│  ─────────────────── UI Preferences ───────────────        │
│                                                             │
│  Theme:             [ Dark ▼ ]                             │
│  Refresh Interval:  [ 5 ] seconds                          │
│  Show Notifications: [ ✓ ]                                 │
│                                                             │
│  ─────────────────── Advanced ─────────────────────        │
│                                                             │
│  [ Bootstrap WFP ]  [ Teardown WFP ]                       │
│  ⚠ Teardown removes all WFP infrastructure                 │
│                                                             │
│  Log File:  %ProgramData%\WfpTrafficControl\audit.log      │
│  LKG File:  %ProgramData%\WfpTrafficControl\lkg-policy.json│
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Components:**
- Connection settings (read-only status, timeout config)
- Hot reload configuration (enable toggle, file picker)
- UI preferences (theme, auto-refresh interval, notifications)
- Advanced section with Bootstrap/Teardown (with confirmation dialogs)
- File path references (for manual access)

---

## 4. Key User Flows

### 4.1 First-Time Setup

```
1. User launches UI (as Administrator)
2. UI checks service status via Ping
   ├─ Service running → Dashboard loads
   └─ Service not running → "Service Offline" overlay with instructions
3. UI checks WFP bootstrap status
   ├─ Bootstrapped → Ready to use
   └─ Not bootstrapped → Prompt to run Bootstrap (one-time setup)
4. User creates first policy in Policy Editor
5. User applies policy → Confirmation dialog → Apply
6. Dashboard updates with filter count
```

### 4.2 Daily Policy Update

```
1. User opens Policy Editor
2. Loads existing policy file (or creates new)
3. Adds/modifies rules with real-time validation
4. Clicks "Apply to Service"
5. Preview dialog shows:
   - Rules to apply: N
   - Filters to create: M
   - Filters to remove: K
6. User confirms → Apply executes
7. Success notification with summary
8. LKG automatically updated
```

### 4.3 Emergency Rollback

```
1. User notices connectivity issues
2. Opens Dashboard
3. Clicks "Rollback" button
4. Confirmation dialog: "Remove ALL filters? Traffic will be unfiltered."
5. User confirms → Rollback executes
6. Success notification: "All filters removed. Connectivity restored."
```

### 4.4 Recovery from Bad Policy

```
1. User applied a bad policy (e.g., blocks critical services)
2. Opens Dashboard (or uses CLI if UI unreachable)
3. Clicks "Revert to LKG"
4. Preview shows LKG policy version and date
5. User confirms → LKG restore executes
6. Filters recompiled from LKG
7. Success notification with restored policy details
```

---

## 5. Design Principles

### 5.1 Safety First

- **Confirmation dialogs** for all destructive operations (Apply, Rollback, Teardown)
- **Preview before commit** — Show what will change before applying
- **Visible rollback** — Rollback button always accessible on Dashboard
- **LKG indicator** — Clear indication that a recovery point exists

### 5.2 Progressive Disclosure

- **Dashboard** shows summary; details available on demand
- **Policy Editor** shows essential fields; advanced options expandable
- **Settings** separates common from advanced options

### 5.3 Immediate Feedback

- **Real-time validation** in forms (not just on submit)
- **Optimistic UI updates** with loading states
- **Toast notifications** for operation results
- **Error recovery guidance** (not just "Error occurred")

### 5.4 Accessibility

- **Keyboard navigation** for all controls
- **High contrast** theme option
- **Screen reader** compatible labels and announcements
- **Sufficient color contrast** (WCAG AA minimum)

---

## 6. Implementation Phases

### Phase 1: Foundation (MVP)

**Goal:** Basic functional UI that can replace core CLI operations

**Deliverables:**
1. WPF application shell with navigation
2. Dashboard screen with status indicators
3. Basic policy editor (load/save JSON, edit rules)
4. IPC client service (reuse shared models)
5. Apply and Rollback functionality

**Exit Criteria:**
- Can view service status
- Can load, edit, and apply a policy
- Can execute rollback
- Basic error handling

### Phase 2: Full Feature Parity

**Goal:** Match all CLI capabilities

**Deliverables:**
1. Audit log viewer with filtering/search
2. LKG show and revert functionality
3. Hot reload configuration
4. Policy validation with detailed feedback
5. Bootstrap/Teardown in Settings

**Exit Criteria:**
- All `wfpctl` commands available in UI
- Validation messages match CLI behavior

### Phase 3: Enhanced UX

**Goal:** Improve usability beyond CLI capabilities

**Deliverables:**
1. Visual rule builder with field pickers
2. Filter preview (show compiled filters before apply)
3. Drag-and-drop rule reordering
4. Rule templates (common patterns)
5. Dark/Light theme support
6. Notifications (system tray integration)

**Exit Criteria:**
- User can build policies without JSON knowledge
- Polished visual design

### Phase 4: Advanced Features

**Goal:** Power user and enterprise features

**Deliverables:**
1. Policy diff viewer (compare current vs. new)
2. Rule search and filtering
3. Export/import multiple formats
4. Session history (undo/redo)
5. Batch rule operations
6. Performance optimizations for large policies

**Exit Criteria:**
- Handles 1000+ rules smoothly
- Professional-grade UX

---

## 7. Technical Implementation Details

### 7.1 Project Structure

```
/src
  /ui
    /WfpTrafficControl.UI              # Main WPF application
      /Views                           # XAML views
        DashboardView.xaml
        PolicyEditorView.xaml
        AuditLogsView.xaml
        SettingsView.xaml
        MainWindow.xaml
      /ViewModels                      # MVVM ViewModels
        DashboardViewModel.cs
        PolicyEditorViewModel.cs
        AuditLogsViewModel.cs
        SettingsViewModel.cs
        MainViewModel.cs
      /Services                        # UI-specific services
        IServiceClient.cs              # IPC abstraction
        ServiceClient.cs               # Named pipe implementation
        IDialogService.cs
        DialogService.cs
      /Controls                        # Custom/reusable controls
        RuleEditorControl.xaml
        StatusCardControl.xaml
        LogEntryControl.xaml
      /Converters                      # Value converters
        BoolToVisibilityConverter.cs
        StatusToColorConverter.cs
      /Resources                       # Styles, themes
        Themes/
        Icons/
      App.xaml
      App.xaml.cs
```

### 7.2 Shared Code Reuse

The UI will reference `WfpTrafficControl.Shared` directly:

```xml
<ProjectReference Include="..\shared\WfpTrafficControl.Shared.csproj" />
```

**Reused components:**
- `Policy`, `Rule`, `EndpointFilter` models
- IPC message types (requests/responses)
- Policy validation logic
- JSON serialization settings
- Constants (GUIDs, paths, limits)

### 7.3 IPC Client Implementation

```csharp
public interface IServiceClient
{
    Task<PingResponse> PingAsync(CancellationToken ct = default);
    Task<ApplyResponse> ApplyAsync(string policyPath, CancellationToken ct = default);
    Task<RollbackResponse> RollbackAsync(CancellationToken ct = default);
    Task<LkgShowResponse> GetLkgAsync(CancellationToken ct = default);
    Task<LkgRevertResponse> RevertToLkgAsync(CancellationToken ct = default);
    Task<AuditLogsResponse> GetLogsAsync(int? tail = null, int? sinceMinutes = null, CancellationToken ct = default);
    Task<WatchResponse> SetWatchAsync(string? filePath, CancellationToken ct = default);
    Task<ValidateResponse> ValidateAsync(Policy policy, CancellationToken ct = default);
}
```

### 7.4 Error Handling Strategy

```csharp
// All IPC operations return Result<T> or throw typed exceptions
public class ServiceException : Exception
{
    public string ErrorCode { get; }
    public ServiceException(string code, string message) : base(message)
    {
        ErrorCode = code;
    }
}

// ViewModel pattern for operation results
public async Task ApplyPolicyAsync()
{
    try
    {
        IsLoading = true;
        var result = await _serviceClient.ApplyAsync(_policyPath);

        if (result.Success)
        {
            _dialogService.ShowSuccess($"Applied {result.FiltersCreated} filters");
        }
        else
        {
            _dialogService.ShowError($"Apply failed: {result.ErrorMessage}");
        }
    }
    catch (ServiceException ex)
    {
        _dialogService.ShowError($"Service error: {ex.Message}");
    }
    catch (TimeoutException)
    {
        _dialogService.ShowError("Service not responding. Check if service is running.");
    }
    finally
    {
        IsLoading = false;
    }
}
```

### 7.5 Elevation Requirements

The UI must run elevated (Administrator) because:
1. Named pipe ACL restricts access to Administrators
2. Consistent with CLI behavior
3. Simplifies authorization model

**Manifest configuration:**
```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

---

## 8. Testing Strategy

### 8.1 Unit Tests

- ViewModel logic (state transitions, validation)
- Value converters
- Service client message serialization

### 8.2 Integration Tests

- IPC communication with mock service
- End-to-end flows with test policies

### 8.3 Manual Testing Checklist

| Test Case | Steps | Expected Result |
|-----------|-------|-----------------|
| Service offline | Stop service, launch UI | "Service Offline" message |
| Apply valid policy | Load policy, click Apply | Success notification, filter count updated |
| Apply invalid policy | Load bad JSON, click Apply | Validation errors shown |
| Rollback | Click Rollback, confirm | All filters removed |
| LKG revert | Click Revert, confirm | LKG policy restored |
| Hot reload toggle | Enable, select file | Watch status updates |
| Audit log query | Set time filter | Logs displayed |

---

## 9. Dependencies & Prerequisites

### 9.1 NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| CommunityToolkit.Mvvm | 8.x | MVVM infrastructure |
| MaterialDesignThemes | 5.x | Modern WPF styling (or ModernWpf) |
| Microsoft.Extensions.DependencyInjection | 8.x | DI container |
| System.Text.Json | 8.x | JSON serialization (shared) |

### 9.2 Development Requirements

- Visual Studio 2022 with WPF workload
- .NET 8.0 SDK
- Windows 10/11 development machine
- Test VM with service installed

---

## 10. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Service communication failure | UI unusable | Graceful degradation, clear error messages, offline mode for validation |
| Policy too large for UI | Performance issues | Virtualized lists, pagination, progress indicators |
| User applies blocking policy | Locked out | Prominent rollback button, pre-apply warnings, LKG always available |
| Concurrent CLI/UI operations | State mismatch | Refresh on focus, polling status updates |
| Theme/styling complexity | Development delays | Start with default WPF, enhance in Phase 3 |

---

## 11. Success Metrics

### 11.1 Functional Completeness

- [ ] All CLI commands accessible via UI
- [ ] Policy editor supports full rule schema
- [ ] Real-time validation matches CLI validation
- [ ] Error messages are actionable

### 11.2 Usability

- [ ] New user can apply a policy in < 5 minutes
- [ ] Rollback achievable in < 3 clicks
- [ ] No JSON knowledge required for basic operations

### 11.3 Reliability

- [ ] UI handles service disconnection gracefully
- [ ] No data loss on UI crash (policies persisted)
- [ ] Operations are idempotent (safe retry)

---

## 12. Open Questions

1. **System tray integration?** — Should the UI minimize to tray for persistent monitoring?
2. **Auto-start with Windows?** — Should users be able to configure UI auto-launch?
3. **Multi-language support?** — Is localization required for v1?
4. **Policy version control?** — Should UI track policy history beyond LKG?
5. **Telemetry/analytics?** — Should UI report usage (opt-in)?

---

## 13. Appendix: Wireframe Summary

```
┌──────────────────────────────────────────────────────────────────────┐
│                         APPLICATION SHELL                             │
├──────────────────────────────────────────────────────────────────────┤
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐                │
│  │Dashboard │ │ Policy   │ │ Audit    │ │ Settings │                │
│  │          │ │ Editor   │ │ Logs     │ │          │                │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘                │
├──────────────────────────────────────────────────────────────────────┤
│                                                                       │
│                         CONTENT AREA                                  │
│                                                                       │
│   Dashboard: Status cards, quick actions, recent activity            │
│   Policy Editor: Rule list + detail form + toolbar                   │
│   Audit Logs: Filter bar + table + detail panel                      │
│   Settings: Grouped options with advanced section                    │
│                                                                       │
├──────────────────────────────────────────────────────────────────────┤
│  Status Bar: Connection • Filters • Last update                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 14. Next Steps

1. **Review this plan** with stakeholders
2. **Select UI framework** (recommend WPF + MaterialDesign)
3. **Set up UI project** in `/src/ui/`
4. **Implement Phase 1** (Foundation/MVP)
5. **User testing** with sample policies
6. **Iterate** through Phases 2-4

---

*Document prepared as part of the WFP Traffic Control project UI development initiative.*
