# NEIL_TASKS.md — Project Improvement Backlog

## Purpose

This file tracks improvements and enhancements for the WFP Traffic Control project that have been identified but **not yet started**. The goal is to make this project convincing to deeply skilled Windows engineers as evidence that AI tools can build production-quality Windows infrastructure.

**Audience:**
- Neil (project owner) for prioritization and tracking
- Future Claude Code sessions for context on what work remains

**How to use this file:**
1. Tasks are organized by priority (P1 = critical, P4 = nice-to-have)
2. Each task includes rationale, implementation notes, and acceptance criteria
3. When a task is completed, move it to the "Completed" section at the bottom with the completion date
4. Future Claude sessions should read this file to understand outstanding work

**Created:** 2026-02-23
**Last Updated:** 2026-02-23 (added P3-06: WPF GUI)

---

## Priority 1: Critical Gaps

These gaps would cause immediate skepticism from experienced Windows engineers and undermine the "production quality" claim.

---

### P1-01: Implement IPv6 Support

**Status:** Not Started

**Why Critical:**
The project explicitly blocks IPv6 addresses with an error. Modern Windows networks are dual-stack by default. A "production firewall" that only handles IPv4 will leak IPv6 traffic entirely, making it ineffective in real deployments.

**Current Behavior:**
- `PolicyValidator` rejects IPv6 addresses with an error
- `RuleCompiler` only generates IPv4 filter conditions
- WFP layers used are V4-only (`FWPM_LAYER_ALE_AUTH_CONNECT_V4`, etc.)

**Implementation Notes:**
1. Add V6 layer constants to `WfpConstants.cs`:
   - `FWPM_LAYER_ALE_AUTH_CONNECT_V6`
   - `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V6`
2. Update `PolicyValidator` to accept IPv6 addresses and CIDR notation
3. Update `RuleCompiler` to:
   - Detect IPv4 vs IPv6 addresses
   - Generate appropriate layer and condition types
   - For rules without IP specified, generate BOTH v4 and v6 filters
4. Update `NativeMethods.cs` with any needed V6 condition structures
5. Add unit tests for IPv6 policy validation and compilation
6. Add PowerShell test script `Test-IPv6Block.ps1`
7. Update feature docs (011, 012, 015, 016) to document IPv6 support

**Acceptance Criteria:**
- [ ] IPv6 addresses (e.g., `2001:db8::1`) accepted in policy JSON
- [ ] IPv6 CIDR ranges (e.g., `2001:db8::/32`) work correctly
- [ ] Rules without IP constraint block both IPv4 and IPv6 traffic
- [ ] Existing IPv4 tests still pass
- [ ] New IPv6 tests pass in VM environment

**Estimated Effort:** Medium (2-3 days)

