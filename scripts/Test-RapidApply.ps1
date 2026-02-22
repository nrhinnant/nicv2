#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Rapid policy apply stress test.

.DESCRIPTION
    Applies a policy repeatedly at a configurable interval to verify the service
    handles rapid policy changes without crashes, WFP transaction failures,
    orphaned filters, or memory leaks.

    Records success/failure counts, elapsed time, applies/second, memory delta,
    and checks audit log entries after the run.

    Requires the WfpTrafficControl service to be running.

.PARAMETER Iterations
    Number of apply operations. Default 100.

.PARAMETER DelayMs
    Delay between applies in milliseconds. Default 100.

.PARAMETER RuleCount
    Number of rules in the test policy. Default 5.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.EXAMPLE
    .\scripts\Test-RapidApply.ps1

.EXAMPLE
    .\scripts\Test-RapidApply.ps1 -Iterations 600 -DelayMs 100 -RuleCount 10

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int]$Iterations = 100,

    [Parameter()]
    [int]$DelayMs = 100,

    [Parameter()]
    [int]$RuleCount = 5,

    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe"
)

$ErrorActionPreference = "Stop"

# ============================================================
# ANSI color codes
# ============================================================
$Green  = "`e[32m"
$Red    = "`e[31m"
$Yellow = "`e[33m"
$Cyan   = "`e[36m"
$Reset  = "`e[0m"

# ============================================================
# Helper Functions
# ============================================================

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
        Output   = $output -join "`n"
        ExitCode = $exitCode
        Success  = ($exitCode -eq 0)
    }
}

function Get-ServiceMemoryMB {
    $proc = Get-Process | Where-Object { $_.ProcessName -match "WfpTrafficControl" } | Select-Object -First 1
    if ($null -eq $proc) {
        return $null
    }
    return [math]::Round($proc.WorkingSet64 / 1MB, 2)
}

# ============================================================
# Main Script
# ============================================================

Write-Host ""
Write-Host "=================================================="
Write-Host "  WfpTrafficControl - Rapid Apply Stress Test"
Write-Host "=================================================="
Write-Host ""
Write-Host "  Iterations: $Iterations"
Write-Host "  Delay:      ${DelayMs}ms"
Write-Host "  Rule count: $RuleCount"
Write-Host ""

# --- Prerequisites ---

if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}
$WfpctlPath = Resolve-Path $WfpctlPath

Write-TestStep "Checking service status..."
$statusResult = Invoke-Wfpctl @("status")
if (-not $statusResult.Success) {
    Write-TestFail "Service is not running or not responding"
    Write-Host $statusResult.Output
    exit 1
}
Write-TestPass "Service is running"
Write-Host ""

# --- Record initial memory ---

$initialMemoryMB = Get-ServiceMemoryMB
if ($null -ne $initialMemoryMB) {
    Write-TestStep "Initial service memory: ${initialMemoryMB} MB"
} else {
    Write-TestWarn "Could not find service process for memory measurement"
}

# --- Generate base policy ---

Write-TestStep "Generating test policy with $RuleCount rules..."

$rules = @()
for ($i = 1; $i -le $RuleCount; $i++) {
    $octet3 = [int][math]::Floor($i / 256)
    $octet4 = $i % 256
    $rules += @{
        id        = "rapid-rule-$i"
        action    = "block"
        direction = "outbound"
        protocol  = "tcp"
        remote    = @{ ip = "10.99.$octet3.$octet4"; ports = "$((5000 + $i))" }
        priority  = $i
        enabled   = $true
        comment   = "Rapid apply stress test rule $i"
    }
}

$policy = @{
    version       = "1.0.0"
    defaultAction = "allow"
    updatedAt     = (Get-Date).ToString("o")
    rules         = $rules
}

$policyFile = [System.IO.Path]::Combine(
    [System.IO.Path]::GetTempPath(),
    "wfp-rapid-apply-test.json"
)

Write-TestPass "Policy generated ($RuleCount rules)"
Write-Host ""

# --- Rapid apply loop ---

Write-Host "=========================================="
Write-Host "  Running $Iterations applies..."
Write-Host "=========================================="
Write-Host ""

