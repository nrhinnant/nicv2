#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Large policy stress test with idempotent re-apply and partial diff phases.

.DESCRIPTION
    Generates a policy with a configurable number of rules and measures:

    PHASE A — First apply: creates all filters from scratch.
    PHASE B — Idempotent re-apply: applies the same policy again (expect 0 changes).
    PHASE C — Partial diff: modifies the first half of rules and re-applies.

    Reports wall-clock time and filter operation counts for each phase.

    Requires the WfpTrafficControl service to be running.

.PARAMETER RuleCount
    Number of rules to generate. Default 500.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER SkipCleanup
    If specified, skips removal of temporary files after the test run.

.EXAMPLE
    .\scripts\Test-LargePolicyStress.ps1

.EXAMPLE
    .\scripts\Test-LargePolicyStress.ps1 -RuleCount 1000

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
    Max supported rule count is 10000 (per PolicyValidator).
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateRange(1, 10000)]
    [int]$RuleCount = 500,

    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

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

function New-StressRules {
    param(
        [int]$Count,
        [string]$IpPrefix = "10.0"
    )

    $rules = [System.Collections.ArrayList]::new()

    for ($i = 1; $i -le $Count; $i++) {
        $octet3 = [int][math]::Floor($i / 256)
        $octet4 = $i % 256
        $direction = if ($i % 3 -eq 0) { "inbound" } else { "outbound" }
        $protocol  = if ($i % 4 -eq 0) { "udp" } else { "tcp" }
        $action    = if ($i % 2 -eq 0) { "block" } else { "allow" }

        $rules.Add(@{
            id        = "stress-rule-$i"
            action    = $action
            direction = $direction
            protocol  = $protocol
            remote    = @{ ip = "$IpPrefix.$octet3.$octet4"; ports = "$((1000 + $i))" }
            priority  = $i
            enabled   = $true
        }) | Out-Null
    }

    return $rules
}

function Write-PolicyFile {
    param(
        [System.Collections.ArrayList]$Rules,
        [string]$FilePath,
        [string]$Version = "1.0.0"
    )

    $policy = @{
        version       = $Version
        defaultAction = "allow"
        updatedAt     = (Get-Date).ToString("o")
        rules         = $Rules.ToArray()
    }

    $policy | ConvertTo-Json -Depth 10 | Set-Content -Path $FilePath -Encoding UTF8
}

function Parse-ApplyOutput {
    param([string]$Output)

    $result = @{
        Created   = $null
        Removed   = $null
        Unchanged = $null
    }

    if ($Output -match "(?:Created|Added):\s*(\d+)") {
        $result.Created = [int]$Matches[1]
    }
    if ($Output -match "(?:Removed|Deleted):\s*(\d+)") {
        $result.Removed = [int]$Matches[1]
    }
    if ($Output -match "(?:Unchanged|Matched|Skipped):\s*(\d+)") {
        $result.Unchanged = [int]$Matches[1]
    }

    return $result
}

# ============================================================
# Main Script
# ============================================================

Write-Host ""
Write-Host "=================================================="
Write-Host "  WfpTrafficControl - Large Policy Stress Test"
Write-Host "=================================================="
Write-Host ""
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

# --- Generate policies ---

$policyFile = [System.IO.Path]::Combine(
    [System.IO.Path]::GetTempPath(),
    "wfp-large-stress-test.json"
)

Write-TestStep "Generating $RuleCount rules..."
$rules = New-StressRules -Count $RuleCount -IpPrefix "10.0"
Write-TestPass "Generated $($rules.Count) rules"
Write-Host ""

# Track results per phase
$phaseResults = @{}

# ============================================================
# PHASE A — First Apply
# ============================================================

Write-Host "=========================================="
Write-Host "  PHASE A - First Apply ($RuleCount new filters)"
Write-Host "=========================================="
Write-Host ""

Write-PolicyFile -Rules $rules -FilePath $policyFile -Version "1.0.0"

Write-TestStep "Applying policy..."
$swA = [System.Diagnostics.Stopwatch]::StartNew()
$resultA = Invoke-Wfpctl @("apply", $policyFile)
$swA.Stop()

$timeA = [math]::Round($swA.Elapsed.TotalMilliseconds, 0)
$countsA = Parse-ApplyOutput -Output $resultA.Output

if ($resultA.Success) {
    Write-TestPass "Phase A apply succeeded in ${timeA}ms"
    Write-Host $resultA.Output
} else {
    Write-TestFail "Phase A apply failed"
    Write-Host $resultA.Output
}

$phaseResults["A"] = @{
    Time     = $timeA
    Success  = $resultA.Success
    Counts   = $countsA
    Output   = $resultA.Output
}
Write-Host ""

# ============================================================
# PHASE B — Idempotent Re-Apply
# ============================================================

Write-Host "=========================================="
Write-Host "  PHASE B - Idempotent Re-Apply (same policy)"
Write-Host "=========================================="
Write-Host ""

# Update only the timestamp, same rules
Write-PolicyFile -Rules $rules -FilePath $policyFile -Version "1.0.0"

Write-TestStep "Re-applying same policy..."
$swB = [System.Diagnostics.Stopwatch]::StartNew()
$resultB = Invoke-Wfpctl @("apply", $policyFile)
$swB.Stop()

$timeB = [math]::Round($swB.Elapsed.TotalMilliseconds, 0)
$countsB = Parse-ApplyOutput -Output $resultB.Output

