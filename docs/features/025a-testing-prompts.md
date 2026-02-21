# 025a — Testing Strategy: Execution Prompts for Claude Code

## How to Use This Document

Each section below is a **self-contained prompt** you can paste into a fresh Claude Code session. They are ordered by dependency — earlier sessions create patterns that later sessions reference. Each prompt is scoped to fit comfortably within a single context window.

**Before each session:** Ensure your working directory is the repo root and the solution builds (`dotnet build`).

**After each session:** Commit the output before starting the next prompt.

---

## Session 1 of 6 — IPC Security Integration Test Script

**Tier:** 1 (run every demo session)
**Creates:** `scripts/Test-IpcSecurity.ps1`
**Estimated scope:** ~1 new file, ~200-300 lines

```
Read docs/features/025-testing-strategy.md section 3.2 ("IPC Security Verification") for
the specification of what this script must test.

Read scripts/Test-DemoBlock.ps1 as the canonical pattern for all new PowerShell test
scripts. Match its conventions exactly: #Requires -RunAsAdministrator, CmdletBinding param
block with $WfpctlPath default, ANSI color codes, Write-TestStep/Pass/Fail/Warn helpers,
Invoke-Wfpctl wrapper, numbered test steps, try/finally cleanup, and a summary with
pass/fail counts and exit code.

Read src/service/Ipc/PipeServer.cs to understand the pipe name, ACL, message format
(4-byte length prefix + JSON, max 64KB), and rate limiter configuration.

Read tests/IpcSecurityTests.cs to understand the unit-level test cases that this script
confirms at the VM integration level.

Create scripts/Test-IpcSecurity.ps1 implementing these test cases:

1. Pipe connectivity (admin) — verify wfpctl status succeeds as admin (baseline sanity).

2. Oversized message — open the named pipe directly using System.IO.Pipes.NamedPipeClientStream,
   send a message larger than 64KB (the max message size), and verify the service rejects it
   or disconnects cleanly. Do NOT use wfpctl for this test since it enforces size limits
   client-side — the point is to test the server's enforcement.

3. Rate limiting — send 20 rapid wfpctl status calls in a tight loop from a single process.
   The rate limiter allows 10 requests per 10-second window per identity. Assert that the
   first ~10 succeed and subsequent requests return rate-limit errors. Parse the output
   to count successes vs rate-limit rejections.

4. Non-admin access (optional, gated) — if a parameter -TestNonAdmin is provided AND a
   non-admin local user account name is provided via -NonAdminUser, use
   Start-Process -Credential to attempt wfpctl status as that user. Expect failure
   (Access Denied at the pipe ACL level). If -TestNonAdmin is not specified, skip this
   test with a warning explaining how to run it.

Follow the 5-phase workflow from CLAUDE.md. The feature doc update for Phase 4 should be a
brief addition to docs/features/025-testing-strategy.md noting that the script was
implemented and how to run it.
```

---

## Session 2 of 6 — Rule Enforcement + Process-Path Scripts

**Tier:** 2 (before significant commits)
**Creates:** `scripts/Test-RuleEnforcement.ps1`, `scripts/Test-ProcessPath.ps1`
**Estimated scope:** ~2 new files, ~400-500 lines total

