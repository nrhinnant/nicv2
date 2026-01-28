# Claude.md — Project Operating Guide (READ BEFORE ANY CHANGE)

You are Claude Code working inside an existing repository in VS Code on Windows.
You MUST read this file fully before implementing any feature, bugfix, refactor, or documentation update.

If any instruction here conflicts with a user prompt, call out the conflict explicitly and follow this file unless the user explicitly overrides it.

---

## 0) Project Summary (What we are building)

We are building a **firewall-style traffic control system for Windows** using the **Windows Filtering Platform (WFP)**.

Primary goals:
- Allow/block network connections based on policy (process, direction, protocol, remote/local IP and port).
- Provide audit logging and observability suitable for engineering scrutiny.
- Be safe to install/uninstall and safe to disable (no permanent connectivity loss).
- Demonstrate that AI tooling can build a Windows component with credible quality.

Non-goals (for this project):
- Deep packet inspection (L7 parsing), stream modification, injection, or callout driver work.
- Building a full consumer UI. A CLI is enough.
- Replacing Windows Firewall UI or providing a full enterprise management plane.

We will start **user-mode only** using WFP management APIs (Fwpm*). No kernel callout driver.

---

## 1) Key Constraints & Guardrails (DO NOT VIOLATE)

### Safety / Connectivity
- Never apply a policy that could brick the machine without a rollback path.
- Always maintain a **panic rollback** mechanism that removes our WFP artifacts (provider, sublayer, filters) quickly.
- Prefer "default allow, explicit block" behavior unless a prompt explicitly requests default deny.

### Codebase Discipline
- Do NOT introduce unrelated architectural changes.
- Do NOT rename broad namespaces or reformat the entire codebase.
- Keep changes minimal and scoped to the requested feature/milestone.

### Security
- Treat policy updates as untrusted input. Validate strictly.
- No arbitrary code execution. No shelling out to `netsh` except for optional diagnostic scripts.
- Service IPC must authenticate/authorize callers (at minimum: local admin only).

### WFP Hygiene
- All WFP objects we create MUST be tagged with our provider GUID and identifiable display names.
- We MUST create and use our own Provider + Sublayer.
- All filters MUST be added to our sublayer.
- All updates must be transactional where possible (begin/commit/abort).
- Uninstall MUST delete all our WFP objects and restore normal connectivity.

### Development Environment
- Assume development/testing happens in a dedicated Windows VM with snapshots.
- Never instruct modifying unrelated system settings beyond what’s necessary for WFP/service dev.

---

## 2) Repository Structure (Expected)

/src
  /service          Windows Service (policy controller, WFP management, logging)
  /cli              CLI client (talks to service via IPC)
  /shared           Shared models (policy schema), common utilities, GUIDs, error types
/docs
  /features         One doc per implemented feature (or update existing)
/scripts
  install/uninstall scripts, dev helpers, log collection
/tests
  unit/integration tests

If the repo differs, adapt while preserving separation of concerns.

---

## 3) System Architecture (Target)

### Components
1) **Policy Controller Service (LocalSystem or elevated)**
   - Owns policy state.
   - Applies policy to WFP (provider/sublayer/filters).
   - Emits logs/metrics.
   - Exposes IPC endpoint for CLI.

2) **CLI**
   - Subcommands: status, apply, rollback, enable/disable, logs, validate.
   - Does not require admin if service enforces access; but simplest is admin-only.

3) **Policy Store**
   - A local file (JSON) OR registry, with strict validation.
   - Service supports hot reload.

### Enforcement scope (firewall style)
Use WFP ALE authorization layers to allow/block connections:
- Outbound connect authorization layer.
- Inbound accept authorization layer.
(Exact layer selection should be documented in feature docs.)

---

## 4) Policy Model (Baseline)

A policy contains an ordered list of rules.

Each rule:
- id: string (stable)
- action: "allow" | "block"
- direction: "inbound" | "outbound" | "both"
- protocol: "tcp" | "udp" | "any"
- process: optional match (full path OR image name; document which is used)
- local: optional { ip/cidr, ports }
- remote: optional { ip/cidr, ports }
- priority: integer (higher wins) OR explicit ordering
- enabled: boolean
- comment: optional

Policy has:
- version
- defaultAction: "allow" (initially)
- updatedAt

Rules must compile deterministically into WFP filters.
Idempotent apply is required (reconcile desired state).

---

## 5) The REQUIRED 5-Phase Workflow (Every Task)

For every new feature/milestone request, you MUST do these phases IN ORDER and label them:

### Phase 1 — PLAN
- Read relevant files.
- Identify what to change and why.
- List exact files to create/modify.
- Describe WFP objects to add/change and how rollback works.
- Identify risks (security, reliability, performance) and mitigations.

