#Requires -Version 5.1
<#
.SYNOPSIS
    Validates the test runner and helper module functionality.

.DESCRIPTION
    Runs quick validation checks on the test infrastructure:
    - Module loading
    - Helper function availability
    - Test registry structure
    - List tests functionality
    - Report generation (in-memory)

    This script does NOT require the service to be running.

.EXAMPLE
    .\scripts\Test-TestRunner.ps1

.NOTES
    This is a meta-test that validates the test runner itself.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$passed = 0
$failed = 0

function Write-Result {
    param(
        [string]$TestName,
        [bool]$Success,
        [string]$Message = ""
    )

    if ($Success) {
        Write-Host "[PASS] $TestName" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "[FAIL] $TestName" -ForegroundColor Red
        if ($Message) {
            Write-Host "       $Message" -ForegroundColor Red
        }
        $script:failed++
    }
}

Write-Host ""
Write-Host "=============================================="
Write-Host "  Test Runner Validation"
Write-Host "=============================================="
Write-Host ""

# Test 1: Module exists
$modulePath = Join-Path $scriptDir "Test-Helpers.psm1"
$moduleExists = Test-Path $modulePath
Write-Result "Test-Helpers.psm1 exists" $moduleExists

# Test 2: Module loads without error
$moduleLoaded = $false
try {
    Import-Module $modulePath -Force -ErrorAction Stop
    $moduleLoaded = $true
} catch {
    Write-Result "Module loads successfully" $false $_.Exception.Message
}
if ($moduleLoaded) {
    Write-Result "Module loads successfully" $true
}

# Test 3: Helper functions are exported
$expectedFunctions = @(
    'Write-TestHeader',
    'Write-TestPass',
    'Write-TestFail',
    'Write-TestSkip',
    'Invoke-Wfpctl',
    'Test-ServiceRunning',
    'Test-ToolAvailable',
    'Get-WfpctlDefaultPath',
    'Get-TestResultFromOutput',
    'New-TestReport',
    'Add-TestResult',
    'Complete-TestReport',
    'Export-TestReportJson',
    'Export-TestReportHtml',
    'Write-TestSummary'
)

$allFunctionsExported = $true
$missingFunctions = @()
foreach ($func in $expectedFunctions) {
    if (-not (Get-Command $func -ErrorAction SilentlyContinue)) {
        $allFunctionsExported = $false
        $missingFunctions += $func
    }
}
Write-Result "All helper functions exported" $allFunctionsExported ($missingFunctions -join ", ")

# Test 4: Run-Tests.ps1 exists
$runTestsPath = Join-Path $scriptDir "Run-Tests.ps1"
$runTestsExists = Test-Path $runTestsPath
Write-Result "Run-Tests.ps1 exists" $runTestsExists

# Test 5: ListTests mode works
$listTestsWorks = $false
try {
    $output = & $runTestsPath -ListTests 2>&1
    $outputStr = $output -join "`n"
    # Should contain tier names and test names
    $listTestsWorks = ($outputStr -match "Tier1" -and $outputStr -match "Test-DemoBlock")
} catch {
    # Ignore errors
}
Write-Result "ListTests mode works" $listTestsWorks

# Test 6: Report generation (in-memory)
$reportWorks = $false
try {
    $report = New-TestReport -RunId "test-run"

    Add-TestResult -Report $report `
        -Name "TestA" `
        -Tier "Tier1" `
        -Status "Passed" `
        -Duration ([TimeSpan]::FromSeconds(5))

    Add-TestResult -Report $report `
        -Name "TestB" `
        -Tier "Tier1" `
        -Status "Failed" `
        -Duration ([TimeSpan]::FromSeconds(3)) `
        -Error "Test error message"

    Add-TestResult -Report $report `
        -Name "TestC" `
        -Tier "Tier2" `
        -Status "Skipped" `
        -SkipReason "Missing prerequisite"

    Complete-TestReport -Report $report

    $reportWorks = (
        $report.summary.passed -eq 1 -and
        $report.summary.failed -eq 1 -and
        $report.summary.skipped -eq 1 -and
        $report.summary.total -eq 3 -and
        $null -ne $report.summary.duration
    )
} catch {
    Write-Result "Report generation" $false $_.Exception.Message
}
if ($reportWorks) {
    Write-Result "Report generation works" $true
}

# Test 7: JSON export
$jsonExportWorks = $false
$tempJsonPath = Join-Path ([System.IO.Path]::GetTempPath()) "test-runner-validation.json"
try {
    Export-TestReportJson -Report $report -OutputPath $tempJsonPath
    $jsonExportWorks = (Test-Path $tempJsonPath)

    if ($jsonExportWorks) {
        $jsonContent = Get-Content $tempJsonPath -Raw | ConvertFrom-Json
        $jsonExportWorks = ($null -ne $jsonContent.summary -and $jsonContent.summary.total -eq 3)
    }
} catch {
    # Ignore
} finally {
    if (Test-Path $tempJsonPath) {
        Remove-Item $tempJsonPath -Force -ErrorAction SilentlyContinue
    }
}
Write-Result "JSON export works" $jsonExportWorks

# Test 8: HTML export
$htmlExportWorks = $false
$tempHtmlPath = Join-Path ([System.IO.Path]::GetTempPath()) "test-runner-validation.html"
try {
    Export-TestReportHtml -Report $report -OutputPath $tempHtmlPath
    $htmlExportWorks = (Test-Path $tempHtmlPath)

    if ($htmlExportWorks) {
        $htmlContent = Get-Content $tempHtmlPath -Raw
        # Check for key HTML elements
        $htmlExportWorks = (
            $htmlContent -match "<html" -and
            $htmlContent -match "TestA" -and
            $htmlContent -match "PASS" -and
            $htmlContent -match "FAIL"
        )
    }
} catch {
    # Ignore
} finally {
    if (Test-Path $tempHtmlPath) {
        Remove-Item $tempHtmlPath -Force -ErrorAction SilentlyContinue
    }
}
Write-Result "HTML export works" $htmlExportWorks

# Test 9: Result parsing
$parsingWorks = $false
try {
    $testOutput = @"
[PASS] Test step 1
[PASS] Test step 2
[FAIL] Test step 3
[SKIP] Test step 4
Enforcement latency: 45ms
Created: 10
Removed: 5
"@
    $parsed = Get-TestResultFromOutput -Output $testOutput

    $parsingWorks = (
        $parsed.Passed -ge 2 -and
        $parsed.Failed -ge 1 -and
        $parsed.Metrics["enforcementLatency"] -eq "45ms" -and
        $parsed.Metrics["filtersCreated"] -eq 10 -and
        $parsed.Metrics["filtersRemoved"] -eq 5
    )
} catch {
    # Ignore
}
Write-Result "Output parsing works" $parsingWorks

# Test 10: Service check doesn't throw
$serviceCheckWorks = $false
try {
    $result = Test-ServiceRunning
    # Result can be true or false, just shouldn't throw
    $serviceCheckWorks = ($result -is [bool])
} catch {
    # Ignore
}
Write-Result "Service check works" $serviceCheckWorks

# Summary
Write-Host ""
Write-Host "=============================================="
Write-Host "  Summary"
Write-Host "=============================================="
Write-Host ""
Write-Host "  Passed: $passed" -ForegroundColor $(if ($passed -gt 0) { "Green" } else { "Gray" })
Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
Write-Host ""

if ($failed -gt 0) {
    Write-Host "  Result: FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "  Result: ALL TESTS PASSED" -ForegroundColor Green
    exit 0
}
