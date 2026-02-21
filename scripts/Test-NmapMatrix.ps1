#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Comprehensive rule enforcement matrix test using nmap and nping.

.DESCRIPTION
    Runs four test groups against a target IP to verify WFP rule enforcement:

    GROUP 1 — Outbound TCP Block Matrix (exact ports 80,443,8080)
    GROUP 2 — Port Range Block (range 8000-9000, boundary ports)
    GROUP 3 — Protocol/Direction Bypass (negative-space validation)
    GROUP 4 — nping Port Boundary (raw packet boundary probing)

    Each group applies a policy, runs verification scans, and rolls back.
    Prints a summary table at the end.

    Requires the WfpTrafficControl service to be running, nmap installed,
    and a reachable target IP (second VM on the same virtual switch).

.PARAMETER TargetIp
    IP address of the second VM or test target. REQUIRED (no default).

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER SkipNping
    Skip GROUP 4 (nping boundary tests) if nping is not available.

.PARAMETER SkipCleanup
    If specified, skips removal of temporary files after the test run.

.EXAMPLE
    .\scripts\Test-NmapMatrix.ps1 -TargetIp 192.168.1.20

.EXAMPLE
    .\scripts\Test-NmapMatrix.ps1 -TargetIp 10.0.0.5 -SkipNping

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
    Requires nmap on PATH (winget install Insecure.Nmap).
    Both VMs must be on the same virtual switch with the target reachable.
    The target VM's own firewall should allow the tested ports for accurate results.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TargetIp,

    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [switch]$SkipNping,

    [Parameter()]
    [switch]$SkipCleanup
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
# Test tracking
# ============================================================
$script:testsPassed  = 0
$script:testsFailed  = 0
$script:testsSkipped = 0
$script:Results      = [System.Collections.ArrayList]::new()
$script:tempFiles    = [System.Collections.ArrayList]::new()

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

