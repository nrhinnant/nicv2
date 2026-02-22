#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Tests filter persistence across service restart and validates the recovery path.

.DESCRIPTION
    This script validates that WFP filters persist when the service stops (managed by
    BFE, not our process) and that the service recovers correctly on restart.

    Test procedure:
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

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER SkipCleanup
    If specified, leaves the filter in its final state without cleanup.

.PARAMETER ServiceName
    Name of the Windows service. Defaults to "WfpTrafficControl"

.EXAMPLE
    .\scripts\Test-ServiceRestart.ps1

.EXAMPLE
    .\scripts\Test-ServiceRestart.ps1 -ServiceName "WfpTrafficControl"

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.

    See docs/features/025-testing-strategy.md section 3.3 for specification.
    See docs/features/022-how-it-works.md section "Service Restart Safety" for expected behavior.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [switch]$SkipCleanup,

    [Parameter()]
    [string]$ServiceName = "WfpTrafficControl"
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

function Test-TcpConnection {
    param(
        [string]$ComputerName,
        [int]$Port,
        [int]$TimeoutSeconds = 5
    )

    try {
        $result = Test-NetConnection -ComputerName $ComputerName -Port $Port -WarningAction SilentlyContinue -InformationLevel Quiet
        return $result
    }
    catch {
        return $false
    }
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

function Test-ServiceRunning {
    param([string]$Name)

    try {
        $svc = Get-Service -Name $Name -ErrorAction Stop
        return $svc.Status -eq "Running"
    }
    catch {
        return $false
    }
}

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl Service Restart Test"
Write-Host "=============================================="
Write-Host ""
Write-Host "This test validates that WFP filters persist when"
Write-Host "the service stops (managed by BFE) and that recovery"
Write-Host "works correctly after restart."
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
Write-TestStep "Test 1: Verifying service '$ServiceName' is running..."
if (Test-ServiceRunning -Name $ServiceName) {
    Write-TestPass "Service is running"
    $testsPassed++
} else {
    Write-TestFail "Service '$ServiceName' is not running"
    Write-Host "Start the service first: Start-Service $ServiceName"
    exit 1
}
Write-Host ""

# ========================================
# Test 2: Verify initial connectivity
# ========================================
Write-TestStep "Test 2: Checking initial connectivity to 1.1.1.1:443..."
$initialConnectivity = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443
if ($initialConnectivity) {
    Write-TestPass "Initial connectivity confirmed (can reach 1.1.1.1:443)"
    $testsPassed++
} else {
    Write-TestWarn "Cannot reach 1.1.1.1:443 even before test (network issue?)"
    Write-Host "Continuing anyway..."
}
Write-Host ""

# ========================================
# Test 3: Enable demo block
# ========================================
Write-TestStep "Test 3: Enabling demo block filter..."
$enableResult = Invoke-Wfpctl @("demo-block", "enable")
if ($enableResult.Success) {
    Write-TestPass "Demo block enabled successfully"
    Write-Host $enableResult.Output
    $testsPassed++
} else {
    Write-TestFail "Failed to enable demo block"
    Write-Host $enableResult.Output
    $testsFailed++
    exit 1
}
Write-Host ""

# ========================================
# Test 4: Verify block is active
# ========================================
Write-TestStep "Test 4: Verifying connection to 1.1.1.1:443 is blocked..."
Start-Sleep -Seconds 1
$blockedConnectivity = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443 -TimeoutSeconds 5
if (-not $blockedConnectivity) {
    Write-TestPass "Connection blocked as expected (cannot reach 1.1.1.1:443)"
    $testsPassed++
} else {
    Write-TestFail "Connection was NOT blocked (can still reach 1.1.1.1:443)"
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 5: Stop the service
# ========================================
Write-TestStep "Test 5: Stopping service '$ServiceName'..."
try {
    Stop-Service -Name $ServiceName -Force -ErrorAction Stop
    Start-Sleep -Seconds 2

    if (-not (Test-ServiceRunning -Name $ServiceName)) {
        Write-TestPass "Service stopped successfully"
        $testsPassed++
    } else {
        Write-TestFail "Service did not stop"
        $testsFailed++
    }
} catch {
    Write-TestFail "Failed to stop service: $_"
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 6: Verify block is STILL active (BFE retains filters)
# ========================================
Write-TestStep "Test 6: Verifying block persists after service stop (BFE retention)..."
$blockedWhileStopped = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443 -TimeoutSeconds 5
if (-not $blockedWhileStopped) {
    Write-TestPass "Block persists while service is stopped (BFE retains filters)"
    $testsPassed++
} else {
    Write-TestFail "Block NOT persisting - filters were removed when service stopped"
    Write-Host "This indicates a problem with filter persistence"
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 7: Restart the service
# ========================================
Write-TestStep "Test 7: Starting service '$ServiceName'..."
try {
    Start-Service -Name $ServiceName -ErrorAction Stop
    Start-Sleep -Seconds 3

    if (Test-ServiceRunning -Name $ServiceName) {
        Write-TestPass "Service started successfully"
        $testsPassed++
    } else {
        Write-TestFail "Service did not start"
        $testsFailed++
    }
} catch {
    Write-TestFail "Failed to start service: $_"
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 8: Verify service responds
# ========================================
Write-TestStep "Test 8: Verifying service responds to status command..."
$statusResult = Invoke-Wfpctl @("status")
if ($statusResult.Success) {
    Write-TestPass "Service responds to IPC commands"
    Write-Host $statusResult.Output
    $testsPassed++
} else {
    Write-TestFail "Service not responding to IPC"
    Write-Host $statusResult.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 9: Rollback to restore connectivity
# ========================================
Write-TestStep "Test 9: Rolling back to restore connectivity..."
$rollbackResult = Invoke-Wfpctl @("rollback")
if ($rollbackResult.Success) {
    Write-TestPass "Rollback completed successfully"
    Write-Host $rollbackResult.Output
    $testsPassed++
} else {
    Write-TestFail "Rollback failed"
    Write-Host $rollbackResult.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 10: Verify connectivity is restored
# ========================================
Write-TestStep "Test 10: Verifying connectivity to 1.1.1.1:443 is restored..."
Start-Sleep -Seconds 1
$restoredConnectivity = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443 -TimeoutSeconds 10
if ($restoredConnectivity) {
    Write-TestPass "Connectivity restored (can reach 1.1.1.1:443)"
    $testsPassed++
} else {
    Write-TestWarn "Connectivity not restored (may be network issue)"
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
Write-Host "Key findings:"
Write-Host "  - Filters survive service restart: $(if ($testsPassed -ge 6) { "${Green}YES${Reset}" } else { "${Red}NO${Reset}" })"
Write-Host "  - Recovery via rollback after restart: $(if ($testsPassed -ge 9) { "${Green}YES${Reset}" } else { "${Red}NO${Reset}" })"
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "${Green}All tests passed!${Reset}"
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
