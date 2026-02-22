#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Wrapper script for running the iperf3 throughput benchmark with the firewall.

.DESCRIPTION
    This test requires a second VM running "iperf3 -s" as the server.

    The WFP Traffic Control system uses ALE (Application Layer Enforcement) layers,
    which evaluate ONCE PER CONNECTION, not once per packet. This means established
    connection throughput should be unaffected by the number of filter rules. This
    is the core performance story and this test validates it.

    Test procedure:
    1. Check iperf3 is on PATH. If not, print install instructions and exit.
    2. PHASE A - Baseline: run iperf3 3 times, compute mean throughput
    3. PHASE B - With rules: apply policy with $RuleCount block rules (blocking
       IPs in 10.99.x.x range - guaranteed NOT to match iperf traffic).
       Run iperf3 3 times, compute mean.
    4. PHASE C - Rollback: run iperf3 3 times to confirm baseline is restored.
    5. Report comparison table.

    Expected result: throughput with rules active should be within 1% of baseline
    because ALE evaluates per-connection, not per-packet.

.PARAMETER ServerIp
    IP address of the iperf3 server. REQUIRED.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER Duration
    Test duration in seconds. Default 30.

.PARAMETER Streams
    Number of parallel TCP streams. Default 4.

.PARAMETER RuleCount
    Number of non-matching block rules to apply. Default 50.

.EXAMPLE
    .\scripts\Test-Iperf3Baseline.ps1 -ServerIp 192.168.1.20

.EXAMPLE
    .\scripts\Test-Iperf3Baseline.ps1 -ServerIp 192.168.1.20 -Duration 60 -RuleCount 100

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
    Requires iperf3 to be installed and on PATH.

    Setup:
    1. On the second VM (server): iperf3 -s
    2. On this VM (client): .\scripts\Test-Iperf3Baseline.ps1 -ServerIp <server-ip>

    Both VMs should be on the same virtual switch for accurate measurements.

    See docs/features/025-testing-strategy.md section 2.1 for specification.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerIp,

    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [int]$Duration = 30,

    [Parameter()]
    [int]$Streams = 4,

    [Parameter()]
    [int]$RuleCount = 50
)

$ErrorActionPreference = "Stop"

# ANSI color codes for output
$Green = "`e[32m"
$Red = "`e[31m"
$Yellow = "`e[33m"
$Cyan = "`e[36m"
$Reset = "`e[0m"

function Write-TestStep {
    param([string]$Message)
    Write-Host "${Cyan}[TEST]${Reset} $Message"
}

function Write-TestPass {
    param([string]$Message)
    Write-Host "${Green}[PASS]${Reset} $Message"
}

function Write-TestFail {
    param([string]$Message)
    Write-Host "${Red}[FAIL]${Reset} $Message"
}

function Write-TestWarn {
    param([string]$Message)
    Write-Host "${Yellow}[WARN]${Reset} $Message"
}

function Invoke-Wfpctl {
    param([string[]]$Arguments)

    $output = & $WfpctlPath @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    return @{
        Output = $output -join "`n"
        ExitCode = $exitCode
        Success = ($exitCode -eq 0)
    }
}

function Invoke-Iperf3 {
    param(
        [string]$Server,
        [int]$Duration,
        [int]$Streams
    )

    try {
        $output = iperf3 -c $Server -t $Duration -P $Streams --json 2>&1
        $exitCode = $LASTEXITCODE

        if ($exitCode -ne 0) {
            return @{
                Success = $false
                Error = $output -join "`n"
            }
        }

        # Parse JSON output
        $json = $output | ConvertFrom-Json
        $bitsPerSecond = $json.end.sum_received.bits_per_second

        return @{
            Success = $true
            BitsPerSecond = $bitsPerSecond
            Mbps = [math]::Round($bitsPerSecond / 1000000, 2)
            Gbps = [math]::Round($bitsPerSecond / 1000000000, 3)
        }
    } catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

function New-NonMatchingPolicy {
    param([int]$RuleCount)

    $rules = @()
    for ($i = 1; $i -le $RuleCount; $i++) {
        # Block IPs in 10.99.x.x range - guaranteed NOT to match iperf traffic
        $octet3 = [int]($i / 256)
        $octet4 = $i % 256
        $rules += @{
            id        = "perf-test-rule-$i"
            action    = "block"
            direction = "outbound"
            protocol  = "tcp"
            remote    = @{
                ip    = "10.99.$octet3.$octet4"
                ports = "$((9000 + ($i % 1000)))"
            }
            priority  = $i
            enabled   = $true
            comment   = "Performance test rule $i (non-matching)"
        }
    }

    return @{
        version       = "1.0"
        defaultAction = "allow"
        updatedAt     = (Get-Date).ToString("o")
        rules         = $rules
    }
}

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl iperf3 Throughput Benchmark"
Write-Host "=============================================="
Write-Host ""
Write-Host "IMPORTANT: This test requires a second VM running 'iperf3 -s'"
Write-Host ""
Write-Host "The WFP Traffic Control system uses ALE layers, which evaluate"
Write-Host "ONCE PER CONNECTION, not once per packet. This means established"
Write-Host "connection throughput should be unaffected by the number of filter"
Write-Host "rules. This test validates that behavior."
Write-Host ""
Write-Host "Parameters:"
Write-Host "  ServerIp:  $ServerIp"
Write-Host "  Duration:  ${Duration}s"
Write-Host "  Streams:   $Streams"
Write-Host "  RuleCount: $RuleCount"
Write-Host ""

# ========================================
# Pre-flight: Check iperf3
# ========================================
Write-TestStep "Pre-flight: Checking for iperf3..."

$iperf3Path = Get-Command iperf3 -ErrorAction SilentlyContinue
if (-not $iperf3Path) {
    Write-TestFail "iperf3 is not installed or not on PATH"
    Write-Host ""
    Write-Host "Installation options:"
    Write-Host "  1. winget: winget install iperf3"
    Write-Host "  2. Download: https://iperf.fr/iperf-download.php"
    Write-Host "  3. Chocolatey: choco install iperf3"
    Write-Host ""
    Write-Host "After installing, ensure iperf3.exe is in your PATH."
    exit 1
}
Write-TestPass "iperf3 found at: $($iperf3Path.Source)"
Write-Host ""

# Verify wfpctl exists
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}

