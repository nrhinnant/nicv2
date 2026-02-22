#Requires -Version 5.1
<#
.SYNOPSIS
    Shared helper functions for WfpTrafficControl test scripts.

.DESCRIPTION
    This module provides common utilities for test execution, output formatting,
    result parsing, and report generation used by Run-Tests.ps1 and individual
    test scripts.
#>

# ============================================================
# ANSI Color Codes
# ============================================================

$script:Colors = @{
    Green  = "`e[32m"
    Red    = "`e[31m"
    Yellow = "`e[33m"
    Cyan   = "`e[36m"
    White  = "`e[37m"
    Gray   = "`e[90m"
    Bold   = "`e[1m"
    Reset  = "`e[0m"
}

# ============================================================
# Output Formatting Functions
# ============================================================

function Write-TestHeader {
    param([string]$Title)
    $line = "=" * 60
    Write-Host ""
    Write-Host "$($script:Colors.Cyan)$line$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Cyan)  $Title$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Cyan)$line$($script:Colors.Reset)"
    Write-Host ""
}

function Write-TestSection {
    param([string]$Title)
    $line = "-" * 50
    Write-Host ""
    Write-Host "$($script:Colors.Cyan)$line$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Cyan)  $Title$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Cyan)$line$($script:Colors.Reset)"
    Write-Host ""
}

function Write-TestStep {
    param([string]$Message)
    Write-Host "$($script:Colors.Cyan)[TEST]$($script:Colors.Reset) $Message"
}

function Write-TestPass {
    param([string]$Message)
    Write-Host "$($script:Colors.Green)[PASS]$($script:Colors.Reset) $Message"
}

function Write-TestFail {
    param([string]$Message)
    Write-Host "$($script:Colors.Red)[FAIL]$($script:Colors.Reset) $Message"
}

function Write-TestWarn {
    param([string]$Message)
    Write-Host "$($script:Colors.Yellow)[WARN]$($script:Colors.Reset) $Message"
}

function Write-TestSkip {
    param([string]$Message)
    Write-Host "$($script:Colors.Gray)[SKIP]$($script:Colors.Reset) $Message"
}

function Write-TestInfo {
    param([string]$Message)
    Write-Host "$($script:Colors.White)[INFO]$($script:Colors.Reset) $Message"
}

# ============================================================
# CLI Invocation
# ============================================================

function Invoke-Wfpctl {
    param(
        [Parameter(Mandatory)]
        [string]$WfpctlPath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [int]$TimeoutSeconds = 60
    )

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $WfpctlPath
    $pinfo.Arguments = $Arguments -join " "
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.CreateNoWindow = $true

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

        $completed = $process.WaitForExit($TimeoutSeconds * 1000)

        if (-not $completed) {
            $process.Kill()
            return @{
                Output   = "TIMEOUT: Process exceeded ${TimeoutSeconds}s"
                Error    = ""
                ExitCode = -1
                Success  = $false
                TimedOut = $true
            }
        }

        # Small delay to ensure async reads complete
        Start-Sleep -Milliseconds 100

        return @{
            Output   = $stdout.ToString().Trim()
            Error    = $stderr.ToString().Trim()
            ExitCode = $process.ExitCode
            Success  = ($process.ExitCode -eq 0)
            TimedOut = $false
        }
    }
    finally {
        Unregister-Event -SourceIdentifier $stdoutEvent.Name -ErrorAction SilentlyContinue
        Unregister-Event -SourceIdentifier $stderrEvent.Name -ErrorAction SilentlyContinue
        if (-not $process.HasExited) {
            $process.Kill()
        }
        $process.Dispose()
    }
}

# ============================================================
# Prerequisite Checks
# ============================================================

function Test-ServiceRunning {
    param([string]$ServiceName = "WfpTrafficControl")

    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return ($null -ne $service -and $service.Status -eq "Running")
}

function Test-ToolAvailable {
    param([string]$ToolName)

    $result = Get-Command $ToolName -ErrorAction SilentlyContinue
    return ($null -ne $result)
}

function Test-WfpctlExists {
    param([string]$WfpctlPath)

    return (Test-Path $WfpctlPath)
}

function Get-WfpctlDefaultPath {
    $candidates = @(
        ".\src\cli\bin\Debug\net8.0\wfpctl.exe",
        ".\src\cli\bin\Release\net8.0\wfpctl.exe",
        ".\bin\Debug\net8.0\wfpctl.exe",
        ".\bin\Release\net8.0\wfpctl.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            return (Resolve-Path $path).Path
        }
    }

    return $candidates[0]  # Return default even if not found
}

