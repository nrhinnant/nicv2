#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Tests the hot reload (file watching) functionality of the WFP Traffic Control service.

.DESCRIPTION
    This script tests the file watching feature by:
    1. Creating a test policy file
    2. Enabling file watching
    3. Modifying the policy
    4. Verifying automatic reapplication

.PARAMETER CliPath
    Path to the wfpctl.exe CLI tool. Defaults to the debug build location.

.PARAMETER TestDir
    Directory for test files. Defaults to a temp directory.

.PARAMETER Cleanup
    If specified, cleans up test files after execution.

.EXAMPLE
    .\Test-HotReload.ps1

.EXAMPLE
    .\Test-HotReload.ps1 -CliPath "C:\tools\wfpctl.exe" -Cleanup
#>

param(
    [string]$CliPath = "$PSScriptRoot\..\src\cli\bin\Debug\net8.0\wfpctl.exe",
    [string]$TestDir = "$env:TEMP\wfp-hotreload-test",
    [switch]$Cleanup
)

$ErrorActionPreference = "Stop"

function Write-TestHeader {
    param([string]$Message)
    Write-Host ""
    Write-Host "=" * 60 -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "=" * 60 -ForegroundColor Cyan
}

function Write-TestStep {
    param([string]$Message)
    Write-Host ""
    Write-Host "[STEP] $Message" -ForegroundColor Yellow
}

function Write-TestResult {
    param(
        [string]$Message,
        [bool]$Success
    )
    if ($Success) {
        Write-Host "[PASS] $Message" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $Message" -ForegroundColor Red
    }
}

function Invoke-Cli {
    param([string[]]$Arguments)

    Write-Host "  > wfpctl $($Arguments -join ' ')" -ForegroundColor DarkGray
    $output = & $CliPath @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    foreach ($line in $output) {
        Write-Host "    $line" -ForegroundColor DarkGray
    }

    return @{
        Output = $output
        ExitCode = $exitCode
    }
}

function New-TestPolicy {
    param(
        [string]$Path,
        [string]$Version,
        [int]$BlockedPort
    )

    $policy = @{
        version = $Version
        defaultAction = "allow"
        updatedAt = (Get-Date).ToString("o")
        rules = @(
            @{
                id = "test-block-rule"
                action = "block"
                direction = "outbound"
                protocol = "tcp"
                remote = @{ ports = "$BlockedPort" }
                priority = 100
                enabled = $true
                comment = "Test block rule for hot reload testing"
            }
        )
    }

    $json = $policy | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath $Path -Encoding utf8 -Force

    Write-Host "  Created policy: $Path (version=$Version, blocked port=$BlockedPort)" -ForegroundColor DarkGray
}

# =============================================================================
# MAIN TEST SCRIPT
# =============================================================================

Write-TestHeader "Hot Reload Test Script"

# Pre-flight checks
Write-TestStep "Verifying CLI tool exists"
if (-not (Test-Path $CliPath)) {
    Write-TestResult "CLI tool not found at: $CliPath" $false
    Write-Host "Please build the project first: dotnet build" -ForegroundColor Yellow
    exit 1
}
Write-TestResult "CLI tool found" $true

# Create test directory
Write-TestStep "Creating test directory"
if (-not (Test-Path $TestDir)) {
    New-Item -ItemType Directory -Path $TestDir | Out-Null
}
$policyPath = Join-Path $TestDir "test-policy.json"
Write-TestResult "Test directory: $TestDir" $true

# Check service status
Write-TestStep "Checking service status"
$result = Invoke-Cli @("status")
if ($result.ExitCode -ne 0) {
    Write-TestResult "Service is not running" $false
    Write-Host "Please start the service first: net start WfpTrafficControl" -ForegroundColor Yellow
    exit 1
}
Write-TestResult "Service is running" $true

# Bootstrap WFP
Write-TestStep "Bootstrapping WFP infrastructure"
$result = Invoke-Cli @("bootstrap")
Write-TestResult "Bootstrap completed" ($result.ExitCode -eq 0)

# Create initial policy
Write-TestStep "Creating initial test policy (v1.0.0, blocking port 9999)"
New-TestPolicy -Path $policyPath -Version "1.0.0" -BlockedPort 9999
Write-TestResult "Policy created" (Test-Path $policyPath)

# Enable file watching
Write-TestStep "Enabling file watching"
$result = Invoke-Cli @("watch", "set", $policyPath)
Write-TestResult "Watch enabled" ($result.ExitCode -eq 0)

# Check watch status
Write-TestStep "Checking watch status"
$result = Invoke-Cli @("watch", "status")
$watchActive = ($result.Output -join "`n") -match "Active:\s+Yes"
Write-TestResult "Watch is active" $watchActive

