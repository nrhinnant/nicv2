# 025 — Testing & Benchmarking Strategy

## Overview

This document proposes a comprehensive testing and benchmarking strategy for the WFP Traffic Control system using industry-standard tools. It covers five categories: functional rule enforcement, performance/throughput, security/leak testing, stress/reliability, and internal code benchmarking.

**Target audience:** Engineers evaluating the firewall's correctness, performance, and security posture.

**Current state:** The project has 600 xUnit unit tests covering policy validation, rule compilation, filter diffing, WFP engine reconciliation, rollback, IPC security, rate limiting, and audit logging. Three PowerShell smoke test scripts (`Test-DemoBlock.ps1`, `Test-InboundBlock.ps1`, `Test-UdpBlock.ps1`) provide basic live-fire validation. No external testing tools or performance benchmarks exist.

**Environment assumptions:** All live-fire tests run in a dedicated Windows VM with snapshots. Two-VM tests use VMs on the same virtual switch (Hyper-V internal or VirtualBox Host-Only).

---

## Category 1: Functional / Rule Enforcement Testing

Goal: Black-box verification that applied policies actually block or allow traffic as declared.

### 1.1 Test-NetConnection (PowerShell Built-in) — Must-Have

**What it is.** `Test-NetConnection -ComputerName <ip> -Port <port>` performs a TCP SYN attempt and reports `TcpTestSucceeded`. Already used in `Test-DemoBlock.ps1` and `Test-InboundBlock.ps1`.

**What it tests here.**
- Outbound TCP block: apply a rule blocking `1.1.1.1:443`, assert connection fails
- Outbound TCP allow: explicit allow at higher priority than a broad block, assert connection succeeds
- Inbound TCP block: start a `TcpListener` on port N, apply inbound block, assert loopback connection fails

**Integration.** Expand existing scripts into a parameterized `Test-RuleEnforcement.ps1`:
```powershell
.\scripts\Test-RuleEnforcement.ps1 -Direction outbound -Protocol tcp `
    -RemoteIp 1.1.1.1 -RemotePort 443 -ExpectedResult blocked
```

**Key metrics.** Pass/fail per test case. Time from `wfpctl apply` to first blocked connection (enforcement latency).

**Implementation status.** `scripts/Test-RuleEnforcement.ps1` is implemented as a fully parameterized script:

```powershell
# Outbound TCP block (default):
.\scripts\Test-RuleEnforcement.ps1 -Direction outbound -Protocol tcp

# Outbound UDP block (DNS):
.\scripts\Test-RuleEnforcement.ps1 -Direction outbound -Protocol udp -RemoteIp 8.8.8.8 -RemotePort 53

# Inbound TCP block:
.\scripts\Test-RuleEnforcement.ps1 -Direction inbound -Protocol tcp -RemotePort 19876