$WfpctlPath = Resolve-Path $WfpctlPath

# ========================================
# Pre-flight: Verify service
# ========================================
Write-TestStep "Pre-flight: Verifying service is running..."
$statusResult = Invoke-Wfpctl @("status")
if (-not $statusResult.Success) {
    Write-TestFail "Service is not running"
    exit 1
}
Write-TestPass "Service is running"
Write-Host ""

# ========================================
# Pre-flight: Test server connectivity
# ========================================
Write-TestStep "Pre-flight: Testing connectivity to $ServerIp..."
$testResult = Invoke-Iperf3 -Server $ServerIp -Duration 2 -Streams 1
if (-not $testResult.Success) {
    Write-TestFail "Cannot connect to iperf3 server at $ServerIp"
    Write-Host "Error: $($testResult.Error)"
    Write-Host ""
    Write-Host "Ensure the server is running: iperf3 -s"
    exit 1
}
Write-TestPass "Server is reachable (quick test: $($testResult.Mbps) Mbps)"
Write-Host ""

# Results storage
$phaseA = @()
$phaseB = @()
$phaseC = @()

# ========================================
# PHASE A: Baseline (no policy)
# ========================================
Write-Host "=============================================="
Write-Host "  PHASE A: Baseline (no firewall rules)"
Write-Host "=============================================="
Write-Host ""

# Ensure clean state
Write-TestStep "Ensuring clean state (rollback)..."
Invoke-Wfpctl @("rollback") | Out-Null
Write-Host ""

for ($run = 1; $run -le 3; $run++) {
    Write-TestStep "Baseline run $run of 3 (${Duration}s, $Streams streams)..."
    $result = Invoke-Iperf3 -Server $ServerIp -Duration $Duration -Streams $Streams

    if ($result.Success) {
        Write-TestPass "Run $run: $($result.Mbps) Mbps ($($result.Gbps) Gbps)"
        $phaseA += $result.Mbps
    } else {
        Write-TestFail "Run $run failed: $($result.Error)"
    }
}

$phaseAMean = if ($phaseA.Count -gt 0) { [math]::Round(($phaseA | Measure-Object -Average).Average, 2) } else { 0 }
Write-Host ""
Write-Host "Phase A Mean: ${Green}$phaseAMean Mbps${Reset}"
Write-Host ""

# ========================================
# PHASE B: With non-matching rules
# ========================================
Write-Host "=============================================="
Write-Host "  PHASE B: With $RuleCount non-matching block rules"
Write-Host "=============================================="
Write-Host ""

Write-TestStep "Generating and applying $RuleCount-rule policy..."
$policy = New-NonMatchingPolicy -RuleCount $RuleCount
$policyPath = Join-Path $env:TEMP "iperf-test-policy.json"
$policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyPath -Encoding UTF8

$applyResult = Invoke-Wfpctl @("apply", $policyPath)
if ($applyResult.Success) {
    Write-TestPass "Policy applied successfully"
} else {
    Write-TestFail "Failed to apply policy: $($applyResult.Output)"
    Remove-Item $policyPath -Force
    exit 1
}
Write-Host ""

for ($run = 1; $run -le 3; $run++) {
    Write-TestStep "With-rules run $run of 3 (${Duration}s, $Streams streams)..."
    $result = Invoke-Iperf3 -Server $ServerIp -Duration $Duration -Streams $Streams

    if ($result.Success) {
        Write-TestPass "Run $run: $($result.Mbps) Mbps ($($result.Gbps) Gbps)"
        $phaseB += $result.Mbps
    } else {
        Write-TestFail "Run $run failed: $($result.Error)"
    }
}

