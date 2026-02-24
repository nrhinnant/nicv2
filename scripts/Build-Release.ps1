#Requires -Version 5.1
<#
.SYNOPSIS
    Builds a complete release package for WfpTrafficControl.

.DESCRIPTION
    Produces a release package in the \release\ folder ready to zip and distribute.
    Archives existing releases to versioned folders before building new ones.

    This script is SAFE and IDEMPOTENT - it only reads from the project and writes
    to release folders. It does NOT modify source files, project files, or existing
    build outputs outside of the release directory.

.PARAMETER Version
    The version number for the release (e.g., 1.0.0).
    Default: 1.0.0

.PARAMETER SkipTests
    Skip test execution for faster iteration.

.PARAMETER SkipMsi
    Skip MSI build (useful if WiX is not installed).

.PARAMETER SkipArchive
    Skip archiving existing release to versioned folder.

.PARAMETER Configuration
    Build configuration (Release or Debug).
    Default: Release

.EXAMPLE
    .\Build-Release.ps1

.EXAMPLE
    .\Build-Release.ps1 -Version 1.2.0 -SkipTests

.EXAMPLE
    .\Build-Release.ps1 -SkipMsi -SkipArchive
#>

[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+(\.\d+)?$')]
    [string]$Version = "1.0.0",
    [switch]$SkipTests,
    [switch]$SkipMsi,
    [switch]$SkipArchive,
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# ============================================================================
# Configuration
# ============================================================================

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

# Project paths
$SolutionFile = Join-Path $RepoRoot "WfpTrafficControl.sln"
$ServiceProject = Join-Path $RepoRoot "src\service\Service.csproj"
$CliProject = Join-Path $RepoRoot "src\cli\Cli.csproj"
$UiProject = Join-Path $RepoRoot "src\ui\WfpTrafficControl.UI\WfpTrafficControl.UI.csproj"
$InstallerProject = Join-Path $RepoRoot "installer\WfpTrafficControl.Installer\WfpTrafficControl.Installer.wixproj"
$TestsProject = Join-Path $RepoRoot "tests\Tests.csproj"

# Output paths (where dotnet publish will place files)
$ServicePublishDir = Join-Path $RepoRoot "src\service\bin\$Configuration\net8.0-windows\win-x64\publish"
$CliPublishDir = Join-Path $RepoRoot "src\cli\bin\$Configuration\net8.0\publish"
$UiPublishDir = Join-Path $RepoRoot "src\ui\WfpTrafficControl.UI\bin\$Configuration\net8.0-windows\publish"
$MsiOutputDir = Join-Path $RepoRoot "installer\WfpTrafficControl.Installer\bin\$Configuration"

# Release paths
$ReleaseDir = Join-Path $RepoRoot "release"

# Source files to copy
$ScriptsToCopy = @(
    "Install-Service.ps1"
    "Uninstall-Service.ps1"
    "Start-Service.ps1"
    "Stop-Service.ps1"
)

$DocsToCopy = @(
    @{ Source = "docs\EXECUTIVE_SUMMARY.md"; Dest = "EXECUTIVE_SUMMARY.md" }
    @{ Source = "docs\features\022-how-it-works.md"; Dest = "022-how-it-works.md" }
    @{ Source = "docs\features\023-troubleshooting.md"; Dest = "023-troubleshooting.md" }
    @{ Source = "installer\WfpTrafficControl.Installer\License.rtf"; Dest = "License.rtf" }
)

# Tracking
$Warnings = [System.Collections.ArrayList]::new()
$StartTime = Get-Date

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Step {
    param([string]$Step, [string]$Message)
    Write-Host "[$Step] $Message" -ForegroundColor Green
}

function Write-SubStep {
    param([string]$Message)
    Write-Host "       $Message" -ForegroundColor Gray
}

function Write-Warning-Tracked {
    param([string]$Message)
    $null = $Warnings.Add($Message)
    Write-Host "WARNING: $Message" -ForegroundColor Yellow
}

function Write-Error-Exit {
    param([string]$Message, [int]$ExitCode = 1)
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit $ExitCode
}

function Get-MajorMinorVersion {
    param([string]$VersionString)
    $parts = $VersionString -split '\.'
    if ($parts.Count -ge 2) {
        return "$($parts[0]).$($parts[1])"
    }
    return $VersionString
}

function Get-DirectorySize {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return 0 }
    $size = (Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue |
             Measure-Object -Property Length -Sum).Sum
    return $size
}

function Format-Size {
    param([long]$Bytes)
    if ($null -eq $Bytes -or $Bytes -eq 0) { return "0 bytes" }
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
    return "$Bytes bytes"
}

