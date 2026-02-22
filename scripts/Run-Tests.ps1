#Requires -RunAsAdministrator
#Requires -Version 5.1
<#
.SYNOPSIS
    Unified test runner for WfpTrafficControl test suite.

.DESCRIPTION
    Executes tests across all categories defined in the testing strategy (025-testing-strategy.md):
    - Tier 1: Smoke tests (< 2 minutes)
    - Tier 2: Integration tests (weekly/per-phase)
    - Tier 3: Benchmarks
    - Tier 4: Full matrix (pre-presentation)
    - Unit: .NET xUnit tests

    Produces thorough reporting including:
    - Live console output with color-coded results
    - JSON report for machine consumption
    - HTML summary report for human review

.PARAMETER Tier
    Which test tier(s) to run. Default is "Tier1".
    Options: All, Tier1, Tier2, Tier3, Tier4, Unit, Integration, Smoke

.PARAMETER Include
    Run only tests matching these names (supports wildcards).

.PARAMETER Exclude
    Skip tests matching these names (supports wildcards).

.PARAMETER StopOnFailure
    Stop execution at the first test failure.

.PARAMETER SkipPrerequisites
    Skip prerequisite checks (service running, tools available).

.PARAMETER NoCleanup
    Don't run rollback after tests complete.

.PARAMETER OutputDirectory
    Directory for test reports. Default: .\TestResults

.PARAMETER NoHtmlReport
    Skip generating HTML report.

.PARAMETER NoJsonReport
    Skip generating JSON report.

.PARAMETER TargetIp
    IP address of second VM for network tests (nmap, iperf3).

.PARAMETER PolicyPath
    Path to policy file for hot reload tests.

.PARAMETER TimeoutMinutes
    Per-test timeout in minutes. Default: 10

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Auto-detected if not specified.

.PARAMETER Verbose
    Show detailed output from each test.

.EXAMPLE
    .\scripts\Run-Tests.ps1
    Runs Tier 1 smoke tests with default settings.

.EXAMPLE
    .\scripts\Run-Tests.ps1 -Tier All
    Runs all test tiers.

.EXAMPLE
    .\scripts\Run-Tests.ps1 -Tier Tier1,Tier2 -StopOnFailure
    Runs Tier 1 and 2, stopping at first failure.

.EXAMPLE
    .\scripts\Run-Tests.ps1 -Tier Tier2 -TargetIp 192.168.1.20
    Runs Tier 2 tests with second VM for network tests.

.EXAMPLE
    .\scripts\Run-Tests.ps1 -Include "*Large*","*Rapid*"
    Runs only tests matching the patterns.

.NOTES
    Must be run as Administrator.
    See docs/features/025-testing-strategy.md for test details.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet("All", "Tier1", "Tier2", "Tier3", "Tier4", "Unit", "Integration", "Smoke")]
    [string[]]$Tier = @("Tier1"),

    [Parameter()]
    [string[]]$Include,

    [Parameter()]
    [string[]]$Exclude,

    [Parameter()]
    [switch]$StopOnFailure,

    [Parameter()]
    [switch]$SkipPrerequisites,

    [Parameter()]
    [switch]$NoCleanup,

    [Parameter()]
    [string]$OutputDirectory = ".\TestResults",

    [Parameter()]
    [switch]$NoHtmlReport,

    [Parameter()]
    [switch]$NoJsonReport,

    [Parameter()]
    [string]$TargetIp,

    [Parameter()]
    [string]$PolicyPath,

    [Parameter()]
    [ValidateRange(1, 60)]
    [int]$TimeoutMinutes = 10,

    [Parameter()]
    [string]$WfpctlPath,

    [Parameter()]
    [switch]$ListTests
)

$ErrorActionPreference = "Stop"

# ============================================================
# Import Helper Module
# ============================================================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$modulePath = Join-Path $scriptDir "Test-Helpers.psm1"

if (-not (Test-Path $modulePath)) {
    Write-Error "Test-Helpers.psm1 not found at: $modulePath"
    exit 1
}