# ============================================================
# Result Parsing
# ============================================================

function Get-TestResultFromOutput {
    <#
    .SYNOPSIS
        Parses test script output to extract pass/fail counts.
    #>
    param([string]$Output)

    $result = @{
        Passed  = 0
        Failed  = 0
        Skipped = 0
        Metrics = @{}
    }

    # Parse "[PASS]" and "[FAIL]" markers
    $passMatches = [regex]::Matches($Output, '\[PASS\]')
    $failMatches = [regex]::Matches($Output, '\[FAIL\]')
    $skipMatches = [regex]::Matches($Output, '\[SKIP\]')

    $result.Passed = $passMatches.Count
    $result.Failed = $failMatches.Count
    $result.Skipped = $skipMatches.Count

    # Parse summary if present (e.g., "Passed: 5")
    if ($Output -match "Passed:\s*(\d+)") {
        $result.Passed = [int]$Matches[1]
    }
    if ($Output -match "Failed:\s*(\d+)") {
        $result.Failed = [int]$Matches[1]
    }

    # Parse enforcement latency if present
    if ($Output -match "(?:latency|Latency).*?(\d+(?:\.\d+)?)\s*ms") {
        $result.Metrics["enforcementLatency"] = "$($Matches[1])ms"
    }

    # Parse throughput if present
    if ($Output -match "(\d+(?:\.\d+)?)\s*(?:Mbps|Gbps)") {
        $result.Metrics["throughput"] = $Matches[0]
    }

    # Parse filter counts if present
    if ($Output -match "Created:\s*(\d+)") {
        $result.Metrics["filtersCreated"] = [int]$Matches[1]
    }
    if ($Output -match "Removed:\s*(\d+)") {
        $result.Metrics["filtersRemoved"] = [int]$Matches[1]
    }

    return $result
}

# ============================================================
# Report Generation
# ============================================================

function New-TestReport {
    <#
    .SYNOPSIS
        Creates a new test report object.
    #>
    param(
        [string]$RunId = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    )

    return @{
        runId       = $RunId
        startTime   = Get-Date
        endTime     = $null
        environment = @{
            hostname   = $env:COMPUTERNAME
            username   = $env:USERNAME
            powershell = $PSVersionTable.PSVersion.ToString()
            os         = [System.Environment]::OSVersion.VersionString
        }
        summary     = @{
            passed   = 0
            failed   = 0
            skipped  = 0
            total    = 0
            duration = $null
        }
        tests       = [System.Collections.ArrayList]::new()
    }
}

function Add-TestResult {
    <#
    .SYNOPSIS
        Adds a test result to the report.
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$Report,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Tier,

        [Parameter(Mandatory)]
        [ValidateSet("Passed", "Failed", "Skipped")]
        [string]$Status,

        [TimeSpan]$Duration = [TimeSpan]::Zero,

        [string]$Output = "",

        [string]$Error = "",

        [hashtable]$Metrics = @{},

        [string]$SkipReason = ""
    )

    $testResult = @{
        name       = $Name
        tier       = $Tier
        status     = $Status
        duration   = $Duration.ToString("hh\:mm\:ss\.fff")
        durationMs = [int]$Duration.TotalMilliseconds
        output     = $Output
        error      = $Error
        metrics    = $Metrics
        skipReason = $SkipReason
        timestamp  = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    }

    $Report.tests.Add($testResult) | Out-Null
    $Report.summary.total++

    switch ($Status) {
        "Passed"  { $Report.summary.passed++ }
        "Failed"  { $Report.summary.failed++ }
        "Skipped" { $Report.summary.skipped++ }
    }
}

function Complete-TestReport {
    <#
    .SYNOPSIS
        Finalizes the test report with end time and duration.
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$Report
    )

    $Report.endTime = Get-Date
    $duration = $Report.endTime - $Report.startTime
    $Report.summary.duration = $duration.ToString("hh\:mm\:ss")
}

function Export-TestReportJson {
    <#
    .SYNOPSIS
        Exports the test report to JSON format.
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$Report,

        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $directory = Split-Path -Parent $OutputPath
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $Report | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8
}

function ConvertTo-HtmlSafe {
    <#
    .SYNOPSIS
        HTML-encodes a string to prevent XSS.
    #>
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return ""
    }

    return $Text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace('"', "&quot;").Replace("'", "&#39;")
}