function Test-CommandExists {
    param([string]$Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

function Invoke-ExternalCommand {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [string]$WorkingDirectory = $RepoRoot,
        [string]$ErrorMessage
    )

    $originalLocation = Get-Location
    try {
        Set-Location $WorkingDirectory
        & $Command @Arguments
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Exit "$ErrorMessage (exit code: $LASTEXITCODE)"
        }
    }
    finally {
        Set-Location $originalLocation
    }
}

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination,
        [switch]$ValidateNotEmpty
    )

    if (-not (Test-Path $Source)) {
        Write-Error-Exit "Source directory not found: $Source"
    }

    # Validate source has content if required
    if ($ValidateNotEmpty) {
        $fileCount = (Get-ChildItem -Path $Source -File -Force -ErrorAction SilentlyContinue).Count
        if ($fileCount -eq 0) {
            Write-Error-Exit "Source directory is empty: $Source"
        }
    }

    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force -ErrorAction Stop | Out-Null
    }

    Get-ChildItem -Path $Source -Recurse -Force | ForEach-Object {
        $targetPath = Join-Path $Destination $_.FullName.Substring($Source.Length + 1)
        if ($_.PSIsContainer) {
            if (-not (Test-Path $targetPath)) {
                New-Item -ItemType Directory -Path $targetPath -Force -ErrorAction Stop | Out-Null
            }
        } else {
            $targetDir = Split-Path -Parent $targetPath
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force -ErrorAction Stop | Out-Null
            }
            Copy-Item -Path $_.FullName -Destination $targetPath -Force -ErrorAction Stop
        }
    }
}

# ============================================================================
# Pre-flight Checks
# ============================================================================

Write-Host ""
Write-Host "=== WfpTrafficControl Release Builder ===" -ForegroundColor Cyan
Write-Host "Version:       $Version" -ForegroundColor White
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Repository:    $RepoRoot" -ForegroundColor White
Write-Host ""

# Check prerequisites
if (-not (Test-CommandExists "dotnet")) {
    Write-Error-Exit "dotnet CLI not found. Please install .NET SDK."
}

# Validate project files exist
$requiredFiles = @($SolutionFile, $ServiceProject, $CliProject)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Error-Exit "Required file not found: $file"
    }
}

# Check UI project (warn if missing, not fatal)
if (-not (Test-Path $UiProject)) {
    Write-Warning-Tracked "UI project not found at: $UiProject"
}

# Check installer project (warn if missing, not fatal unless MSI is required)
if (-not $SkipMsi -and -not (Test-Path $InstallerProject)) {
    Write-Warning-Tracked "Installer project not found, will skip MSI build: $InstallerProject"
    $SkipMsi = $true
}

# ============================================================================
# Step 1: Archive Existing Release
# ============================================================================

$ArchivedReleasePath = $null
$MajorMinorVersion = Get-MajorMinorVersion -VersionString $Version

if (-not $SkipArchive -and (Test-Path $ReleaseDir)) {
    $releaseContents = Get-ChildItem -Path $ReleaseDir -ErrorAction SilentlyContinue
    if ($releaseContents.Count -gt 0) {
        Write-Step "1/7" "Archiving existing release..."

        # Try to detect the version of the existing release
        $existingVersion = $null
        $existingMsi = Get-ChildItem -Path (Join-Path $ReleaseDir "installer") -Filter "*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($existingMsi) {
            # Extract version from filename like WfpTrafficControl-1.0.0.msi
            if ($existingMsi.Name -match 'WfpTrafficControl-(\d+\.\d+(\.\d+)?)\.msi') {
                $existingVersion = $Matches[1]
            }
        }

        # Use detected version or fall back to timestamp
        if ($existingVersion) {
            $existingMajorMinor = Get-MajorMinorVersion -VersionString $existingVersion
            $archiveDir = Join-Path $RepoRoot "release.$existingMajorMinor"
        } else {
            # Fallback: use timestamp if version can't be detected
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $archiveDir = Join-Path $RepoRoot "release.$timestamp"
            Write-Warning-Tracked "Could not detect existing release version, using timestamp for archive"
        }

        # Check if archive already exists
        if (Test-Path $archiveDir) {
            Write-SubStep "Archive folder already exists: $archiveDir"
            Write-SubStep "Skipping archive to preserve historical release"
        } else {
            Write-SubStep "Archiving to: $archiveDir"
            Copy-Item -Path $ReleaseDir -Destination $archiveDir -Recurse -Force -ErrorAction Stop
            $ArchivedReleasePath = $archiveDir
            Write-SubStep "Archive complete"
        }
    } else {
        Write-Step "1/7" "No existing release content to archive"
    }
} else {
    Write-Step "1/7" "Skipping archive step"
}