# Inbound UDP is gracefully skipped (not supported by ALE RECV_ACCEPT layer)
```

Supports outbound TCP, outbound UDP (Resolve-DnsName for port 53, UDP socket for other ports), inbound TCP (with TcpListener), and reports enforcement latency. Usable standalone or as a building block for orchestration scripts.

### 1.2 Resolve-DnsName (PowerShell Built-in) — Must-Have

**What it is.** `Resolve-DnsName -Name example.com -Server 8.8.8.8 -DnsOnly` forces a UDP query to a specific server. Already used in `Test-UdpBlock.ps1`.

**What it tests here.**
- Outbound UDP block: apply rule blocking `8.8.8.8:53/udp`, assert DNS query fails
- Port-range blocking: block `8.8.8.8:50-60/udp`, verify port 53 is caught by the range
- CIDR blocking: block `8.8.0.0/16:53/udp`, verify a server in-range is blocked while one outside is not

**Integration.** Parameterize `Test-UdpBlock.ps1` to accept `DnsServer`, `ExpectedResult`, and CIDR variants.

**Key metrics.** Pass/fail. DNS query round-trip time before and after policy apply.

### 1.3 nmap (Port Scanning / Rule Matrix) — Must-Have

**What it is.** The industry-standard network port scanner. Available via `winget install Insecure.Nmap` or from [nmap.org](https://nmap.org). Runs from a second VM or against loopback.

**What it tests here.**
- **Outbound block verification.** From the firewall VM, apply a policy blocking outbound TCP to `<second-vm-ip>:80,443,8080`. Run `nmap -p 80,443,8080 <second-vm-ip>`. Expect all ports reported as `filtered`.
- **Inbound block verification.** Start listeners on ports 7001-7003 on the firewall VM. Apply inbound block for those ports. From the second VM, run `nmap -p 7001-7003 <firewall-vm-ip>`. Expect `filtered` instead of `open`.
- **Priority/ordering test.** Apply two rules: block all TCP to `<ip>:80` at priority 100, allow TCP to `<ip>:80` from process `curl.exe` at priority 200. Use nmap to confirm port 80 is filtered (no process match from nmap), then verify curl succeeds separately.

**Limitations.** nmap cannot verify process-path matching (it has no process identity). Use it for IP/port/direction matrix coverage; use `curl` and `Test-NetConnection` for process-path tests.

**Integration.** Script as `scripts/Test-NmapMatrix.ps1`. Output nmap XML via `-oX`, parse in PowerShell to assert per-port state.

```powershell
nmap -p 80,443,8080 -oX results.xml $TargetIp
[xml]$scan = Get-Content results.xml
$scan.nmaprun.host.ports.port | ForEach-Object {
    # Assert state is "filtered" for blocked ports
}
```

**Key metrics.** Port state (open/closed/filtered) per rule. Diff between expected and actual state table.

**Implementation status.** `scripts/Test-NmapMatrix.ps1` is implemented with four test groups:

```powershell
# Basic run (requires second VM):
.\scripts\Test-NmapMatrix.ps1 -TargetIp 192.168.1.20

# Skip nping tests:
.\scripts\Test-NmapMatrix.ps1 -TargetIp 192.168.1.20 -SkipNping
```

- **GROUP 1:** Outbound TCP block on exact ports (80, 443, 8080) — verifies blocked state, then rollback and restoration.
- **GROUP 2:** Port range block (8000-9000) — verifies boundary ports 7999/9001 are not filtered while 8000/8500/9000 are.
- **GROUP 3:** Protocol/direction bypass (negative-space, covers Category 3.1) — verifies TCP-only rule doesn't bleed to UDP or other ports.
- **GROUP 4:** nping port boundary probing (optional, requires nping on PATH).

Prints a summary table with per-test expected/actual/pass-fail results.

### 1.4 curl / Invoke-WebRequest — Must-Have

**What it is.** Standard HTTP client. Exercises a real application-layer connection and can be paired with process-path matching tests. `curl.exe` ships with Windows 10 1803+.

**What it tests here.**
- Outbound TCP allow/block for HTTPS to `1.1.1.1:443`. `curl -v --max-time 5 https://1.1.1.1` — blocked produces timeout (exit code 28); allowed succeeds with a TLS handshake.
- Process-path rule: block all outbound TCP to `1.1.1.1:443` except from `C:\Windows\System32\curl.exe`. Confirm curl succeeds while nmap from the same machine fails.
- Rollback lifecycle: verify curl fails while rule is active, then succeeds after `wfpctl rollback`.

**Integration.** Add a `scripts/Test-ProcessPath.ps1` script.

**Key metrics.** Exit code (0 = success, 28 = timeout). Total connection time.

**Implementation status.** `scripts/Test-ProcessPath.ps1` is implemented:

```powershell
.\scripts\Test-ProcessPath.ps1
```

