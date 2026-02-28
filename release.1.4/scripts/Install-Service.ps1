#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the WfpTrafficControl Windows service.

.DESCRIPTION
    Builds the service in Release mode, publishes it to a local directory,
    and registers it as a Windows service using sc.exe.

.PARAMETER InstallPath
    The directory where the service will be installed.
    Default: C:\Program Files\WfpTrafficControl

.EXAMPLE
    .\Install-Service.ps1

.EXAMPLE
    .\Install-Service.ps1 -InstallPath "D:\Services\WfpTrafficControl"
#>

param(
    [string]$InstallPath = "C:\Program Files\WfpTrafficControl"
)

$ErrorActionPreference = "Stop"

$ServiceName = "WfpTrafficControl"
$ServiceDisplayName = "WFP Traffic Control Service"
$ServiceDescription = "Windows Filtering Platform traffic control service for policy-based network filtering"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ServiceProject = Join-Path $RepoRoot "src\service\Service.csproj"
$ExeName = "WfpTrafficControl.Service.exe"

Write-Host "=== WfpTrafficControl Service Installer ===" -ForegroundColor Cyan
Write-Host ""

# Check if service already exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service '$ServiceName' already exists." -ForegroundColor Yellow
    Write-Host "Please run Uninstall-Service.ps1 first to remove it." -ForegroundColor Yellow
    exit 1
}

# Build and publish the service
Write-Host "[1/4] Building service in Release mode..." -ForegroundColor Green
dotnet publish $ServiceProject -c Release -r win-x64 --self-contained false -o "$InstallPath" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "       Published to: $InstallPath" -ForegroundColor Gray

# Verify the executable exists
$ExePath = Join-Path $InstallPath $ExeName
if (-not (Test-Path $ExePath)) {
    Write-Host "Executable not found at: $ExePath" -ForegroundColor Red
    exit 1
}

# Create the Windows service
Write-Host "[2/4] Creating Windows service..." -ForegroundColor Green
$binPath = "`"$ExePath`""
sc.exe create $ServiceName binPath= $binPath start= demand displayname= "$ServiceDisplayName" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to create service!" -ForegroundColor Red
    exit 1
}

# Set service description
Write-Host "[3/4] Setting service description..." -ForegroundColor Green
sc.exe description $ServiceName "$ServiceDescription" | Out-Null

# Configure service recovery options (restart on failure)
Write-Host "[4/4] Configuring service recovery..." -ForegroundColor Green
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Service Name:     $ServiceName" -ForegroundColor White
Write-Host "Display Name:     $ServiceDisplayName" -ForegroundColor White
Write-Host "Install Path:     $InstallPath" -ForegroundColor White
Write-Host "Executable:       $ExePath" -ForegroundColor White
Write-Host ""
Write-Host "To start the service, run:" -ForegroundColor Yellow
Write-Host "  .\Start-Service.ps1" -ForegroundColor Gray
Write-Host "  -- or --" -ForegroundColor Gray
Write-Host "  sc.exe start $ServiceName" -ForegroundColor Gray
Write-Host ""
