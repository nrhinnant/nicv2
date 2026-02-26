# 022 — How It Works: Architecture and Policy Flow

## Overview

This document provides a high-level architectural overview of the WFP Traffic Control system, explaining how components interact, how policy flows through the system, and the guarantees provided for safety and reliability.

**Target audience:** Engineers seeking to understand the system architecture before diving into implementation details.

---

## System Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         User Space                              │
│                                                                 │
│  ┌──────────┐                                                   │
│  │   CLI    │  wfpctl apply / rollback / status / logs         │
│  │ (wfpctl) │                                                   │
│  └────┬─────┘                                                   │
│       │ IPC (Named Pipe)                                        │
│       │ - Admin-only ACL                                        │
│       │ - Length-prefixed JSON                                  │
│       │ - Protocol versioning                                   │
│       ▼                                                          │
│  ┌───────────────────────────────────────────────────┐          │
│  │  Windows Service (LocalSystem)                    │          │
│  │  ┌──────────────────────────────────────────┐    │          │
│  │  │  PipeServer (IPC Handler)                │    │          │
│  │  │  - Authorization (admin-only)            │    │          │
│  │  │  - Request routing                       │    │          │
│  │  │  - Audit logging                         │    │          │
│  │  └───────────────┬──────────────────────────┘    │          │
│  │                  │                                │          │
│  │  ┌───────────────▼──────────────────────────┐    │          │
│  │  │  WfpEngine                               │    │          │
│  │  │  - Provider/sublayer management          │    │          │
│  │  │  - Filter reconciliation                 │    │          │
│  │  │  - Transaction management                │    │          │
│  │  │  - Rollback logic                        │    │          │
│  │  └───────────────┬──────────────────────────┘    │          │
│  │                  │                                │          │
│  │  ┌───────────────▼──────────────────────────┐    │          │
│  │  │  RuleCompiler                            │    │          │
│  │  │  - Policy validation                     │    │          │
│  │  │  - Rule → Filter compilation             │    │          │
│  │  │  - Deterministic GUID generation         │    │          │
│  │  └──────────────────────────────────────────┘    │          │
│  │                                                   │          │
│  │  ┌────────────────────────────────────────┐      │          │
│  │  │  PolicyFileWatcher (Hot Reload)        │      │          │
│  │  │  - Debounced file watching             │      │          │
│  │  │  - Auto-apply on change                │      │          │
│  │  └────────────────────────────────────────┘      │          │
│  └───────────────────────────────────────────────────┘          │
│                                                                 │
│  ┌───────────────────────────────────────────────────┐          │
│  │  Policy Store                                     │          │
│  │  %ProgramData%\WfpTrafficControl\                 │          │
│  │  - lkg-policy.json (last known good)              │          │
│  │  - audit.log (control-plane events)               │          │
│  └───────────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────┘
                          │
                          │ Fwpm* APIs (user-mode WFP management)
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Kernel Space                             │
│                                                                 │
│  ┌───────────────────────────────────────────────────┐          │
│  │  Windows Filtering Platform (WFP)                │          │
│  │  ┌────────────────────────────────────────────┐  │          │
│  │  │  Base Filtering Engine (BFE Service)      │  │          │
│  │  └────────────────────────────────────────────┘  │          │
│  │  ┌────────────────────────────────────────────┐  │          │
│  │  │  Our Provider                             │  │          │
│  │  │  - GUID: 6c3e3f71-...                     │  │          │
│  │  │  - Display Name: "WFP Traffic Control"    │  │          │
│  │  └────────────────────────────────────────────┘  │          │
│  │  ┌────────────────────────────────────────────┐  │          │
│  │  │  Our Sublayer (weight: 0x8000)            │  │          │
│  │  │  - GUID: a8f9d7c6-...                     │  │          │
│  │  │  - All our filters live here              │  │          │
│  │  └────────────────────────────────────────────┘  │          │
│  │  ┌────────────────────────────────────────────┐  │          │
│  │  │  Filters (in our sublayer)                │  │          │
│  │  │  - ALE_AUTH_CONNECT_V4 (outbound TCP/UDP) │  │          │
│  │  │  - ALE_AUTH_RECV_ACCEPT_V4 (inbound)      │  │          │
│  │  │  - Each with deterministic GUID           │  │          │
│  │  └────────────────────────────────────────────┘  │          │
│  └───────────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Entry Point |
|-----------|----------------|-------------|
| **CLI** | User interface, IPC client, displays results | [src/cli/Program.cs](../../src/cli/Program.cs) |
| **PipeServer** | IPC request handling, authorization, audit logging | [src/service/Ipc/PipeServer.cs](../../src/service/Ipc/PipeServer.cs) |
| **WfpEngine** | WFP object lifecycle, filter reconciliation, transactions | [src/service/Wfp/WfpEngine.cs](../../src/service/Wfp/WfpEngine.cs) |
| **RuleCompiler** | Policy validation, rule → filter compilation | [src/shared/Policy/RuleCompiler.cs](../../src/shared/Policy/RuleCompiler.cs) |
| **PolicyValidator** | Schema validation, constraint checking | [src/shared/Policy/PolicyValidator.cs](../../src/shared/Policy/PolicyValidator.cs) |
| **LkgStore** | Last Known Good persistence, integrity verification | [src/shared/Lkg/LkgStore.cs](../../src/shared/Lkg/LkgStore.cs) |
| **PolicyFileWatcher** | Hot reload (debounced file watching) | [src/service/FileWatcher.cs](../../src/service/FileWatcher.cs) |