Import-Module $modulePath -Force

# ============================================================
# Test Registry
# ============================================================

$TestRegistry = @{
    Tier1 = @(
        @{
            Name            = "Test-DemoBlock"
            Script          = "Test-DemoBlock.ps1"
            Description     = "Outbound TCP block + rollback"
            RequiresService = $true
        }
        @{
            Name            = "Test-InboundBlock"
            Script          = "Test-InboundBlock.ps1"
            Description     = "Inbound TCP block + rollback"
            RequiresService = $true
        }
        @{
            Name            = "Test-UdpBlock"
            Script          = "Test-UdpBlock.ps1"
            Description     = "Outbound UDP block + rollback"
            RequiresService = $true
        }
        @{
            Name            = "Test-IpcSecurity"
            Script          = "Test-IpcSecurity.ps1"
            Description     = "IPC authorization checks"
            RequiresService = $true
        }
    )

    Tier2 = @(
        @{
            Name            = "Test-RuleEnforcement"
            Script          = "Test-RuleEnforcement.ps1"
            Description     = "Parameterized rule enforcement"
            RequiresService = $true
        }
        @{
            Name            = "Test-NmapMatrix"
            Script          = "Test-NmapMatrix.ps1"
            Description     = "Full rule enforcement matrix (4 groups)"
            RequiresService = $true
            RequiresTool    = "nmap"
            RequiresParam   = "TargetIp"
        }
        @{
            Name            = "Test-LargePolicyStress"
            Script          = "Test-LargePolicyStress.ps1"
            Description     = "500-rule compile/apply stress"
            RequiresService = $true
        }
        @{
            Name            = "Test-RapidApply"
            Script          = "Test-RapidApply.ps1"
            Description     = "Rapid policy apply stress"
            RequiresService = $true
        }
        @{
            Name            = "Test-ProcessPath"
            Script          = "Test-ProcessPath.ps1"
            Description     = "Process-path matching with curl"
            RequiresService = $true
        }
    )

    Tier3 = @(
        @{
            Name        = "Benchmarks"
            Type        = "dotnet-benchmark"
            Project     = "benchmarks"
            Description = "BenchmarkDotNet performance tests"
        }
        @{
            Name          = "Test-Iperf3Baseline"
            Script        = "Test-Iperf3Baseline.ps1"
            Description   = "TCP throughput benchmark"
            RequiresTool  = "iperf3"
            RequiresParam = "TargetIp"
        }
    )

    Tier4 = @(
        @{
            Name            = "Test-ServiceRestart"
            Script          = "Test-ServiceRestart.ps1"
            Description     = "Filter persistence + service recovery"
            RequiresService = $true
        }
        @{
            Name            = "Test-HotReloadStress"
            Script          = "Test-HotReloadStress.ps1"
            Description     = "File watcher debounce validation"
            RequiresService = $true
            RequiresParam   = "PolicyPath"
        }
        @{
            Name            = "Test-ConcurrentIpc"
            Script          = "Test-ConcurrentIpc.ps1"
            Description     = "Concurrent IPC client stress"
            RequiresService = $true
        }
    )

    Unit = @(
        @{
            Name        = "UnitTests"
            Type        = "dotnet-test"
            Project     = "tests"
            Description = "xUnit unit tests (600+ tests)"
        }
    )
}

# ============================================================
# Helper Functions
# ============================================================

