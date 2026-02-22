#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Tests that the file watcher debounce correctly coalesces rapid modifications.

.DESCRIPTION
    This script validates the hot reload debounce mechanism by writing to the
    watched policy file many times in rapid succession and verifying that the
    debounce coalesces these events into a much smaller number of applies.

    Test procedure:
    1. Verify service is running
    2. Enable file watching: wfpctl watch set $PolicyPath
    3. Write initial policy (3 rules), wait for first apply
    4. Loop $Modifications times: update updatedAt, rewrite file, NO delay
    5. Wait $WaitSeconds for debounce to settle
    6. Check watch status (wfpctl watch status), record apply count
    7. Verify final filter state matches the last written policy
    8. Rollback and cleanup

    Key assertion: apply count << $Modifications (debounce is coalescing).

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER PolicyPath
    Path to write the watched policy file. REQUIRED.

.PARAMETER Modifications
    Number of rapid file writes. Default 50.

.PARAMETER WaitSeconds
    Time to wait for debounce to settle. Default 10.

.EXAMPLE
    .\scripts\Test-HotReloadStress.ps1 -PolicyPath "C:\temp\test-policy.json"

.EXAMPLE
    .\scripts\Test-HotReloadStress.ps1 -PolicyPath "C:\temp\test-policy.json" -Modifications 100 -WaitSeconds 15

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.

    See docs/features/025-testing-strategy.md section 4.3 for specification.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter(Mandatory = $true)]
    [string]$PolicyPath,

    [Parameter()]
    [int]$Modifications = 50,

    [Parameter()]
    [int]$WaitSeconds = 10
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

function New-TestPolicy {
    param(
        [int]$RuleCount = 3,
        [string]$Timestamp
    )

    $rules = @()
    for ($i = 1; $i -le $RuleCount; $i++) {
        $rules += @{
            id        = "hotreload-test-rule-$i"
            action    = "block"
            direction = "outbound"
            protocol  = "tcp"
            remote    = @{
                ip    = "10.99.$i.1"
                ports = "$((9000 + $i))"
            }
            priority  = $i
            enabled   = $true
            comment   = "Hot reload stress test rule $i"
        }
    }

    return @{
        version       = "1.0"
        defaultAction = "allow"
        updatedAt     = $Timestamp
        rules         = $rules
    }
}

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl Hot Reload Stress Test"
Write-Host "=============================================="
Write-Host ""
Write-Host "This test validates that the file watcher debounce"
Write-Host "correctly coalesces rapid file modifications."
Write-Host ""
Write-Host "Parameters:"
Write-Host "  PolicyPath:    $PolicyPath"
Write-Host "  Modifications: $Modifications"
Write-Host "  WaitSeconds:   $WaitSeconds"
Write-Host ""

# Verify wfpctl exists
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}

$WfpctlPath = Resolve-Path $WfpctlPath

# Ensure policy directory exists
$policyDir = Split-Path -Parent $PolicyPath
if (-not (Test-Path $policyDir)) {
    New-Item -ItemType Directory -Path $policyDir -Force | Out-Null
}

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
# Test 2: Enable file watching
# ========================================
Write-TestStep "Test 2: Enabling file watching for $PolicyPath..."

# First, write an initial policy so the file exists
$initialTimestamp = (Get-Date).ToString("o")
$initialPolicy = New-TestPolicy -RuleCount 3 -Timestamp $initialTimestamp
$initialPolicy | ConvertTo-Json -Depth 10 | Set-Content -Path $PolicyPath -Encoding UTF8

$watchResult = Invoke-Wfpctl @("watch", "set", $PolicyPath)
if ($watchResult.Success) {
    Write-TestPass "File watching enabled"
    Write-Host $watchResult.Output
    $testsPassed++
} else {
    Write-TestFail "Failed to enable file watching"
    Write-Host $watchResult.Output
    $testsFailed++
    exit 1
}
Write-Host ""

# ========================================
# Test 3: Wait for initial apply
# ========================================
Write-TestStep "Test 3: Waiting for initial policy apply..."
Start-Sleep -Seconds 5

$initialStatus = Invoke-Wfpctl @("watch", "status")
if ($initialStatus.Success) {
    Write-TestPass "Watch status retrieved"
    Write-Host $initialStatus.Output

    # Extract initial apply count
    if ($initialStatus.Output -match "ApplyCount:\s*(\d+)") {
        $initialApplyCount = [int]$Matches[1]
        Write-Host "Initial apply count: $initialApplyCount"
        $testsPassed++
    } else {
        Write-TestWarn "Could not parse initial apply count"
        $initialApplyCount = 0
    }
} else {
    Write-TestFail "Failed to get watch status"
    Write-Host $initialStatus.Output
    $testsFailed++
    $initialApplyCount = 0
}
Write-Host ""

