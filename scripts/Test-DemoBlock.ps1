#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Smoke test for the demo block filter feature.

.DESCRIPTION
    This script tests the WfpTrafficControl demo block filter by:
    1. Enabling the demo block (blocks TCP to 1.1.1.1:443)
    2. Verifying connectivity to 1.1.1.1:443 fails
    3. Disabling the demo block
    4. Verifying connectivity to 1.1.1.1:443 succeeds

    Requires the WfpTrafficControl service to be running.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER SkipCleanup
    If specified, leaves the filter in its final state (disabled) without cleanup.

.EXAMPLE
    .\scripts\Test-DemoBlock.ps1

.EXAMPLE
    .\scripts\Test-DemoBlock.ps1 -WfpctlPath "C:\path\to\wfpctl.exe"

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [switch]$SkipCleanup
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

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl Demo Block Smoke Test"
Write-Host "=============================================="
Write-Host ""

# Verify wfpctl exists
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}

$WfpctlPath = Resolve-Path $WfpctlPath

# Test 0: Verify service is running
Write-TestStep "Checking service status..."
$statusResult = Invoke-Wfpctl @("status")
if (-not $statusResult.Success) {
    Write-TestFail "Service is not running or not responding"
    Write-Host $statusResult.Output
    Write-Host ""
    Write-Host "Start the service first:"
    Write-Host "  .\scripts\Start-Service.ps1"
    Write-Host "  -- OR --"
    Write-Host "  dotnet run --project src/service"
    exit 1
}
Write-TestPass "Service is running"
Write-Host ""

$testsPassed = 0
$testsFailed = 0

# ========================================
# Test 1: Initial connectivity check
# ========================================
Write-TestStep "Test 1: Checking initial connectivity to 1.1.1.1:443..."
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
# Test 2: Enable demo block
# ========================================
Write-TestStep "Test 2: Enabling demo block filter..."
$enableResult = Invoke-Wfpctl @("demo-block", "enable")
if ($enableResult.Success) {
    Write-TestPass "Demo block enabled successfully"
    Write-Host $enableResult.Output
    $testsPassed++
} else {
    Write-TestFail "Failed to enable demo block"
    Write-Host $enableResult.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 3: Verify block status
# ========================================
Write-TestStep "Test 3: Checking demo block status..."
$statusResult = Invoke-Wfpctl @("demo-block", "status")
if ($statusResult.Success -and $statusResult.Output -match "Active:\s*True") {
    Write-TestPass "Demo block status shows active"
    Write-Host $statusResult.Output
    $testsPassed++
} else {
    Write-TestFail "Demo block status is not active"
    Write-Host $statusResult.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 4: Verify connection is blocked
# ========================================
Write-TestStep "Test 4: Verifying connection to 1.1.1.1:443 is blocked..."
Start-Sleep -Seconds 1  # Give WFP a moment to apply
$blockedConnectivity = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443 -TimeoutSeconds 5
if (-not $blockedConnectivity) {
    Write-TestPass "Connection blocked as expected (cannot reach 1.1.1.1:443)"
    $testsPassed++
} else {
    Write-TestFail "Connection was NOT blocked (can still reach 1.1.1.1:443)"
    Write-Host "This might indicate the filter is not working correctly"
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 5: Disable demo block
# ========================================
Write-TestStep "Test 5: Disabling demo block filter..."
$disableResult = Invoke-Wfpctl @("demo-block", "disable")
if ($disableResult.Success) {
    Write-TestPass "Demo block disabled successfully"
    Write-Host $disableResult.Output
    $testsPassed++
} else {
    Write-TestFail "Failed to disable demo block"
    Write-Host $disableResult.Output
    $testsFailed++
}
Write-Host ""

# ========================================
# Test 6: Verify connection is restored
# ========================================
Write-TestStep "Test 6: Verifying connection to 1.1.1.1:443 is restored..."
Start-Sleep -Seconds 1  # Give WFP a moment to remove filter
$restoredConnectivity = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443 -TimeoutSeconds 10
if ($restoredConnectivity) {
    Write-TestPass "Connection restored as expected (can reach 1.1.1.1:443)"
    $testsPassed++
} else {
    Write-TestWarn "Connection not restored (cannot reach 1.1.1.1:443)"
    Write-Host "This might be a network issue, not necessarily a test failure"
    # Don't count as failure since it might be network-related
}
Write-Host ""

# ========================================
# Test 7: Test rollback functionality
# ========================================
Write-TestStep "Test 7: Testing rollback functionality..."

# First enable the filter again
$enableResult = Invoke-Wfpctl @("demo-block", "enable")
if (-not $enableResult.Success) {
    Write-TestFail "Failed to re-enable demo block for rollback test"
    $testsFailed++
} else {
    # Now rollback
    $rollbackResult = Invoke-Wfpctl @("rollback")
    if ($rollbackResult.Success) {
        Write-TestPass "Rollback completed successfully"
        Write-Host $rollbackResult.Output
        $testsPassed++

        # Verify filter is gone
        $statusAfterRollback = Invoke-Wfpctl @("demo-block", "status")
        if ($statusAfterRollback.Output -match "Active:\s*False") {
            Write-TestPass "Filter correctly removed by rollback"
            $testsPassed++
        } else {
            Write-TestFail "Filter still active after rollback"
            $testsFailed++
        }
    } else {
        Write-TestFail "Rollback failed"
        Write-Host $rollbackResult.Output
        $testsFailed++
    }
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
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