Locates curl.exe automatically (`C:\Windows\System32\curl.exe` or PATH fallback), applies a two-rule policy (block all + allow curl.exe at higher priority), verifies curl.exe can connect while Test-NetConnection is blocked, then rolls back and verifies restoration. Seven test steps total.

### 1.5 nping (Bundled with nmap) — Nice-to-Have

**What it is.** TCP/UDP packet crafter with control over all fields. Sends raw TCP SYN or UDP packets.

**What it tests here.** Port-range boundary probing. Apply a rule blocking ports 8000-9000, then probe 7999, 8000, 9000, 9001 individually:
```
nping --tcp -p 7999 <target>   # Should succeed (outside range)
nping --tcp -p 8000 <target>   # Should be blocked (range start)
nping --tcp -p 9000 <target>   # Should be blocked (range end)
nping --tcp -p 9001 <target>   # Should succeed (outside range)
```

**Key metrics.** Response (ICMP admin prohibited / no response) versus expected.

### 1.6 Rule Enforcement Coverage Matrix

Define and track a coverage matrix. Each row is a combination of:

| Direction | Protocol | Match Type | Action | Verification Tool |
|-----------|----------|------------|--------|-------------------|
| outbound | tcp | exact IP + port | block | Test-NetConnection |
| outbound | tcp | CIDR + port range | block | nmap |
| outbound | tcp | process path allow | allow | curl |
| outbound | udp | exact IP + port | block | Resolve-DnsName |
| inbound | tcp | exact port | block | Test-NetConnection (loopback) |
| inbound | tcp | port range | block | nmap (from second VM) |
| outbound | tcp | comma-list ports | block | nmap |
| outbound | tcp | priority override | allow over block | curl + nmap |

This matrix should have a pass/fail column filled in during each VM test session.

---

## Category 2: Performance / Throughput Benchmarking

Goal: Measure the overhead the firewall introduces on network throughput and connection latency.

**Important architectural context:** This firewall uses WFP ALE authorization layers, which evaluate **once per connection**, not once per packet. Established-connection throughput should be unaffected by the number of filter rules. This is the core performance story and must be documented alongside any benchmark results.

### 2.1 iperf3 (TCP Throughput) — Must-Have