```
Read docs/features/025-testing-strategy.md sections 1.1 ("Test-NetConnection"), 1.2
("Resolve-DnsName"), and 1.4 ("curl / Invoke-WebRequest") for the specification.

Read scripts/Test-InboundBlock.ps1 as the pattern for inbound testing (TcpListener setup,
loopback connection testing, policy creation with temp file, cleanup in finally block).

Read scripts/Test-UdpBlock.ps1 as the pattern for UDP testing (Resolve-DnsName approach).

Read scripts/Test-DemoBlock.ps1 for the general script conventions to follow.

--- SCRIPT 1: scripts/Test-RuleEnforcement.ps1 ---

Create a parameterized rule enforcement test that can verify any single rule by parameters:

Parameters:
  -Direction (outbound | inbound) — required
  -Protocol (tcp | udp) — required
  -RemoteIp (string) — target IP, default "1.1.1.1"
  -RemotePort (int) — target port, default 443
  -Action (block | allow) — what the rule does, default "block"
  -ExpectedResult (blocked | allowed) — what we expect to observe, default "blocked"
  -Priority (int) — rule priority, default 100
  -WfpctlPath — standard default
  -SkipCleanup — standard switch

Behavior:
  - For outbound TCP: use Test-NetConnection (or TcpClient with timeout for faster results)
  - For outbound UDP: use Resolve-DnsName if port is 53, else use a UDP socket send/receive
  - For inbound TCP: start a TcpListener, test loopback connection
  - For inbound UDP: skip with a warning (not supported by the firewall)
  - Create a temp policy JSON with the specified rule, apply via wfpctl, verify the
    expected result, rollback, verify restoration, cleanup

This script should be usable as both a standalone test and as a building block called from
other test orchestrators.

--- SCRIPT 2: scripts/Test-ProcessPath.ps1 ---

Create a process-path matching test using curl.exe:

Parameters:
  -WfpctlPath — standard default
  -SkipCleanup — standard switch

Test procedure:
  1. Verify curl.exe exists (check C:\Windows\System32\curl.exe and PATH)
  2. Verify initial connectivity: curl --max-time 5 -s -o NUL https://1.1.1.1 succeeds
  3. Apply a policy with TWO rules:
     - Rule 1: block all outbound TCP to 1.1.1.1:443 at priority 100
     - Rule 2: allow outbound TCP to 1.1.1.1:443 from curl.exe process at priority 200
  4. Verify curl.exe can still connect (process-path allow overrides the block)
  5. Verify Test-NetConnection to 1.1.1.1:443 fails (PowerShell has no process-path allow)
  6. Rollback and verify connectivity restored for both methods

Follow the 5-phase workflow. Update docs/features/025-testing-strategy.md noting these
scripts were implemented.
```

---

## Session 3 of 6 — nmap Matrix + Bypass Tests

**Tier:** 2/4 (before significant commits / pre-presentation)
**Creates:** `scripts/Test-NmapMatrix.ps1`
**Estimated scope:** ~1 file, ~400-500 lines

```
Read docs/features/025-testing-strategy.md sections 1.3 ("nmap"), 1.5 ("nping"), 1.6
("Rule Enforcement Coverage Matrix"), and 3.1 ("Protocol/Direction Bypass Attempts")
for the full specification.

Read scripts/Test-DemoBlock.ps1 for the script conventions to follow.

Create scripts/Test-NmapMatrix.ps1 that runs a comprehensive rule enforcement matrix
using nmap and nping.

Parameters:
  -TargetIp (string) — IP of the second VM or test target, REQUIRED (no default)
  -WfpctlPath — standard default
  -SkipNping — switch to skip nping boundary tests if nping is not available
  -SkipCleanup — standard switch

Prerequisites check: verify nmap is on PATH (winget install Insecure.Nmap). If not found,
print install instructions and exit.

The script should run these test groups, applying and rolling back a policy for each group:

GROUP 1 — Outbound TCP Block Matrix:
  Apply a policy blocking outbound TCP to $TargetIp on ports 80,443,8080.
  Run: nmap -Pn -sT -p 80,443,8080 $TargetIp -oX $tempXml
  Parse the XML output. Assert all three ports show state "filtered".
  Rollback. Re-run nmap. Assert ports are no longer "filtered".

GROUP 2 — Port Range Block:
  Apply a policy blocking outbound TCP to $TargetIp ports "8000-9000".
  Run nmap on ports 7999,8000,8500,9000,9001.
  Assert 8000,8500,9000 are filtered; 7999,9001 are not.
  Rollback.

GROUP 3 — Protocol/Direction Bypass ("negative space" — Category 3.1):
  Apply a single rule: block outbound TCP to $TargetIp:443.
  Test 1: outbound UDP to $TargetIp (Resolve-DnsName if target is a DNS server, or
           nping --udp -p 53) — should NOT be blocked (different protocol)
  Test 2: outbound TCP to $TargetIp:80 — should NOT be blocked (different port)
  Test 3: nmap -sT -p 443 $TargetIp — SHOULD be blocked
  This validates that rules don't bleed across protocol/port boundaries.
  Rollback.

GROUP 4 — nping Port Boundary (if -SkipNping not set):
  Apply a policy blocking outbound TCP to $TargetIp ports "8000-9000".
  Use nping to probe boundary ports individually:
    nping --tcp -p 7999 $TargetIp -c 1 — expect response
    nping --tcp -p 8000 $TargetIp -c 1 — expect no response / filtered
    nping --tcp -p 9000 $TargetIp -c 1 — expect no response / filtered
    nping --tcp -p 9001 $TargetIp -c 1 — expect response
  Rollback.

For nmap XML parsing, use:
  [xml]$scan = Get-Content $tempXml
  $ports = $scan.nmaprun.host.ports.port
  # Each port has .portid and .state.state attributes

Print a summary table at the end showing each test case, expected result, actual result,
and pass/fail.

Follow the 5-phase workflow. Update docs/features/025-testing-strategy.md noting this
script was implemented with the test groups it covers.
```

