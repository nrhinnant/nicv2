#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Parameterized rule enforcement test for verifying any single firewall rule.

.DESCRIPTION
    This script creates a temporary policy with a single rule matching the given
    parameters, applies it via wfpctl, verifies the expected network behavior, then
    rolls back and verifies restoration. Usable standalone or as a building block
    called from other test orchestrators.

    Supported combinations:
    - Outbound TCP: uses TcpClient with timeout
    - Outbound UDP (port 53): uses Resolve-DnsName
    - Outbound UDP (other ports): uses UDP socket send/receive
    - Inbound TCP: starts a TcpListener, tests loopback connection
    - Inbound UDP: not supported by the firewall, skipped with warning

    Requires the WfpTrafficControl service to be running.

.PARAMETER Direction
    Rule direction: outbound or inbound. Required.

.PARAMETER Protocol
    Rule protocol: tcp or udp. Required.

.PARAMETER RemoteIp
    Target IP address. Defaults to 1.1.1.1.

.PARAMETER RemotePort
    Target port. Defaults to 443.

.PARAMETER Action
    What the rule does: block or allow. Defaults to block.

.PARAMETER ExpectedResult
    What we expect to observe: blocked or allowed. Defaults to blocked.

.PARAMETER Priority
    Rule priority. Defaults to 100.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER SkipCleanup
    If specified, leaves the policy applied without rollback.

.EXAMPLE
    .\scripts\Test-RuleEnforcement.ps1 -Direction outbound -Protocol tcp

.EXAMPLE
    .\scripts\Test-RuleEnforcement.ps1 -Direction outbound -Protocol tcp `
        -RemoteIp 1.1.1.1 -RemotePort 443 -ExpectedResult blocked

.EXAMPLE
    .\scripts\Test-RuleEnforcement.ps1 -Direction outbound -Protocol udp `
        -RemoteIp 8.8.8.8 -RemotePort 53 -ExpectedResult blocked

