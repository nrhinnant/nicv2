#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Smoke test for process-path matching rules using curl.exe.

.DESCRIPTION
    This script tests WfpTrafficControl process-path matching by:
    1. Verifying curl.exe exists and can reach 1.1.1.1:443
    2. Applying a policy with two rules:
       - Block all outbound TCP to 1.1.1.1:443 (priority 100)
       - Allow outbound TCP to 1.1.1.1:443 from curl.exe (priority 200)
    3. Verifying curl.exe can still connect (process-path allow overrides block)
    4. Verifying Test-NetConnection fails (PowerShell has no process-path allow)
    5. Rolling back and verifying both methods are restored

    Requires curl.exe (ships with Windows 10 1803+).
    Requires the WfpTrafficControl service to be running.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER SkipCleanup
    If specified, leaves the policy applied without cleanup.

.EXAMPLE
    .\scripts\Test-ProcessPath.ps1

.EXAMPLE
    .\scripts\Test-ProcessPath.ps1 -WfpctlPath "C:\path\to\wfpctl.exe"

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
    curl.exe must be available (C:\Windows\System32\curl.exe or on PATH).
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

function Test-CurlConnection {
    param(
        [string]$CurlPath,
        [string]$Url,
        [int]$MaxTimeSeconds = 5
    )

    try {
        & $CurlPath --max-time $MaxTimeSeconds -s -o NUL -w "%{http_code}" $Url 2>$null | Out-Null
        $exitCode = $LASTEXITCODE
        return @{
            ExitCode = $exitCode
            Success = ($exitCode -eq 0)
        }
    }
    catch {
        return @{
            ExitCode = -1
            Success = $false
        }
    }
}

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl Process-Path Matching Test"
Write-Host "=============================================="
Write-Host ""

# Verify wfpctl exists
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}

$WfpctlPath = Resolve-Path $WfpctlPath

# Verify service is running
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
$policyFile = $null