$phaseBMean = if ($phaseB.Count -gt 0) { [math]::Round(($phaseB | Measure-Object -Average).Average, 2) } else { 0 }
Write-Host ""
Write-Host "Phase B Mean: ${Green}$phaseBMean Mbps${Reset}"
Write-Host ""

# ========================================
# PHASE C: After rollback
# ========================================
Write-Host "=============================================="
Write-Host "  PHASE C: After rollback (confirm baseline restored)"
Write-Host "=============================================="
Write-Host ""

Write-TestStep "Rolling back policy..."
$rollbackResult = Invoke-Wfpctl @("rollback")
if ($rollbackResult.Success) {
    Write-TestPass "Rollback complete"
} else {
    Write-TestWarn "Rollback issue: $($rollbackResult.Output)"
}
Write-Host ""

for ($run = 1; $run -le 3; $run++) {
    Write-TestStep "Post-rollback run $run of 3 (${Duration}s, $Streams streams)..."
    $result = Invoke-Iperf3 -Server $ServerIp -Duration $Duration -Streams $Streams

    if ($result.Success) {
        Write-TestPass "Run $run: $($result.Mbps) Mbps ($($result.Gbps) Gbps)"
        $phaseC += $result.Mbps
    } else {
        Write-TestFail "Run $run failed: $($result.Error)"
    }
}

$phaseCMean = if ($phaseC.Count -gt 0) { [math]::Round(($phaseC | Measure-Object -Average).Average, 2) } else { 0 }
Write-Host ""
Write-Host "Phase C Mean: ${Green}$phaseCMean Mbps${Reset}"
Write-Host ""

# Cleanup
Remove-Item $policyPath -Force -ErrorAction SilentlyContinue

# ========================================
# Results Comparison
# ========================================
Write-Host "=============================================="
Write-Host "  Results Comparison"
Write-Host "=============================================="
Write-Host ""

# Calculate deltas
$deltaBvsA = if ($phaseAMean -gt 0) { [math]::Round((($phaseBMean - $phaseAMean) / $phaseAMean) * 100, 2) } else { 0 }
$deltaCvsA = if ($phaseAMean -gt 0) { [math]::Round((($phaseCMean - $phaseAMean) / $phaseAMean) * 100, 2) } else { 0 }

# Determine pass/fail color for delta
$deltaBColor = if ([math]::Abs($deltaBvsA) -le 1) { $Green } elseif ([math]::Abs($deltaBvsA) -le 5) { $Yellow } else { $Red }
$deltaCColor = if ([math]::Abs($deltaCvsA) -le 2) { $Green } elseif ([math]::Abs($deltaCvsA) -le 5) { $Yellow } else { $Red }

Write-Host "+-------+--------------------+--------------------+-------------+"
Write-Host "| Phase | Configuration      | Mean Throughput    | vs Baseline |"
Write-Host "+-------+--------------------+--------------------+-------------+"
Write-Host "| A     | Baseline (no rules)| $($phaseAMean.ToString().PadLeft(10)) Mbps | (baseline)  |"
Write-Host "| B     | $($RuleCount.ToString().PadLeft(3)) block rules     | $($phaseBMean.ToString().PadLeft(10)) Mbps | ${deltaBColor}$($deltaBvsA.ToString('+0.00;-0.00').PadLeft(6))%${Reset}     |"
Write-Host "| C     | After rollback     | $($phaseCMean.ToString().PadLeft(10)) Mbps | ${deltaCColor}$($deltaCvsA.ToString('+0.00;-0.00').PadLeft(6))%${Reset}     |"
Write-Host "+-------+--------------------+--------------------+-------------+"
Write-Host ""

# ========================================
# Verdict
# ========================================
$passed = [math]::Abs($deltaBvsA) -le 5  # Allow 5% variance for VM environment noise

if ($passed) {
    Write-Host "${Green}PASS:${Reset} Throughput with $RuleCount rules is within acceptable variance of baseline."
    Write-Host ""
    Write-Host "This confirms the ALE per-connection optimization: filter rules do not"
    Write-Host "affect established connection throughput because evaluation happens only"
    Write-Host "at connection setup time."
} else {
    Write-Host "${Red}WARNING:${Reset} Throughput delta ($deltaBvsA%) exceeds expected variance."
    Write-Host ""
    Write-Host "Expected: < 5% variance (ideally < 1%)"
    Write-Host "Possible causes:"
    Write-Host "  - VM resource contention during test"
    Write-Host "  - Network variability"
    Write-Host "  - Run additional tests to confirm"
}

Write-Host ""
Write-Host "Note: Some variance is expected in VM environments. Run multiple times"
Write-Host "to establish consistent measurements."
Write-Host ""
