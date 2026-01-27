#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls the WfpTrafficControl Windows service.

.DESCRIPTION
    Stops the service if running, removes the service registration,
    and optionally deletes the installation directory.

.PARAMETER RemoveFiles
    If specified, also removes the installation directory.
    Default: $false (keeps files)

.PARAMETER InstallPath
    The directory where the service is installed.
    Default: C:\Program Files\WfpTrafficControl

.EXAMPLE
    .\Uninstall-Service.ps1

.EXAMPLE
    .\Uninstall-Service.ps1 -RemoveFiles
#>

param(
    [switch]$RemoveFiles,
    [string]$InstallPath = "C:\Program Files\WfpTrafficControl"
)

$ErrorActionPreference = "Stop"

$ServiceName = "WfpTrafficControl"

Write-Host "=== WfpTrafficControl Service Uninstaller ===" -ForegroundColor Cyan
Write-Host ""

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Yellow
    if ($RemoveFiles -and (Test-Path $InstallPath)) {
        Write-Host "Removing installation directory..." -ForegroundColor Green
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "Directory removed: $InstallPath" -ForegroundColor Gray
    }
    exit 0
}

# Stop service if running
Write-Host "[1/3] Checking service status..." -ForegroundColor Green
if ($existingService.Status -eq 'Running') {
    Write-Host "       Stopping service..." -ForegroundColor Gray
    sc.exe stop $ServiceName | Out-Null

    # Wait for service to stop (max 30 seconds)
    $timeout = 30
    $elapsed = 0
    while ($elapsed -lt $timeout) {
        Start-Sleep -Seconds 1
        $elapsed++
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc.Status -eq 'Stopped') {
            break
        }
    }

    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc.Status -ne 'Stopped') {
        Write-Host "Warning: Service did not stop within $timeout seconds." -ForegroundColor Yellow
        Write-Host "Attempting to force removal..." -ForegroundColor Yellow
    } else {
        Write-Host "       Service stopped." -ForegroundColor Gray
    }
} else {
    Write-Host "       Service is not running." -ForegroundColor Gray
}

# Delete the service
Write-Host "[2/3] Removing service registration..." -ForegroundColor Green
sc.exe delete $ServiceName | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to delete service!" -ForegroundColor Red
    Write-Host "You may need to reboot and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host "       Service registration removed." -ForegroundColor Gray

# Optionally remove files
if ($RemoveFiles) {
    Write-Host "[3/3] Removing installation directory..." -ForegroundColor Green
    if (Test-Path $InstallPath) {
        Remove-Item -Path $InstallPath -Recurse -Force
        Write-Host "       Directory removed: $InstallPath" -ForegroundColor Gray
    } else {
        Write-Host "       Directory not found (already removed)." -ForegroundColor Gray
    }
} else {
    Write-Host "[3/3] Keeping installation files." -ForegroundColor Green
    Write-Host "       To remove files, run: .\Uninstall-Service.ps1 -RemoveFiles" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Uninstallation Complete ===" -ForegroundColor Cyan
Write-Host ""