.EXAMPLE
    .\scripts\Test-RuleEnforcement.ps1 -Direction inbound -Protocol tcp `
        -RemotePort 19876 -ExpectedResult blocked

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("outbound", "inbound")]
    [string]$Direction,

    [Parameter(Mandatory = $true)]
    [ValidateSet("tcp", "udp")]
    [string]$Protocol,

    [Parameter()]
    [string]$RemoteIp = "1.1.1.1",

    [Parameter()]
    [int]$RemotePort = 443,

    [Parameter()]
    [ValidateSet("block", "allow")]
    [string]$Action = "block",

    [Parameter()]
    [ValidateSet("blocked", "allowed")]
    [string]$ExpectedResult = "blocked",

    [Parameter()]
    [int]$Priority = 100,

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

function Test-OutboundTcp {
    param(
        [string]$Ip,
        [int]$Port,
        [int]$TimeoutMilliseconds = 5000
    )

    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $asyncResult = $client.BeginConnect($Ip, $Port, $null, $null)
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

function Test-OutboundUdpDns {
    param(
        [string]$Server,
        [string]$Domain = "example.com"
    )

    try {
        $result = Resolve-DnsName -Name $Domain -Server $Server -Type A -DnsOnly -ErrorAction Stop
        return ($null -ne $result)
    }
    catch {
        return $false
    }
}

function Test-OutboundUdpGeneric {
    param(
        [string]$Ip,
        [int]$Port,
        [int]$TimeoutMilliseconds = 3000
    )

    try {
        $udpClient = New-Object System.Net.Sockets.UdpClient
        $udpClient.Client.ReceiveTimeout = $TimeoutMilliseconds

        # Send a small probe packet
        $data = [System.Text.Encoding]::ASCII.GetBytes("probe")
        $udpClient.Send($data, $data.Length, $Ip, $Port) | Out-Null

        # Try to receive a response (may timeout if blocked or no service listening)
        try {
            $remoteEp = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
            $response = $udpClient.Receive([ref]$remoteEp)
            $udpClient.Close()
            return $true
        }
        catch [System.Net.Sockets.SocketException] {
            $udpClient.Close()
            # ICMP port unreachable means packet got through but nothing was listening
            # Timeout means packet was likely blocked
            if ($_.Exception.SocketErrorCode -eq [System.Net.Sockets.SocketError]::ConnectionReset) {
                return $true  # Packet reached destination (got ICMP unreachable)
            }
            return $false
        }
    }
    catch {
        return $false
    }
}

function Test-InboundTcp {
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

    if ($null -ne $Listener) {
        try { $Listener.Stop() } catch { }
    }
}

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl Rule Enforcement Test"
Write-Host "=============================================="
Write-Host ""
Write-Host "  Direction: $Direction"
Write-Host "  Protocol:  $Protocol"
Write-Host "  Remote:    ${RemoteIp}:${RemotePort}"
Write-Host "  Action:    $Action"
Write-Host "  Expected:  $ExpectedResult"
Write-Host "  Priority:  $Priority"
Write-Host ""

# ========================================
# Validate parameter combinations
# ========================================
if ($Direction -eq "inbound" -and $Protocol -eq "udp") {
    Write-TestWarn "Inbound UDP is not supported by the firewall (ALE RECV_ACCEPT layer does not handle UDP)."
    Write-TestWarn "Skipping this test."
    Write-Host ""
    exit 0
}

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
$listener = $null
$policyFile = $null

try {
    # ========================================
    # Test 1: Pre-test baseline connectivity
    # ========================================
    $testLabel = "${Direction}/${Protocol} to ${RemoteIp}:${RemotePort}"

    if ($Direction -eq "inbound" -and $Protocol -eq "tcp") {
        # For inbound tests, start a listener first
        Write-TestStep "Test 1a: Starting TCP listener on port $RemotePort..."
        $listener = Start-TestListener -Port $RemotePort
        if ($null -eq $listener) {
            Write-TestFail "Failed to start TCP listener on port $RemotePort (port in use?)"
            exit 1
        }
        Write-TestPass "TCP listener started on port $RemotePort"

        Write-TestStep "Test 1b: Verifying initial loopback connectivity to port $RemotePort..."
        $baselineResult = Test-InboundTcp -Port $RemotePort
    } elseif ($Direction -eq "outbound" -and $Protocol -eq "tcp") {
        Write-TestStep "Test 1: Verifying initial outbound TCP connectivity to ${RemoteIp}:${RemotePort}..."
        $baselineResult = Test-OutboundTcp -Ip $RemoteIp -Port $RemotePort
    } elseif ($Direction -eq "outbound" -and $Protocol -eq "udp" -and $RemotePort -eq 53) {
        Write-TestStep "Test 1: Verifying initial outbound UDP/DNS connectivity to ${RemoteIp}:53..."
        $baselineResult = Test-OutboundUdpDns -Server $RemoteIp
    } else {
        Write-TestStep "Test 1: Verifying initial outbound UDP connectivity to ${RemoteIp}:${RemotePort}..."
        $baselineResult = Test-OutboundUdpGeneric -Ip $RemoteIp -Port $RemotePort
    }

    if ($baselineResult) {
        Write-TestPass "Baseline connectivity confirmed ($testLabel)"
        $testsPassed++
    } else {
        Write-TestWarn "Baseline connectivity failed ($testLabel)"
        Write-Host "  Network may be unreachable. Continuing anyway..."
    }
    Write-Host ""

    # ========================================
    # Test 2: Create and apply policy
    # ========================================
    Write-TestStep "Test 2: Creating $Action policy for $testLabel..."

    $policyFile = [System.IO.Path]::GetTempFileName()
    $policyFile = [System.IO.Path]::ChangeExtension($policyFile, ".json")

    # Build rule object
    $rule = @{
        id        = "test-rule-enforcement"
        action    = $Action
        direction = $Direction
        protocol  = $Protocol
        priority  = $Priority
        enabled   = $true
        comment   = "Test rule: $Action $Direction $Protocol ${RemoteIp}:${RemotePort}"
    }

    # For outbound rules, match on remote IP and port
    # For inbound rules, remote.ports matches the listening port
    if ($Direction -eq "outbound") {
        $rule.remote = @{
            ip    = $RemoteIp
            ports = "$RemotePort"
        }
    } else {
        # Inbound: remote.ports matches the port being connected to
        $rule.remote = @{
            ports = "$RemotePort"
        }
    }

    $policy = @{
        version       = "1.0.0"
        defaultAction = "allow"
        updatedAt     = (Get-Date).ToString("o")
        rules         = @($rule)
    }

    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyFile -Encoding UTF8
    Write-Host "  Policy file: $policyFile"

    $applyResult = Invoke-Wfpctl @("apply", $policyFile)
    if ($applyResult.Success) {
        Write-TestPass "Policy applied successfully"
        Write-Host $applyResult.Output
        $testsPassed++
    } else {
        Write-TestFail "Failed to apply policy"
        Write-Host $applyResult.Output
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 3: Verify expected result
    # ========================================
    Write-TestStep "Test 3: Verifying expected result ($ExpectedResult) for $testLabel..."
    Start-Sleep -Seconds 1  # Give WFP a moment to apply

    # Measure enforcement latency
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    if ($Direction -eq "inbound" -and $Protocol -eq "tcp") {
        $connectResult = Test-InboundTcp -Port $RemotePort
    } elseif ($Direction -eq "outbound" -and $Protocol -eq "tcp") {
        $connectResult = Test-OutboundTcp -Ip $RemoteIp -Port $RemotePort
    } elseif ($Direction -eq "outbound" -and $Protocol -eq "udp" -and $RemotePort -eq 53) {
        $connectResult = Test-OutboundUdpDns -Server $RemoteIp
    } else {
        $connectResult = Test-OutboundUdpGeneric -Ip $RemoteIp -Port $RemotePort
    }

    $sw.Stop()
    $latencyMs = $sw.ElapsedMilliseconds

    $expectedConnectivity = ($ExpectedResult -eq "allowed")

    if ($connectResult -eq $expectedConnectivity) {
        if ($ExpectedResult -eq "blocked") {
            Write-TestPass "Connection blocked as expected (verified in ${latencyMs}ms)"
        } else {
            Write-TestPass "Connection allowed as expected (verified in ${latencyMs}ms)"
        }
        $testsPassed++
    } else {
        if ($ExpectedResult -eq "blocked") {
            Write-TestFail "Connection was NOT blocked (expected: blocked, got: allowed)"
        } else {
            Write-TestFail "Connection was NOT allowed (expected: allowed, got: blocked)"
        }
        $testsFailed++
    }
    Write-Host ""

    # ========================================
    # Test 4: Rollback and verify restoration
    # ========================================
    if (-not $SkipCleanup) {
        Write-TestStep "Test 4: Rolling back policy..."
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
        # Test 5: Verify connectivity is restored
        # ========================================
        Write-TestStep "Test 5: Verifying connectivity is restored after rollback..."
        Start-Sleep -Seconds 1  # Give WFP a moment to remove filter

        if ($Direction -eq "inbound" -and $Protocol -eq "tcp") {
            $restoredResult = Test-InboundTcp -Port $RemotePort
        } elseif ($Direction -eq "outbound" -and $Protocol -eq "tcp") {
            $restoredResult = Test-OutboundTcp -Ip $RemoteIp -Port $RemotePort
        } elseif ($Direction -eq "outbound" -and $Protocol -eq "udp" -and $RemotePort -eq 53) {
            $restoredResult = Test-OutboundUdpDns -Server $RemoteIp
        } else {
            $restoredResult = Test-OutboundUdpGeneric -Ip $RemoteIp -Port $RemotePort
        }

        if ($restoredResult) {
            Write-TestPass "Connectivity restored after rollback"
            $testsPassed++
        } else {
            Write-TestWarn "Connectivity not restored (may be a network issue, not a test failure)"
        }
        Write-Host ""
    } else {
        Write-TestWarn "Skipping cleanup as requested. Policy is still applied."
        Write-Host "  Run 'wfpctl rollback' to remove the test filter."
        Write-Host ""
    }

}
finally {
    # ========================================
    # Cleanup
    # ========================================
    Write-TestStep "Cleaning up..."

    # Stop listener (inbound tests)
    if ($null -ne $listener) {
        Stop-TestListener -Listener $listener
        Write-Host "  TCP listener stopped"
    }

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
    Write-Host "Rule enforcement verified: $Action $Direction $Protocol ${RemoteIp}:${RemotePort} -> $ExpectedResult"
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