---

## Session 4 of 6 — Stress Test Scripts

**Tier:** 2 (before significant commits)
**Creates:** `scripts/Test-RapidApply.ps1`, `scripts/Test-LargePolicyStress.ps1`
**Estimated scope:** ~2 files, ~400-500 lines total

```
Read docs/features/025-testing-strategy.md sections 4.1 ("Rapid Policy Apply Stress Test")
and 4.2 ("Large Policy Stress Test") for the full specification.

Read scripts/Test-DemoBlock.ps1 for script conventions.

Read src/shared/Policy/PolicyModels.cs to understand the policy JSON schema so generated
policies are valid (version format, updatedAt as ISO 8601, rule field names and types,
defaultAction).

Read src/shared/Policy/PolicyValidator.cs to understand validation constraints (max rule
count 10000, rule ID format, port format, etc.) so generated policies pass validation.

--- SCRIPT 1: scripts/Test-RapidApply.ps1 ---

Parameters:
  -Iterations (int) — number of applies, default 100 (not 600 — allow user to scale)
  -DelayMs (int) — delay between applies in milliseconds, default 100
  -RuleCount (int) — number of rules in the test policy, default 5
  -WfpctlPath — standard default

Behavior:
  1. Verify service is running
  2. Record initial service memory via Get-Process
  3. Generate a valid base policy with $RuleCount rules (outbound TCP blocks to
     unique IPs like 10.99.0.1, 10.99.0.2, etc.)
  4. Loop $Iterations times:
     - Update updatedAt timestamp
     - Write policy to temp file
     - Run wfpctl apply, capture exit code
     - Track success/failure counts
     - Sleep $DelayMs
  5. After loop: run wfpctl status, record final memory
  6. Run wfpctl logs --tail 20 to verify audit entries
  7. Rollback and cleanup

Summary should show: total applies, successes, failures, elapsed time,
applies/second, memory delta.

--- SCRIPT 2: scripts/Test-LargePolicyStress.ps1 ---

Parameters:
  -RuleCount (int) — number of rules, default 500
  -WfpctlPath — standard default
  -SkipCleanup — standard switch

Behavior:
  1. Verify service is running
  2. Generate a policy with $RuleCount rules programmatically:
     - Vary direction (outbound for most, inbound for every 3rd)
     - Vary protocol (tcp for most, udp for every 4th)
     - Unique remote IPs: 10.0.{i/256}.{i%256}
     - Unique ports: 1000 + i
     - Alternate block/allow
     - All enabled
  3. PHASE A — First apply: measure wall-clock time with Stopwatch.
     Record filters created count from wfpctl output.
  4. PHASE B — Idempotent re-apply: apply same policy again.
     Measure time. Expect near-instant (0 created, 0 removed, N unchanged).
  5. PHASE C — Partial diff: generate a modified policy where the first half of
     rules are changed (different IPs). Apply and measure time.
     Record filters created/removed counts.
  6. Rollback and cleanup.

Summary table should show: rule count, Phase A time, Phase B time, Phase C time,
filter counts per phase.

Follow the 5-phase workflow. Update docs/features/025-testing-strategy.md noting these
scripts were implemented.
```

---

## Session 5 of 6 — BenchmarkDotNet Project + All Benchmarks

**Tier:** 3 (after significant changes)
**Creates:** `benchmarks/` directory with project and benchmark files
**Estimated scope:** ~5-6 new files