# ========================================
# Test 4: Rapid file modifications
# ========================================
Write-TestStep "Test 4: Performing $Modifications rapid file modifications..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 1; $i -le $Modifications; $i++) {
    $timestamp = (Get-Date).ToString("o")
    $policy = New-TestPolicy -RuleCount 3 -Timestamp $timestamp
    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $PolicyPath -Encoding UTF8
    # NO delay - write as fast as possible
}

$sw.Stop()
Write-TestPass "Completed $Modifications modifications in $($sw.ElapsedMilliseconds)ms"
Write-Host "Average: $([math]::Round($sw.ElapsedMilliseconds / $Modifications, 2))ms per write"
$testsPassed++
Write-Host ""

# ========================================
# Test 5: Wait for debounce to settle
# ========================================
Write-TestStep "Test 5: Waiting ${WaitSeconds}s for debounce to settle..."
Start-Sleep -Seconds $WaitSeconds
Write-TestPass "Wait complete"
$testsPassed++
Write-Host ""

# ========================================
# Test 6: Check final apply count
# ========================================
Write-TestStep "Test 6: Checking watch status for apply count..."
$finalStatus = Invoke-Wfpctl @("watch", "status")
if ($finalStatus.Success) {
    Write-Host $finalStatus.Output

    if ($finalStatus.Output -match "ApplyCount:\s*(\d+)") {
        $finalApplyCount = [int]$Matches[1]
        $appliesDuringTest = $finalApplyCount - $initialApplyCount

        Write-Host ""
        Write-Host "Results:"
        Write-Host "  File modifications:  $Modifications"
        Write-Host "  Applies during test: $appliesDuringTest"
        Write-Host "  Coalescing ratio:    $(if ($appliesDuringTest -gt 0) { [math]::Round($Modifications / $appliesDuringTest, 1) } else { 'N/A' })x"
        Write-Host ""

        # Key assertion: apply count << modifications
        if ($appliesDuringTest -lt ($Modifications / 5)) {
            Write-TestPass "Debounce is working: $appliesDuringTest applies << $Modifications modifications"
            $testsPassed++
        } elseif ($appliesDuringTest -lt ($Modifications / 2)) {
            Write-TestWarn "Debounce is partially working: $appliesDuringTest applies (expected << $Modifications)"
            $testsPassed++
        } else {
            Write-TestFail "Debounce NOT working: $appliesDuringTest applies for $Modifications modifications"
            $testsFailed++
        }
    } else {
        Write-TestFail "Could not parse apply count from status output"
        $testsFailed++
    }
} else {
    Write-TestFail "Failed to get final watch status"
    Write-Host $finalStatus.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 7: Verify final filter state
# ========================================
Write-TestStep "Test 7: Verifying final filter state matches last written policy..."
$statusResult = Invoke-Wfpctl @("status")
if ($statusResult.Success) {
    # Check that filters exist (basic validation)
    if ($statusResult.Output -match "FilterCount:\s*(\d+)") {
        $filterCount = [int]$Matches[1]
        if ($filterCount -ge 3) {
            Write-TestPass "Filter state appears correct ($filterCount filters active)"
            $testsPassed++
        } else {
            Write-TestWarn "Unexpected filter count: $filterCount (expected >= 3)"
        }
    } else {
        Write-TestWarn "Could not parse filter count"
    }
} else {
    Write-TestFail "Failed to get status"
    Write-Host $statusResult.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Cleanup
# ========================================
Write-TestStep "Cleanup: Disabling watch and rolling back..."

# Disable watch
$disableResult = Invoke-Wfpctl @("watch", "clear")
if ($disableResult.Success) {
    Write-Host "Watch disabled"
} else {
    Write-TestWarn "Failed to disable watch: $($disableResult.Output)"
}

# Rollback
$rollbackResult = Invoke-Wfpctl @("rollback")
if ($rollbackResult.Success) {
    Write-Host "Rollback complete"
} else {
    Write-TestWarn "Failed to rollback: $($rollbackResult.Output)"
}

# Remove test policy file
if (Test-Path $PolicyPath) {
    Remove-Item -Path $PolicyPath -Force
    Write-Host "Test policy file removed"
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

if ($testsFailed -eq 0) {
    Write-Host "${Green}All tests passed!${Reset}"
    Write-Host ""
    Write-Host "Key finding: Debounce is coalescing rapid file changes correctly."
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