function Get-TestsToRun {
    param(
        [string[]]$RequestedTiers,
        [string[]]$IncludePatterns,
        [string[]]$ExcludePatterns
    )

    $testsToRun = [System.Collections.ArrayList]::new()

    # Expand tier aliases
    $expandedTiers = @()
    foreach ($t in $RequestedTiers) {
        switch ($t) {
            "All"         { $expandedTiers += @("Tier1", "Tier2", "Tier3", "Tier4", "Unit") }
            "Integration" { $expandedTiers += @("Tier1", "Tier2", "Tier4") }
            "Smoke"       { $expandedTiers += @("Tier1") }
            default       { $expandedTiers += $t }
        }
    }
    $expandedTiers = $expandedTiers | Select-Object -Unique

    foreach ($tierName in $expandedTiers) {
        if (-not $TestRegistry.ContainsKey($tierName)) {
            Write-TestWarn "Unknown tier: $tierName"
            continue
        }

        foreach ($test in $TestRegistry[$tierName]) {
            $testEntry = $test.Clone()
            $testEntry["Tier"] = $tierName

            # Apply include filter
            if ($IncludePatterns -and $IncludePatterns.Count -gt 0) {
                $matched = $false
                foreach ($pattern in $IncludePatterns) {
                    if ($testEntry.Name -like $pattern) {
                        $matched = $true
                        break
                    }
                }
                if (-not $matched) { continue }
            }

            # Apply exclude filter
            if ($ExcludePatterns -and $ExcludePatterns.Count -gt 0) {
                $excluded = $false
                foreach ($pattern in $ExcludePatterns) {
                    if ($testEntry.Name -like $pattern) {
                        $excluded = $true
                        break
                    }
                }
                if ($excluded) { continue }
            }

            $testsToRun.Add($testEntry) | Out-Null
        }
    }

    return $testsToRun
}

function Test-Prerequisites {
    param(
        [hashtable]$Test,
        [bool]$ServiceRunning,
        [string]$WfpctlPath,
        [string]$TargetIp,
        [string]$PolicyPath
    )

    $issues = [System.Collections.ArrayList]::new()

    # Check service requirement
    if ($Test.RequiresService -and -not $ServiceRunning) {
        $issues.Add("Service not running") | Out-Null
    }

    # Check tool requirement
    if ($Test.RequiresTool) {
        if (-not (Test-ToolAvailable $Test.RequiresTool)) {
            $issues.Add("Tool not found: $($Test.RequiresTool)") | Out-Null
        }
    }

    # Check parameter requirements
    if ($Test.RequiresParam) {
        switch ($Test.RequiresParam) {
            "TargetIp" {
                if (-not $TargetIp) {
                    $issues.Add("Parameter required: -TargetIp") | Out-Null
                }
            }
            "PolicyPath" {
                if (-not $PolicyPath) {
                    $issues.Add("Parameter required: -PolicyPath") | Out-Null
                }
            }
        }
    }

    # Check wfpctl for script tests
    if ($Test.Script -and -not (Test-Path $WfpctlPath)) {
        $issues.Add("wfpctl not found: $WfpctlPath") | Out-Null
    }

    return $issues
}

function Invoke-PowerShellTest {
    param(
        [hashtable]$Test,
        [string]$ScriptDir,
        [string]$WfpctlPath,
        [string]$TargetIp,
        [string]$PolicyPath,
        [int]$TimeoutMinutes
    )

    $scriptPath = Join-Path $ScriptDir $Test.Script

    if (-not (Test-Path $scriptPath)) {
        return @{
            Success  = $false
            Output   = ""
            Error    = "Script not found: $scriptPath"
            Duration = [TimeSpan]::Zero
        }
    }

    # Build arguments
    $arguments = @("-WfpctlPath", $WfpctlPath)

    if ($Test.RequiresParam -eq "TargetIp" -and $TargetIp) {
        $arguments += @("-TargetIp", $TargetIp)
    }
    if ($Test.RequiresParam -eq "PolicyPath" -and $PolicyPath) {
        $arguments += @("-PolicyPath", $PolicyPath)
    }

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = "powershell.exe"
    $pinfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" $($arguments -join ' ')"
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true
    $pinfo.WorkingDirectory = (Get-Location).Path

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $pinfo

    $stdout = New-Object System.Text.StringBuilder
    $stderr = New-Object System.Text.StringBuilder

    $stdoutEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $stdout

    $stderrEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $stderr

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $process.Start() | Out-Null
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        $timeoutMs = $TimeoutMinutes * 60 * 1000
        $completed = $process.WaitForExit($timeoutMs)

        $sw.Stop()

        if (-not $completed) {
            $process.Kill()
            return @{
                Success  = $false
                Output   = $stdout.ToString()
                Error    = "TIMEOUT: Test exceeded $TimeoutMinutes minutes"
                Duration = $sw.Elapsed
            }
        }

        # Small delay for async reads
        Start-Sleep -Milliseconds 100

        return @{
            Success  = ($process.ExitCode -eq 0)
            Output   = $stdout.ToString()
            Error    = $stderr.ToString()
            ExitCode = $process.ExitCode
            Duration = $sw.Elapsed
        }
    }
    finally {
        Unregister-Event -SourceIdentifier $stdoutEvent.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $stderrEvent.Name -ErrorAction SilentlyContinue
        if (-not $process.HasExited) {
            try { $process.Kill() } catch { }
        }
        $process.Dispose()
    }
}