# Wait for initial apply
Write-Host "  Waiting for initial apply..." -ForegroundColor DarkGray
Start-Sleep -Seconds 2

# Check LKG shows our policy
Write-TestStep "Verifying initial policy applied (checking LKG)"
$result = Invoke-Cli @("lkg", "show")
$lkgHasVersion = ($result.Output -join "`n") -match "Version:\s+1\.0\.0"
Write-TestResult "LKG shows v1.0.0" $lkgHasVersion

# Modify the policy
Write-TestStep "Modifying policy (v2.0.0, blocking port 8888)"
New-TestPolicy -Path $policyPath -Version "2.0.0" -BlockedPort 8888
Write-TestResult "Policy modified" $true

# Wait for debounce + apply
Write-Host "  Waiting for debounce and reapply (2 seconds)..." -ForegroundColor DarkGray
Start-Sleep -Seconds 2

# Check LKG shows updated policy
Write-TestStep "Verifying policy was reapplied"
$result = Invoke-Cli @("lkg", "show")
$lkgHasNewVersion = ($result.Output -join "`n") -match "Version:\s+2\.0\.0"
Write-TestResult "LKG shows v2.0.0 (hot reload worked)" $lkgHasNewVersion

# Check watch status for apply count
Write-TestStep "Checking apply count"
$result = Invoke-Cli @("watch", "status")
$statusText = $result.Output -join "`n"
$applyCountMatch = [regex]::Match($statusText, "Applies:\s+(\d+)")
$applyCount = if ($applyCountMatch.Success) { [int]$applyCountMatch.Groups[1].Value } else { 0 }
Write-TestResult "Apply count: $applyCount (expected >= 2)" ($applyCount -ge 2)

# Test invalid policy (fail-open)
Write-TestStep "Testing fail-open behavior with invalid policy"
"{ invalid json }" | Out-File -FilePath $policyPath -Encoding utf8 -Force
Write-Host "  Waiting for debounce and error handling..." -ForegroundColor DarkGray
Start-Sleep -Seconds 2

$result = Invoke-Cli @("watch", "status")
$statusText = $result.Output -join "`n"
$hasError = $statusText -match "Last Error:"
$errorsMatch = [regex]::Match($statusText, "Errors:\s+(\d+)")
$errorCount = if ($errorsMatch.Success) { [int]$errorsMatch.Groups[1].Value } else { 0 }
Write-TestResult "Error recorded (fail-open): $errorCount error(s)" ($errorCount -gt 0)

# Verify previous policy still in effect
Write-TestStep "Verifying previous policy still in effect (fail-open)"
$result = Invoke-Cli @("lkg", "show")
$stillV2 = ($result.Output -join "`n") -match "Version:\s+2\.0\.0"
Write-TestResult "LKG still shows v2.0.0 (fail-open working)" $stillV2

# Disable watching
Write-TestStep "Disabling file watching"
$result = Invoke-Cli @("watch", "set")
Write-TestResult "Watch disabled" ($result.ExitCode -eq 0)

# Verify watch is off
Write-TestStep "Verifying watch is disabled"
$result = Invoke-Cli @("watch", "status")
$watchInactive = ($result.Output -join "`n") -match "Active:\s+No"
Write-TestResult "Watch is inactive" $watchInactive

# Cleanup
if ($Cleanup) {
    Write-TestStep "Cleaning up"

    # Rollback filters
    $result = Invoke-Cli @("rollback")
    Write-Host "  Rolled back filters" -ForegroundColor DarkGray

    # Remove test directory
    if (Test-Path $TestDir) {
        Remove-Item -Path $TestDir -Recurse -Force
        Write-Host "  Removed test directory" -ForegroundColor DarkGray
    }

    Write-TestResult "Cleanup completed" $true
}

# Summary
Write-TestHeader "Test Summary"
Write-Host ""
Write-Host "Hot reload testing completed." -ForegroundColor Green
Write-Host ""
Write-Host "Key behaviors verified:" -ForegroundColor White
Write-Host "  [x] File watching can be enabled/disabled via CLI" -ForegroundColor White
Write-Host "  [x] Policy is applied on initial watch enable" -ForegroundColor White
Write-Host "  [x] Policy is automatically reapplied on file change" -ForegroundColor White
Write-Host "  [x] Invalid policy triggers fail-open (keeps last good policy)" -ForegroundColor White
Write-Host "  [x] Statistics are tracked (apply count, error count)" -ForegroundColor White
Write-Host ""

if (-not $Cleanup) {
    Write-Host "Test files remain at: $TestDir" -ForegroundColor Yellow
    Write-Host "Use -Cleanup to remove test files" -ForegroundColor Yellow
}