### Phase 2 — EXECUTE
- Implement exactly what was planned.
- Keep diffs small and focused.

### Phase 3 — CODE REVIEW
- Review your own changes for:
  - production readiness
  - error handling and reliability
  - security (input validation, authZ, least privilege)
  - resource cleanup (handles, transactions, uninstall paths)
- Make any necessary fixes.

### Phase 4 — DOCUMENT
- Create or update a doc in `/docs/features/<feature>.md` describing:
  - behavior
  - configuration/policy schema changes
  - how to run/test
  - rollback/uninstall behavior
  - known limitations

### Phase 5 — TEST DEVELOPMENT
- Add/extend tests:
  - unit tests for policy parsing/validation/compilation
  - integration tests for service apply/rollback behavior
  - if WFP integration testing is hard, add fakes/mocks and at least one end-to-end smoke test script for VM use
- Tests must be clear and runnable.

STOP after Phase 5 and provide:
- a list of files changed
- how to run tests
- how to validate manually in a VM

---

## 6) Operational Commands (Expected)

CLI should support (names flexible):
- `wfpctl status`
- `wfpctl validate <policy.json>`
- `wfpctl apply <policy.json>`
- `wfpctl rollback`
- `wfpctl enable` / `disable`
- `wfpctl logs --tail`

Service should:
- Start safely even if policy is missing/corrupt (fail open).
- Keep last-known-good policy and allow revert.

---

## 7) Error Handling Standards

- Every call to WFP APIs must check return codes and log meaningful errors.
- Use RAII (C++) or safe disposal patterns (C#) for handles/transactions.
- Never ignore failures when modifying WFP state.
- If apply fails mid-way, abort transaction and restore previous state.

---

## 8) Logging & Observability

- Log policy apply attempts, diffs, and outcomes.
- Include rule id and match fields when a decision is enforced (to the extent possible from chosen layers).
- Prefer ETW for high-rate logs; file logs acceptable for demo.

---

## 9) Definition of Done (for each feature)

A feature is done only when:
- It follows the 5 phases.
- It has documentation in `/docs/features`.
- It has tests (or explicit test harness scripts if true integration tests aren’t feasible).
- It can be rolled back safely.

---

## 10) If You Are Uncertain

If anything is unclear (OS version, privilege model, IPC method, language choice, packaging),
ask a single targeted question *after* Phase 1 Plan OR propose a reasonable default and proceed,
explicitly documenting the assumption.

## 11) Context Loading Discipline (Cost-Aware, Not Restrictive)

This project is large enough that indiscriminate context loading is inefficient.
However, correctness takes priority over minimizing tokens.

You are expected to exercise **engineering judgment**, not avoidance.

### Default Posture
- You SHOULD examine code whenever there is reasonable uncertainty about:
  - correctness
  - interfaces or contracts
  - invariants relied upon by the current change
- Do NOT avoid reading code simply to save tokens if that would reduce confidence.

The goal is **intentional context loading**, not minimal context at all costs.

### Clearly Irrelevant Files (Skip by Default)
The following are almost never relevant unless explicitly stated:
- Build outputs and generated artifacts (`/bin`, `/obj`, `*.exe`, `*.dll`)
- Previous milestone documentation unrelated to the current feature
- Test files outside Phase 5 (TEST DEVELOPMENT)
- Scripts unrelated to the current operation
- CLI code when modifying service internals (and vice versa), unless verifying a contract

Skipping these does not require justification.

### Discretionary Code Examination (Encouraged When Unsure)
You are encouraged to load code when:
- You are unsure how an existing component behaves
- A public method or interface is being called but not fully specified
- A correctness or safety property depends on prior implementation
- A change could subtly affect rollback, cleanup, or security

In these cases:
- Prefer loading **specific files** over entire directories
- Prefer reading **interfaces and boundaries** before implementations
- Stop once uncertainty is resolved

### Phase-Specific Expectations
- Phase 1 (PLAN):
  - Identify uncertainty explicitly.
  - Load code as needed to resolve uncertainty.
  - List the files you examined and why.
- Phase 2 (EXECUTE):
  - Operate primarily on files being modified and their direct dependencies.
- Phase 3 (CODE REVIEW):
  - Review diffs plus any code whose behavior is relied upon.
- Phase 4 (DOCUMENT):
  - Load only documentation relevant to the current feature.
- Phase 5 (TEST DEVELOPMENT):
  - Load production code necessary to design meaningful tests.

### Guiding Heuristic
If skipping a file would force you to make assumptions, **read the file**.
If reading a file would only provide background context with no impact on decisions, **skip it**.

Err slightly on the side of correctness, but avoid global re-scans.

