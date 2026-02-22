# 026 — Unified Test Runner

## Overview

The unified test runner (`Run-Tests.ps1`) orchestrates execution of all test categories defined in the [testing strategy](025-testing-strategy.md). It provides comprehensive reporting, prerequisite validation, and flexible test selection.

**Key features:**
- Tier-based test organization (Tier 1-4, Unit)
- Prerequisite validation (service status, tool availability)
- JSON and HTML report generation
- Include/exclude filtering by test name
- Stop-on-failure mode
- Per-test timeout with graceful termination

---

## Quick Start

```powershell
# Run Tier 1 smoke tests (default)
.\scripts\Run-Tests.ps1

# Run all tests
.\scripts\Run-Tests.ps1 -Tier All

# Run specific tiers
.\scripts\Run-Tests.ps1 -Tier Tier1,Tier2

# Run with second VM for network tests
.\scripts\Run-Tests.ps1 -Tier Tier2 -TargetIp 192.168.1.20

# List available tests without running
.\scripts\Run-Tests.ps1 -ListTests
```

---

## Test Tiers

| Tier | Purpose | Typical Duration | Tests |
|------|---------|------------------|-------|
| Tier1 | Smoke tests | < 2 minutes | DemoBlock, InboundBlock, UdpBlock, IpcSecurity |
| Tier2 | Integration | 5-15 minutes | NmapMatrix, LargePolicyStress, RapidApply, ProcessPath |
| Tier3 | Benchmarks | 10-30 minutes | BenchmarkDotNet, Iperf3Baseline |
| Tier4 | Full matrix | 10-20 minutes | ServiceRestart, HotReloadStress, ConcurrentIpc |
| Unit | xUnit tests | 1-2 minutes | 600+ unit tests |

### Tier Aliases

| Alias | Expands To |
|-------|------------|
| `All` | Tier1, Tier2, Tier3, Tier4, Unit |
| `Integration` | Tier1, Tier2, Tier4 |
| `Smoke` | Tier1 |

---

## Parameters

### Test Selection

| Parameter | Type | Description |
|-----------|------|-------------|
| `-Tier` | string[] | Which tier(s) to run. Default: `Tier1` |
| `-Include` | string[] | Only run tests matching these patterns (wildcards supported) |
| `-Exclude` | string[] | Skip tests matching these patterns |
| `-ListTests` | switch | List available tests and exit |

### Execution Control

| Parameter | Type | Description |
|-----------|------|-------------|
| `-StopOnFailure` | switch | Stop at first test failure |
| `-SkipPrerequisites` | switch | Skip prerequisite checks |
| `-NoCleanup` | switch | Don't run rollback after tests |
| `-TimeoutMinutes` | int | Per-test timeout (default: 10) |

### Reporting

| Parameter | Type | Description |
|-----------|------|-------------|
| `-OutputDirectory` | string | Report output directory (default: `.\TestResults`) |
| `-NoHtmlReport` | switch | Skip HTML report generation |
| `-NoJsonReport` | switch | Skip JSON report generation |

### Test-Specific Parameters

| Parameter | Type | Required By |
|-----------|------|-------------|
| `-TargetIp` | string | Test-NmapMatrix, Test-Iperf3Baseline |
| `-PolicyPath` | string | Test-HotReloadStress |
| `-WfpctlPath` | string | All PowerShell tests (auto-detected if not specified) |

---

## Report Output

### JSON Report

Location: `TestResults/report-YYYYMMDD-HHmmss.json` (also `TestResults/latest.json`)

```json
{
  "runId": "2026-02-21T10:30:00Z",
  "startTime": "2026-02-21T10:30:00Z",
  "endTime": "2026-02-21T10:35:23Z",
  "environment": {
    "hostname": "WIN-VM1",
    "username": "Administrator",
    "powershell": "7.4.1",
    "os": "Microsoft Windows NT 10.0.22631.0"
  },
  "summary": {
    "passed": 12,
    "failed": 1,
    "skipped": 3,
    "total": 16,
    "duration": "00:05:23"
  },
  "tests": [
    {
      "name": "Test-DemoBlock",
      "tier": "Tier1",
      "status": "Passed",
      "duration": "00:00:12.456",
      "durationMs": 12456,
      "output": "...",
      "metrics": {
        "enforcementLatency": "45ms"
      },
      "timestamp": "2026-02-21T10:30:12Z"
    }
  ]
}
```

### HTML Report

Location: `TestResults/report-YYYYMMDD-HHmmss.html` (also `TestResults/latest.html`)

Features:
- Color-coded status (green/red/gray)
- Progress bar showing pass percentage
- Summary cards (passed/failed/skipped/total)
- Expandable test details with output
- Metrics display
- Environment information

---

## Prerequisite Handling

The test runner validates prerequisites before each test:

| Prerequisite | How Checked | Tests Affected |
|--------------|-------------|----------------|
| Service running | `Get-Service WfpTrafficControl` | Most PowerShell tests |
| wfpctl exists | `Test-Path` | All PowerShell tests |
| nmap available | `Get-Command nmap` | Test-NmapMatrix |
| iperf3 available | `Get-Command iperf3` | Test-Iperf3Baseline |
| TargetIp provided | Parameter check | Test-NmapMatrix, Test-Iperf3Baseline |
| PolicyPath provided | Parameter check | Test-HotReloadStress |

Tests with unmet prerequisites are **skipped** (not failed), with the skip reason recorded.

Use `-SkipPrerequisites` to bypass these checks (tests may fail at runtime).

---

## Examples

### Run Quick Validation

```powershell
# Smoke tests only
.\scripts\Run-Tests.ps1
```