# ============================================================================
# Step 2: Clean Release Folder
# ============================================================================

Write-Step "2/7" "Cleaning release folder..."

if (Test-Path $ReleaseDir) {
    Remove-Item -Path $ReleaseDir -Recurse -Force -ErrorAction Stop
    Write-SubStep "Removed existing release folder"
}

# Create release directory structure
$releaseBinDir = Join-Path $ReleaseDir "bin"
$releaseInstallerDir = Join-Path $ReleaseDir "installer"
$releaseScriptsDir = Join-Path $ReleaseDir "scripts"
$releaseDocsDir = Join-Path $ReleaseDir "docs"

New-Item -ItemType Directory -Path $ReleaseDir -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $releaseBinDir -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path (Join-Path $releaseBinDir "cli") -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path (Join-Path $releaseBinDir "service") -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path (Join-Path $releaseBinDir "ui") -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $releaseInstallerDir -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $releaseScriptsDir -Force -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $releaseDocsDir -Force -ErrorAction Stop | Out-Null

Write-SubStep "Created release directory structure"

# ============================================================================
# Step 3: Restore NuGet Packages
# ============================================================================

Write-Step "3/7" "Restoring NuGet packages..."

Invoke-ExternalCommand -Command "dotnet" -Arguments @("restore", $SolutionFile) `
    -ErrorMessage "Failed to restore NuGet packages"

Write-SubStep "Packages restored successfully"

# ============================================================================
# Step 4: Build in Release Configuration
# ============================================================================

Write-Step "4/7" "Building projects in $Configuration configuration..."

# Build the solution (include version for WiX project in solution)
$originalLocation = Get-Location
try {
    Set-Location $RepoRoot
    dotnet build $SolutionFile -c $Configuration --no-restore -p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        Write-Error-Exit "Failed to build solution (exit code: $LASTEXITCODE)"
    }
}
finally {
    Set-Location $originalLocation
}

Write-SubStep "Solution build complete"

# Build and publish UI separately if it exists (it may not be in the solution)
if (Test-Path $UiProject) {
    Write-SubStep "Building and publishing UI project..."
    Invoke-ExternalCommand -Command "dotnet" -Arguments @("publish", $UiProject, "-c", $Configuration, "-o", $UiPublishDir) `
        -ErrorMessage "Failed to publish UI project"
}

# Publish Service
Write-SubStep "Publishing Service..."
Invoke-ExternalCommand -Command "dotnet" -Arguments @("publish", $ServiceProject, "-c", $Configuration, "-r", "win-x64", "--self-contained", "false", "-o", $ServicePublishDir) `
    -ErrorMessage "Failed to publish Service"

# Publish CLI
Write-SubStep "Publishing CLI..."
Invoke-ExternalCommand -Command "dotnet" -Arguments @("publish", $CliProject, "-c", $Configuration, "-o", $CliPublishDir) `
    -ErrorMessage "Failed to publish CLI"

Write-SubStep "All projects published successfully"

# ============================================================================
# Step 5: Run Tests
# ============================================================================

