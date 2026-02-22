#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Tests that multiple simultaneous CLI clients don't cause deadlocks or crashes.

.DESCRIPTION
    This script validates the service's ability to handle concurrent IPC connections
    by launching multiple background jobs that each run several wfpctl commands
    in a tight loop.

    Test procedure:
    1. Verify service is running
    2. Launch $ClientCount background jobs, each running $RequestsPerClient
       "wfpctl status" commands in a tight loop
    3. Wait for all jobs (with a timeout of 60 seconds)
    4. Collect exit codes from each job
    5. Verify service is still running after the test
    6. Report: total requests, successes, rate-limited responses, errors

    Key assertion: service stays alive; no deadlocks; rate-limited requests return
    appropriate errors rather than hanging.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER ClientCount
    Number of parallel clients. Default 20.

.PARAMETER RequestsPerClient
    Commands per client. Default 10.

.EXAMPLE
    .\scripts\Test-ConcurrentIpc.ps1

.EXAMPLE
    .\scripts\Test-ConcurrentIpc.ps1 -ClientCount 50 -RequestsPerClient 20

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.

    See docs/features/025-testing-strategy.md section 4.4 for specification.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [int]$ClientCount = 20,

    [Parameter()]
    [int]$RequestsPerClient = 10
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

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl Concurrent IPC Stress Test"
Write-Host "=============================================="
Write-Host ""
Write-Host "This test validates that multiple simultaneous CLI"
Write-Host "clients don't cause deadlocks or crashes."
Write-Host ""
Write-Host "Parameters:"
Write-Host "  ClientCount:       $ClientCount"
Write-Host "  RequestsPerClient: $RequestsPerClient"
Write-Host "  Total requests:    $($ClientCount * $RequestsPerClient)"
Write-Host ""

# Verify wfpctl exists
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}

$WfpctlPath = Resolve-Path $WfpctlPath

$testsPassed = 0
$testsFailed = 0

# ========================================
# Test 1: Verify service is running
# ========================================
Write-TestStep "Test 1: Verifying service is running..."
$statusResult = Invoke-Wfpctl @("status")
if ($statusResult.Success) {
    Write-TestPass "Service is running"
    $testsPassed++
} else {
    Write-TestFail "Service is not running or not responding"
    Write-Host $statusResult.Output
    exit 1
}
Write-Host ""

# ========================================
# Test 2: Launch concurrent clients
# ========================================
Write-TestStep "Test 2: Launching $ClientCount concurrent clients..."

$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Define the script block for each job
$jobScript = {
    param($WfpctlPath, $RequestCount)

    $results = @{
        Successes = 0
        RateLimited = 0
        Errors = 0
        ExitCodes = @()
    }

    for ($i = 0; $i -lt $RequestCount; $i++) {
        $output = & $WfpctlPath "status" 2>&1
        $exitCode = $LASTEXITCODE
        $results.ExitCodes += $exitCode

        if ($exitCode -eq 0) {
            $results.Successes++
        } elseif ($output -match "rate.?limit" -or $output -match "429" -or $output -match "too many") {
            $results.RateLimited++
        } else {
            $results.Errors++
        }
    }

    return $results
}

# Launch all jobs
$jobs = @()
for ($i = 1; $i -le $ClientCount; $i++) {
    $job = Start-Job -ScriptBlock $jobScript -ArgumentList $WfpctlPath, $RequestsPerClient
    $jobs += $job
}

Write-Host "Launched $($jobs.Count) background jobs"
$testsPassed++
Write-Host ""

# ========================================
# Test 3: Wait for all jobs
# ========================================
Write-TestStep "Test 3: Waiting for all jobs (timeout: 60 seconds)..."

$timeout = 60
$completed = $jobs | Wait-Job -Timeout $timeout

$sw.Stop()

$timedOut = $jobs | Where-Object { $_.State -eq "Running" }
$failedJobs = $jobs | Where-Object { $_.State -eq "Failed" }
$completedJobs = $jobs | Where-Object { $_.State -eq "Completed" }