$successes = 0
$failures = 0
$sw = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 1; $i -le $Iterations; $i++) {
    # Update timestamp for each iteration
    $policy.updatedAt = (Get-Date).ToString("o")
    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyFile -Encoding UTF8

    try {
        $result = Invoke-Wfpctl @("apply", $policyFile)
        if ($result.Success) {
            $successes++
        } else {
            $failures++
            if ($failures -le 5) {
                Write-TestWarn "Apply #$i failed: $($result.Output)"
            }
        }
    } catch {
        $failures++
        if ($failures -le 5) {
            Write-TestWarn "Apply #$i exception: $_"
        }
    }

    # Progress indicator every 10%
    if ($i % [math]::Max(1, [int]($Iterations / 10)) -eq 0) {
        $pct = [int]($i / $Iterations * 100)
        Write-Host "  ... $pct% ($i/$Iterations) - successes: $successes, failures: $failures"
    }

    if ($i -lt $Iterations) {
        Start-Sleep -Milliseconds $DelayMs
    }
}

$sw.Stop()
$elapsedSec = [math]::Round($sw.Elapsed.TotalSeconds, 2)
$appliesPerSec = if ($elapsedSec -gt 0) { [math]::Round($Iterations / $elapsedSec, 2) } else { "N/A" }

Write-Host ""
Write-TestPass "Apply loop completed"
Write-Host ""

# --- Post-loop verification ---

Write-Host "=========================================="
Write-Host "  Post-Loop Verification"
Write-Host "=========================================="
Write-Host ""

# Final status
Write-TestStep "Checking final service status..."
$finalStatus = Invoke-Wfpctl @("status")
if ($finalStatus.Success) {
    Write-TestPass "Service still running after stress test"
    Write-Host $finalStatus.Output
} else {
    Write-TestFail "Service not responding after stress test"
    Write-Host $finalStatus.Output
}
Write-Host ""

# Final memory
$finalMemoryMB = Get-ServiceMemoryMB
if ($null -ne $finalMemoryMB) {
    Write-TestStep "Final service memory: ${finalMemoryMB} MB"
    if ($null -ne $initialMemoryMB) {
        $memoryDelta = [math]::Round($finalMemoryMB - $initialMemoryMB, 2)
        $memoryDeltaSign = if ($memoryDelta -ge 0) { "+" } else { "" }
        Write-Host "  Memory delta: ${memoryDeltaSign}${memoryDelta} MB"
    }
} else {
    Write-TestWarn "Could not measure final service memory"
}
Write-Host ""

# Audit logs
Write-TestStep "Checking recent audit log entries..."
$logsResult = Invoke-Wfpctl @("logs", "--tail", "20")
if ($logsResult.Success) {
    Write-Host $logsResult.Output
} else {
    Write-TestWarn "Could not retrieve logs: $($logsResult.Output)"
}
Write-Host ""

# --- Rollback ---

Write-TestStep "Rolling back policy..."
$rollbackResult = Invoke-Wfpctl @("rollback")
if ($rollbackResult.Success) {
    Write-TestPass "Rollback successful"
} else {
    Write-TestWarn "Rollback error: $($rollbackResult.Output)"
}

# Cleanup temp file
if (Test-Path $policyFile) {
    Remove-Item $policyFile -Force -ErrorAction SilentlyContinue
}

# ============================================================
# Summary
# ============================================================

Write-Host ""
Write-Host "=================================================="
Write-Host "  Rapid Apply Stress Test Summary"
Write-Host "=================================================="
Write-Host ""

$col1 = 28
$col2 = 25

$summaryData = @(
    @("Total applies",         "$Iterations")
    @("Successes",             "$successes")
    @("Failures",              "$failures")
    @("Elapsed time",          "${elapsedSec}s")
    @("Applies/second",        "$appliesPerSec")
    @("Rule count per policy", "$RuleCount")
    @("Delay between applies", "${DelayMs}ms")
)

if ($null -ne $initialMemoryMB) {
    $summaryData += ,@("Initial memory",  "${initialMemoryMB} MB")
}
if ($null -ne $finalMemoryMB) {
    $summaryData += ,@("Final memory",    "${finalMemoryMB} MB")
}
if ($null -ne $initialMemoryMB -and $null -ne $finalMemoryMB) {
    $summaryData += ,@("Memory delta",    "${memoryDeltaSign}${memoryDelta} MB")
}

foreach ($row in $summaryData) {
    Write-Host ("  {0,-$col1} {1}" -f $row[0], $row[1])
}

Write-Host ""

$successRate = if ($Iterations -gt 0) { [math]::Round($successes / $Iterations * 100, 1) } else { 0 }

if ($failures -eq 0) {
    Write-Host "${Green}All $Iterations applies succeeded (100% success rate).${Reset}"
    exit 0
} else {
    Write-Host "${Red}$failures of $Iterations applies failed (${successRate}% success rate).${Reset}"
    exit 1
}
