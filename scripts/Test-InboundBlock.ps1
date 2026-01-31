#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Smoke test for inbound TCP block rules.

.DESCRIPTION
    This script tests WfpTrafficControl inbound TCP blocking by:
    1. Starting a TCP listener on a test port
    2. Verifying initial connectivity works
    3. Applying an inbound block policy
    4. Verifying inbound connections are blocked
    5. Rolling back the policy
    6. Verifying connectivity is restored

    Requires the WfpTrafficControl service to be running.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER TestPort
    Port to use for testing. Defaults to 19876.

.PARAMETER SkipCleanup
    If specified, leaves the policy applied without cleanup.

.EXAMPLE
    .\scripts\Test-InboundBlock.ps1

.EXAMPLE
    .\scripts\Test-InboundBlock.ps1 -TestPort 9999

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [int]$TestPort = 19876,

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

function Test-LocalTcpConnection {
    param(
        [int]$Port,
        [int]$TimeoutMilliseconds = 3000
    )

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $asyncResult = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
        $waitHandle = $asyncResult.AsyncWaitHandle

        $success = $waitHandle.WaitOne($TimeoutMilliseconds, $false)

        if ($success) {
            $client.EndConnect($asyncResult)
            $client.Close()
            return $true
        } else {
            $client.Close()
            return $false
        }
    }
    catch {
        return $false
    }
}

function Start-TestListener {
    param([int]$Port)

    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        return $listener
    }
    catch {
        return $null
    }
}

function Stop-TestListener {
    param($Listener)

    if ($Listener -ne $null) {
        try {
            $Listener.Stop()
        }
        catch {
            # Ignore errors during cleanup
        }
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
Write-Host "  WfpTrafficControl Inbound Block Smoke Test"
Write-Host "=============================================="
Write-Host ""
Write-Host "Test Port: $TestPort"
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
$listener = $null
$policyFile = $null

try {
    # ========================================
    # Test 1: Start TCP listener
    # ========================================
    Write-TestStep "Test 1: Starting TCP listener on port $TestPort..."
    $listener = Start-TestListener -Port $TestPort
    if ($listener -ne $null) {
        Write-TestPass "TCP listener started on port $TestPort"
        $testsPassed++
    } else {
        Write-TestFail "Failed to start TCP listener on port $TestPort"
        Write-Host "Port might be in use. Try a different port with -TestPort"
        exit 1
    }
    Write-Host ""

    # ========================================
    # Test 2: Verify initial connectivity
    # ========================================
    Write-TestStep "Test 2: Verifying initial connectivity to localhost:$TestPort..."
    $initialConnectivity = Test-LocalTcpConnection -Port $TestPort
    if ($initialConnectivity) {
        Write-TestPass "Initial connectivity confirmed (can reach localhost:$TestPort)"
        $testsPassed++
    } else {
        Write-TestFail "Cannot connect to localhost:$TestPort even before applying policy"
        $testsFailed++
        exit 1
    }
    Write-Host ""

    # ========================================
    # Test 3: Create and apply inbound block policy
    # ========================================
    Write-TestStep "Test 3: Creating inbound block policy..."

    # Create temporary policy file
    $policyFile = [System.IO.Path]::GetTempFileName()
    $policyFile = [System.IO.Path]::ChangeExtension($policyFile, ".json")

    $policy = @{
        version = "1.0.0"
        defaultAction = "allow"
        updatedAt = (Get-Date).ToString("o")
        rules = @(
            @{
                id = "test-inbound-block"
                action = "block"
                direction = "inbound"
                protocol = "tcp"
                remote = @{
                    ports = "$TestPort"
                }
                priority = 100
                enabled = $true
                comment = "Test inbound block rule"
            }
        )
    }

    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyFile -Encoding UTF8
    Write-Host "Policy file created: $policyFile"

    Write-TestStep "Applying inbound block policy..."
    $applyResult = Invoke-Wfpctl @("apply", $policyFile)
    if ($applyResult.Success) {
        Write-TestPass "Inbound block policy applied successfully"
        Write-Host $applyResult.Output
        $testsPassed++
    } else {
        Write-TestFail "Failed to apply inbound block policy"
        Write-Host $applyResult.Output
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 4: Verify inbound connection is blocked
    # ========================================
    Write-TestStep "Test 4: Verifying inbound connection to localhost:$TestPort is blocked..."
    Start-Sleep -Seconds 1  # Give WFP a moment to apply

    $blockedConnectivity = Test-LocalTcpConnection -Port $TestPort -TimeoutMilliseconds 3000
    if (-not $blockedConnectivity) {
        Write-TestPass "Inbound connection blocked as expected (cannot reach localhost:$TestPort)"
        $testsPassed++
    } else {
        Write-TestFail "Inbound connection was NOT blocked (can still reach localhost:$TestPort)"
        Write-Host "This might indicate the inbound filter is not working correctly"
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 5: Rollback policy
    # ========================================
    if (-not $SkipCleanup) {
        Write-TestStep "Test 5: Rolling back policy..."
        $rollbackResult = Invoke-Wfpctl @("rollback")
        if ($rollbackResult.Success) {
            Write-TestPass "Policy rollback completed successfully"
            Write-Host $rollbackResult.Output
            $testsPassed++
        } else {
            Write-TestFail "Failed to rollback policy"
            Write-Host $rollbackResult.Output
            $testsFailed++
        }
        Write-Host ""

        # ========================================
        # Test 6: Verify connectivity is restored
        # ========================================
        Write-TestStep "Test 6: Verifying connectivity to localhost:$TestPort is restored..."
        Start-Sleep -Seconds 1  # Give WFP a moment to remove filter

        $restoredConnectivity = Test-LocalTcpConnection -Port $TestPort
        if ($restoredConnectivity) {
            Write-TestPass "Connectivity restored as expected (can reach localhost:$TestPort)"
            $testsPassed++
        } else {
            Write-TestFail "Connectivity not restored (cannot reach localhost:$TestPort)"
            $testsFailed++
        }
        Write-Host ""
    } else {
        Write-TestWarn "Skipping cleanup as requested. Policy is still applied."
        Write-Host "Run 'wfpctl rollback' to remove the test filter."
        Write-Host ""
    }

}
finally {
    # ========================================
    # Cleanup
    # ========================================
    Write-TestStep "Cleaning up..."

    # Stop listener
    if ($listener -ne $null) {
        Stop-TestListener -Listener $listener
        Write-Host "  TCP listener stopped"
    }

    # Remove policy file
    if ($policyFile -ne $null -and (Test-Path $policyFile)) {
        Remove-Item $policyFile -Force
        Write-Host "  Policy file removed"
    }

    Write-Host ""
}

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
    Write-Host "Inbound TCP blocking is working correctly."
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