function Export-TestReportHtml {
    <#
    .SYNOPSIS
        Exports the test report to HTML format with styling.
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$Report,

        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $directory = Split-Path -Parent $OutputPath
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $passedPct = if ($Report.summary.total -gt 0) {
        [math]::Round(($Report.summary.passed / $Report.summary.total) * 100, 1)
    } else { 0 }

    $statusColor = if ($Report.summary.failed -eq 0) { "#28a745" } else { "#dc3545" }

    $testsHtml = ""
    foreach ($test in $Report.tests) {
        $rowClass = switch ($test.status) {
            "Passed"  { "passed" }
            "Failed"  { "failed" }
            "Skipped" { "skipped" }
        }

        $statusBadge = switch ($test.status) {
            "Passed"  { '<span class="badge badge-pass">PASS</span>' }
            "Failed"  { '<span class="badge badge-fail">FAIL</span>' }
            "Skipped" { '<span class="badge badge-skip">SKIP</span>' }
        }

        $metricsHtml = ""
        if ($test.metrics.Count -gt 0) {
            $metricsHtml = "<ul class='metrics'>"
            foreach ($key in $test.metrics.Keys) {
                $metricsHtml += "<li><strong>$(ConvertTo-HtmlSafe $key)</strong>: $(ConvertTo-HtmlSafe $test.metrics[$key])</li>"
            }
            $metricsHtml += "</ul>"
        }

        $outputHtml = ""
        if ($test.output -or $test.error -or $test.skipReason) {
            $content = if ($test.skipReason) { $test.skipReason }
                       elseif ($test.error) { $test.error }
                       else { $test.output }
            $escapedContent = ConvertTo-HtmlSafe $content
            $outputHtml = @"
<details>
    <summary>View Output</summary>
    <pre class="output">$escapedContent</pre>
</details>
"@
        }

        $testsHtml += @"
<tr class="$rowClass">
    <td>$(ConvertTo-HtmlSafe $test.name)</td>
    <td>$(ConvertTo-HtmlSafe $test.tier)</td>
    <td>$statusBadge</td>
    <td>$(ConvertTo-HtmlSafe $test.duration)</td>
    <td>$metricsHtml</td>
    <td>$outputHtml</td>
</tr>
"@
    }

    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>WfpTrafficControl Test Report</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: #333;
            background: #f5f5f5;
            padding: 20px;
        }
        .container { max-width: 1400px; margin: 0 auto; }
        h1 { color: #1a1a1a; margin-bottom: 10px; }
        .subtitle { color: #666; margin-bottom: 30px; }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }
        .card {
            background: white;
            border-radius: 8px;
            padding: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            text-align: center;
        }
        .card-value { font-size: 2.5em; font-weight: bold; }
        .card-label { color: #666; font-size: 0.9em; text-transform: uppercase; }
        .card-passed .card-value { color: #28a745; }
        .card-failed .card-value { color: #dc3545; }
        .card-skipped .card-value { color: #6c757d; }
        .card-total .card-value { color: #007bff; }

        .progress-bar {
            height: 8px;
            background: #e9ecef;
            border-radius: 4px;
            overflow: hidden;
            margin: 20px 0;
        }
        .progress-fill {
            height: 100%;
            background: $statusColor;
            transition: width 0.3s ease;
        }

        table {
            width: 100%;
            background: white;
            border-collapse: collapse;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        th, td { padding: 12px 15px; text-align: left; }
        th { background: #1a1a1a; color: white; font-weight: 500; }
        tr:nth-child(even) { background: #f8f9fa; }
        tr:hover { background: #e9ecef; }
        tr.failed { background: #fff5f5; }
        tr.failed:hover { background: #ffe0e0; }
        tr.skipped { background: #f8f9fa; opacity: 0.7; }

        .badge {
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 0.8em;
            font-weight: 600;
        }
        .badge-pass { background: #d4edda; color: #155724; }
        .badge-fail { background: #f8d7da; color: #721c24; }
        .badge-skip { background: #e2e3e5; color: #383d41; }

        .metrics { list-style: none; font-size: 0.85em; }
        .metrics li { margin: 2px 0; }

        details { cursor: pointer; }
        details summary { color: #007bff; font-size: 0.85em; }
        .output {
            background: #1a1a1a;
            color: #f8f8f2;
            padding: 10px;
            border-radius: 4px;
            font-size: 0.8em;
            max-height: 300px;
            overflow: auto;
            white-space: pre-wrap;
            word-wrap: break-word;
            margin-top: 10px;
        }

        .env-info {
            background: white;
            border-radius: 8px;
            padding: 15px 20px;
            margin-top: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            font-size: 0.9em;
            color: #666;
        }
        .env-info span { margin-right: 20px; }
    </style>
</head>
<body>
    <div class="container">
        <h1>WfpTrafficControl Test Report</h1>
        <p class="subtitle">Run ID: $($Report.runId) | Duration: $($Report.summary.duration)</p>

        <div class="progress-bar">
            <div class="progress-fill" style="width: ${passedPct}%"></div>
        </div>

        <div class="summary-cards">
            <div class="card card-passed">
                <div class="card-value">$($Report.summary.passed)</div>
                <div class="card-label">Passed</div>
            </div>
            <div class="card card-failed">
                <div class="card-value">$($Report.summary.failed)</div>
                <div class="card-label">Failed</div>
            </div>
            <div class="card card-skipped">
                <div class="card-value">$($Report.summary.skipped)</div>
                <div class="card-label">Skipped</div>
            </div>
            <div class="card card-total">
                <div class="card-value">$($Report.summary.total)</div>
                <div class="card-label">Total</div>
            </div>
        </div>

        <table>
            <thead>
                <tr>
                    <th>Test Name</th>
                    <th>Tier</th>
                    <th>Status</th>
                    <th>Duration</th>
                    <th>Metrics</th>
                    <th>Details</th>
                </tr>
            </thead>
            <tbody>
                $testsHtml
            </tbody>
        </table>

        <div class="env-info">
            <span><strong>Host:</strong> $($Report.environment.hostname)</span>
            <span><strong>User:</strong> $($Report.environment.username)</span>
            <span><strong>PowerShell:</strong> $($Report.environment.powershell)</span>
            <span><strong>OS:</strong> $($Report.environment.os)</span>
        </div>
    </div>
</body>
</html>
"@

    $html | Set-Content -Path $OutputPath -Encoding UTF8
}

function Write-TestSummary {
    <#
    .SYNOPSIS
        Writes a formatted test summary to the console.
    #>
    param(
        [Parameter(Mandatory)]
        [hashtable]$Report
    )

    Write-Host ""
    Write-Host "$($script:Colors.Bold)============================================================$($script:Colors.Reset)"
    Write-Host "$($script:Colors.Bold)                    TEST SUMMARY                           $($script:Colors.Reset)"
    Write-Host "$($script:Colors.Bold)============================================================$($script:Colors.Reset)"
    Write-Host ""

    $passColor = if ($Report.summary.passed -gt 0) { $script:Colors.Green } else { $script:Colors.Gray }
    $failColor = if ($Report.summary.failed -gt 0) { $script:Colors.Red } else { $script:Colors.Gray }
    $skipColor = if ($Report.summary.skipped -gt 0) { $script:Colors.Yellow } else { $script:Colors.Gray }

    Write-Host "  ${passColor}Passed:  $($Report.summary.passed)$($script:Colors.Reset)"
    Write-Host "  ${failColor}Failed:  $($Report.summary.failed)$($script:Colors.Reset)"
    Write-Host "  ${skipColor}Skipped: $($Report.summary.skipped)$($script:Colors.Reset)"
    Write-Host "  Total:   $($Report.summary.total)"
    Write-Host ""
    Write-Host "  Duration: $($Report.summary.duration)"
    Write-Host ""

    if ($Report.summary.failed -gt 0) {
        Write-Host "$($script:Colors.Red)  FAILED TESTS:$($script:Colors.Reset)"
        foreach ($test in $Report.tests | Where-Object { $_.status -eq "Failed" }) {
            Write-Host "    - $($test.name) ($($test.tier))"
        }
        Write-Host ""
    }

    $resultText = if ($Report.summary.failed -eq 0) {
        "$($script:Colors.Green)ALL TESTS PASSED$($script:Colors.Reset)"
    } else {
        "$($script:Colors.Red)TESTS FAILED$($script:Colors.Reset)"
    }
    Write-Host "  Result: $resultText"
    Write-Host ""
}

# ============================================================
# Export Module Members
# ============================================================

Export-ModuleMember -Function @(
    # Output formatting
    'Write-TestHeader',
    'Write-TestSection',
    'Write-TestStep',
    'Write-TestPass',
    'Write-TestFail',
    'Write-TestWarn',
    'Write-TestSkip',
    'Write-TestInfo',

    # CLI invocation
    'Invoke-Wfpctl',

    # Prerequisites
    'Test-ServiceRunning',
    'Test-ToolAvailable',
    'Test-WfpctlExists',
    'Get-WfpctlDefaultPath',

    # Result parsing
    'Get-TestResultFromOutput',

    # Report generation
    'New-TestReport',
    'Add-TestResult',
    'Complete-TestReport',
    'Export-TestReportJson',
    'Export-TestReportHtml',
    'Write-TestSummary'
)

Export-ModuleMember -Variable 'Colors'