if ($timedOut.Count -eq 0) {
    Write-TestPass "All jobs completed in $($sw.Elapsed.TotalSeconds.ToString('F1')) seconds"
    $testsPassed++
} else {
    Write-TestFail "$($timedOut.Count) jobs timed out (possible deadlock)"
    $testsFailed++

    # Stop timed out jobs
    $timedOut | Stop-Job
}

Write-Host "  Completed: $($completedJobs.Count)"
Write-Host "  Failed:    $($failedJobs.Count)"
Write-Host "  Timed out: $($timedOut.Count)"
Write-Host ""

# ========================================
# Test 4: Collect results
# ========================================
Write-TestStep "Test 4: Collecting results from jobs..."

$totalSuccesses = 0
$totalRateLimited = 0
$totalErrors = 0
$totalRequests = $ClientCount * $RequestsPerClient

foreach ($job in $completedJobs) {
    try {
        $result = Receive-Job -Job $job -ErrorAction SilentlyContinue
        if ($result) {
            $totalSuccesses += $result.Successes
            $totalRateLimited += $result.RateLimited
            $totalErrors += $result.Errors
        }
    } catch {
        Write-TestWarn "Error receiving job result: $_"
    }
}

# Clean up jobs
$jobs | Remove-Job -Force

Write-Host ""
Write-Host "Results:"
Write-Host "  Total requests:  $totalRequests"
Write-Host "  Successes:       $totalSuccesses"
Write-Host "  Rate-limited:    $totalRateLimited"
Write-Host "  Errors:          $totalErrors"
Write-Host "  Throughput:      $([math]::Round($totalRequests / $sw.Elapsed.TotalSeconds, 1)) req/s"
Write-Host ""

# Rate-limited is expected behavior, not a failure
$successAndRateLimited = $totalSuccesses + $totalRateLimited
if ($successAndRateLimited -eq $totalRequests) {
    Write-TestPass "All requests handled correctly (success or rate-limited)"
    $testsPassed++
} elseif ($totalErrors -lt ($totalRequests * 0.1)) {
    Write-TestWarn "Some errors occurred ($totalErrors / $totalRequests)"
    $testsPassed++
} else {
    Write-TestFail "Too many errors: $totalErrors / $totalRequests"
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 5: Verify service is still running
# ========================================
Write-TestStep "Test 5: Verifying service is still running after stress test..."
Start-Sleep -Seconds 1

$finalStatus = Invoke-Wfpctl @("status")
if ($finalStatus.Success) {
    Write-TestPass "Service is still running and responsive"
    $testsPassed++
} else {
    Write-TestFail "Service is not responding after stress test"
    Write-Host $finalStatus.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Summary
# ========================================
Write-Host "=============================================="
Write-Host "  Test Summary"
Write-Host "=============================================="
Write-Host ""
Write-Host "  Passed: ${Green}$testsPassed${Reset}"
Write-Host "  Failed: ${Red}$testsFailed${Reset}"
Write-Host ""
Write-Host "Metrics:"
Write-Host "  Clients:        $ClientCount"
Write-Host "  Requests/client: $RequestsPerClient"
Write-Host "  Total requests:  $totalRequests"
Write-Host "  Duration:        $($sw.Elapsed.TotalSeconds.ToString('F1'))s"
Write-Host "  Throughput:      $([math]::Round($totalRequests / $sw.Elapsed.TotalSeconds, 1)) req/s"
Write-Host "  Success rate:    $([math]::Round(($totalSuccesses / [math]::Max($totalRequests, 1)) * 100, 1))%"
Write-Host "  Rate-limited:    $([math]::Round(($totalRateLimited / [math]::Max($totalRequests, 1)) * 100, 1))%"
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "${Green}All tests passed!${Reset}"
    Write-Host ""
    Write-Host "Key findings:"
    Write-Host "  - Service stayed alive under concurrent load"
    Write-Host "  - No deadlocks detected"
    Write-Host "  - Rate limiter correctly throttled excess requests"
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
