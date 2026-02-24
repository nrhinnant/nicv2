#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Tests the WfpTrafficControl MSI installer.

.DESCRIPTION
    Runs a series of tests to validate MSI installation, service registration,
    PATH modification, and uninstallation. Should be run in a VM with snapshots.

.PARAMETER MsiPath
    Path to the MSI file to test.

.PARAMETER SkipUninstall
    If set, skips the uninstall test at the end (leaves the product installed).

.EXAMPLE
    .\Test-Installer.ps1 -MsiPath ".\installer\WfpTrafficControl.Installer\bin\Release\WfpTrafficControl-1.0.0.msi"

.NOTES
    IMPORTANT: Run this in a VM with a snapshot. The test will install and uninstall the product.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,

    [switch]$SkipUninstall
)

$ErrorActionPreference = "Stop"
$script:TestsPassed = 0
$script:TestsFailed = 0

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Details = ""
    )

    if ($Passed) {
        Write-Host "[PASS] $TestName" -ForegroundColor Green
        $script:TestsPassed++
    } else {
        Write-Host "[FAIL] $TestName" -ForegroundColor Red
        if ($Details) {
            Write-Host "       $Details" -ForegroundColor Yellow
        }
        $script:TestsFailed++
    }
}

function Test-ServiceExists {
    $service = Get-Service -Name "WfpTrafficControl" -ErrorAction SilentlyContinue
    return $null -ne $service
}

function Test-ServiceConfiguration {
    $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='WfpTrafficControl'" -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        return @{ Exists = $false }
    }

    return @{
        Exists = $true
        StartMode = $service.StartMode
        State = $service.State
        PathName = $service.PathName
    }
}

function Test-FileExists {
    param([string]$Path)
    return Test-Path $Path
}

function Test-PathContains {
    param([string]$Directory)
    $envPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
    return $envPath -like "*$Directory*"
}

# ============================================================================
# Main Test Execution
# ============================================================================

Write-Host ""
Write-Host "=== WfpTrafficControl MSI Installer Test Suite ===" -ForegroundColor Cyan
Write-Host "MSI: $MsiPath" -ForegroundColor White
Write-Host ""

# Validate MSI exists
if (-not (Test-Path $MsiPath)) {
    Write-Host "ERROR: MSI file not found: $MsiPath" -ForegroundColor Red
    exit 1
}

$MsiFullPath = (Resolve-Path $MsiPath).Path
$InstallDir = "C:\Program Files\WfpTrafficControl"

# ============================================================================
# Pre-Installation Checks
# ============================================================================

Write-Host "--- Pre-Installation Checks ---" -ForegroundColor Yellow

$preServiceExists = Test-ServiceExists
Write-TestResult "Service does not exist before install" (-not $preServiceExists) `
    "Service already exists. Uninstall first or use a clean VM."

if ($preServiceExists) {
    Write-Host "Aborting: Clean environment required." -ForegroundColor Red
    exit 1
}

# ============================================================================
# Installation Test
# ============================================================================

Write-Host ""
Write-Host "--- Installation Test ---" -ForegroundColor Yellow

Write-Host "Installing MSI (silent)..." -ForegroundColor Gray
$installLog = Join-Path $env:TEMP "wfp-install.log"
$installProcess = Start-Process -FilePath "msiexec.exe" `
    -ArgumentList "/i `"$MsiFullPath`" /qn /l*v `"$installLog`"" `
    -Wait -PassThru