function Invoke-DotNetTest {
    param(
        [string]$ProjectPath,
        [int]$TimeoutMinutes
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = "dotnet"
    $pinfo.Arguments = "test `"$ProjectPath`" --no-build --verbosity normal"
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true
    $pinfo.WorkingDirectory = (Get-Location).Path

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $pinfo

    $stdout = New-Object System.Text.StringBuilder
    $stderr = New-Object System.Text.StringBuilder

    $stdoutEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $stdout

    $stderrEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $stderr

    try {
        $process.Start() | Out-Null
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        $timeoutMs = $TimeoutMinutes * 60 * 1000
        $completed = $process.WaitForExit($timeoutMs)

        $sw.Stop()

        if (-not $completed) {
            $process.Kill()
            return @{
                Success  = $false
                Output   = $stdout.ToString()
                Error    = "TIMEOUT: Test exceeded $TimeoutMinutes minutes"
                Duration = $sw.Elapsed
            }
        }

        Start-Sleep -Milliseconds 100

        return @{
            Success  = ($process.ExitCode -eq 0)
            Output   = $stdout.ToString()
            Error    = $stderr.ToString()
            ExitCode = $process.ExitCode
            Duration = $sw.Elapsed
        }
    }
    finally {
        Unregister-Event -SourceIdentifier $stdoutEvent.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $stderrEvent.Name -ErrorAction SilentlyContinue
        if (-not $process.HasExited) {
            try { $process.Kill() } catch { }
        }
        $process.Dispose()
    }
}

function Invoke-DotNetBenchmark {
    param(
        [string]$ProjectPath,
        [int]$TimeoutMinutes
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = "dotnet"
    $pinfo.Arguments = "run -c Release --project `"$ProjectPath`" -- --filter *"
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true
    $pinfo.WorkingDirectory = (Get-Location).Path

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $pinfo

    $stdout = New-Object System.Text.StringBuilder
    $stderr = New-Object System.Text.StringBuilder

    $stdoutEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $stdout

    $stderrEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
        if ($null -ne $EventArgs.Data) {
            $Event.MessageData.AppendLine($EventArgs.Data) | Out-Null
        }
    } -MessageData $stderr

    try {
        $process.Start() | Out-Null
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        $timeoutMs = $TimeoutMinutes * 60 * 1000
        $completed = $process.WaitForExit($timeoutMs)

        $sw.Stop()

        if (-not $completed) {
            $process.Kill()
            return @{
                Success  = $false
                Output   = $stdout.ToString()
                Error    = "TIMEOUT: Benchmark exceeded $TimeoutMinutes minutes"
                Duration = $sw.Elapsed
            }
        }

        Start-Sleep -Milliseconds 100

        return @{
            Success  = ($process.ExitCode -eq 0)
            Output   = $stdout.ToString()
            Error    = $stderr.ToString()
            ExitCode = $process.ExitCode
            Duration = $sw.Elapsed
        }
    }
    finally {
        Unregister-Event -SourceIdentifier $stdoutEvent.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $stderrEvent.Name -ErrorAction SilentlyContinue
        if (-not $process.HasExited) {
            try { $process.Kill() } catch { }
        }
        $process.Dispose()
    }
}