**What it is.** The standard open-source network throughput benchmark. Sends a stream of TCP or UDP data between two endpoints and reports Mbps, jitter, and loss. Available from [iperf.fr](https://iperf.fr) or via winget.

**Test procedure.**
1. Run iperf3 server on second VM: `iperf3 -s`
2. **Baseline (no policy):** `iperf3 -c <second-vm-ip> -t 30 -P 4` (30 seconds, 4 parallel streams). Record baseline Mbps.
3. **Non-matching rules:** Apply a policy with 50 block rules (none matching the iperf traffic). Re-run iperf3. Record Mbps. This measures filter evaluation overhead.
4. **Matching allow rule:** Apply a policy with an explicit allow rule matching the iperf traffic. Re-run iperf3. Record Mbps.
5. **Compare** across 3 runs per configuration. Report mean and standard deviation.

**Setup.** Both VMs on the same virtual switch. iperf3 runs as administrator on the firewall VM.

**Expected results.** Throughput with rules active should be within 1% of baseline (ALE evaluates per-connection, not per-packet). Documenting this result — even when the answer is "negligible overhead" — demonstrates the architectural advantage of ALE layers.

**Key metrics.**
| Configuration | Metric |
|---------------|--------|
| Baseline (no policy) | Mbps (mean of 3 runs) |
| 50 non-matching rules | Mbps, % delta from baseline |
| Allow rule matching traffic | Mbps, % delta from baseline |

### 2.2 TCP Connection-Setup Latency — Nice-to-Have

**What it tests.** ALE fires at connection setup. Measure TCP handshake time scaling with rule count.

```powershell
# Open 1000 TCP connections and measure total time
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 0; $i -lt 1000; $i++) {
    $client = [System.Net.Sockets.TcpClient]::new()
    $client.Connect("192.168.x.y", 8080)
    $client.Close()
}
$sw.Stop()
$avgMs = $sw.ElapsedMilliseconds / 1000
Write-Host "Average connect time: ${avgMs}ms"
```

Run with 0, 10, 50, and 200 rules. Expected: near-zero scaling.

**Key metrics.** Average TCP connect time (microseconds) per rule-count tier.

### 2.3 iperf3 UDP Mode — Nice-to-Have

**What it tests.** UDP throughput overhead. `iperf3 -c <ip> -u -b 100M -t 30`. Compare baseline to 50 non-matching rules, then to a UDP block rule matching the traffic (expect complete block = 100% loss).

**Key metrics.** Mbps, loss percent, jitter.

---

## Category 3: Security / Leak Testing

Goal: Verify the firewall cannot be bypassed and that the IPC surface is properly secured.

### 3.1 Protocol/Direction Bypass Attempts — Must-Have

**What it tests.** Does a TCP block rule also block UDP? Does an outbound block affect inbound? Does a port-specific block bleed to other ports? These are correctness tests framed as security tests — verifying rule scope is correct.

**Procedure.**
1. Apply rule: block outbound TCP to `1.1.1.1:443`
2. Attempt outbound UDP to `1.1.1.1:53` — **should succeed** (different protocol)
3. Attempt inbound TCP from `1.1.1.1` — **should succeed** (different direction; separate ALE layers)
4. Attempt outbound TCP to `1.1.1.1:80` — **should succeed** (different port)

A firewall that blocks more than intended is a reliability bug; one that blocks less is a security bug. Both sides must be tested.

**Integration.** Add as "negative space" cases in `Test-NmapMatrix.ps1`.

**Implementation status.** Implemented as GROUP 3 in `scripts/Test-NmapMatrix.ps1`. Tests: (1) UDP send not blocked when only TCP is blocked, (2) TCP to a different port not blocked, (3) TCP to the blocked port is blocked. Uses nping for UDP when available, UdpClient fallback otherwise. nmap `-sT` for TCP verification.

### 3.2 IPC Security Verification — Must-Have

**What it tests.** VM-level confirmation of the IPC security model (unit-tested in `IpcSecurityTests.cs`).

| Test Case | Expected Result |
|-----------|-----------------|
| Non-admin user connects to pipe | Access Denied (OS ACL rejection) |
| Oversized message (> 64KB) | Rejected with size limit error |
| Invalid protocol version | Rejected with version error |
| 20 rapid requests from one identity | First 10 succeed, next 10 rate-limited |

**Integration.** Script as `scripts/Test-IpcSecurity.ps1`. Requires a second local user account on the VM for the non-admin test.

**Key metrics.** Each scenario returns the expected response. Non-admin connection is rejected at the OS pipe ACL level (cannot even open the pipe).

**Implementation status.** `scripts/Test-IpcSecurity.ps1` is implemented with four test cases:

```powershell
# Basic run (tests 1-3, skips non-admin test):
.\scripts\Test-IpcSecurity.ps1

# Full run including non-admin access denial test:
.\scripts\Test-IpcSecurity.ps1 -TestNonAdmin -NonAdminUser "testuser"
```

Test 1 (pipe connectivity) and Test 2 (oversized message) validate server behavior directly. Test 3 (rate limiting) uses tolerant bounds to handle VM timing variance. Test 4 (non-admin access) is gated behind `-TestNonAdmin` since it requires a pre-existing local non-admin user account.

### 3.3 Filter Persistence After Service Stop — Must-Have

**What it tests.** WFP filters are managed by the Base Filtering Engine (BFE), not our process. When the service crashes or stops, blocking filters persist. This is both a feature (enforcement survives crashes) and a risk (no CLI available to roll back). This test validates the recovery path.

**Procedure.**
1. Apply a policy with a block rule
2. Stop the service (`Stop-Service WfpTrafficControl`)
3. Verify block is still active (connection still blocked)
4. Restart the service (`Start-Service WfpTrafficControl`)
5. Verify service starts (fail-open: no policy re-applied unless LKG auto-apply is enabled)
6. Run `wfpctl rollback`
7. Verify connectivity is restored

**Key metrics.** Filters survive service stop (expected). Recovery via rollback after restart. Time to restore connectivity.

### 3.4 CIDR Boundary Testing — Nice-to-Have

**What it tests.** Apply a rule blocking `192.168.1.0/24`. Probe boundary IPs:

| IP | Expected |
|----|----------|
| `192.168.0.255` | Allowed (just outside range) |
| `192.168.1.0` | Blocked (network address) |
| `192.168.1.1` | Blocked (inside range) |
| `192.168.1.255` | Blocked (broadcast address) |
| `192.168.2.0` | Allowed (just outside range) |

**Note.** `RuleCompilerTests.cs` already validates CIDR mask computation at the unit level. This test is the live confirmation.

### 3.5 Deliberate Exclusions

| Tool | Why Excluded |
|------|--------------|
| **Comodo Leak Test / FirewallLeakTester** | Designed to test kernel callout evasion techniques (process injection, raw socket bypasses). This user-mode WFP implementation does not claim to resist kernel-level evasion. Results would be misleading without extensive caveats. |
| **Wireshark / ETW packet capture** | ALE authorization layers are connection-decision layers, not packet inspection layers. Wireshark shows packets after the WFP decision for allowed traffic and nothing for blocked outbound. Useful for diagnostics but not a correctness tool for ALE. |
| **IPv6 testing** | The system is IPv4-only by design. IPv6 belongs in a future milestone. |
| **DPI / L7 testing** | Non-goal per project scope (no deep packet inspection). |

---

## Category 4: Stress / Reliability Testing

Goal: Validate behavior under sustained load, rapid policy changes, and large rule sets.

### 4.1 Rapid Policy Apply Stress Test — Must-Have

**What it tests.** Apply a new policy every 100ms for 60 seconds (600 applies). Verify no crashes, no WFP transaction failures, no orphaned filters, and audit log shows all applies.

```powershell
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($i = 1; $i -le 600; $i++) {
    $policy.updatedAt = (Get-Date).ToString("o")
    $policy | ConvertTo-Json -Depth 10 | Set-Content $policyPath -Encoding UTF8
    & $WfpctlPath apply $policyPath | Out-Null
    Start-Sleep -Milliseconds 100
}
$sw.Stop()
Write-Host "600 applies in $($sw.Elapsed.TotalSeconds)s"

# Verify final state
& $WfpctlPath status
& $WfpctlPath logs --tail 10
```

**Key metrics.**
| Metric | Target |
|--------|--------|
| Apply success rate | 100% |
| Final filter count | Matches last applied policy |
| Orphaned filters | 0 |
| Audit log entries | Matches apply count |
| Service memory (before/after) | No significant growth |

**Implementation status.** `scripts/Test-RapidApply.ps1` is implemented:

```powershell
# Default run (100 applies, 100ms delay, 5 rules):
.\scripts\Test-RapidApply.ps1

# Scale up to match original spec (600 applies):
.\scripts\Test-RapidApply.ps1 -Iterations 600 -DelayMs 100 -RuleCount 10
```

Parameterized for iterations, delay, and rule count. Records initial/final service memory via `Get-Process`, runs the apply loop with progress indicators, checks `wfpctl status` and `wfpctl logs --tail 20` after the run, then rolls back. Reports: total applies, successes, failures, elapsed time, applies/second, and memory delta.

### 4.2 Large Policy Stress Test — Must-Have

**What it tests.** Compile and apply a policy with 500 rules. Measure compilation time, apply time, and filter count.

```powershell
# Generate 500-rule policy programmatically
$rules = 1..500 | ForEach-Object {
    @{
        id        = "stress-rule-$_"
        action    = if ($_ % 2 -eq 0) { "block" } else { "allow" }
        direction = if ($_ % 3 -eq 0) { "inbound" } else { "outbound" }
        protocol  = if ($_ % 4 -eq 0) { "udp" } else { "tcp" }
        remote    = @{ ip = "10.0.$([int]($_ / 256)).$(($_ % 256))"; ports = "$((1000 + $_))" }
        priority  = $_
        enabled   = $true
    }
}
```

**Key metrics.**
| Scenario | Target |
|----------|--------|
| First apply (500 new filters) | < 2 seconds |
| Idempotent re-apply (same policy) | < 100ms (transaction skipped) |
| Partial diff (250 rules changed) | < 1 second |
| Peak memory during apply | Documented, not necessarily targeted |

**Implementation status.** `scripts/Test-LargePolicyStress.ps1` is implemented:

```powershell
# Default run (500 rules):
.\scripts\Test-LargePolicyStress.ps1

# Scale up (1000 rules, max 10000):
.\scripts\Test-LargePolicyStress.ps1 -RuleCount 1000
```

Generates rules with varied direction (outbound/inbound), protocol (tcp/udp), and action (allow/block). Three phases: (A) first apply with all new filters, (B) idempotent re-apply of the same policy, (C) partial diff with first half of rules changed to different IPs. Reports wall-clock time and filter operation counts (created/removed/unchanged) per phase in a summary table.

### 4.3 Hot Reload Stress Test — Nice-to-Have

**What it tests.** Enable file watching. Modify the policy file 100 times in rapid succession (faster than the debounce interval). Verify the debounce coalesces events and only a bounded number of applies occur.

**Key metrics.** Apply count from `wfpctl watch status` should be much less than 100 (debounce working). No crashes. Final filter state matches last written policy.

### 4.4 Concurrent IPC Client Stress Test — Nice-to-Have

**What it tests.** 20 parallel PowerShell jobs each sending 10 `wfpctl status` commands. Verify no deadlocks, no crashes, and the rate limiter correctly throttles per-identity.

```powershell
$jobs = 1..20 | ForEach-Object {
    Start-Job -ScriptBlock {
        param($wfpctl)
        $results = @()
        for ($i = 0; $i -lt 10; $i++) {
            $output = & $wfpctl status 2>&1
            $results += $LASTEXITCODE
        }
        $results
    } -ArgumentList $WfpctlPath
}
$jobs | Wait-Job | Receive-Job
```

**Key metrics.** All jobs complete. Service remains running. Rate-limited requests return appropriate errors.

---

## Category 5: Internal Code Benchmarking (BenchmarkDotNet)

Goal: Establish precise performance baselines for the hot paths in policy compilation, diffing, and validation using the standard .NET microbenchmarking library.

**What it is.** [BenchmarkDotNet](https://benchmarkdotnet.org/) is the authoritative .NET benchmarking library. It handles warmup, statistical analysis, GC measurement, and produces publication-quality results.

### 5.1 Project Structure

**Implementation status.** The benchmarks project is implemented and included in the solution.

```
/benchmarks
    Benchmarks.csproj              # net8.0, BenchmarkDotNet 0.14.0, refs Shared only
    Program.cs                     # BenchmarkSwitcher entry point (supports --filter)
    RuleCompilerBenchmarks.cs      # 7 benchmarks (scale + code-path variants)
    FilterDiffBenchmarks.cs        # 5 benchmarks (empty, idempotent, partial, full-change)
    PolicyValidatorBenchmarks.cs   # 3 benchmarks (valid, large, all-invalid)
```

**Running:**
```powershell
cd benchmarks
dotnet run -c Release -- --filter *RuleCompiler*
dotnet run -c Release -- --filter *FilterDiff*
dotnet run -c Release -- --filter *PolicyValidator*
dotnet run -c Release                              # run all benchmarks
dotnet run -c Release -- --exporters JSON          # machine-readable output
```

**Note:** BenchmarkDotNet requires Release configuration for reliable results. Debug builds produce warnings and unreliable timing. The benchmarks project targets `net8.0` (not `net8.0-windows`) and does not reference the Service project, so it can build cross-platform.

### 5.2 RuleCompiler.Compile Benchmarks — Must-Have

Primary target: `RuleCompiler.Compile()` in [src/shared/Policy/RuleCompiler.cs](../../src/shared/Policy/RuleCompiler.cs).

| Benchmark | Description |
|-----------|-------------|
| `Compile_1Rule` | Smallest valid policy |
| `Compile_10Rules` | Typical policy size |
| `Compile_100Rules` | Larger policy |
| `Compile_500Rules` | Stress-level policy |
| `Compile_WithCidr` | CIDR parsing path |
| `Compile_WithPortRange` | Port range expansion path |
| `Compile_MultiPort100` | 100-port comma-list (creates 100 filters per rule) |

**Why this matters.** `RuleCompiler.Compile` runs synchronously on every `wfpctl apply`. If it takes >50ms for a 500-rule policy, apply latency degrades noticeably.

**Expected results.** Well under 1ms for 100 rules. The deterministic GUID computation (MD5) and CIDR parsing are the heaviest operations.

**Key metrics.** Mean time (ns/us), memory allocation per call (bytes), 99th percentile.

### 5.3 FilterDiffComputer.ComputeDiff Benchmarks — Must-Have

Primary target: `FilterDiffComputer.ComputeDiff()` in [src/shared/Policy/FilterDiff.cs](../../src/shared/Policy/FilterDiff.cs).

| Benchmark | Description |
|-----------|-------------|
| `Diff_EmptyToEmpty` | No-op baseline |
| `Diff_EmptyTo100` | First apply (100 new filters) |
| `Diff_100ToSame100` | Idempotent case (expect zero changes, fast path) |
| `Diff_100To100_HalfChanged` | Partial update |
| `Diff_500To500_AllChanged` | Worst case |

**Why this matters.** The diff runs on every apply. The idempotent case (same policy re-applied) should be significantly faster than the full-change case, confirming the optimization.

**Key metrics.** Mean time, allocation. Idempotent case vs. full-change case ratio.

### 5.4 PolicyValidator Benchmarks — Nice-to-Have

| Benchmark | Description |
|-----------|-------------|
| `Validate_ValidPolicy_10Rules` | Happy path |
| `Validate_PolicyWith50Rules` | Moderate size |
| `Validate_AllRulesInvalid` | Error accumulation path |

### 5.5 IPC Serialization Benchmarks — Nice-to-Have (Not Implemented)

| Benchmark | Description |
|-----------|-------------|
| `SerializeApplyResponse` | JSON serialization |
| `ParseApplyRequest` | JSON deserialization |
| `RoundTrip_Request_Response` | Full serialize/deserialize cycle |

**Why this matters.** IPC is on the critical path for every CLI command. `System.Text.Json` is fast, but measuring it establishes a regression baseline.

**Implementation note.** Not implemented in the current benchmarks project. IPC types live in the Service project which has Windows-only dependencies (`net8.0-windows`). Adding a Service reference would break cross-platform builds. These benchmarks can be added if the IPC models are moved to Shared or a separate project.

---

## Tiered Execution Plan

### Tier 1 — Every Demo/Validation Session (< 2 minutes)

Run before any demonstration or manual validation:

1. `scripts/Test-DemoBlock.ps1` — outbound TCP block + rollback
2. `scripts/Test-InboundBlock.ps1` — inbound TCP block + rollback
3. `scripts/Test-UdpBlock.ps1` — outbound UDP block + rollback
4. `scripts/Test-IpcSecurity.ps1` (new) — IPC authorization checks

### Tier 2 — Before Significant Commits (Weekly or Per-Phase)

5. `scripts/Test-NmapMatrix.ps1` — full rule enforcement matrix (4 groups: exact port block, port range, protocol/direction bypass, nping boundary)
6. `scripts/Test-LargePolicyStress.ps1` — 500-rule compile/apply with idempotent re-apply and partial diff phases
7. `scripts/Test-RapidApply.ps1` — configurable rapid applies (default 100, scalable to 600+) with memory tracking
8. `scripts/Test-ProcessPath.ps1` (new) — process-path matching with curl

### Tier 3 — Benchmarking (After Significant Changes)

9. `benchmarks/` — BenchmarkDotNet suite (results documented in this file)
10. iperf3 throughput test (baseline established once per VM rebuild)

### Tier 4 — Full Matrix (Pre-Presentation)

11. Two-VM nmap scan (inbound + outbound matrix from external VM)
12. Service restart safety test (filter persistence + recovery)
13. CIDR boundary tests
14. Concurrent IPC stress test
15. Hot reload stress test

---

## Key Metrics Reference Table

| Category | Metric | Target / Expectation |
|----------|--------|----------------------|
| Rule enforcement | Pass/fail per matrix cell | 100% pass |
| Enforcement latency | Time from apply to first blocked connection | < 200ms |
| TCP throughput (no policy) | iperf3 baseline | VM hardware dependent |
| TCP throughput (50 rules) | Versus baseline | Within 1% (ALE is per-connection) |
| RuleCompiler.Compile (100 rules) | BenchmarkDotNet mean | < 1ms |
| FilterDiff (100 rules, idempotent) | BenchmarkDotNet mean | < 500us |
| IPC serialize/parse round trip | BenchmarkDotNet mean | < 100us |
| Rapid apply (600 applies) | Success rate | 100% |
| Rapid apply (600 applies) | Orphaned filters | 0 |
| Large policy (500 rules) | First apply time | < 2 seconds |
| Large policy (500 rules) | Idempotent re-apply | < 100ms |
| IPC security (non-admin) | Pipe access | Access Denied at OS level |
| IPC rate limit | 20 rapid requests | First 10 succeed, next 10 rejected |

---

## Required Tool Installation

| Tool | Install Method | Required For |
|------|---------------|--------------|
| Test-NetConnection | Built into Windows | Tier 1 |
| Resolve-DnsName | Built into Windows | Tier 1 |
| curl.exe | Ships with Windows 10 1803+ | Tier 2 |
| nmap / nping | `winget install Insecure.Nmap` or [nmap.org](https://nmap.org) | Tier 2, 4 |
| iperf3 | [iperf.fr](https://iperf.fr) or `winget install iperf3` | Tier 3 |
| BenchmarkDotNet | NuGet: `BenchmarkDotNet` (added to benchmarks project) | Tier 3 |

---

## Known Limitations

- **Process-path testing requires known binaries.** nmap cannot test process-path rules; must use binaries like `curl.exe` whose paths are predictable.
- **Two-VM tests require network configuration.** Both VMs must be on the same virtual switch with static or predictable IPs.
- **Inbound UDP not supported.** The `FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4` layer does not handle UDP. Inbound UDP tests are excluded from the matrix.
- **No IPv6 coverage.** The system is IPv4-only. IPv6 tests belong in a future milestone.
- **BenchmarkDotNet requires Release builds.** Debug builds produce unreliable timing. Always run `dotnet run -c Release`.

---

## Related Documentation

- [022-how-it-works.md](022-how-it-works.md) — Architecture overview (ALE layer explanation, reconciliation model)
- [013-idempotent-reconcile.md](013-idempotent-reconcile.md) — Filter diff and idempotent apply
- [010-panic-rollback.md](010-panic-rollback.md) — Rollback mechanism
- [019-ipc-security.md](019-ipc-security.md) — IPC authorization model
- [024-rate-limiting.md](024-rate-limiting.md) — Rate limiter design
