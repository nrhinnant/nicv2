#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Smoke test for outbound UDP block rules.

.DESCRIPTION
    This script tests WfpTrafficControl outbound UDP blocking by:
    1. Verifying initial DNS resolution works to a public DNS server
    2. Applying an outbound UDP block policy
    3. Verifying DNS queries to that server are blocked
    4. Rolling back the policy
    5. Verifying DNS resolution is restored

    Uses DNS as a practical test since DNS over UDP is common.
    Requires the WfpTrafficControl service to be running.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER DnsServer
    DNS server to block. Defaults to 8.8.8.8 (Google DNS).

.PARAMETER TestDomain
    Domain to use for DNS queries. Defaults to example.com.

.PARAMETER SkipCleanup
    If specified, leaves the policy applied without cleanup.

.EXAMPLE
    .\scripts\Test-UdpBlock.ps1

.EXAMPLE
    .\scripts\Test-UdpBlock.ps1 -DnsServer 1.1.1.1

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [string]$DnsServer = "8.8.8.8",

    [Parameter()]
    [string]$TestDomain = "example.com",

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

function Test-DnsQuery {
    param(
        [string]$Domain,
        [string]$Server,
        [int]$TimeoutSeconds = 5
    )

    try {
        # Use Resolve-DnsName with a specific server
        # -DnsOnly forces UDP-only query (no fallback to TCP or other methods)
        $result = Resolve-DnsName -Name $Domain -Server $Server -Type A -DnsOnly -ErrorAction Stop
        return @{
            Success = $true
            Result = $result
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
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
Write-Host "  WfpTrafficControl Outbound UDP Block Test"
Write-Host "=============================================="
Write-Host ""
Write-Host "DNS Server: $DnsServer"
Write-Host "Test Domain: $TestDomain"
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
$policyFile = $null

try {
    # ========================================
    # Test 1: Verify initial DNS connectivity
    # ========================================
    Write-TestStep "Test 1: Verifying initial DNS resolution via $DnsServer..."
    $initialDns = Test-DnsQuery -Domain $TestDomain -Server $DnsServer
    if ($initialDns.Success) {
        Write-TestPass "Initial DNS resolution succeeded"
        Write-Host "  Resolved: $($initialDns.Result | Select-Object -First 1 | ForEach-Object { $_.IPAddress })"
        $testsPassed++
    } else {
        Write-TestFail "Cannot resolve DNS via $DnsServer even before applying policy"
        Write-Host "  Error: $($initialDns.Error)"
        Write-TestWarn "This might indicate network issues or the DNS server is unreachable."
        Write-TestWarn "Try a different DNS server with -DnsServer"
        $testsFailed++
        exit 1
    }
    Write-Host ""

    # ========================================
    # Test 2: Create and apply outbound UDP block policy
    # ========================================
    Write-TestStep "Test 2: Creating outbound UDP block policy for $DnsServer`:53..."

    # Create temporary policy file
    $policyFile = [System.IO.Path]::GetTempFileName()
    $policyFile = [System.IO.Path]::ChangeExtension($policyFile, ".json")

    $policy = @{
        version = "1.0.0"
        defaultAction = "allow"
        updatedAt = (Get-Date).ToString("o")
        rules = @(
            @{
                id = "test-udp-dns-block"
                action = "block"
                direction = "outbound"
                protocol = "udp"
                remote = @{
                    ip = $DnsServer
                    ports = "53"
                }
                priority = 100
                enabled = $true
                comment = "Test outbound UDP block rule for DNS"
            }
        )
    }

    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyFile -Encoding UTF8
    Write-Host "Policy file created: $policyFile"

    Write-TestStep "Applying outbound UDP block policy..."
    $applyResult = Invoke-Wfpctl @("apply", $policyFile)
    if ($applyResult.Success) {
        Write-TestPass "Outbound UDP block policy applied successfully"
        Write-Host $applyResult.Output
        $testsPassed++
    } else {
        Write-TestFail "Failed to apply outbound UDP block policy"
        Write-Host $applyResult.Output
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 3: Verify DNS is blocked
    # ========================================
    Write-TestStep "Test 3: Verifying DNS resolution via $DnsServer is blocked..."
    Start-Sleep -Seconds 1  # Give WFP a moment to apply

    $blockedDns = Test-DnsQuery -Domain $TestDomain -Server $DnsServer
    if (-not $blockedDns.Success) {
        Write-TestPass "DNS query blocked as expected (cannot resolve via $DnsServer)"
        Write-Host "  Error (expected): $($blockedDns.Error)"
        $testsPassed++
    } else {
        Write-TestFail "DNS query was NOT blocked (can still resolve via $DnsServer)"
        Write-Host "  Unexpectedly resolved: $($blockedDns.Result | Select-Object -First 1 | ForEach-Object { $_.IPAddress })"
        Write-Host "  This might indicate the UDP filter is not working correctly."
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 4: Verify other DNS still works (optional)
    # ========================================
    Write-TestStep "Test 4: Verifying DNS via system resolver still works..."
    try {
        # Use default DNS (not the blocked one)
        $systemDns = Resolve-DnsName -Name $TestDomain -Type A -ErrorAction Stop
        if ($systemDns) {
            Write-TestPass "System DNS resolution still works (using default resolver)"
            $testsPassed++
        }
    }
    catch {
        Write-TestWarn "System DNS resolution failed (might be using blocked DNS server)"
        Write-Host "  This is not necessarily a failure - depends on your DNS config"
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
        # Test 6: Verify DNS is restored
        # ========================================
        Write-TestStep "Test 6: Verifying DNS resolution via $DnsServer is restored..."
        Start-Sleep -Seconds 1  # Give WFP a moment to remove filter

        $restoredDns = Test-DnsQuery -Domain $TestDomain -Server $DnsServer
        if ($restoredDns.Success) {
            Write-TestPass "DNS resolution restored as expected"
            Write-Host "  Resolved: $($restoredDns.Result | Select-Object -First 1 | ForEach-Object { $_.IPAddress })"
            $testsPassed++
        } else {
            Write-TestFail "DNS resolution not restored (still cannot resolve via $DnsServer)"
            Write-Host "  Error: $($restoredDns.Error)"
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
    Write-Host "Outbound UDP blocking is working correctly."
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
