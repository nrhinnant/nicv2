# Static Analysis Plan — WFP Traffic Control System

This document describes the static analysis tooling for the WFP Traffic Control project. We use **two industry-standard analyzers** that integrate directly with the .NET build system—no external tools to install.

**Goal:** Static analysis runs automatically on every build and can be verified via `dotnet test`.

---

## Table of Contents

1. [Prerequisites — What You Must Install](#1-prerequisites--what-you-must-install)
2. [Tool Selection](#2-tool-selection)
3. [Configuration](#3-configuration)
4. [Running Analysis](#4-running-analysis)
5. [Suppressing False Positives](#5-suppressing-false-positives)
6. [P/Invoke Review Checklist](#6-pinvoke-review-checklist)
7. [Implementation Checklist](#7-implementation-checklist)

---

## 1. Prerequisites — What You Must Install

Before getting started, ensure you have:

| Requirement | Version | How to Verify | How to Install |
|-------------|---------|---------------|----------------|
| .NET SDK | 8.0 or later | `dotnet --version` | [Download from Microsoft](https://dotnet.microsoft.com/download) |
| Visual Studio 2022 **OR** VS Code with C# extension | Latest | N/A | [VS](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) |

**That's it.** No Python, no separate CLI tools, no servers. All analyzers are NuGet packages that restore automatically.

### Optional (for enhanced IDE experience)

- **Visual Studio:** Install the "Roslynator 2022" extension from Extensions > Manage Extensions for in-editor quick fixes.
- **VS Code:** The C# extension (ms-dotnettools.csharp) supports Roslyn analyzers natively.

---

## 2. Tool Selection

We use two analyzers, both from Microsoft or the official .NET organization:

### 2.1 Microsoft.CodeAnalysis.NetAnalyzers (Microsoft)

| Attribute | Value |
|-----------|-------|
| **Source** | [github.com/dotnet/roslyn-analyzers](https://github.com/dotnet/roslyn-analyzers) |
| **Maintainer** | Microsoft (.NET team) |
| **Rule Prefix** | CA (Code Analysis) |
| **Coverage** | Security, reliability, performance, design, globalization, interop |

This is the **official Microsoft analyzer** included with .NET 5+. We enable it explicitly and set the analysis level to maximum.

**Why this tool:**
- Zero installation — built into .NET SDK
- Official Microsoft support
- Comprehensive security rules (CA2100, CA3001-CA3012)
- P/Invoke correctness rules (CA1401, CA2101)
- Performance rules (CA1822, CA1825-CA1829)

### 2.2 Roslynator.Analyzers (dotnet organization)

| Attribute | Value |
|-----------|-------|
| **Source** | [github.com/dotnet/roslynator](https://github.com/dotnet/roslynator) |
| **Maintainer** | dotnet organization (Microsoft-adjacent) |
| **Rule Prefix** | RCS (Roslynator Code Style) |
| **Coverage** | 500+ analyzers for simplification, redundancy, naming, potential bugs |

**Why this tool:**
- Part of the official dotnet GitHub organization
- Mature project with 500+ analyzers
- Complements NetAnalyzers with additional code quality rules
- NuGet package — restores automatically

### 2.3 Built-in SDK Tools (No Installation)

| Tool | Purpose | Command |
|------|---------|---------|
| **dotnet format** | Enforce code style from .editorconfig | `dotnet format --verify-no-changes` |
| **dotnet list package --vulnerable** | Scan for known CVEs in NuGet dependencies | `dotnet list package --vulnerable --include-transitive` |

---

## 3. Configuration

### 3.1 Directory.Build.props

Create this file at the **solution root** to apply analyzer settings to all projects:

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <!-- Enable .NET analyzers with maximum analysis -->
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-all</AnalysisLevel>
    <AnalysisMode>All</AnalysisMode>

    <!-- Treat analyzer warnings as errors in Release builds -->
    <TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>

    <!-- Output SARIF for tooling integration (optional) -->
    <ErrorLog>$(MSBuildProjectDirectory)/$(MSBuildProjectName).sarif,version=2.1</ErrorLog>
  </PropertyGroup>

  <ItemGroup>
    <!-- Roslynator: 500+ additional analyzers -->
    <PackageReference Include="Roslynator.Analyzers" Version="4.12.9">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

### 3.2 .globalconfig

Create this file at the **solution root** to configure rule severities:

```ini
# .globalconfig
is_global = true

# ============================================
# SECURITY RULES — Keep as errors
# ============================================
dotnet_diagnostic.CA2100.severity = error  # SQL injection (review queries)
dotnet_diagnostic.CA3001.severity = error  # SQL injection
dotnet_diagnostic.CA3002.severity = error  # XSS
dotnet_diagnostic.CA3003.severity = error  # File path injection
dotnet_diagnostic.CA3004.severity = error  # Information disclosure
dotnet_diagnostic.CA3006.severity = error  # Command injection
dotnet_diagnostic.CA3007.severity = error  # Open redirect
dotnet_diagnostic.CA5350.severity = error  # Weak crypto (SHA1)
dotnet_diagnostic.CA5351.severity = error  # Weak crypto (DES/3DES)

# ============================================
# P/INVOKE RULES — Critical for this project
# ============================================
dotnet_diagnostic.CA1401.severity = warning  # P/Invokes should not be visible
dotnet_diagnostic.CA2101.severity = warning  # Specify marshaling for P/Invoke strings
dotnet_diagnostic.SYSLIB1054.severity = suggestion  # Use LibraryImport instead of DllImport

# ============================================
# RELIABILITY RULES — Keep as warnings
# ============================================
dotnet_diagnostic.CA2000.severity = warning  # Dispose objects before losing scope
dotnet_diagnostic.CA2007.severity = none     # ConfigureAwait (not needed in app code)
dotnet_diagnostic.CA2008.severity = warning  # Do not create tasks without TaskScheduler
dotnet_diagnostic.CA2012.severity = warning  # Use ValueTasks correctly
dotnet_diagnostic.CA2013.severity = warning  # Do not use ReferenceEquals with value types

# ============================================
# PERFORMANCE RULES — Keep as warnings
# ============================================
dotnet_diagnostic.CA1822.severity = suggestion  # Mark members as static
dotnet_diagnostic.CA1825.severity = warning     # Avoid zero-length array allocations
dotnet_diagnostic.CA1826.severity = warning     # Use property instead of Enumerable method
dotnet_diagnostic.CA1827.severity = warning     # Do not use Count/LongCount when Any can be used
dotnet_diagnostic.CA1828.severity = warning     # Do not use CountAsync/LongCountAsync when AnyAsync can be used
dotnet_diagnostic.CA1829.severity = warning     # Use Length/Count property instead of Enumerable.Count

# ============================================
# DISABLE RULES — Not applicable to this project
# ============================================
dotnet_diagnostic.CA1014.severity = none  # Mark assemblies with CLSCompliant
dotnet_diagnostic.CA1062.severity = none  # Validate arguments (nullable handles this)
dotnet_diagnostic.CA1303.severity = none  # Do not pass literals as localized parameters
dotnet_diagnostic.CA1304.severity = none  # Specify CultureInfo
dotnet_diagnostic.CA1305.severity = none  # Specify IFormatProvider
dotnet_diagnostic.CA1310.severity = none  # Specify StringComparison for correctness
dotnet_diagnostic.CA1716.severity = none  # Identifiers should not match keywords
dotnet_diagnostic.CA1724.severity = none  # Type names should not match namespaces
dotnet_diagnostic.CA1848.severity = none  # Use LoggerMessage delegates (not using high-perf logging)

# ============================================
# ROSLYNATOR RULES — Adjust as needed
# ============================================
dotnet_diagnostic.RCS1036.severity = suggestion  # Remove redundant empty line
dotnet_diagnostic.RCS1037.severity = suggestion  # Remove trailing whitespace
dotnet_diagnostic.RCS1090.severity = warning     # Add call to ConfigureAwait (disabled above)
dotnet_diagnostic.RCS1194.severity = suggestion  # Implement exception constructors
```

### 3.3 .editorconfig (Code Style)

Create or update `.editorconfig` at the solution root:

```ini
# .editorconfig
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# Namespace preferences
csharp_style_namespace_declarations = file_scoped:suggestion

# var preferences
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# Expression-bodied members
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_accessors = true:suggestion

# Null checking
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

[*.{json,yml,yaml}]
indent_size = 2
```

---

## 4. Running Analysis

### 4.1 Standard Build (Runs Analyzers Automatically)

```powershell
# Build with analyzers — warnings appear in output
dotnet build

# Build with warnings as errors
dotnet build -warnaserror
```

### 4.2 Verify Code Format

```powershell
# Check formatting (fails if changes needed)
dotnet format --verify-no-changes

# Auto-fix formatting issues
dotnet format
```

### 4.3 Check for Vulnerable Packages

```powershell
dotnet list package --vulnerable --include-transitive
```

### 4.4 Run as Part of Test Pass

Add a target to `Directory.Build.targets` to run format verification during test:

```xml
<!-- Directory.Build.targets -->
<Project>
  <Target Name="VerifyFormatBeforeTest" BeforeTargets="VSTest">
    <Exec Command="dotnet format $(MSBuildProjectDirectory) --verify-no-changes"
          IgnoreExitCode="false"
          StandardErrorImportance="high" />
  </Target>
</Project>
```

Now `dotnet test` will fail if code formatting is incorrect.

### 4.5 Complete Verification Script

Create `scripts/Verify-CodeQuality.ps1`:

```powershell
#!/usr/bin/env pwsh
# scripts/Verify-CodeQuality.ps1
# Runs all static analysis checks

param(
    [switch]$Fix  # Auto-fix issues where possible
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "=== Code Quality Verification ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check formatting
Write-Host "[1/4] Checking code format..." -ForegroundColor Yellow
if ($Fix) {
    dotnet format "$root/WfpTrafficControl.sln"
    Write-Host "      Format issues auto-fixed." -ForegroundColor Green
} else {
    dotnet format "$root/WfpTrafficControl.sln" --verify-no-changes
    if ($LASTEXITCODE -ne 0) {
        Write-Host "      FAILED: Run with -Fix to auto-fix." -ForegroundColor Red
        exit 1
    }
}
Write-Host "      PASSED" -ForegroundColor Green

# 2. Build with analyzers
Write-Host ""
Write-Host "[2/4] Building with analyzers..." -ForegroundColor Yellow
dotnet build "$root/WfpTrafficControl.sln" -c Release -warnaserror
if ($LASTEXITCODE -ne 0) {
    Write-Host "      FAILED: Fix analyzer warnings above." -ForegroundColor Red
    exit 1
}
Write-Host "      PASSED" -ForegroundColor Green

# 3. Check vulnerable packages
Write-Host ""
Write-Host "[3/4] Checking for vulnerable packages..." -ForegroundColor Yellow
$vulnOutput = dotnet list "$root/WfpTrafficControl.sln" package --vulnerable --include-transitive 2>&1
Write-Host $vulnOutput
if ($vulnOutput -match "has the following vulnerable packages") {
    Write-Host "      WARNING: Vulnerable packages detected!" -ForegroundColor Yellow
}
Write-Host "      DONE" -ForegroundColor Green

# 4. Run tests
Write-Host ""
Write-Host "[4/4] Running tests..." -ForegroundColor Yellow
dotnet test "$root/WfpTrafficControl.sln" -c Release --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "      FAILED: Tests failed." -ForegroundColor Red
    exit 1
}
Write-Host "      PASSED" -ForegroundColor Green

Write-Host ""
Write-Host "=== All Checks Passed ===" -ForegroundColor Green
```

---

## 5. Suppressing False Positives

### 5.1 In-Code Suppression (Specific Instance)

```csharp
// Suppress with justification
#pragma warning disable CA2100 // SQL injection — using parameterized query
var result = connection.Execute(query, parameters);
#pragma warning restore CA2100

// Or use attribute (persists in code review)
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security", "CA3006:Command injection",
    Justification = "Process path validated by PolicyValidator.ValidateProcessPath")]
public void LaunchProcess(string path) { ... }
```

### 5.2 File-Level Suppression

Add to top of file:

```csharp
// Suppress for entire file
#pragma warning disable CA1822 // Mark members as static
```

### 5.3 Project-Level Suppression

Add to `.globalconfig`:

```ini
dotnet_diagnostic.CA1234.severity = none
```

### 5.4 Suppression Audit

Periodically audit suppressions:

```powershell
# Find all pragma suppressions
Get-ChildItem -Path src/ -Recurse -Include "*.cs" |
    Select-String -Pattern "#pragma warning disable" |
    ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
```

---

## 6. P/Invoke Review Checklist

Since this project uses WFP native APIs, P/Invoke code requires manual review beyond what analyzers catch.

### Handle Lifetime

- [ ] All WFP handles wrapped in `SafeHandle` derivatives
- [ ] `ReleaseHandle()` calls the correct Close/Free function
- [ ] No raw `IntPtr` handle tracking without try/finally

### Error Handling

- [ ] WFP API return codes (DWORD) checked and logged
- [ ] `SetLastError = true` on Windows APIs that use SetLastError
- [ ] `Marshal.GetLastWin32Error()` called immediately after P/Invoke

### Memory Safety

- [ ] No unpinned managed objects passed to native code expecting stable pointers
- [ ] `GCHandle` freed in finally blocks
- [ ] Structures have `[StructLayout]` with correct layout

### Find P/Invoke Code

```powershell
# Locate all P/Invoke for manual review
Get-ChildItem -Path src/ -Recurse -Include "*.cs" |
    Select-String -Pattern '\[(DllImport|LibraryImport)' |
    ForEach-Object { "$($_.Path):$($_.LineNumber)" }
```

---

## 7. Implementation Checklist

Use this checklist to track progress across Claude sessions. Copy to a scratch file or reference directly.

### Phase 1: Configuration Files

- [ ] Create `Directory.Build.props` at solution root (Section 3.1)
- [ ] Create `.globalconfig` at solution root (Section 3.2)
- [ ] Create/update `.editorconfig` at solution root (Section 3.3)
- [ ] Run `dotnet restore` to fetch Roslynator package

### Phase 2: Initial Build

- [ ] Run `dotnet build` — expect analyzer warnings (this is normal on first run)
- [ ] Document initial warning count: _____ warnings
- [ ] Review warnings and categorize: true positives vs. false positives

### Phase 3: Triage Warnings

- [ ] Fix true positive warnings (security, reliability, correctness)
- [ ] Add suppressions with justifications for confirmed false positives
- [ ] Update `.globalconfig` to disable rules that aren't applicable

### Phase 4: Verification Script

- [ ] Create `scripts/Verify-CodeQuality.ps1` (Section 4.5)
- [ ] Optionally create `Directory.Build.targets` for test integration (Section 4.4)
- [ ] Run `.\scripts\Verify-CodeQuality.ps1` and confirm all checks pass

### Phase 5: P/Invoke Audit

- [ ] Run P/Invoke finder script (Section 6)
- [ ] Review each P/Invoke against checklist
- [ ] Document any findings or concerns

### Phase 6: Ongoing Maintenance

- [ ] Run `dotnet list package --vulnerable` periodically
- [ ] Review new warnings when upgrading analyzer packages
- [ ] Audit suppressions quarterly

---

## Quick Reference

```powershell
# Build with analyzers
dotnet build

# Build with warnings as errors
dotnet build -warnaserror -c Release

# Check formatting
dotnet format --verify-no-changes

# Fix formatting
dotnet format

# Check vulnerable packages
dotnet list package --vulnerable --include-transitive

# Full verification
.\scripts\Verify-CodeQuality.ps1

# Full verification with auto-fix
.\scripts\Verify-CodeQuality.ps1 -Fix
```

---

## Related Documentation

- [025-testing-strategy.md](features/025-testing-strategy.md) — Testing strategy
- [CLAUDE.md](../CLAUDE.md) — Project operating guide