```
Read docs/features/025-testing-strategy.md Category 5 ("Internal Code Benchmarking") for
the full specification including all benchmark methods and expected structure.

Read src/shared/Policy/RuleCompiler.cs to understand the Compile method signature, input
types (PolicyRule list or Policy object), and output type (CompilationResult or
List<CompiledFilter>). Understand the code paths: CIDR parsing, port range expansion,
comma-separated port splitting, deterministic GUID generation.

Read src/shared/Policy/FilterDiff.cs to understand FilterDiffComputer.ComputeDiff —
its input types (desired list + current dictionary/list) and output type (FilterDiff
with ToAdd, ToRemove, Unchanged).

Read src/shared/Policy/PolicyValidator.cs to understand the Validate method signature.

Read src/shared/Policy/PolicyModels.cs for the Policy, PolicyRule, and CompiledFilter
model types so you can construct valid test data for benchmarks.

Read WfpTrafficControl.sln and src/shared/Shared.csproj to understand the project
structure and target framework.

Create the following files:

1. benchmarks/Benchmarks.csproj
   - Target net8.0 (NOT net8.0-windows — benchmarks should run cross-platform since
     they only benchmark Shared library code)
   - PackageReference: BenchmarkDotNet (latest stable)
   - ProjectReference: ../src/shared/Shared.csproj
   - Do NOT reference Service.csproj (avoids Windows-only dependency)

2. benchmarks/Program.cs
   - Use BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)
   - This allows --filter from command line

3. benchmarks/RuleCompilerBenchmarks.cs
   - [MemoryDiagnoser] attribute on class
   - Setup methods that create valid Policy objects with N rules
   - Benchmarks: Compile_1Rule, Compile_10Rules, Compile_100Rules, Compile_500Rules,
     Compile_WithCidr (rules using CIDR notation like 10.0.0.0/8),
     Compile_WithPortRange (rules using "1024-65535"),
     Compile_MultiPort100 (one rule with 100 comma-separated ports)
   - Each benchmark calls RuleCompiler.Compile with the prepared policy

4. benchmarks/FilterDiffBenchmarks.cs
   - [MemoryDiagnoser] attribute
   - Benchmarks: Diff_EmptyToEmpty, Diff_EmptyTo100, Diff_100ToSame100,
     Diff_100To100_HalfChanged, Diff_500To500_AllChanged
   - Create CompiledFilter lists or dictionaries matching the ComputeDiff signature
   - The idempotent case (Diff_100ToSame100) should pass identical lists

5. benchmarks/PolicyValidatorBenchmarks.cs
   - [MemoryDiagnoser] attribute
   - Benchmarks: Validate_ValidPolicy_10Rules, Validate_PolicyWith50Rules,
     Validate_AllRulesInvalid
   - Create policy JSON strings as input (PolicyValidator takes JSON string)

6. Add the benchmarks project to WfpTrafficControl.sln

Do NOT create IPC serialization benchmarks — those would require referencing the Service
project which has Windows-only dependencies.

Verify the project builds: dotnet build benchmarks/Benchmarks.csproj

Follow the 5-phase workflow. Update docs/features/025-testing-strategy.md noting the
benchmarks project was created and how to run it.
```

---

## Session 6 of 6 — Tier 4 Remaining Scripts

**Tier:** 4 (pre-presentation full matrix)
**Creates:** `scripts/Test-ServiceRestart.ps1`, `scripts/Test-HotReloadStress.ps1`, `scripts/Test-ConcurrentIpc.ps1`, `scripts/Test-Iperf3Baseline.ps1`
**Estimated scope:** ~4 files, ~500-600 lines total

