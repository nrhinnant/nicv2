#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs all static analysis checks for the WFP Traffic Control project.

.DESCRIPTION
    This script performs code quality verification:
    1. Code format verification (dotnet format)
    2. Build with analyzers (warnings as errors in Release)
    3. NuGet vulnerability scan
    4. Unit tests

.PARAMETER Fix
    Auto-fix formatting issues where possible.

.PARAMETER SkipTests
    Skip running unit tests.

.PARAMETER Configuration
    Build configuration. Default: Release.

.EXAMPLE
    .\Verify-CodeQuality.ps1
    Runs full verification.

.EXAMPLE
    .\Verify-CodeQuality.ps1 -Fix
    Auto-fixes formatting, then runs verification.

.EXAMPLE
    .\Verify-CodeQuality.ps1 -SkipTests
    Runs verification without tests.
#>

param(
    [switch]$Fix,
    [switch]$SkipTests,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root "WfpTrafficControl.sln"

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Code Quality Verification" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$stepCount = if ($SkipTests) { 3 } else { 4 }
$currentStep = 0

# Step 1: Format check
$currentStep++
Write-Host "[$currentStep/$stepCount] Checking code format..." -ForegroundColor Yellow

if ($Fix) {
    dotnet format $solution
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    FAILED: Format auto-fix encountered errors." -ForegroundColor Red
        exit 1
    }
    Write-Host "    Format issues auto-fixed." -ForegroundColor Green
} else {
    dotnet format $solution --verify-no-changes 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    FAILED: Code format issues detected." -ForegroundColor Red
        Write-Host "    Run with -Fix to auto-fix, or run: dotnet format" -ForegroundColor Yellow
        exit 1
    }
}
Write-Host "    PASSED" -ForegroundColor Green

# Step 2: Build with analyzers
$currentStep++
Write-Host ""
Write-Host "[$currentStep/$stepCount] Building with analyzers ($Configuration)..." -ForegroundColor Yellow

# In Release mode, TreatWarningsAsErrors is enabled via Directory.Build.props
dotnet build $solution -c $Configuration --no-incremental 2>&1 | Tee-Object -Variable buildOutput
if ($LASTEXITCODE -ne 0) {
    Write-Host "    FAILED: Build failed or analyzer errors found." -ForegroundColor Red
    Write-Host "    Review the warnings above and fix them." -ForegroundColor Yellow
    exit 1
}

# Count warnings even if build succeeded
$warningCount = ($buildOutput | Select-String -Pattern ": warning " | Measure-Object).Count
if ($warningCount -gt 0) {
    Write-Host "    PASSED with $warningCount warning(s)" -ForegroundColor Yellow
} else {
    Write-Host "    PASSED" -ForegroundColor Green
}

# Step 3: Vulnerable packages
$currentStep++
Write-Host ""
Write-Host "[$currentStep/$stepCount] Checking for vulnerable packages..." -ForegroundColor Yellow

$vulnOutput = dotnet list $solution package --vulnerable --include-transitive 2>&1
$hasVulnerabilities = $vulnOutput -match "has the following vulnerable packages"

if ($hasVulnerabilities) {
    Write-Host "    WARNING: Vulnerable packages detected!" -ForegroundColor Yellow
    Write-Host $vulnOutput
} else {
    Write-Host "    PASSED - No known vulnerabilities" -ForegroundColor Green
}

# Step 4: Tests
if (-not $SkipTests) {
    $currentStep++
    Write-Host ""
    Write-Host "[$currentStep/$stepCount] Running tests..." -ForegroundColor Yellow

    dotnet test $solution -c $Configuration --no-build --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    FAILED: Tests failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "    PASSED" -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  All Checks Passed" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

exit 0