# ============================================================
# Main Script
# ============================================================

Write-TestHeader "WfpTrafficControl Test Runner"

# Resolve wfpctl path
if (-not $WfpctlPath) {
    $WfpctlPath = Get-WfpctlDefaultPath
}
if (Test-Path $WfpctlPath) {
    $WfpctlPath = (Resolve-Path $WfpctlPath).Path
}

Write-TestInfo "wfpctl path: $WfpctlPath"
Write-TestInfo "Output directory: $OutputDirectory"
Write-TestInfo "Tiers: $($Tier -join ', ')"
Write-Host ""

# Get tests to run
$testsToRun = Get-TestsToRun -RequestedTiers $Tier -IncludePatterns $Include -ExcludePatterns $Exclude

if ($testsToRun.Count -eq 0) {
    Write-TestWarn "No tests match the specified criteria."
    exit 0
}

# List tests mode
if ($ListTests) {
    Write-TestSection "Available Tests"

    $grouped = $testsToRun | Group-Object -Property Tier
    foreach ($group in $grouped | Sort-Object Name) {
        Write-Host "$($Colors.Cyan)$($group.Name):$($Colors.Reset)"
        foreach ($test in $group.Group) {
            $reqs = @()
            if ($test.RequiresService) { $reqs += "service" }
            if ($test.RequiresTool) { $reqs += $test.RequiresTool }
            if ($test.RequiresParam) { $reqs += "-$($test.RequiresParam)" }
            $reqsStr = if ($reqs.Count -gt 0) { " (requires: $($reqs -join ', '))" } else { "" }
            Write-Host "  - $($test.Name): $($test.Description)$reqsStr"
        }
        Write-Host ""
    }
    exit 0
}

Write-TestInfo "Tests to run: $($testsToRun.Count)"
Write-Host ""

# Check service status
$serviceRunning = Test-ServiceRunning
if ($serviceRunning) {
    Write-TestPass "WfpTrafficControl service is running"
} else {
    Write-TestWarn "WfpTrafficControl service is not running"
}

# Check wfpctl
if (Test-Path $WfpctlPath) {
    Write-TestPass "wfpctl found"
} else {
    Write-TestWarn "wfpctl not found at: $WfpctlPath"
}

Write-Host ""

# Create report
$report = New-TestReport

# Create output directory
if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}

# ============================================================
# Execute Tests
# ============================================================

$stopRequested = $false
$executionError = $null