### Run Before Commit

```powershell
# Unit tests + Tier 1 + Tier 2
.\scripts\Run-Tests.ps1 -Tier Unit,Tier1,Tier2
```

### Run Stress Tests Only

```powershell
.\scripts\Run-Tests.ps1 -Include "*Stress*","*Rapid*"
```

### Run Everything Except Benchmarks

```powershell
.\scripts\Run-Tests.ps1 -Tier All -Exclude "Benchmarks","*Iperf*"
```

### Full Matrix with Second VM

```powershell
.\scripts\Run-Tests.ps1 -Tier All -TargetIp 192.168.1.20 -PolicyPath C:\temp\test-policy.json
```

### CI/CD Usage

```powershell
# Minimal output, JSON only
.\scripts\Run-Tests.ps1 -Tier Unit,Tier1 -NoHtmlReport

# Check exit code
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed!"
    exit 1
}
```

---

## Architecture

### Components

```
scripts/
├── Run-Tests.ps1           # Main test runner
├── Test-Helpers.psm1       # Shared helper module
├── Test-DemoBlock.ps1      # Tier 1 test
├── Test-InboundBlock.ps1   # Tier 1 test
├── Test-UdpBlock.ps1       # Tier 1 test
├── Test-IpcSecurity.ps1    # Tier 1 test
├── Test-RuleEnforcement.ps1 # Tier 2 test
├── Test-NmapMatrix.ps1     # Tier 2 test
├── Test-LargePolicyStress.ps1 # Tier 2 test
├── Test-RapidApply.ps1     # Tier 2 test
├── Test-ProcessPath.ps1    # Tier 2 test
├── Test-Iperf3Baseline.ps1 # Tier 3 test
├── Test-ServiceRestart.ps1 # Tier 4 test
├── Test-HotReloadStress.ps1 # Tier 4 test
└── Test-ConcurrentIpc.ps1  # Tier 4 test

tests/
├── Tests.csproj            # Unit test project
└── *.cs                    # Unit test files

benchmarks/
├── Benchmarks.csproj       # BenchmarkDotNet project
└── *.cs                    # Benchmark files

TestResults/                # Generated reports
├── latest.json
├── latest.html
├── report-20260221-103000.json
└── report-20260221-103000.html
```

### Test-Helpers.psm1

Shared module providing:
- **Output formatting**: `Write-TestPass`, `Write-TestFail`, `Write-TestSkip`, etc.
- **CLI invocation**: `Invoke-Wfpctl` with timeout support
- **Prerequisite checks**: `Test-ServiceRunning`, `Test-ToolAvailable`
- **Result parsing**: `Get-TestResultFromOutput` (extracts metrics from output)
- **Report generation**: `New-TestReport`, `Add-TestResult`, `Export-TestReportJson`, `Export-TestReportHtml`

### Test Registry

Tests are registered in a hashtable within `Run-Tests.ps1`:

```powershell
$TestRegistry = @{
    Tier1 = @(
        @{
            Name            = "Test-DemoBlock"
            Script          = "Test-DemoBlock.ps1"
            Description     = "Outbound TCP block + rollback"
            RequiresService = $true
        }
        # ...
    )
    # ...
}
```

Each test entry can specify:
- `Name`: Display name
- `Script`: PowerShell script filename (for script-based tests)
- `Type`: `dotnet-test` or `dotnet-benchmark` (for .NET tests)
- `Project`: Project path (for .NET tests)
- `Description`: Human-readable description
- `RequiresService`: Whether WfpTrafficControl service must be running
- `RequiresTool`: External tool required (e.g., "nmap", "iperf3")
- `RequiresParam`: Parameter that must be provided (e.g., "TargetIp")

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All tests passed (or skipped) |
| 1 | One or more tests failed |

---

## Cleanup Behavior

After all tests complete, the runner executes `wfpctl rollback` to ensure no test filters remain active. This can be disabled with `-NoCleanup`.

The cleanup runs even if tests fail, unless:
- The service is not running
- wfpctl is not found
- `-NoCleanup` is specified

---

## Adding New Tests

1. Create the test script in `/scripts/` following the naming convention `Test-<Name>.ps1`
2. Use the standard output conventions (`[PASS]`, `[FAIL]`, etc.)
3. Return exit code 0 for success, 1 for failure
4. Add the test to `$TestRegistry` in `Run-Tests.ps1`:

```powershell
@{
    Name            = "Test-NewFeature"
    Script          = "Test-NewFeature.ps1"
    Description     = "Tests the new feature"
    RequiresService = $true
    # RequiresTool  = "some-tool"  # if needed
    # RequiresParam = "SomeParam"  # if needed
}
```

---

## Troubleshooting

### "Service not running" skip

Start the service:
```powershell
net start WfpTrafficControl
# or
.\scripts\Start-Service.ps1
```

### "wfpctl not found" warning

Build the solution:
```powershell
dotnet build
```

Or specify the path explicitly:
```powershell
.\scripts\Run-Tests.ps1 -WfpctlPath .\path\to\wfpctl.exe
```

### Tests timing out

Increase the timeout:
```powershell
.\scripts\Run-Tests.ps1 -TimeoutMinutes 20
```

### Need verbose output

Use PowerShell's verbose preference:
```powershell
.\scripts\Run-Tests.ps1 -Verbose
```

---

## Related Documentation

- [025-testing-strategy.md](025-testing-strategy.md) — Full testing strategy with test descriptions
- [022-how-it-works.md](022-how-it-works.md) — Architecture overview
- [010-panic-rollback.md](010-panic-rollback.md) — Rollback mechanism