Write-TestResult "MSI installation succeeded (exit code 0)" ($installProcess.ExitCode -eq 0) `
    "Exit code: $($installProcess.ExitCode). Check log: $installLog"

if ($installProcess.ExitCode -ne 0) {
    Write-Host "Installation failed. Aborting remaining tests." -ForegroundColor Red
    Write-Host "Log file: $installLog" -ForegroundColor Yellow
    exit 1
}

# ============================================================================
# Post-Installation Verification
# ============================================================================

Write-Host ""
Write-Host "--- Post-Installation Verification ---" -ForegroundColor Yellow

# Check service exists
$serviceConfig = Test-ServiceConfiguration
Write-TestResult "Service 'WfpTrafficControl' exists" $serviceConfig.Exists

if ($serviceConfig.Exists) {
    Write-TestResult "Service start mode is 'Manual'" ($serviceConfig.StartMode -eq "Manual") `
        "Actual: $($serviceConfig.StartMode)"

    Write-TestResult "Service account is LocalSystem" ($serviceConfig.PathName -notlike "*-u *") `
        "PathName: $($serviceConfig.PathName)"
}

# Check files exist
Write-TestResult "Service executable exists" (Test-FileExists "$InstallDir\WfpTrafficControl.Service.exe")
Write-TestResult "CLI executable exists" (Test-FileExists "$InstallDir\wfpctl.exe")
Write-TestResult "Config file exists" (Test-FileExists "$InstallDir\appsettings.json")
Write-TestResult "Sample policy exists" (Test-FileExists "$InstallDir\sample-policy.json")

# Check PATH
$pathUpdated = Test-PathContains $InstallDir
Write-TestResult "Installation directory added to PATH" $pathUpdated

# ============================================================================
# Service Start Test
# ============================================================================

Write-Host ""
Write-Host "--- Service Start Test ---" -ForegroundColor Yellow

Write-Host "Starting service..." -ForegroundColor Gray
try {
    Start-Service -Name "WfpTrafficControl" -ErrorAction Stop
    Start-Sleep -Seconds 2

    $service = Get-Service -Name "WfpTrafficControl"
    Write-TestResult "Service started successfully" ($service.Status -eq "Running") `
        "Status: $($service.Status)"
} catch {
    Write-TestResult "Service started successfully" $false `
        "Error: $($_.Exception.Message)"
}

# ============================================================================
# CLI Test
# ============================================================================

Write-Host ""
Write-Host "--- CLI Test ---" -ForegroundColor Yellow

# Test CLI from install directory
Write-Host "Testing wfpctl from install directory..." -ForegroundColor Gray
try {
    $cliOutput = & "$InstallDir\wfpctl.exe" status 2>&1
    $cliSuccess = $LASTEXITCODE -eq 0
    Write-TestResult "wfpctl status command succeeds" $cliSuccess `
        "Output: $cliOutput"
} catch {
    Write-TestResult "wfpctl status command succeeds" $false `
        "Error: $($_.Exception.Message)"
}

# Test CLI from PATH (new process to pick up PATH changes)
Write-Host "Testing wfpctl from PATH (new process)..." -ForegroundColor Gray
$pathTestScript = @"
`$env:PATH = [Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH', 'User')
wfpctl status
exit `$LASTEXITCODE
"@
$pathTestResult = powershell -Command $pathTestScript 2>&1
$pathTestSuccess = $LASTEXITCODE -eq 0
Write-TestResult "wfpctl accessible from PATH" $pathTestSuccess

# ============================================================================
# Service Stop Test
# ============================================================================

Write-Host ""
Write-Host "--- Service Stop Test ---" -ForegroundColor Yellow

Write-Host "Stopping service..." -ForegroundColor Gray
try {
    Stop-Service -Name "WfpTrafficControl" -Force -ErrorAction Stop
    Start-Sleep -Seconds 2

    $service = Get-Service -Name "WfpTrafficControl"
    Write-TestResult "Service stopped successfully" ($service.Status -eq "Stopped") `
        "Status: $($service.Status)"
} catch {
    Write-TestResult "Service stopped successfully" $false `
        "Error: $($_.Exception.Message)"
}

# ============================================================================
# Uninstall Test
# ============================================================================

if (-not $SkipUninstall) {
    Write-Host ""
    Write-Host "--- Uninstall Test ---" -ForegroundColor Yellow

    Write-Host "Uninstalling MSI (silent)..." -ForegroundColor Gray
    $uninstallLog = Join-Path $env:TEMP "wfp-uninstall.log"
    $uninstallProcess = Start-Process -FilePath "msiexec.exe" `
        -ArgumentList "/x `"$MsiFullPath`" /qn /l*v `"$uninstallLog`"" `
        -Wait -PassThru

    Write-TestResult "MSI uninstallation succeeded (exit code 0)" ($uninstallProcess.ExitCode -eq 0) `
        "Exit code: $($uninstallProcess.ExitCode). Check log: $uninstallLog"

    Start-Sleep -Seconds 2

    # Verify service removed
    $postServiceExists = Test-ServiceExists
    Write-TestResult "Service removed after uninstall" (-not $postServiceExists)

    # Verify files removed
    Write-TestResult "Service executable removed" (-not (Test-FileExists "$InstallDir\WfpTrafficControl.Service.exe"))
    Write-TestResult "CLI executable removed" (-not (Test-FileExists "$InstallDir\wfpctl.exe"))

    # Verify PATH cleaned
    $pathStillContains = Test-PathContains $InstallDir
    Write-TestResult "Installation directory removed from PATH" (-not $pathStillContains)
} else {
    Write-Host ""
    Write-Host "--- Skipping Uninstall Test (--SkipUninstall) ---" -ForegroundColor Yellow
}

# ============================================================================
# Summary
# ============================================================================

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host "Passed: $script:TestsPassed" -ForegroundColor Green
Write-Host "Failed: $script:TestsFailed" -ForegroundColor $(if ($script:TestsFailed -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($script:TestsFailed -gt 0) {
    Write-Host "Some tests failed. Review the output above." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "All tests passed!" -ForegroundColor Green
    exit 0
}