function Write-TestSkip {
    param([string]$Message)
    Write-Host "${Yellow}[SKIP]${Reset} $Message"
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

function Add-TestResult {
    param(
        [string]$Group,
        [string]$TestCase,
        [string]$Expected,
        [string]$Actual,
        [string]$Status  # PASS, FAIL, SKIP
    )

    $script:Results.Add([PSCustomObject]@{
        Group    = $Group
        Test     = $TestCase
        Expected = $Expected
        Actual   = $Actual
        Result   = $Status
    }) | Out-Null

    switch ($Status) {
        "PASS" { $script:testsPassed++;  Write-TestPass "$Group - $TestCase" }
        "FAIL" { $script:testsFailed++;  Write-TestFail "$Group - $TestCase" }
        "SKIP" { $script:testsSkipped++; Write-TestSkip "$Group - $TestCase" }
    }
}

function New-TempFile {
    param([string]$Extension = ".json")
    $path = [System.IO.Path]::Combine(
        [System.IO.Path]::GetTempPath(),
        "wfp-nmap-$(Get-Random)$Extension"
    )
    $script:tempFiles.Add($path) | Out-Null
    return $path
}

function Apply-TestPolicy {
    param(
        [hashtable[]]$Rules,
        [string]$Version = "nmap-test-1.0.0"
    )

    $policyFile = New-TempFile -Extension ".json"
    $policy = @{
        version       = $Version
        defaultAction = "allow"
        updatedAt     = (Get-Date).ToString("o")
        rules         = $Rules
    }

    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $policyFile -Encoding UTF8

    Write-TestStep "Applying policy ($($Rules.Count) rule(s))..."
    $result = Invoke-Wfpctl @("apply", $policyFile)
    if (-not $result.Success) {
        Write-TestFail "Failed to apply policy: $($result.Output)"
        return $false
    }
    Write-TestPass "Policy applied"
    Start-Sleep -Milliseconds 500  # Allow WFP to settle
    return $true
}

function Invoke-Rollback {
    Write-TestStep "Rolling back policy..."
    $result = Invoke-Wfpctl @("rollback")
    if (-not $result.Success) {
        Write-TestWarn "Rollback returned error: $($result.Output)"
        return $false
    }
    Write-TestPass "Policy rolled back"
    Start-Sleep -Milliseconds 500
    return $true
}

function Invoke-NmapScan {
    param(
        [string]$Target,
        [string]$Ports
    )

    $tempXml = New-TempFile -Extension ".xml"

    $nmapArgs = @("-Pn", "-sT", "-p", $Ports, $Target, "-oX", $tempXml, "--max-retries", "1")
    Write-TestStep "Running: nmap $($nmapArgs -join ' ')"

    $output = & nmap @nmapArgs 2>&1
    $outputStr = $output -join "`n"

    if (-not (Test-Path $tempXml)) {
        Write-TestWarn "nmap did not produce XML output"
        Write-Host $outputStr
        return $null
    }

    [xml]$scan = Get-Content $tempXml

    $portResults = @{}

    if ($null -eq $scan.nmaprun.host) {
        Write-TestWarn "nmap output contains no host data"
        Write-Host $outputStr
        return $null
    }

    $xmlPorts = $scan.nmaprun.host.ports.port
    if ($null -ne $xmlPorts) {
        # Handle single port (XmlElement) vs multiple ports (array)
        if ($xmlPorts -is [System.Xml.XmlElement]) {
            $portResults[$xmlPorts.portid] = $xmlPorts.state.state
        } else {
            foreach ($p in $xmlPorts) {
                $portResults[$p.portid] = $p.state.state
            }
        }
    }

    return $portResults
}

function Test-NpingTcp {
    param(
        [string]$Target,
        [int]$Port
    )

    $npingArgs = @("--tcp", "-p", "$Port", $Target, "-c", "1")
    Write-TestStep "Running: nping $($npingArgs -join ' ')"

    $output = & nping @npingArgs 2>&1
    $outputStr = $output -join "`n"

    $hasResponse = $false
    if ($outputStr -match "Rcvd:\s*(\d+)") {
        $hasResponse = [int]$Matches[1] -gt 0
    }

    return @{
        HasResponse = $hasResponse
        RawOutput   = $outputStr
    }
}

function Test-NpingUdp {
    param(
        [string]$Target,
        [int]$Port
    )

    $npingArgs = @("--udp", "-p", "$Port", $Target, "-c", "1")
    Write-TestStep "Running: nping $($npingArgs -join ' ')"

    $output = & nping @npingArgs 2>&1
    $outputStr = $output -join "`n"

    # Check if packet was sent successfully (SENT line present, no send errors)
    $sentOk = $outputStr -match "SENT"

    return @{
        SentOk    = $sentOk
        RawOutput = $outputStr
    }
}

# ============================================================
# Main Script
# ============================================================

Write-Host ""
Write-Host "=================================================="
Write-Host "  WfpTrafficControl - nmap Rule Enforcement Matrix"
Write-Host "=================================================="
Write-Host ""
Write-Host "  Target IP:   $TargetIp"
Write-Host "  SkipNping:   $SkipNping"
Write-Host "  SkipCleanup: $SkipCleanup"
Write-Host ""

# --- Prerequisites ---

# Check wfpctl
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}
$WfpctlPath = Resolve-Path $WfpctlPath

# Check nmap
$nmapCmd = Get-Command nmap -ErrorAction SilentlyContinue
if ($null -eq $nmapCmd) {
    Write-TestFail "nmap is not on PATH."
    Write-Host ""
    Write-Host "Install nmap:"
    Write-Host "  winget install Insecure.Nmap"
    Write-Host "  -- OR --"
    Write-Host "  Download from https://nmap.org/download.html"
    Write-Host ""
    Write-Host "After installation, ensure nmap is on your PATH and restart this shell."
    exit 1
}
Write-TestPass "nmap found: $($nmapCmd.Source)"

# Check nping
$npingAvailable = -not $SkipNping
if ($npingAvailable) {
    $npingCmd = Get-Command nping -ErrorAction SilentlyContinue
    if ($null -eq $npingCmd) {
        Write-TestWarn "nping not found on PATH. GROUP 4 will be skipped."
        Write-Host "  nping is bundled with nmap. Ensure the nmap install directory is on your PATH."
        $npingAvailable = $false
    } else {
        Write-TestPass "nping found: $($npingCmd.Source)"
    }
}

# Check service
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

# ============================================================
# GROUP 1 - Outbound TCP Block Matrix
# ============================================================

Write-Host ""
Write-Host "=========================================="
Write-Host "  GROUP 1 - Outbound TCP Block Matrix"
Write-Host "=========================================="
Write-Host ""