**References:**
- [WFP Layer Identifiers](https://docs.microsoft.com/en-us/windows/win32/fwp/management-filtering-layer-identifiers-)
- Current implementation: `src/shared/Policy/RuleCompiler.cs`

---

### P1-02: Execute Static Analysis Plan

**Status:** Not Started

**Why Critical:**
The project has an excellent 950-line static analysis plan (`docs/STATIC_ANALYSIS_PLAN.md`) but none of it is implemented. A Windows engineer reviewing this would immediately run `dotnet build -warnaserror` and check for analyzer configuration. Finding none undermines the "production quality" claim.

**Current State:**
- `STATIC_ANALYSIS_PLAN.md` exists with comprehensive tool selection
- No `.globalconfig` file exists
- No `Directory.Build.props` with analyzer packages
- No `Directory.Build.targets` for CI integration
- No `.semgrep.yml` custom rules
- No `scripts/Analyze-Local.ps1`
- No `scripts/Find-PInvoke.ps1`

**Implementation Notes:**
1. Create `.globalconfig` at solution root (per Section 3.1 of plan)
2. Create `Directory.Build.props` adding these analyzers to all projects:
   - `Microsoft.CodeAnalysis.NetAnalyzers` (built-in)
   - `Roslynator.Analyzers`
   - `SecurityCodeScan.VS2019`
   - `Microsoft.Interop.Analyzers`
3. Create `Directory.Build.targets` for CI integration
4. Create `.semgrep.yml` with WFP-specific rules
5. Create `scripts/Analyze-Local.ps1` runner script
6. Create `scripts/Find-PInvoke.ps1` audit script
7. Run analysis and fix any warnings
8. Ensure `dotnet build -warnaserror` passes

**Acceptance Criteria:**
- [ ] `dotnet build` shows analyzer warnings in IDE
- [ ] `dotnet build -warnaserror` passes with zero warnings (or documented suppressions)
- [ ] `scripts/Analyze-Local.ps1` runs successfully
- [ ] `scripts/Find-PInvoke.ps1` produces audit report
- [ ] Any suppressions have documented justifications

**Estimated Effort:** Low-Medium (1-2 days)

**References:**
- Full plan: `docs/STATIC_ANALYSIS_PLAN.md`
- Section 7.3 has the implementation prompt for Claude

---

### P1-03: Implement Inbound UDP Support

**Status:** Not Started

**Why Critical:**
The protocol/direction matrix is incomplete:
- Outbound TCP: Feature 012 ✓
- Inbound TCP: Feature 015 ✓
- Outbound UDP: Feature 016 ✓
- Inbound UDP: **Missing**

A skilled engineer will notice this gap immediately when reviewing feature coverage.

**Current State:**
- `RuleCompiler` handles outbound UDP via `FWPM_LAYER_ALE_AUTH_CONNECT_V4`
- No handling for inbound UDP
- Rules with `direction: "inbound"` and `protocol: "udp"` likely produce compilation errors or are silently skipped

**Implementation Notes:**
1. Add layer constant for inbound UDP (same layer as inbound TCP: `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4`)
2. Update `RuleCompiler.CompileRule()` to handle inbound + UDP combination
3. Add unit tests in `RuleCompilerTests.cs`
4. Create `Test-InboundUdpBlock.ps1` script
5. Create feature doc `docs/features/017-inbound-udp.md` (renumber hot-reload if needed)
6. Update `docs/features/022-how-it-works.md` protocol matrix

**Acceptance Criteria:**
- [ ] Policy with `direction: "inbound", protocol: "udp"` compiles successfully
- [ ] Inbound UDP traffic to blocked port is dropped
- [ ] Inbound UDP to non-blocked port is allowed
- [ ] Test script validates behavior in VM

**Estimated Effort:** Low (0.5-1 day)

**References:**
- Similar implementation: `docs/features/016-outbound-udp.md`
- Code: `src/shared/Policy/RuleCompiler.cs`

---

## Priority 2: Credibility Gaps

These gaps would cause raised eyebrows from experienced engineers—they won't reject the project outright, but will question the production-readiness claims.

---

### P2-01: Add ETW EventSource for Production Observability

**Status:** Not Started

**Why Important:**
`CLAUDE.md` states "Prefer ETW for high-rate logs" but the implementation uses file-based logging only. Windows engineers expect `EventSource`/ETW for production services because:
- Zero performance impact when not collecting
- Can be enabled dynamically without restart
- Integrates with Windows diagnostic tools (WPR, PerfView, logman)
- Structured events with strong typing

**Current State:**
- `AuditLogWriter` writes JSON Lines to file
- Service uses `Microsoft.Extensions.Logging` to console/file
- No ETW provider defined

**Implementation Notes:**
1. Create `WfpTrafficControlEventSource.cs` with `[EventSource]` attribute
2. Define events for:
   - PolicyApplied (policy version, rule count, duration)
   - PolicyRolledBack (reason)
   - FilterAdded (filter ID, rule ID)
   - FilterRemoved (filter ID)
   - ConnectionBlocked (if observable from user-mode)
   - ServiceStarted / ServiceStopped
   - Error events with error codes
3. Call EventSource methods alongside existing logging
4. Document how to capture with `logman` or PerfView
5. Add to troubleshooting guide

**Acceptance Criteria:**
- [ ] EventSource provider registered and discoverable
- [ ] Key operations emit ETW events
- [ ] `logman` can capture events to .etl file
- [ ] Documentation explains how to capture and view traces

**Estimated Effort:** Medium (1-2 days)

**References:**
- [EventSource Class](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.tracing.eventsource)
- [PerfView](https://github.com/microsoft/perfview)

---

### P2-02: Add CI Pipeline with GitHub Actions

**Status:** Not Started

**Why Important:**
The static analysis plan includes `.github/workflows/static-analysis.yml` but it doesn't exist. There's no evidence the tests actually pass. A passing CI badge in the README provides immediate credibility.

**Current State:**
- No `.github/workflows/` directory
- No CI configuration
- Tests exist but no proof they pass

**Implementation Notes:**
1. Create `.github/workflows/build-and-test.yml`:
   - Trigger on push/PR to main
   - Run on `windows-latest`
   - Steps: checkout, setup .NET 8, restore, build, test
   - Upload test results as artifact
2. Create `.github/workflows/static-analysis.yml` (per STATIC_ANALYSIS_PLAN.md Section 4.3)
3. Add status badges to README.md
4. Ensure all 661 tests pass in CI

**Acceptance Criteria:**
- [ ] CI runs on every push and PR
- [ ] Build succeeds
- [ ] All tests pass
- [ ] Status badge shows in README
- [ ] Static analysis runs (at least format check + vulnerability scan)

**Estimated Effort:** Low (0.5-1 day)

**References:**
- Plan: `docs/STATIC_ANALYSIS_PLAN.md` Section 4.3
- [GitHub Actions for .NET](https://docs.github.com/en/actions/automating-builds-and-tests/building-and-testing-net)

---

### P2-03: Clarify Process Path Matching Semantics

**Status:** Not Started

**Why Important:**
The documentation is ambiguous about whether policy rules match by:
- Full path: `C:\Program Files\Google\Chrome\Application\chrome.exe`
- Image name only: `chrome.exe`
- Wildcard patterns: `*\chrome.exe`

This is fundamental to policy authoring. A user cannot write correct policies without knowing this.

**Current State:**
- `RuleCompiler` calls `FwpmGetAppIdFromFileName0` but docs don't clarify input requirements
- No examples in policy schema doc
- No unit tests explicitly verifying the behavior

**Implementation Notes:**
1. Research and document exact WFP behavior:
   - Does `FwpmGetAppIdFromFileName0` require full path?
   - Does it resolve environment variables?
   - Are wildcards supported?
2. Update `docs/features/011-policy-schema-v1.md` with:
   - Clear specification of `process` field format
   - Multiple examples (full path, with spaces, etc.)
   - Error cases (what happens with invalid paths)
3. Add unit tests in `RuleCompilerTests.cs` or `PolicyValidatorTests.cs` verifying behavior
4. Consider adding path normalization/validation in `PolicyValidator`

**Acceptance Criteria:**
- [ ] Documentation clearly states required format
- [ ] At least 3 examples in docs
- [ ] Unit tests verify expected behavior
- [ ] Invalid paths produce clear error messages

**Estimated Effort:** Low (0.5 day)

**References:**
- [FwpmGetAppIdFromFileName0](https://docs.microsoft.com/en-us/windows/win32/api/fwpmu/nf-fwpmu-fwpmgetappidfromfilename0)
- Current code: `src/service/Wfp/WfpInterop.cs`

---

## Priority 3: Polish

These improvements would impress engineers and strengthen the "production quality" argument, but aren't blockers.

---

### P3-01: Record a Working Video Demo

**Status:** Not Started

**Why Valuable:**
No amount of documentation beats visual proof. A 2-3 minute video showing the system actually working is immediately convincing.

**Implementation Notes:**
1. Set up clean Windows VM
2. Record (OBS or Windows built-in) showing:
   - Service startup with bootstrap
   - `wfpctl status` showing healthy
   - Apply policy blocking port 443 to a specific IP
   - `curl https://<ip>` times out (blocked)
   - `wfpctl rollback`
   - `curl https://<ip>` succeeds (unblocked)
   - `wfpctl logs --tail` showing audit entries
3. Keep it concise (under 3 minutes)
4. Host on YouTube (unlisted) or include in repo
5. Link from README.md and EXECUTIVE_SUMMARY.md

**Acceptance Criteria:**
- [ ] Video clearly shows policy enforcement working
- [ ] Video is under 3 minutes
- [ ] Linked from README

**Estimated Effort:** Low (1-2 hours)

---

### P3-02: Publish Benchmark Results

**Status:** Not Started

**Why Valuable:**
Benchmarks exist (`benchmarks/*.cs`) but no results are published. Running and documenting results shows you measured performance, not just wrote code.

**Implementation Notes:**
1. Run benchmarks: `dotnet run -c Release --project benchmarks`
2. Capture results for:
   - `RuleCompilerBenchmarks` (rules/ms for various policy sizes)
   - `PolicyValidatorBenchmarks` (validation throughput)
   - `FilterDiffBenchmarks` (diff computation speed)
3. Create `docs/BENCHMARK_RESULTS.md` with:
   - Hardware specs (CPU, RAM, Windows version)
   - Results table
   - Analysis (e.g., "10K rules compile in Xms")
4. Link from README and EXECUTIVE_SUMMARY

**Acceptance Criteria:**
- [ ] Benchmark results documented
- [ ] Hardware/environment specified
- [ ] Linked from README

**Estimated Effort:** Low (1-2 hours)

---

### P3-03: Add ICMP Protocol Support

**Status:** Not Started

**Why Valuable:**
TCP/UDP only is limiting. ICMP (ping) is commonly blocked in security policies. Adding it demonstrates broader protocol coverage.

**Implementation Notes:**
1. Research WFP ICMP filtering (may require different layer or condition types)
2. Add `"icmp"` as valid protocol value in `PolicyValidator`
3. Update `RuleCompiler` to generate ICMP filter conditions
4. Add unit tests
5. Create `Test-IcmpBlock.ps1` script
6. Update feature docs

**Note:** ICMP filtering may work differently than TCP/UDP at the ALE layer. Research required.

**Acceptance Criteria:**
- [ ] `protocol: "icmp"` accepted in policy
- [ ] ICMP echo requests blocked when rule matches
- [ ] Test script validates behavior

**Estimated Effort:** Medium (depends on WFP ICMP complexity)

**References:**
- [WFP and ICMP](https://docs.microsoft.com/en-us/windows/win32/fwp/filtering-conditions-available-at-each-filtering-layer)

---

### P3-04: Document Stress Test Results

**Status:** Not Started

**Why Valuable:**
`Test-LargePolicyStress.ps1` exists but results aren't documented. What's the max rule count? How long does 10K rules take to apply?

**Implementation Notes:**
1. Run `Test-LargePolicyStress.ps1` with various rule counts (100, 1K, 5K, 10K)
2. Measure:
   - Policy compilation time
   - WFP apply time
   - Memory usage
   - Any failures or degradation
3. Document results in `docs/PERFORMANCE.md` or add to benchmark results
4. Identify practical limits

**Acceptance Criteria:**
- [ ] Stress test run at multiple scale points
- [ ] Results documented with timings
- [ ] Practical limits identified

**Estimated Effort:** Low (2-3 hours)

---

### P3-05: Document Windows Version Compatibility

**Status:** Not Started

**Why Valuable:**
WFP behavior can differ across Windows versions. Documenting tested versions shows deployment readiness.

**Implementation Notes:**
1. Test on multiple Windows versions:
   - Windows 10 21H2 / 22H2
   - Windows 11 22H2 / 23H2
   - Windows Server 2019
   - Windows Server 2022
2. Document any version-specific behaviors
3. Note minimum supported version
4. Add to troubleshooting guide

**Acceptance Criteria:**
- [ ] At least 3 Windows versions tested
- [ ] Results documented
- [ ] Any version-specific issues noted

**Estimated Effort:** Medium (requires multiple VMs)

---

### P3-06: Implement WPF GUI Application

**Status:** Not Started
**NEIL'S NOTE** See claude-produced prompt for UI generation! 

**Why Valuable:**
A graphical user interface demonstrates that AI can build complete end-user applications, not just backend services. It also makes the project more accessible for demonstrations and expands the audience beyond CLI-savvy engineers.

**Full Plan:** See `docs/UX_PLAN.md` for comprehensive design document (743 lines).

**Summary:**
Build a WPF (.NET 8) desktop application with MVVM architecture that provides visual policy management, real-time monitoring, and system administration capabilities—complementing the existing CLI.

**Technology Selection:**
- **Framework:** WPF with .NET 8 (same runtime as service/CLI)
- **Styling:** MaterialDesign or ModernWpf theme
- **Pattern:** MVVM with CommunityToolkit.Mvvm
- **IPC:** Reuse existing named pipe protocol and `WfpTrafficControl.Shared` models

**Screens:**
1. **Dashboard** — Status cards, quick actions (Apply/Rollback), recent activity feed
2. **Policy Editor** — Visual rule builder with real-time validation, no JSON knowledge required
3. **Audit Logs** — Filterable/searchable log viewer with detail panel
4. **Settings** — Connection config, hot reload toggle, Bootstrap/Teardown

**Implementation Phases:**

| Phase | Name | Deliverables | Exit Criteria |
|-------|------|--------------|---------------|
| 1 | Foundation (MVP) | Shell, Dashboard, basic Policy Editor, IPC client, Apply/Rollback | Can view status, load/edit/apply policy, execute rollback |
| 2 | Feature Parity | Audit log viewer, LKG show/revert, hot reload config, validation UI, Bootstrap/Teardown | All `wfpctl` commands available in UI |
| 3 | Enhanced UX | Visual rule builder, filter preview, drag-drop reordering, rule templates, themes, notifications | User can build policies without JSON knowledge |
| 4 | Advanced | Policy diff viewer, rule search, batch operations, session history, large policy optimization | Handles 1000+ rules smoothly |

**Project Structure:**
```
/src/ui/WfpTrafficControl.UI/
  /Views/           (XAML views)
  /ViewModels/      (MVVM ViewModels)
  /Services/        (IPC client, dialog service)
  /Controls/        (Custom controls)
  /Converters/      (Value converters)
  /Resources/       (Themes, icons)
  App.xaml
```

**Key Design Decisions:**
- Runs elevated (Administrator) — consistent with CLI, simplifies auth
- Reuses `WfpTrafficControl.Shared` project directly
- Confirmation dialogs for all destructive operations
- Preview before commit — show what will change before applying
- Visible rollback — panic button always accessible

**Acceptance Criteria (Phase 1 MVP):**
- [ ] WPF application shell with navigation
- [ ] Dashboard shows service status, filter count, policy version
- [ ] Can load policy JSON file
- [ ] Can edit rules in form-based UI
- [ ] Can apply policy to service
- [ ] Can execute rollback
- [ ] Basic error handling with user-friendly messages
- [ ] Runs elevated with manifest

**Acceptance Criteria (Full Implementation):**
- [ ] All `wfpctl` commands available in UI
- [ ] Real-time validation matches CLI behavior
- [ ] New user can apply policy in < 5 minutes
- [ ] Rollback achievable in < 3 clicks
- [ ] No JSON knowledge required for basic operations
- [ ] Handles service disconnection gracefully

**Open Questions (from UX_PLAN.md):**
1. System tray integration — minimize for persistent monitoring?
2. Auto-start with Windows?
3. Multi-language/localization for v1?
4. Policy version control beyond LKG?
5. Opt-in telemetry/analytics?

**Estimated Effort:**
- Phase 1 (MVP): 3-5 days
- Phase 2 (Feature Parity): 2-3 days
- Phase 3 (Enhanced UX): 3-5 days
- Phase 4 (Advanced): 3-5 days
- **Total:** 11-18 days

**Dependencies:**
- Service and CLI must be stable
- IPC protocol frozen (or UI updated with protocol changes)

**References:**
- Full design: `docs/UX_PLAN.md`
- IPC messages: `src/shared/Ipc/`
- Shared models: `src/shared/Policy/PolicyModels.cs`

---

## Priority 4: Nice to Have

Lower priority improvements that would add polish but aren't essential for the core goal.

---

### P4-01: Create MSI Installer with WiX

**Status:** Not Started

**Why Valuable:**
PowerShell install scripts exist, but a proper MSI shows deployment maturity and enables enterprise deployment via SCCM/Intune.

**Implementation Notes:**
1. Add WiX project to solution
2. Define:
   - Service installation
   - File deployment
   - Registry entries (if any)
   - Upgrade handling
3. Sign MSI (requires code signing cert)

**Estimated Effort:** Medium-High (2-3 days)

---

### P4-02: Obtain Code Signing Certificate

**Status:** Not Started

**Why Valuable:**
Production Windows binaries should be signed. Unsigned binaries trigger SmartScreen warnings.

**Implementation Notes:**
1. Obtain code signing certificate (self-signed for demo, or proper CA for production)
2. Sign service and CLI binaries
3. Sign MSI installer (if created)
4. Document signing process

**Estimated Effort:** Low (but may have cost)

---

### P4-03: Add WPR/ETW Trace Documentation

**Status:** Not Started

**Why Valuable:**
Once ETW is implemented (P2-01), document how to capture and analyze traces for debugging filter application performance.

**Depends On:** P2-01 (ETW EventSource)

**Estimated Effort:** Low

---

### P4-04: Memory Profiling and Allocation Analysis

**Status:** Not Started

**Why Valuable:**
Prove there's no boxing or excessive allocations in hot paths. Use BenchmarkDotNet memory diagnoser or dotMemory.

**Implementation Notes:**
1. Add `[MemoryDiagnoser]` to benchmark classes
2. Run benchmarks and check allocations
3. Optimize any allocation-heavy hot paths
4. Document results

**Estimated Effort:** Low-Medium

---

## Completed Tasks

_Move completed tasks here with completion date._

| Task ID | Description | Completed | Notes |
|---------|-------------|-----------|-------|
| — | — | — | — |

---

## Quick Reference: Effort Estimates

| Priority | Tasks | Total Effort |
|----------|-------|--------------|
| P1 | 3 tasks | 3-5 days |
| P2 | 3 tasks | 2-3 days |
| P3 | 6 tasks | 12-20 days (includes GUI) |
| P4 | 4 tasks | 3-5 days |

**Note:** P3-06 (WPF GUI) is a major feature with 4 phases totaling 11-18 days. Other P3 tasks total ~1-2 days.

**Recommended order:** P1-02 (static analysis) → P1-03 (inbound UDP) → P2-02 (CI) → P3-01 (video demo) → P1-01 (IPv6) → P3-06 Phase 1 (GUI MVP)

Starting with static analysis is quick and shows immediate rigor. Video demo provides visual proof with minimal effort. IPv6 is highest impact but also highest effort—save for when other gaps are closed. GUI can be developed incrementally after core gaps are addressed.
