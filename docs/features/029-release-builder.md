# Feature 029: Release Builder Script

## Summary

PowerShell script that produces a complete, distributable release package containing compiled binaries, installer, scripts, and documentation.

## Behavior

The `Build-Release.ps1` script performs a complete release build:

1. **Archives existing release** - Copies current `\release\` to `\release.{version}\` to preserve historical releases
2. **Cleans release folder** - Removes and recreates `\release\` directory
3. **Restores packages** - Runs `dotnet restore` on the solution
4. **Builds all projects** - Runs `dotnet build -c Release` and publishes all components
5. **Runs tests** - Executes unit tests (skippable with `-SkipTests`)
6. **Builds MSI installer** - Builds WiX installer with version number (skippable with `-SkipMsi`)
7. **Copies files** - Populates `\release\` with binaries, installer, scripts, and docs

### Release Folder Structure

```
\release\
  \bin\
    \cli\           # wfpctl.exe and dependencies
    \service\       # WfpTrafficControl.Service.exe and dependencies
    \ui\            # WfpTrafficControl.UI.exe and dependencies
  \installer\
    WfpTrafficControl-{version}.msi
  \scripts\
    Install-Service.ps1
    Uninstall-Service.ps1
    Start-Service.ps1
    Stop-Service.ps1
  \docs\
    EXECUTIVE_SUMMARY.md
    022-how-it-works.md
    023-troubleshooting.md
    License.rtf
```

### Version Archiving

When running a new build, existing releases are preserved:

```
\release\          # Always the latest build (e.g., 1.3)
\release.1.0\      # Historical release 1.0
\release.1.1\      # Historical release 1.1
\release.1.2\      # Historical release 1.2
```

Version detection:
- Reads version from existing MSI filename (e.g., `WfpTrafficControl-1.0.0.msi`)
- Falls back to timestamp if version cannot be detected
- Skips archiving if versioned folder already exists (preserves historical releases)

## Usage

### Basic Usage

```powershell
# Build release with default version 1.0.0
.\scripts\Build-Release.ps1

# Build release with specific version
.\scripts\Build-Release.ps1 -Version 1.2.0
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Version` | string | "1.0.0" | Version number (format: X.Y or X.Y.Z) |
| `-SkipTests` | switch | false | Skip test execution for faster iteration |
| `-SkipMsi` | switch | false | Skip MSI build (if WiX not installed) |
| `-SkipArchive` | switch | false | Skip archiving existing release |
| `-Configuration` | string | "Release" | Build configuration (Release or Debug) |

### Examples

```powershell
# Quick build without tests
.\scripts\Build-Release.ps1 -SkipTests

# Build without MSI (WiX not installed)
.\scripts\Build-Release.ps1 -SkipMsi

# Full release with specific version
.\scripts\Build-Release.ps1 -Version 2.0.0

# Development build without archiving
.\scripts\Build-Release.ps1 -SkipArchive -SkipTests
```

## Safety Guarantees

The script is designed to be **safe and idempotent**:

1. **Read-only source** - Never modifies source files, project files, or existing build outputs
2. **Isolated output** - Only writes to `\release\` and `\release.*\` folders
3. **Error handling** - Uses `-ErrorAction Stop` throughout; exits with non-zero code on failure
4. **Validation** - Validates all inputs and checks prerequisites before starting
5. **Version validation** - Validates version format matches `X.Y` or `X.Y.Z` pattern

## Prerequisites

- .NET SDK 8.0+ installed
- WiX Toolset (for MSI build, or use `-SkipMsi`)
- PowerShell 5.1+

## Error Handling

The script stops immediately if any step fails:

- **Build failure** - Exits with error message and code 1
- **Test failure** - Exits with error message (unless `-SkipTests`)
- **MSI build failure** - Exits with error message
- **Missing files** - Validates required files exist before copying

Non-fatal issues are tracked as warnings and displayed in the summary.

## Output

The script provides clear progress messages and a final summary:

```
=== WfpTrafficControl Release Builder ===
Version:       1.0.0
Configuration: Release
Repository:    C:\path\to\repo

[1/7] Archiving existing release...
       Archiving to: C:\path\to\repo\release.0.9
       Archive complete
[2/7] Cleaning release folder...
       Created release directory structure
[3/7] Restoring NuGet packages...
       Packages restored successfully
[4/7] Building projects in Release configuration...
       Solution build complete
       Publishing Service...
       Publishing CLI...
       All projects published successfully
[5/7] Running tests...
       All tests passed
[6/7] Building MSI installer...
       MSI build complete
[7/7] Copying files to release folder...
       Copied CLI binaries
       Copied Service binaries
       Copied UI binaries
       Copied MSI: WfpTrafficControl-1.0.0.msi
       Copied script: Install-Service.ps1
       ...

=== Release Build Complete ===

Files included in release:
  bin\cli\wfpctl.exe
  bin\cli\Shared.dll
  ...

Summary:
  Version:         1.0.0
  Configuration:   Release
  Total files:     47
  Total size:      12.35 MB
  Build time:      01:23
  Release path:    C:\path\to\repo\release
  Archived prev:   C:\path\to\repo\release.0.9

Release package is ready at: C:\path\to\repo\release

Next steps:
  1. Review the release contents
  2. Test the MSI installer in a VM
  3. Zip the release folder for distribution
```

## Known Limitations

1. UI project must be built separately if not in the main solution
2. WiX Toolset required for MSI build (use `-SkipMsi` to skip)
3. Version must be specified manually (not auto-detected from project)

## Rollback

No rollback needed - the script only creates new folders. To undo:

1. Delete `\release\` folder
2. Optionally delete `\release.*\` versioned folders

## Related Files

- `scripts\Build-Installer.ps1` - MSI-only build script (used internally)
- `scripts\Install-Service.ps1` - Service installation script (included in release)
- `installer\WfpTrafficControl.Installer\` - WiX installer project