try {
    $g1Rule = @{
        id        = "nmap-g1-block-tcp"
        action    = "block"
        direction = "outbound"
        protocol  = "tcp"
        remote    = @{ ip = $TargetIp; ports = "80,443,8080" }
        priority  = 100
        enabled   = $true
        comment   = "Test: block outbound TCP to ports 80,443,8080"
    }

    if (-not (Apply-TestPolicy -Rules @($g1Rule))) {
        Add-TestResult "G1" "Apply policy" "success" "failed" "FAIL"
    } else {
        # Scan with policy active — all three ports should be filtered
        $blocked = Invoke-NmapScan -Target $TargetIp -Ports "80,443,8080"
        if ($null -eq $blocked) {
            Add-TestResult "G1" "nmap scan (blocked)" "filtered" "nmap error" "FAIL"
        } else {
            foreach ($port in @("80", "443", "8080")) {
                $state = $blocked[$port]
                if ($null -eq $state) { $state = "(no data)" }
                $isFiltered = $state -eq "filtered"
                Add-TestResult "G1" "Port $port blocked" "filtered" $state $(if ($isFiltered) { "PASS" } else { "FAIL" })
            }
        }

        # Rollback and re-scan — ports should no longer be filtered
        # NOTE: "not filtered" assumes the target's own firewall allows these ports.
        Invoke-Rollback | Out-Null

        $unblocked = Invoke-NmapScan -Target $TargetIp -Ports "80,443,8080"
        if ($null -eq $unblocked) {
            Add-TestResult "G1" "nmap scan (restored)" "not filtered" "nmap error" "FAIL"
        } else {
            foreach ($port in @("80", "443", "8080")) {
                $state = $unblocked[$port]
                if ($null -eq $state) { $state = "(no data)" }
                $isNotFiltered = $state -ne "filtered"
                Add-TestResult "G1" "Port $port restored" "not filtered" $state $(if ($isNotFiltered) { "PASS" } else { "FAIL" })
            }
        }
    }
} catch {
    Write-TestFail "GROUP 1 error: $_"
    Add-TestResult "G1" "Unexpected error" "complete" "$_" "FAIL"
    Invoke-Rollback | Out-Null
}

# ============================================================
# GROUP 2 - Port Range Block
# ============================================================

Write-Host ""
Write-Host "=========================================="
Write-Host "  GROUP 2 - Port Range Block"
Write-Host "=========================================="
Write-Host ""

try {
    $g2Rule = @{
        id        = "nmap-g2-block-range"
        action    = "block"
        direction = "outbound"
        protocol  = "tcp"
        remote    = @{ ip = $TargetIp; ports = "8000-9000" }
        priority  = 100
        enabled   = $true
        comment   = "Test: block outbound TCP to port range 8000-9000"
    }

    if (-not (Apply-TestPolicy -Rules @($g2Rule))) {
        Add-TestResult "G2" "Apply policy" "success" "failed" "FAIL"
    } else {
        $scan = Invoke-NmapScan -Target $TargetIp -Ports "7999,8000,8500,9000,9001"
        if ($null -eq $scan) {
            Add-TestResult "G2" "nmap scan" "results" "nmap error" "FAIL"
        } else {
            # Ports inside range (8000-9000) should be filtered
            foreach ($port in @("8000", "8500", "9000")) {
                $state = $scan[$port]
                if ($null -eq $state) { $state = "(no data)" }
                $isFiltered = $state -eq "filtered"
                Add-TestResult "G2" "Port $port (in range)" "filtered" $state $(if ($isFiltered) { "PASS" } else { "FAIL" })
            }
            # Ports outside range should NOT be filtered
            foreach ($port in @("7999", "9001")) {
                $state = $scan[$port]
                if ($null -eq $state) { $state = "(no data)" }
                $isNotFiltered = $state -ne "filtered"
                Add-TestResult "G2" "Port $port (outside)" "not filtered" $state $(if ($isNotFiltered) { "PASS" } else { "FAIL" })
            }
        }

        Invoke-Rollback | Out-Null
    }
} catch {
    Write-TestFail "GROUP 2 error: $_"
    Add-TestResult "G2" "Unexpected error" "complete" "$_" "FAIL"
    Invoke-Rollback | Out-Null
}

# ============================================================
# GROUP 3 - Protocol/Direction Bypass (Negative Space)
# ============================================================

Write-Host ""
Write-Host "=========================================="
Write-Host "  GROUP 3 - Protocol/Direction Bypass"
Write-Host "=========================================="
Write-Host ""
Write-Host "  Validates rules don't bleed across protocol/port boundaries."
Write-Host "  Rule: block outbound TCP to ${TargetIp}:443 only."
Write-Host ""