if (-not $SkipTests) {
    Write-Step "5/7" "Running tests..."

    if (Test-Path $TestsProject) {
        Invoke-ExternalCommand -Command "dotnet" -Arguments @("test", $TestsProject, "-c", $Configuration, "--no-build", "--verbosity", "normal") `
            -ErrorMessage "Tests failed! Fix test failures before creating a release."

        Write-SubStep "All tests passed"
    } else {
        Write-Warning-Tracked "Tests project not found: $TestsProject"
    }
} else {
    Write-Step "5/7" "Skipping tests (--SkipTests specified)"
}

# ============================================================================
# Step 6: Build MSI Installer
# ============================================================================

if (-not $SkipMsi) {
    Write-Step "6/7" "Building MSI installer..."

    if (Test-Path $InstallerProject) {
        # Build MSI directly (avoid wrapper function for complex argument handling)
        $originalLocation = Get-Location
        try {
            Set-Location $RepoRoot
            dotnet build $InstallerProject -c $Configuration -p:Version=$Version
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Exit "Failed to build MSI installer (exit code: $LASTEXITCODE)"
            }
        }
        finally {
            Set-Location $originalLocation
        }

        Write-SubStep "MSI build complete"
    } else {
        Write-Warning-Tracked "Installer project not found, skipping MSI build"
    }
} else {
    Write-Step "6/7" "Skipping MSI build (--SkipMsi specified)"
}

# ============================================================================
# Step 7: Copy Files to Release Folder
# ============================================================================

Write-Step "7/7" "Copying files to release folder..."

# Copy CLI binaries
$cliDestDir = Join-Path $releaseBinDir "cli"
if (Test-Path $CliPublishDir) {
    Copy-DirectoryContents -Source $CliPublishDir -Destination $cliDestDir -ValidateNotEmpty
    Write-SubStep "Copied CLI binaries"
} else {
    Write-Error-Exit "CLI publish directory not found: $CliPublishDir"
}

# Copy Service binaries
$serviceDestDir = Join-Path $releaseBinDir "service"
if (Test-Path $ServicePublishDir) {
    Copy-DirectoryContents -Source $ServicePublishDir -Destination $serviceDestDir -ValidateNotEmpty
    Write-SubStep "Copied Service binaries"
} else {
    Write-Error-Exit "Service publish directory not found: $ServicePublishDir"
}

# Copy UI binaries
$uiDestDir = Join-Path $releaseBinDir "ui"
if (Test-Path $UiPublishDir) {
    Copy-DirectoryContents -Source $UiPublishDir -Destination $uiDestDir -ValidateNotEmpty
    Write-SubStep "Copied UI binaries"
} else {
    Write-Warning-Tracked "UI publish directory not found, skipping UI binaries: $UiPublishDir"
}

# Copy MSI
if (-not $SkipMsi) {
    $msiFile = Get-ChildItem -Path $MsiOutputDir -Filter "*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($msiFile) {
        $expectedMsiName = "WfpTrafficControl-$Version.msi"
        $destMsiPath = Join-Path $releaseInstallerDir $expectedMsiName
        Copy-Item -Path $msiFile.FullName -Destination $destMsiPath -Force -ErrorAction Stop
        Write-SubStep "Copied MSI: $expectedMsiName"
    } else {
        Write-Warning-Tracked "MSI file not found in: $MsiOutputDir"
    }
}

# Copy scripts
foreach ($script in $ScriptsToCopy) {
    $srcPath = Join-Path $ScriptDir $script
    $destPath = Join-Path $releaseScriptsDir $script
    if (Test-Path $srcPath) {
        Copy-Item -Path $srcPath -Destination $destPath -Force -ErrorAction Stop
        Write-SubStep "Copied script: $script"
    } else {
        Write-Warning-Tracked "Script not found: $srcPath"
    }
}

# Copy docs
foreach ($doc in $DocsToCopy) {
    $srcPath = Join-Path $RepoRoot $doc.Source
    $destPath = Join-Path $releaseDocsDir $doc.Dest
    if (Test-Path $srcPath) {
        Copy-Item -Path $srcPath -Destination $destPath -Force -ErrorAction Stop
        Write-SubStep "Copied doc: $($doc.Dest)"
    } else {
        Write-Warning-Tracked "Documentation file not found: $srcPath"
    }
}

# ============================================================================
# Summary
# ============================================================================

$EndTime = Get-Date
$Duration = $EndTime - $StartTime

Write-Host ""
Write-Host "=== Release Build Complete ===" -ForegroundColor Cyan
Write-Host ""

# List all files in release
Write-Host "Files included in release:" -ForegroundColor White
$allFiles = Get-ChildItem -Path $ReleaseDir -Recurse -File -ErrorAction SilentlyContinue
$fileCount = 0
foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Substring($ReleaseDir.Length + 1)
    Write-Host "  $relativePath" -ForegroundColor Gray
    $fileCount++
}
Write-Host ""

# Calculate size
$totalSize = Get-DirectorySize -Path $ReleaseDir
Write-Host "Summary:" -ForegroundColor White
Write-Host "  Version:         $Version" -ForegroundColor Gray
Write-Host "  Configuration:   $Configuration" -ForegroundColor Gray
Write-Host "  Total files:     $fileCount" -ForegroundColor Gray
Write-Host "  Total size:      $(Format-Size -Bytes $totalSize)" -ForegroundColor Gray
Write-Host "  Build time:      $($Duration.ToString('mm\:ss'))" -ForegroundColor Gray
Write-Host "  Release path:    $ReleaseDir" -ForegroundColor Gray

if ($ArchivedReleasePath) {
    Write-Host "  Archived prev:   $ArchivedReleasePath" -ForegroundColor Gray
}

Write-Host ""

# Warnings
if ($Warnings.Count -gt 0) {
    Write-Host "Warnings encountered:" -ForegroundColor Yellow
    foreach ($warning in $Warnings) {
        Write-Host "  - $warning" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "Release package is ready at: $ReleaseDir" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Review the release contents" -ForegroundColor Gray
Write-Host "  2. Test the MSI installer in a VM" -ForegroundColor Gray
Write-Host "  3. Zip the release folder for distribution" -ForegroundColor Gray
Write-Host ""

exit 0
