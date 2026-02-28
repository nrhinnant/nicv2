#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Stops the WfpTrafficControl Windows service.

.DESCRIPTION
    Stops the service and waits for it to enter the Stopped state.

.EXAMPLE
    .\Stop-Service.ps1
#>

$ErrorActionPreference = "Stop"

$ServiceName = "WfpTrafficControl"

Write-Host "=== Stopping WfpTrafficControl Service ===" -ForegroundColor Cyan
Write-Host ""

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Red
    exit 1
}

# Check if already stopped
if ($existingService.Status -eq 'Stopped') {
    Write-Host "Service is already stopped." -ForegroundColor Yellow
    exit 0
}

# Stop the service
Write-Host "Stopping service..." -ForegroundColor Green
sc.exe stop $ServiceName | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to stop service!" -ForegroundColor Red
    exit 1
}

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
if ($svc.Status -eq 'Stopped') {
    Write-Host ""
    Write-Host "Service stopped successfully." -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Service did not stop within $timeout seconds." -ForegroundColor Red
    Write-Host "Current status: $($svc.Status)" -ForegroundColor Yellow
    exit 1
}