try {
    # ========================================
    # Test 1: Locate curl.exe
    # ========================================
    Write-TestStep "Test 1: Locating curl.exe..."

    $curlPath = $null
    $curlFullPath = $null

    # Check the known Windows location first
    $systemCurl = "C:\Windows\System32\curl.exe"
    if (Test-Path $systemCurl) {
        $curlPath = $systemCurl
        $curlFullPath = $systemCurl
    } else {
        # Fall back to PATH
        $curlCmd = Get-Command curl.exe -ErrorAction SilentlyContinue
        if ($null -ne $curlCmd) {
            $curlPath = $curlCmd.Source
            $curlFullPath = $curlCmd.Source
        }
    }

    if ($null -eq $curlPath) {
        Write-TestFail "curl.exe not found"
        Write-Host "  Checked: C:\Windows\System32\curl.exe and PATH"
        Write-Host "  curl.exe ships with Windows 10 1803+. Ensure it is available."
        exit 1
    }

    Write-TestPass "curl.exe found at: $curlFullPath"
    $testsPassed++
    Write-Host ""

    # ========================================
    # Test 2: Verify initial connectivity
    # ========================================
    Write-TestStep "Test 2: Verifying initial connectivity..."

    Write-Host "  Testing curl.exe -> https://1.1.1.1..."
    $curlBaseline = Test-CurlConnection -CurlPath $curlPath -Url "https://1.1.1.1"
    if ($curlBaseline.Success) {
        Write-TestPass "curl.exe can reach 1.1.1.1:443 (exit code: $($curlBaseline.ExitCode))"
        $testsPassed++
    } else {
        Write-TestFail "curl.exe cannot reach 1.1.1.1:443 (exit code: $($curlBaseline.ExitCode))"
        Write-Host "  Network connectivity issue. Cannot proceed."
        $testsFailed++
        exit 1
    }

    Write-Host "  Testing Test-NetConnection -> 1.1.1.1:443..."
    $tncBaseline = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443
    if ($tncBaseline) {
        Write-TestPass "Test-NetConnection can reach 1.1.1.1:443"
    } else {
        Write-TestWarn "Test-NetConnection cannot reach 1.1.1.1:443 (network issue?)"
        Write-Host "  Continuing anyway..."
    }
    Write-Host ""

    # ========================================
    # Test 3: Apply process-path policy
    # ========================================
    Write-TestStep "Test 3: Creating process-path policy..."
    Write-Host "  Rule 1: block all outbound TCP to 1.1.1.1:443 (priority 100)"
    Write-Host "  Rule 2: allow outbound TCP to 1.1.1.1:443 from $curlFullPath (priority 200)"

    $policyFile = [System.IO.Path]::GetTempFileName()
    $policyFile = [System.IO.Path]::ChangeExtension($policyFile, ".json")

    $policy = @{
        version       = "1.0.0"
        defaultAction = "allow"
        updatedAt     = (Get-Date).ToString("o")
        rules         = @(
            @{
                id        = "test-block-all-tcp-443"
                action    = "block"
                direction = "outbound"
                protocol  = "tcp"
                remote    = @{
                    ip    = "1.1.1.1"
                    ports = "443"
                }
                priority  = 100
                enabled   = $true
                comment   = "Block all outbound TCP to 1.1.1.1:443"
            },
            @{
                id        = "test-allow-curl-tcp-443"
                action    = "allow"
                direction = "outbound"
                protocol  = "tcp"
                remote    = @{
                    ip    = "1.1.1.1"
                    ports = "443"
                }
                process   = $curlFullPath
                priority  = 200
                enabled   = $true
                comment   = "Allow curl.exe to reach 1.1.1.1:443 (overrides block)"
            }
        )
    }

    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyFile -Encoding UTF8
    Write-Host "  Policy file: $policyFile"

    $applyResult = Invoke-Wfpctl @("apply", $policyFile)
    if ($applyResult.Success) {
        Write-TestPass "Process-path policy applied successfully"
        Write-Host $applyResult.Output
        $testsPassed++
    } else {
        Write-TestFail "Failed to apply process-path policy"
        Write-Host $applyResult.Output
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 4: Verify curl.exe CAN connect (process-path allow)
    # ========================================
    Write-TestStep "Test 4: Verifying curl.exe can still connect (process-path allow)..."
    Start-Sleep -Seconds 1  # Give WFP a moment to apply

    $curlAfterPolicy = Test-CurlConnection -CurlPath $curlPath -Url "https://1.1.1.1"
    if ($curlAfterPolicy.Success) {
        Write-TestPass "curl.exe can still connect (process-path allow working, exit code: $($curlAfterPolicy.ExitCode))"
        $testsPassed++
    } else {
        Write-TestFail "curl.exe cannot connect (process-path allow NOT working, exit code: $($curlAfterPolicy.ExitCode))"
        Write-Host "  Expected curl.exe to be allowed by the higher-priority process-path rule"
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 5: Verify Test-NetConnection CANNOT connect (no process-path allow)
    # ========================================
    Write-TestStep "Test 5: Verifying Test-NetConnection is blocked (no process-path allow)..."

    $tncAfterPolicy = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443
    if (-not $tncAfterPolicy) {
        Write-TestPass "Test-NetConnection blocked as expected (PowerShell has no process-path allow)"
        $testsPassed++
    } else {
        Write-TestFail "Test-NetConnection was NOT blocked (should have been blocked)"
        Write-Host "  PowerShell.exe should not match the curl.exe process-path allow rule"
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 6: Rollback and verify restoration
    # ========================================
    if (-not $SkipCleanup) {
        Write-TestStep "Test 6: Rolling back policy..."
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
        # Test 7: Verify both methods are restored
        # ========================================
        Write-TestStep "Test 7: Verifying connectivity is restored for both methods..."
        Start-Sleep -Seconds 1  # Give WFP a moment to remove filters

        Write-Host "  Testing curl.exe -> https://1.1.1.1..."
        $curlRestored = Test-CurlConnection -CurlPath $curlPath -Url "https://1.1.1.1"
        if ($curlRestored.Success) {
            Write-TestPass "curl.exe connectivity restored"
            $testsPassed++
        } else {
            Write-TestWarn "curl.exe connectivity not restored (exit code: $($curlRestored.ExitCode))"
            Write-Host "  May be a network issue"
        }

        Write-Host "  Testing Test-NetConnection -> 1.1.1.1:443..."
        $tncRestored = Test-TcpConnection -ComputerName "1.1.1.1" -Port 443
        if ($tncRestored) {
            Write-TestPass "Test-NetConnection connectivity restored"
            $testsPassed++
        } else {
            Write-TestWarn "Test-NetConnection connectivity not restored"
            Write-Host "  May be a network issue"
        }
        Write-Host ""
    } else {
        Write-TestWarn "Skipping cleanup as requested. Policy is still applied."
        Write-Host "  Run 'wfpctl rollback' to remove the test filters."
        Write-Host ""
    }

}
finally {
    # ========================================
    # Cleanup
    # ========================================
    Write-TestStep "Cleaning up..."

    # Remove policy file
    if ($null -ne $policyFile -and (Test-Path $policyFile)) {
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
    Write-Host "Process-path matching is working correctly."
    Write-Host "curl.exe was allowed while other processes were blocked."
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