try {
    $g3Rule = @{
        id        = "nmap-g3-block-tcp-443"
        action    = "block"
        direction = "outbound"
        protocol  = "tcp"
        remote    = @{ ip = $TargetIp; ports = "443" }
        priority  = 100
        enabled   = $true
        comment   = "Test: block only outbound TCP to port 443"
    }

    if (-not (Apply-TestPolicy -Rules @($g3Rule))) {
        Add-TestResult "G3" "Apply policy" "success" "failed" "FAIL"
    } else {
        # --- Test 1: Outbound UDP should NOT be blocked (different protocol) ---
        if ($npingAvailable) {
            $udpResult = Test-NpingUdp -Target $TargetIp -Port 53
            # nping sending without error means WFP did not block the UDP path.
            # No response is normal (target may not run DNS); the key is the send succeeded.
            Add-TestResult "G3" "UDP:53 not blocked (diff protocol)" "not blocked" $(if ($udpResult.SentOk) { "send ok" } else { "send failed" }) $(if ($udpResult.SentOk) { "PASS" } else { "FAIL" })
        } else {
            # Fallback: UdpClient. connect() + send() succeeding means WFP allowed it.
            $udp = $null
            try {
                $udp = New-Object System.Net.Sockets.UdpClient
                $udp.Connect($TargetIp, 53)
                $bytes = [byte[]](0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00,
                                  0x00, 0x00, 0x00, 0x00, 0x07, 0x65, 0x78, 0x61,
                                  0x6D, 0x70, 0x6C, 0x65, 0x03, 0x63, 0x6F, 0x6D,
                                  0x00, 0x00, 0x01, 0x00, 0x01)  # DNS query for example.com
                $udp.Send($bytes, $bytes.Length) | Out-Null
                Add-TestResult "G3" "UDP:53 not blocked (diff protocol)" "not blocked" "send ok" "PASS"
            } catch {
                Add-TestResult "G3" "UDP:53 not blocked (diff protocol)" "not blocked" "error: $_" "FAIL"
            } finally {
                if ($null -ne $udp) { $udp.Close() }
            }
        }

        # --- Test 2: Outbound TCP to port 80 should NOT be blocked (different port) ---
        $tcpScan80 = Invoke-NmapScan -Target $TargetIp -Ports "80"
        if ($null -eq $tcpScan80) {
            Add-TestResult "G3" "TCP:80 not blocked (diff port)" "not filtered" "nmap error" "FAIL"
        } else {
            $state80 = $tcpScan80["80"]
            if ($null -eq $state80) { $state80 = "(no data)" }
            $isNotFiltered = $state80 -ne "filtered"
            Add-TestResult "G3" "TCP:80 not blocked (diff port)" "not filtered" $state80 $(if ($isNotFiltered) { "PASS" } else { "FAIL" })
        }

        # --- Test 3: Outbound TCP to port 443 SHOULD be blocked ---
        $tcpScan443 = Invoke-NmapScan -Target $TargetIp -Ports "443"
        if ($null -eq $tcpScan443) {
            Add-TestResult "G3" "TCP:443 blocked" "filtered" "nmap error" "FAIL"
        } else {
            $state443 = $tcpScan443["443"]
            if ($null -eq $state443) { $state443 = "(no data)" }
            $isFiltered = $state443 -eq "filtered"
            Add-TestResult "G3" "TCP:443 blocked" "filtered" $state443 $(if ($isFiltered) { "PASS" } else { "FAIL" })
        }

        Invoke-Rollback | Out-Null
    }
} catch {
    Write-TestFail "GROUP 3 error: $_"
    Add-TestResult "G3" "Unexpected error" "complete" "$_" "FAIL"
    Invoke-Rollback | Out-Null
}

# ============================================================
# GROUP 4 - nping Port Boundary
# ============================================================

