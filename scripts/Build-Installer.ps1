#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the WfpTrafficControl MSI installer.

.DESCRIPTION
    Publishes the Service and CLI projects, then builds the WiX installer.
    Outputs the MSI to installer/WfpTrafficControl.Installer/bin/Release/

.PARAMETER Version
    The version number for the installer (e.g., 1.0.0).
    Default: 1.0.0

.PARAMETER Configuration
    Build configuration. Default: Release

.EXAMPLE
    .\Build-Installer.ps1

.EXAMPLE
    .\Build-Installer.ps1 -Version 1.2.0
#>

param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ServiceProject = Join-Path $RepoRoot "src\service\Service.csproj"
$CliProject = Join-Path $RepoRoot "src\cli\Cli.csproj"
$InstallerProject = Join-Path $RepoRoot "installer\WfpTrafficControl.Installer\WfpTrafficControl.Installer.wixproj"

Write-Host "=== WfpTrafficControl MSI Builder ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor White
Write-Host ""

# Step 1: Publish the Service
Write-Host "[1/3] Publishing Service..." -ForegroundColor Green
$ServicePublishDir = Join-Path $RepoRoot "src\service\bin\$Configuration\net8.0-windows\win-x64\publish"
dotnet publish $ServiceProject -c $Configuration -r win-x64 --self-contained false -o $ServicePublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to publish Service!" -ForegroundColor Red
    exit 1
}
Write-Host "       Published to: $ServicePublishDir" -ForegroundColor Gray

# Step 2: Publish the CLI
Write-Host "[2/3] Publishing CLI..." -ForegroundColor Green
$CliPublishDir = Join-Path $RepoRoot "src\cli\bin\$Configuration\net8.0\publish"
dotnet publish $CliProject -c $Configuration -o $CliPublishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to publish CLI!" -ForegroundColor Red
    exit 1
}
Write-Host "       Published to: $CliPublishDir" -ForegroundColor Gray

# Step 3: Build the MSI installer
Write-Host "[3/3] Building MSI installer..." -ForegroundColor Green
dotnet build $InstallerProject -c $Configuration -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build installer!" -ForegroundColor Red
    exit 1
}

# Find the output MSI
$OutputDir = Join-Path $RepoRoot "installer\WfpTrafficControl.Installer\bin\$Configuration"
$MsiFile = Get-ChildItem -Path $OutputDir -Filter "*.msi" | Select-Object -First 1

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
Write-Host ""
if ($MsiFile) {
    Write-Host "MSI Output: $($MsiFile.FullName)" -ForegroundColor White
    Write-Host "Size:       $([math]::Round($MsiFile.Length / 1MB, 2)) MB" -ForegroundColor Gray
} else {
    Write-Host "MSI Output: $OutputDir\WfpTrafficControl-$Version.msi" -ForegroundColor White
}
Write-Host ""
Write-Host "To install, run (as Administrator):" -ForegroundColor Yellow
Write-Host "  msiexec /i `"$OutputDir\WfpTrafficControl-$Version.msi`"" -ForegroundColor Gray
Write-Host ""