if ($resultB.Success) {
    Write-TestPass "Phase B re-apply succeeded in ${timeB}ms"
    Write-Host $resultB.Output

    # Validate idempotent expectations
    if ($null -ne $countsB.Created -and $countsB.Created -eq 0 -and
        $null -ne $countsB.Removed -and $countsB.Removed -eq 0) {
        Write-TestPass "Idempotent: 0 created, 0 removed (as expected)"
    } elseif ($null -ne $countsB.Created -or $null -ne $countsB.Removed) {
        Write-TestWarn "Idempotent check: Created=$($countsB.Created), Removed=$($countsB.Removed)"
    }
} else {
    Write-TestFail "Phase B re-apply failed"
    Write-Host $resultB.Output
}

$phaseResults["B"] = @{
    Time     = $timeB
    Success  = $resultB.Success
    Counts   = $countsB
    Output   = $resultB.Output
}
Write-Host ""

# ============================================================
# PHASE C — Partial Diff (first half changed)
# ============================================================

Write-Host "=========================================="
Write-Host "  PHASE C - Partial Diff (first half changed)"
Write-Host "=========================================="
Write-Host ""

$halfCount = [int][math]::Floor($RuleCount / 2)
Write-TestStep "Modifying first $halfCount rules (different IPs)..."

# Create modified rules: change the IP prefix for the first half
$modifiedRules = [System.Collections.ArrayList]::new()
for ($i = 0; $i -lt $rules.Count; $i++) {
    $rule = $rules[$i].Clone()
    if ($i -lt $halfCount) {
        # Change IP to a different range for the first half
        $idx = $i + 1
        $octet3 = [int][math]::Floor($idx / 256)
        $octet4 = $idx % 256
        $rule.remote = @{ ip = "10.50.$octet3.$octet4"; ports = "$((1000 + $idx))" }
    }
    $modifiedRules.Add($rule) | Out-Null
}

Write-PolicyFile -Rules $modifiedRules -FilePath $policyFile -Version "1.0.0"

Write-TestStep "Applying modified policy..."
$swC = [System.Diagnostics.Stopwatch]::StartNew()
$resultC = Invoke-Wfpctl @("apply", $policyFile)
$swC.Stop()

$timeC = [math]::Round($swC.Elapsed.TotalMilliseconds, 0)
$countsC = Parse-ApplyOutput -Output $resultC.Output

if ($resultC.Success) {
    Write-TestPass "Phase C partial diff apply succeeded in ${timeC}ms"
    Write-Host $resultC.Output
} else {
    Write-TestFail "Phase C partial diff apply failed"
    Write-Host $resultC.Output
}

$phaseResults["C"] = @{
    Time     = $timeC
    Success  = $resultC.Success
    Counts   = $countsC
    Output   = $resultC.Output
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
Write-Host ""

# Cleanup temp file
if (-not $SkipCleanup) {
    if (Test-Path $policyFile) {
        Remove-Item $policyFile -Force -ErrorAction SilentlyContinue
    }
}

# ============================================================
# Summary Table
# ============================================================

Write-Host "=================================================="
Write-Host "  Large Policy Stress Test Summary"
Write-Host "=================================================="
Write-Host ""

$col1 = 10  # Phase
$col2 = 35  # Description
$col3 = 12  # Time
$col4 = 10  # Created
$col5 = 10  # Removed
$col6 = 12  # Unchanged
$col7 = 8   # Result

$header = "{0,-$col1} {1,-$col2} {2,-$col3} {3,-$col4} {4,-$col5} {5,-$col6} {6,-$col7}" -f `
    "Phase", "Description", "Time (ms)", "Created", "Removed", "Unchanged", "Result"
$separator = "-" * ($col1 + $col2 + $col3 + $col4 + $col5 + $col6 + $col7 + 6)

Write-Host $header
Write-Host $separator

$phaseDescriptions = @{
    "A" = "First apply ($RuleCount new filters)"
    "B" = "Idempotent re-apply (same policy)"
    "C" = "Partial diff ($halfCount rules changed)"
}

foreach ($phase in @("A", "B", "C")) {
    $p = $phaseResults[$phase]
    $color = if ($p.Success) { $Green } else { $Red }
    $resultText = if ($p.Success) { "OK" } else { "FAIL" }

    $created   = if ($null -ne $p.Counts.Created)   { "$($p.Counts.Created)" }   else { "-" }
    $removed   = if ($null -ne $p.Counts.Removed)   { "$($p.Counts.Removed)" }   else { "-" }
    $unchanged = if ($null -ne $p.Counts.Unchanged)  { "$($p.Counts.Unchanged)" } else { "-" }

    $line = "{0,-$col1} {1,-$col2} {2,-$col3} {3,-$col4} {4,-$col5} {5,-$col6} {6}{7,-$col7}{8}" -f `
        "Phase $phase", $phaseDescriptions[$phase], "$($p.Time)", $created, $removed, $unchanged, $color, $resultText, $Reset
    Write-Host $line
}

Write-Host ""
Write-Host "  Rule count: $RuleCount"
Write-Host ""

$allPassed = $phaseResults.Values | ForEach-Object { $_.Success } | Where-Object { -not $_ }
if ($null -eq $allPassed -or $allPassed.Count -eq 0) {
    Write-Host "${Green}All phases completed successfully.${Reset}"
    exit 0
} else {
    Write-Host "${Red}One or more phases failed. Review output above.${Reset}"
    exit 1
}
