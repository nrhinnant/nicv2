#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Starts the WfpTrafficControl Windows service.

.DESCRIPTION
    Starts the service and waits for it to enter the Running state.

.EXAMPLE
    .\Start-Service.ps1
#>

$ErrorActionPreference = "Stop"

$ServiceName = "WfpTrafficControl"

Write-Host "=== Starting WfpTrafficControl Service ===" -ForegroundColor Cyan
Write-Host ""

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $existingService) {
    Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Red
    Write-Host "Run Install-Service.ps1 first." -ForegroundColor Yellow
    exit 1
}

# Check if already running
if ($existingService.Status -eq 'Running') {
    Write-Host "Service is already running." -ForegroundColor Yellow
    exit 0
}

# Start the service
Write-Host "Starting service..." -ForegroundColor Green
sc.exe start $ServiceName | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start service!" -ForegroundColor Red
    Write-Host "Check Windows Event Viewer for details." -ForegroundColor Yellow
    exit 1
}

# Wait for service to start (max 30 seconds)
$timeout = 30
$elapsed = 0
while ($elapsed -lt $timeout) {
    Start-Sleep -Seconds 1
    $elapsed++
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc.Status -eq 'Running') {
        break
    }
    if ($svc.Status -eq 'Stopped') {
        Write-Host "Service stopped unexpectedly!" -ForegroundColor Red
        Write-Host "Check Windows Event Viewer for details." -ForegroundColor Yellow
        exit 1
    }
}

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc.Status -eq 'Running') {
    Write-Host ""
    Write-Host "Service is now running." -ForegroundColor Green
    Write-Host ""
    Write-Host "Service Name:   $ServiceName" -ForegroundColor White
    Write-Host "Status:         $($svc.Status)" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "Service did not start within $timeout seconds." -ForegroundColor Red
    Write-Host "Current status: $($svc.Status)" -ForegroundColor Yellow
    exit 1
}