if (-not $npingAvailable) {
    Write-Host ""
    Write-Host "=========================================="
    Write-Host "  GROUP 4 - nping Port Boundary (SKIPPED)"
    Write-Host "=========================================="
    Write-Host ""
    Write-TestSkip "GROUP 4 skipped (nping not available or -SkipNping specified)"
    Add-TestResult "G4" "All tests" "N/A" "skipped" "SKIP"
} else {
    Write-Host ""
    Write-Host "=========================================="
    Write-Host "  GROUP 4 - nping Port Boundary"
    Write-Host "=========================================="
    Write-Host ""
    Write-Host "  NOTE: nping --tcp sends raw SYN packets via Npcap, which may"
    Write-Host "  bypass ALE-layer filters. If results are unexpected, verify"
    Write-Host "  with nmap -sT (which uses connect()) in GROUP 2 instead."
    Write-Host ""

    try {
        $g4Rule = @{
            id        = "nmap-g4-block-range"
            action    = "block"
            direction = "outbound"
            protocol  = "tcp"
            remote    = @{ ip = $TargetIp; ports = "8000-9000" }
            priority  = 100
            enabled   = $true
            comment   = "Test: block outbound TCP to port range 8000-9000"
        }

        if (-not (Apply-TestPolicy -Rules @($g4Rule))) {
            Add-TestResult "G4" "Apply policy" "success" "failed" "FAIL"
        } else {
            # Port 7999 — below range, should get response
            $r7999 = Test-NpingTcp -Target $TargetIp -Port 7999
            Add-TestResult "G4" "Port 7999 (below range)" "response" `
                $(if ($r7999.HasResponse) { "response" } else { "no response" }) `
                $(if ($r7999.HasResponse) { "PASS" } else { "FAIL" })

            # Port 8000 — range start, should be blocked (no response)
            $r8000 = Test-NpingTcp -Target $TargetIp -Port 8000
            Add-TestResult "G4" "Port 8000 (range start)" "no response" `
                $(if ($r8000.HasResponse) { "response" } else { "no response" }) `
                $(if (-not $r8000.HasResponse) { "PASS" } else { "FAIL" })

            # Port 9000 — range end, should be blocked (no response)
            $r9000 = Test-NpingTcp -Target $TargetIp -Port 9000
            Add-TestResult "G4" "Port 9000 (range end)" "no response" `
                $(if ($r9000.HasResponse) { "response" } else { "no response" }) `
                $(if (-not $r9000.HasResponse) { "PASS" } else { "FAIL" })

            # Port 9001 — above range, should get response
            $r9001 = Test-NpingTcp -Target $TargetIp -Port 9001
            Add-TestResult "G4" "Port 9001 (above range)" "response" `
                $(if ($r9001.HasResponse) { "response" } else { "no response" }) `
                $(if ($r9001.HasResponse) { "PASS" } else { "FAIL" })

            Invoke-Rollback | Out-Null
        }
    } catch {
        Write-TestFail "GROUP 4 error: $_"
        Add-TestResult "G4" "Unexpected error" "complete" "$_" "FAIL"
        Invoke-Rollback | Out-Null
    }
}

# ============================================================
# Summary Table
# ============================================================

Write-Host ""
Write-Host "=================================================="
Write-Host "  Test Results Summary"
Write-Host "=================================================="
Write-Host ""

$col1 = 7   # Group
$col2 = 36  # Test
$col3 = 15  # Expected
$col4 = 20  # Actual
$col5 = 6   # Result

$header = "{0,-$col1} {1,-$col2} {2,-$col3} {3,-$col4} {4,-$col5}" -f "Group", "Test Case", "Expected", "Actual", "Result"
$separator = "-" * ($col1 + $col2 + $col3 + $col4 + $col5 + 4)

Write-Host $header
Write-Host $separator

foreach ($r in $script:Results) {
    $color = switch ($r.Result) {
        "PASS" { $Green }
        "FAIL" { $Red }
        "SKIP" { $Yellow }
        default { $Reset }
    }
    $line = "{0,-$col1} {1,-$col2} {2,-$col3} {3,-$col4} {4}{5,-$col5}{6}" -f `
        $r.Group, $r.Test, $r.Expected, $r.Actual, $color, $r.Result, $Reset
    Write-Host $line
}

Write-Host ""
Write-Host "  Passed:  ${Green}$($script:testsPassed)${Reset}"
Write-Host "  Failed:  ${Red}$($script:testsFailed)${Reset}"
Write-Host "  Skipped: ${Yellow}$($script:testsSkipped)${Reset}"
Write-Host ""

# Cleanup temp files
if (-not $SkipCleanup) {
    foreach ($f in $script:tempFiles) {
        if (Test-Path $f) {
            Remove-Item $f -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($script:testsFailed -eq 0) {
    Write-Host "${Green}All tests passed!${Reset}"
    exit 0
} else {
    Write-Host "${Red}Some tests failed. Review results above.${Reset}"
    exit 1
}