### Service Startup Flow

When the Windows Service starts ([Worker.cs:35](../../src/service/Worker.cs#L35)):

1. **Initialize WfpEngine** - Creates engine with real WfpInterop (production) or mock (testing)
2. **Initialize PolicyFileWatcher** - Sets up file watcher with debounce configuration
3. **Start PipeServer** - Begins listening on named pipe for CLI connections
4. **Auto-apply LKG (optional)** - If enabled in config, loads and applies Last Known Good policy
   - **Fail-open guarantee:** If LKG is missing, corrupt, or fails to apply, service starts successfully with no policy (all traffic allowed)
5. **Enter idle loop** - Service stays running, waiting for IPC requests or file watcher events

See [014-lkg-and-failopen.md](014-lkg-and-failopen.md) for LKG behavior details.

---

## Policy Flow

### Apply Policy Lifecycle

The complete flow from user command to active WFP filters:

```
User:     wfpctl apply policy.json
             │
             ▼
CLI:      1. Read policy file
          2. Send ApplyRequest via named pipe
             │
             ▼
PipeServer: 3. Authorize (admin-only check)
            4. Audit log: apply-started
            5. Route to handler
               │
               ▼
Handler:    6. Load policy JSON
            7. Validate schema (PolicyValidator)
            8. Compile rules (RuleCompiler)
               │
               ▼
RuleCompiler: 9. For each enabled rule:
                 - Validate direction/protocol/ports
                 - Generate deterministic filter GUID
                 - Create CompiledFilter objects
              10. Return CompilationResult
                  - Filters: List<CompiledFilter>
                  - Errors: List<CompilationError>
                  - SkippedRules: count
                  │
                  ▼
WfpEngine:  11. ApplyFilters (idempotent reconciliation)
                a. Open WFP engine session
                b. Enumerate existing filters in our sublayer
                c. Compute diff (FilterDiffComputer)
                   - ToAdd: filters in desired but not in current
                   - ToRemove: filters in current but not in desired
                   - Unchanged: filters in both
                d. Begin WFP transaction
                e. Delete obsolete filters (ToRemove)
                f. Create new filters (ToAdd)
                g. Commit transaction
            12. Return ApplyResult
                - FiltersCreated: count
                - FiltersRemoved: count
                - FiltersUnchanged: count
                │
                ▼
PipeServer: 13. Save LKG (if apply succeeded)
            14. Audit log: apply-finished (success/failure)
            15. Send ApplyResponse to CLI
                │
                ▼
CLI:        16. Display results to user
```

**Key properties:**
- **Idempotent:** Re-applying the same policy makes no changes (see Reconciliation Model below)
- **Atomic:** All filter changes happen in a transaction; failure aborts with no partial state
- **Fail-safe:** Compilation or apply failures leave existing filters unchanged

### Rollback Flow

Simplified flow for `wfpctl rollback`:

```
CLI → PipeServer → WfpEngine.RemoveAllFilters()
                     │
                     ▼
                   1. Enumerate all filters in our sublayer
                   2. Begin transaction
                   3. Delete each filter by ID
                   4. Commit transaction
                   5. Return count removed
```

**Guarantees:**
- Removes **all** filters in our sublayer
- Preserves provider and sublayer (quick re-apply possible)
- Idempotent (safe to call multiple times)
- Does **not** modify LKG (LKG revert still available)

See [010-panic-rollback.md](010-panic-rollback.md) for details.

---

## Reconciliation Model

### Why Reconciliation?

Early versions used a "delete all, add all" approach on every apply. This caused:
- Unnecessary WFP state churn (all filters recreated even if unchanged)
- Brief enforcement gaps (during delete phase)
- Noisy audit logs (every apply showed all filters removed/added)

The reconciliation model (feature 013) solves this with **idempotent apply**.

### How Reconciliation Works

**Core principle:** Only make the minimum changes needed to reach desired state.

1. **Deterministic Filter GUIDs**
   - Each rule generates a deterministic GUID based on:
     - Rule ID
     - Port index (for rules with multiple port ranges)
   - Same rule → same GUID every time
   - Different rules → different GUIDs

2. **Diff Computation** ([FilterDiff.cs](../../src/shared/Policy/FilterDiff.cs))
   - **Current state:** Enumerate all filters in our sublayer (GUID → filter ID map)
   - **Desired state:** Compile policy to list of CompiledFilters with GUIDs
   - **Compute:**
     - `ToAdd`: GUIDs in desired but not in current
     - `ToRemove`: GUIDs in current but not in desired
     - `Unchanged`: GUIDs in both (count only)

3. **Minimal Apply**
   - If diff is empty (`ToAdd` and `ToRemove` both empty), skip transaction entirely
   - Otherwise, apply only the changes:
     - Delete filters in `ToRemove`
     - Create filters in `ToAdd`
     - Leave `Unchanged` filters untouched

### Example: Idempotent Apply

**Initial apply:**
```
Policy: 3 rules (A, B, C)
Current state: empty
Diff: ToAdd=[A, B, C], ToRemove=[], Unchanged=0
Result: 3 filters created, 0 removed, 0 unchanged
```

**Re-apply same policy:**
```
Policy: 3 rules (A, B, C)
Current state: A, B, C
Diff: ToAdd=[], ToRemove=[], Unchanged=3
Result: 0 filters created, 0 removed, 3 unchanged (transaction skipped)
```

**Modify policy (remove B, add D):**
```
Policy: 3 rules (A, C, D)
Current state: A, B, C
Diff: ToAdd=[D], ToRemove=[B], Unchanged=2
Result: 1 filter created, 1 removed, 2 unchanged
```

### Benefits

- **True idempotency:** Same input → no changes
- **Minimal churn:** Only affected filters are modified
- **No enforcement gap:** Unchanged filters remain active throughout
- **Clear audit logs:** Counts show exactly what changed

See [013-idempotent-reconcile.md](013-idempotent-reconcile.md) for implementation details.

---

## Rollback Guarantees

This system provides multiple layers of safety guarantees to prevent connectivity loss.

### 1. Transactional Safety (Feature 008)

All WFP modifications happen in transactions:

```csharp
using var transaction = WfpTransaction.Begin(engineHandle);
// ... modify filters ...
transaction.Commit(); // All or nothing
// If Commit fails or exception occurs, Dispose aborts
```

**Guarantee:** Either all changes succeed, or none do. No partial state.

**Scope:**
- Provider/sublayer creation
- Filter adds/removes
- Provider/sublayer removal

See [008-wfp-transactions.md](008-wfp-transactions.md) for API details.

### 2. Panic Rollback (Feature 010)

The `wfpctl rollback` command provides immediate recovery:

- **Purpose:** Remove all filters instantly to restore connectivity
- **Mechanism:** Enumerate and delete all filters in our sublayer
- **Idempotent:** Safe to call repeatedly
- **Fast:** Single transaction, no policy parsing
- **Preserves infrastructure:** Provider and sublayer remain (fast re-apply)

**Use case:** Policy blocked critical connectivity; need immediate recovery.

See [010-panic-rollback.md](010-panic-rollback.md) for implementation.

### 3. Fail-Open Behavior (Feature 014)

The service is designed to **never brick the machine**:

| Failure Condition | Behavior | Rationale |
|-------------------|----------|-----------|
| LKG missing on startup | Start with no policy (allow all) | First run or LKG deleted |
| LKG corrupt on startup | Start with no policy (allow all) | Integrity failure, don't trust |
| LKG apply fails on startup | Start with no policy (allow all) | Stale policy may be incompatible |
| Policy validation fails | Reject apply, keep existing filters | Bad input, existing state is safe |
| Policy compilation fails | Reject apply, keep existing filters | Unsupported features, existing state is safe |
| Transaction fails during apply | Abort transaction, keep existing filters | WFP error, atomic rollback |

**Core principle:** When in doubt, allow traffic. Prefer availability over enforcement.

See [014-lkg-and-failopen.md](014-lkg-and-failopen.md) for fail-open design.

### 4. Last Known Good (LKG) Recovery

Every successful apply saves the policy as LKG:

- **Location:** `%ProgramData%\WfpTrafficControl\lkg-policy.json`
- **Integrity:** SHA256 checksum verified on load
- **Recovery:** `wfpctl lkg revert` applies the stored policy

**Use case:** After a bad policy apply, quickly revert to the last working state.

**Workflow:**
```powershell
wfpctl apply bad-policy.json  # Oops, this breaks things
wfpctl rollback               # Immediate recovery (no filters)
wfpctl lkg revert             # Restore previous working policy
```

### 5. Service Restart Safety

**If service crashes or is restarted:**
- WFP filters remain active (managed by BFE, not our process)
- On restart, service starts with no policy (fail-open)
- If auto-apply LKG is enabled, LKG is automatically restored

**Manual recovery:**
```powershell
net stop WfpTrafficControl
net start WfpTrafficControl
wfpctl lkg revert  # Or apply a new policy
```

### Rollback Decision Matrix

| Scenario | Recommended Action | Rationale |
|----------|-------------------|-----------|
| Policy broke connectivity | `wfpctl rollback` | Immediate recovery |
| Want to restore previous policy | `wfpctl lkg revert` | Return to known good state |
| Need to test with no policy | `wfpctl rollback` | Clean slate |
| Uninstalling system | `wfpctl rollback` → `wfpctl teardown` | Full cleanup |
| Service not responding | `net stop WfpTrafficControl` → Rollback via PowerShell WFP APIs | Bypass service |

---

## Hot Reload (Feature 017)

The system supports automatic policy reload when the watched file changes:

**Enable hot reload:**
```powershell
wfpctl watch enable C:\policies\policy.json
```

**Behavior:**
1. File watcher monitors the specified file
2. On change, debounce timer starts (default 2 seconds)
3. After debounce, policy is loaded, validated, compiled, and applied
4. Success/failure logged to audit log
5. CLI can query watch status: `wfpctl watch status`

**Use case:** Policy development with instant feedback.

**Disable:**
```powershell
wfpctl watch disable
```

See [017-hot-reload.md](017-hot-reload.md) for implementation.

---

## Audit Logging (Feature 018)

All control-plane events are logged to `%ProgramData%\WfpTrafficControl\audit.log`:

**Events logged:**
- apply-started / apply-finished
- rollback-started / rollback-finished
- teardown-started / teardown-finished
- lkg-revert-started / lkg-revert-finished

**Format:** JSON Lines (one event per line)

**Query:**
```powershell
wfpctl logs --tail 20         # Last 20 entries
wfpctl logs --since 60        # Last hour
```

**Security:**
- Full file paths redacted to filename only
- No sensitive data (IPs, ports, credentials) logged
- Useful for troubleshooting and compliance

See [018-audit-logging.md](018-audit-logging.md) for schema and examples.

---

## IPC Security (Feature 019)

The named pipe uses defense-in-depth security:

1. **OS-level ACL:** Only Administrators and LocalSystem can connect to the pipe
2. **Application-level authorization:** Service impersonates client and checks admin privileges
3. **Protocol versioning:** Mismatched CLI/service versions are rejected with clear error
4. **Size limits:** Maximum 64 KB request size
5. **Timeouts:** Read/write timeouts prevent hung connections

**Result:** Non-admin users receive "Access Denied" before reaching the service.

See [019-ipc-security.md](019-ipc-security.md) for details.

---

## Related Documentation

- [000-project-overview.md](000-project-overview.md) - Project goals and constraints
- [011-policy-schema-v1.md](011-policy-schema-v1.md) - Policy JSON schema and validation
- [013-idempotent-reconcile.md](013-idempotent-reconcile.md) - Reconciliation implementation
- [014-lkg-and-failopen.md](014-lkg-and-failopen.md) - Fail-open design and LKG persistence
- [023-troubleshooting.md](023-troubleshooting.md) - Troubleshooting guide (next doc)

---

## Future Enhancements (Not Yet Implemented)

- **Policy hot reload on LKG change:** Currently only watched file triggers reload
- **Per-connection traffic logging:** Requires kernel callout driver
- **IPv6 support:** Currently IPv4 only
- **Multiple LKG versions:** Currently only one LKG stored
- **Remote management:** Currently local IPC only