```
Read docs/features/025-testing-strategy.md sections 3.3 ("Filter Persistence After Service
Stop"), 4.3 ("Hot Reload Stress Test"), 4.4 ("Concurrent IPC Client Stress Test"), 2.1
("iperf3"), and 3.4 ("CIDR Boundary Testing") for specifications.

Read scripts/Test-DemoBlock.ps1 for script conventions.

Read docs/features/022-how-it-works.md section "Service Restart Safety" for the expected
behavior when the service stops (BFE retains filters, service restarts fail-open).

Create these four scripts:

--- SCRIPT 1: scripts/Test-ServiceRestart.ps1 ---

Tests filter persistence across service restart and validates the recovery path.

Parameters:
  -WfpctlPath, -SkipCleanup (standard)
  -ServiceName (string, default "WfpTrafficControl")

Procedure:
  1. Verify service is running
  2. Apply demo block (wfpctl demo-block enable)
  3. Verify 1.1.1.1:443 is blocked (Test-NetConnection)
  4. Stop the service (Stop-Service $ServiceName)
  5. Verify block is STILL active (BFE retains filters)
  6. Start the service (Start-Service $ServiceName)
  7. Verify service responds (wfpctl status)
  8. Rollback (wfpctl rollback)
  9. Verify 1.1.1.1:443 connectivity is restored
  10. Cleanup

Key assertion: filters survive service restart. This is the safety/recovery test.

--- SCRIPT 2: scripts/Test-HotReloadStress.ps1 ---

Tests that the file watcher debounce correctly coalesces rapid modifications.

Parameters:
  -WfpctlPath (standard)
  -PolicyPath (string) — path to write the watched policy file, REQUIRED
  -Modifications (int) — number of rapid file writes, default 50
  -WaitSeconds (int) — time to wait for debounce to settle, default 10

Procedure:
  1. Verify service is running
  2. Enable file watching: wfpctl watch set $PolicyPath
  3. Write initial policy (3 rules), wait for first apply
  4. Loop $Modifications times: update updatedAt, rewrite file, NO delay
  5. Wait $WaitSeconds for debounce to finish
  6. Check watch status (wfpctl watch status), record apply count
  7. Verify final filter state matches the last written policy
  8. Rollback and cleanup

Key assertion: apply count << $Modifications (debounce is coalescing).

--- SCRIPT 3: scripts/Test-ConcurrentIpc.ps1 ---

Tests that multiple simultaneous CLI clients don't cause deadlocks or crashes.

Parameters:
  -WfpctlPath (standard)
  -ClientCount (int) — number of parallel clients, default 20
  -RequestsPerClient (int) — commands per client, default 10

Procedure:
  1. Verify service is running
  2. Launch $ClientCount background jobs, each running $RequestsPerClient
     "wfpctl status" commands in a tight loop
  3. Wait for all jobs (with a timeout of 60 seconds)
  4. Collect exit codes from each job
  5. Verify service is still running after the test
  6. Report: total requests, successes, rate-limited responses, errors

Key assertion: service stays alive; no deadlocks; rate-limited requests return
appropriate errors rather than hanging.

--- SCRIPT 4: scripts/Test-Iperf3Baseline.ps1 ---

Wrapper script for running the iperf3 throughput benchmark with the firewall.

Parameters:
  -ServerIp (string) — IP of the iperf3 server, REQUIRED
  -WfpctlPath (standard)
  -Duration (int) — test duration in seconds, default 30
  -Streams (int) — parallel TCP streams, default 4
  -RuleCount (int) — number of non-matching block rules to apply, default 50

Procedure:
  1. Check iperf3 is on PATH. If not, print install instructions and exit.
  2. PHASE A — Baseline: run iperf3 -c $ServerIp -t $Duration -P $Streams --json
     Parse JSON output for bits_per_second. Run 3 times. Compute mean.
  3. PHASE B — With rules: generate a policy with $RuleCount block rules (blocking
     IPs in the 10.99.x.x range — guaranteed NOT to match iperf traffic).
     Apply via wfpctl. Run iperf3 3 times. Compute mean.
  4. PHASE C — Rollback. Run iperf3 3 times to confirm baseline is restored.
  5. Report comparison table: Phase A mean, Phase B mean, % delta, Phase C mean.

Note at the top of the script: "This test requires a second VM running iperf3 -s"
and explain the ALE per-connection property (throughput impact should be negligible).

Follow the 5-phase workflow. Update docs/features/025-testing-strategy.md noting these
scripts were implemented. Include a summary of all scripts created across all sessions
in that update.
```

---

## Execution Order Summary

| Session | Creates | Depends On | Tier |
|---------|---------|------------|------|
| 1 | Test-IpcSecurity.ps1 | None | 1 |
| 2 | Test-RuleEnforcement.ps1, Test-ProcessPath.ps1 | None | 2 |
| 3 | Test-NmapMatrix.ps1 | None | 2/4 |
| 4 | Test-RapidApply.ps1, Test-LargePolicyStress.ps1 | None | 2 |
| 5 | benchmarks/ project (all benchmarks) | None | 3 |
| 6 | Test-ServiceRestart.ps1, Test-HotReloadStress.ps1, Test-ConcurrentIpc.ps1, Test-Iperf3Baseline.ps1 | None | 4 |

Sessions 1-4 are independent and can be run in any order. Session 5 is independent.
Session 6 is independent but logically last since it covers the lowest-priority tier.

## Notes for the Operator

- **Each prompt references the strategy doc** — Claude Code will read `docs/features/025-testing-strategy.md` at the start of each session for specifications. This avoids repeating the full spec in the prompt.
- **Each prompt references existing scripts** — This ensures new scripts match the established conventions (ANSI colors, Invoke-Wfpctl wrapper, try/finally cleanup, etc.).
- **Each prompt is self-contained** — No session requires output from a previous session. They can be run in parallel if desired.
- **The 5-phase workflow is requested in every prompt** — This ensures CLAUDE.md compliance (plan, execute, review, document, test).
- **All scripts are Windows PowerShell** — They require a Windows VM with the service running. The BenchmarkDotNet project (Session 5) can build on any OS but benchmarks are most meaningful on the target Windows VM.