try {
    foreach ($test in $testsToRun) {
        if ($stopRequested) { break }

        Write-TestSection "$($test.Tier) / $($test.Name)"
        Write-TestInfo $test.Description

        # Check prerequisites
        if (-not $SkipPrerequisites) {
            $prereqIssues = Test-Prerequisites -Test $test `
                -ServiceRunning $serviceRunning `
                -WfpctlPath $WfpctlPath `
                -TargetIp $TargetIp `
                -PolicyPath $PolicyPath

            if ($prereqIssues.Count -gt 0) {
                $skipReason = "Prerequisites not met: $($prereqIssues -join '; ')"
                Write-TestSkip $skipReason

                Add-TestResult -Report $report `
                    -Name $test.Name `
                    -Tier $test.Tier `
                    -Status "Skipped" `
                    -SkipReason $skipReason

                continue
            }
        }

        # Execute test based on type
        $result = $null

        if ($test.Type -eq "dotnet-test") {
            Write-TestStep "Running dotnet test..."
            $projectPath = Join-Path (Get-Location).Path $test.Project
            $result = Invoke-DotNetTest -ProjectPath $projectPath -TimeoutMinutes $TimeoutMinutes
        }
        elseif ($test.Type -eq "dotnet-benchmark") {
            Write-TestStep "Running BenchmarkDotNet (this may take several minutes)..."
            $projectPath = Join-Path (Get-Location).Path $test.Project
            $result = Invoke-DotNetBenchmark -ProjectPath $projectPath -TimeoutMinutes ($TimeoutMinutes * 3)
        }
        else {
            Write-TestStep "Running PowerShell test..."
            $result = Invoke-PowerShellTest -Test $test `
                -ScriptDir $scriptDir `
                -WfpctlPath $WfpctlPath `
                -TargetIp $TargetIp `
                -PolicyPath $PolicyPath `
                -TimeoutMinutes $TimeoutMinutes
        }

        # Parse metrics from output
        $parsedResult = Get-TestResultFromOutput -Output $result.Output

        # Report result
        if ($result.Success) {
            Write-TestPass "$($test.Name) completed in $($result.Duration.ToString('mm\:ss\.fff'))"

            Add-TestResult -Report $report `
                -Name $test.Name `
                -Tier $test.Tier `
                -Status "Passed" `
                -Duration $result.Duration `
                -Output $result.Output `
                -Metrics $parsedResult.Metrics
        }
        else {
            Write-TestFail "$($test.Name) failed"
            if ($result.Error) {
                Write-Host "  Error: $($result.Error)" -ForegroundColor Red
            }

            Add-TestResult -Report $report `
                -Name $test.Name `
                -Tier $test.Tier `
                -Status "Failed" `
                -Duration $result.Duration `
                -Output $result.Output `
                -Error $result.Error `
                -Metrics $parsedResult.Metrics

            if ($StopOnFailure) {
                Write-TestWarn "Stopping due to -StopOnFailure"
                $stopRequested = $true
            }
        }

        # Show verbose output if requested
        if ($VerbosePreference -eq "Continue" -and $result.Output) {
            Write-Host ""
            Write-Host "--- Test Output ---" -ForegroundColor Gray
            Write-Host $result.Output -ForegroundColor Gray
            Write-Host "-------------------" -ForegroundColor Gray
        }
    }
}
catch {
    $executionError = $_
    Write-TestFail "Test execution error: $($_.Exception.Message)"
}
finally {
    # ============================================================
    # Cleanup (always runs)
    # ============================================================

    if (-not $NoCleanup -and $serviceRunning -and (Test-Path $WfpctlPath)) {
        Write-TestSection "Cleanup"
        Write-TestStep "Running rollback to ensure clean state..."

        try {
            $rollbackResult = Invoke-Wfpctl -WfpctlPath $WfpctlPath -Arguments @("rollback")
            if ($rollbackResult.Success) {
                Write-TestPass "Rollback completed"
            } else {
                Write-TestWarn "Rollback returned non-zero: $($rollbackResult.Output)"
            }
        }
        catch {
            Write-TestWarn "Rollback failed: $($_.Exception.Message)"
        }
    }
}

# ============================================================
# Generate Reports
# ============================================================

Complete-TestReport -Report $report

Write-TestSection "Generating Reports"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if (-not $NoJsonReport) {
    $jsonPath = Join-Path $OutputDirectory "report-$timestamp.json"
    Export-TestReportJson -Report $report -OutputPath $jsonPath
    Write-TestPass "JSON report: $jsonPath"

    # Also write latest.json for easy access
    $latestJsonPath = Join-Path $OutputDirectory "latest.json"
    Export-TestReportJson -Report $report -OutputPath $latestJsonPath
}

if (-not $NoHtmlReport) {
    $htmlPath = Join-Path $OutputDirectory "report-$timestamp.html"
    Export-TestReportHtml -Report $report -OutputPath $htmlPath
    Write-TestPass "HTML report: $htmlPath"

    # Also write latest.html for easy access
    $latestHtmlPath = Join-Path $OutputDirectory "latest.html"
    Export-TestReportHtml -Report $report -OutputPath $latestHtmlPath
}

# ============================================================
# Summary
# ============================================================

Write-TestSummary -Report $report

# Exit code
if ($executionError -or $report.summary.failed -gt 0) {
    exit 1
} else {
    exit 0
}
