# Static Analysis Plan — WFP Traffic Control System

This document provides a comprehensive plan for evaluating the quality, performance, and security of the WFP Traffic Control project using free and open-source static analysis tools.

**Target audience:** Engineers responsible for code quality, security review, and CI/CD integration.

**Scope:** All production code in `/src/service`, `/src/cli`, `/src/shared`, and `/benchmarks`.

---

## Table of Contents

1. [Tool Selection Matrix](#1-tool-selection-matrix)
2. [P/Invoke-Specific Analysis](#2-pinvoke-specific-analysis)
3. [Security Scanner Configuration](#3-security-scanner-configuration)
4. [Integration Plan](#4-integration-plan)
5. [Prioritized Execution Order](#5-prioritized-execution-order)
6. [Appendix: Rule Suppressions](#appendix-rule-suppressions)
7. [Configuring This Plan with Claude's Help](#7-configuring-this-plan-with-claudes-help)

---

## 1. Tool Selection Matrix

### 1.1 Security Analysis Tools

| Tool | URL | What It Detects | Install | Run | Output |
|------|-----|-----------------|---------|-----|--------|
| **Security Code Scan** | [github.com/security-code-scan](https://github.com/security-code-scan/security-code-scan) | SQL injection, XSS, XXE, path traversal, insecure deserialization, weak crypto, command injection | NuGet: `SecurityCodeScan.VS2019` | MSBuild (automatic) | Build warnings (SCSxxxx) |
| **DevSkim** | [github.com/microsoft/DevSkim](https://github.com/microsoft/DevSkim) | Hardcoded secrets, weak crypto, dangerous functions, security anti-patterns | `dotnet tool install -g Microsoft.CST.DevSkim.CLI` | `devskim analyze -I src/ -o results.json` | JSON, SARIF |
| **Semgrep** | [semgrep.dev](https://semgrep.dev) | Custom rules, OWASP patterns, taint tracking, injection flaws | `pip install semgrep` or Docker | `semgrep --config=auto src/` | JSON, SARIF, text |
| **dotnet list package --vulnerable** | Built-in | Known CVEs in NuGet dependencies | N/A (dotnet SDK) | `dotnet list package --vulnerable --include-transitive` | Console text |

### 1.2 Code Quality Tools

| Tool | URL | What It Detects | Install | Run | Output |
|------|-----|-----------------|---------|-----|--------|
| **Microsoft.CodeAnalysis.NetAnalyzers** | [nuget.org](https://www.nuget.org/packages/Microsoft.CodeAnalysis.NetAnalyzers) | API usage, globalization, performance, reliability, security (CA rules) | NuGet (included in .NET 5+) | MSBuild (automatic) | Build warnings (CAxxxx) |
| **Roslynator** | [github.com/dotnet/roslynator](https://github.com/dotnet/roslynator) | 500+ analyzers: simplification, redundancy, formatting, naming, potential bugs | NuGet: `Roslynator.Analyzers` | MSBuild (automatic) | Build warnings (RCSxxxx) |
| **SonarQube Community** | [sonarqube.org](https://www.sonarqube.org/downloads/) | Code smells, bugs, vulnerabilities, duplication, complexity metrics | Docker or standalone server | `dotnet sonarscanner begin/end` | Web dashboard |
| **dotnet format** | Built-in | Code style violations (editorconfig enforcement) | N/A (dotnet SDK) | `dotnet format --verify-no-changes` | Console text, exit code |

### 1.3 Style and Consistency Tools

| Tool | URL | What It Detects | Install | Run | Output |
|------|-----|-----------------|---------|-----|--------|
| **StyleCop.Analyzers** | [github.com/DotNetAnalyzers/StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) | Naming, ordering, spacing, documentation, maintainability | NuGet: `StyleCop.Analyzers` | MSBuild (automatic) | Build warnings (SAxxxx) |
| **EditorConfig** | Built-in | IDE settings, formatting rules | `.editorconfig` file | IDE enforcement | N/A |

### 1.4 Performance Analysis Tools

| Tool | URL | What It Detects | Install | Run | Output |
|------|-----|-----------------|---------|-----|--------|
| **BenchmarkDotNet** | [benchmarkdotnet.org](https://benchmarkdotnet.org/) | Microbenchmark regressions, allocation patterns | NuGet (already in project) | `dotnet run -c Release` | Console, JSON, HTML |
| **Microsoft.CodeAnalysis.PerformanceSensitiveAnalyzers** | [NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PerformanceSensitiveAnalyzers) | Allocations in hot paths, boxing, closures | NuGet | MSBuild (automatic) | Build warnings |
| **JetBrains dotMemory CLI** | [jetbrains.com](https://www.jetbrains.com/dotmemory/) | Memory snapshots, leak detection | Standalone (free for profiling) | CLI profiler | Snapshots |
| **dotnet-counters** | Built-in | Runtime metrics (GC, CPU, exceptions) | `dotnet tool install -g dotnet-counters` | `dotnet-counters monitor` | Console, CSV |
| **dotnet-trace** | Built-in | CPU profiling, event tracing | `dotnet tool install -g dotnet-trace` | `dotnet-trace collect` | .nettrace files |

### 1.5 P/Invoke and Interop Tools

| Tool | URL | What It Detects | Install | Run | Output |
|------|-----|-----------------|---------|-----|--------|
| **Microsoft.Interop.Analyzers** | [NuGet](https://www.nuget.org/packages/Microsoft.Interop.Analyzers) | P/Invoke correctness, marshaling issues, LibraryImport migration | NuGet | MSBuild (automatic) | Build warnings |
| **ClangSharpPInvokeGenerator** | [github.com/dotnet/ClangSharp](https://github.com/dotnet/ClangSharp) | Generate correct P/Invoke from C headers | dotnet tool | Manual generation | C# source |

---

## 2. P/Invoke-Specific Analysis

### 2.1 Automated Analysis with Microsoft.Interop.Analyzers

Install the analyzer package in all projects with P/Invoke code:

```xml
<!-- In Shared.csproj and Service.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.Interop.Analyzers" Version="7.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

**Key rules for this project:**

| Rule ID | Description | Relevance |
|---------|-------------|-----------|
| SYSLIB1054 | Use LibraryImportAttribute instead of DllImportAttribute | Migration path for source-generated marshaling |
| SYSLIB1050 | Invalid LibraryImportAttribute usage | Marshaling correctness |
| CA1838 | Avoid StringBuilder parameters for P/Invokes | Performance |
| CA1401 | P/Invokes should not be visible | Encapsulation |
| CA2101 | Specify marshaling for P/Invoke string arguments | Security |

### 2.2 Manual Review Checklist for P/Invoke

Since automated tools cannot catch all P/Invoke issues, conduct manual review of `/src/shared/Native/` and `/src/service/Wfp/` using this checklist:

#### Handle Lifetime Management

```csharp
// ✅ CORRECT: SafeHandle with release
internal sealed class WfpEngineHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    protected override bool ReleaseHandle()
    {
        return NativeMethods.FwpmEngineClose(handle) == 0;
    }
}

// ❌ INCORRECT: Raw IntPtr without cleanup
[DllImport("fwpuclnt.dll")]
static extern uint FwpmEngineOpen(..., out IntPtr engineHandle);
// Risk: Handle leak if not explicitly closed
```

**Review points:**
- [ ] All WFP handles wrapped in SafeHandle derivatives
- [ ] `ReleaseHandle()` calls appropriate Close/Free function
- [ ] SafeHandle used in P/Invoke signatures (not IntPtr)
- [ ] No manual `IntPtr` handle tracking without try/finally

#### SetLastError Patterns

```csharp
// ✅ CORRECT: Check SetLastError on boolean-returning APIs
[DllImport("kernel32.dll", SetLastError = true)]
static extern bool CloseHandle(IntPtr handle);

if (!CloseHandle(h))
{
    int error = Marshal.GetLastWin32Error();
    throw new Win32Exception(error);
}

// ❌ INCORRECT: Ignoring last error
CloseHandle(h); // Silent failure
```

**Review points:**
- [ ] `SetLastError = true` on Windows APIs that use SetLastError
- [ ] `Marshal.GetLastWin32Error()` called immediately after P/Invoke
- [ ] No intervening managed code between P/Invoke and GetLastWin32Error
- [ ] WFP APIs return DWORD error codes directly (not via SetLastError)

#### Memory Marshaling Safety

```csharp
// ✅ CORRECT: Pinned memory with GCHandle
GCHandle pinnedArray = GCHandle.Alloc(buffer, GCHandleType.Pinned);
try
{
    IntPtr ptr = pinnedArray.AddrOfPinnedObject();
    NativeCall(ptr);
}
finally
{
    pinnedArray.Free();
}

// ✅ CORRECT: stackalloc for small buffers (preferred)
Span<byte> buffer = stackalloc byte[256];
fixed (byte* ptr = buffer) { ... }

// ❌ INCORRECT: Passing managed array directly to native code expecting persistent pointer
NativeCall(managedArray); // Array may be moved by GC
```

**Review points:**
- [ ] No unpinned managed objects passed to native code expecting stable pointers
- [ ] `stackalloc` or `ArrayPool` used for temporary buffers
- [ ] `GCHandle` freed in finally blocks
- [ ] Structures with correct `StructLayout` attributes

#### Marshaling Attributes Audit

```csharp
// WFP-specific structure marshaling
[StructLayout(LayoutKind.Sequential)]
internal struct FWPM_FILTER0
{
    public Guid filterKey;
    public Guid providerKey;
    // ...
}
```

**Review points:**
- [ ] All P/Invoke structures have `[StructLayout]`
- [ ] `LayoutKind.Sequential` matches C struct layout
- [ ] `CharSet` specified for string-containing structs
- [ ] `Pack` attribute matches native packing if non-default
- [ ] Nested structs/unions handled correctly

### 2.3 P/Invoke Review Script

Create a PowerShell script to find all P/Invoke declarations for manual audit:

```powershell
# scripts/Find-PInvoke.ps1
param([string]$Path = "src")

$patterns = @(
    '\[DllImport',
    '\[LibraryImport',
    'Marshal\.(PtrToStructure|StructureToPtr|Copy|AllocHGlobal|FreeHGlobal)',
    'GCHandle\.(Alloc|Free)',
    'IntPtr',
    'SafeHandle',
    'fixed\s*\('
)

foreach ($pattern in $patterns) {
    Write-Host "`n=== $pattern ===" -ForegroundColor Cyan
    Get-ChildItem -Path $Path -Recurse -Include "*.cs" |
        Select-String -Pattern $pattern |
        ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
}
```

---

## 3. Security Scanner Configuration

### 3.1 Security Code Scan Configuration

Install in all production projects:

```xml
<ItemGroup>
  <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

**Relevant rules for this project:**

| Rule | Description | Action |
|------|-------------|--------|
| SCS0001 | Command injection | Enable (validate process paths in policy) |
| SCS0002 | SQL injection | Disable (no SQL in project) |
| SCS0003 | XPath injection | Disable (no XPath) |
| SCS0004 | XXE injection | Disable (using System.Text.Json, not XML) |
| SCS0005 | Weak randomness | Enable (if GUID generation uses Random) |
| SCS0007 | Path traversal | Enable (policy file loading, LKG store) |
| SCS0018 | Deserialization | Enable (JSON policy parsing) |

**Suppression guidance:**

Create `.globalconfig` at solution root:

```ini
# .globalconfig
is_global = true

# Disable SQL-related rules (no database)
dotnet_diagnostic.SCS0002.severity = none
dotnet_diagnostic.SCS0014.severity = none
dotnet_diagnostic.SCS0020.severity = none
dotnet_diagnostic.SCS0026.severity = none

# Disable XPath/XML rules (using JSON)
dotnet_diagnostic.SCS0003.severity = none
dotnet_diagnostic.SCS0004.severity = none
dotnet_diagnostic.SCS0007.severity = none  # Re-enable if XPath ever used

# Keep enabled for this project
dotnet_diagnostic.SCS0001.severity = warning  # Command injection
dotnet_diagnostic.SCS0005.severity = warning  # Weak random
dotnet_diagnostic.SCS0018.severity = warning  # Deserialization
```

**Expected false positives:**

1. **Path traversal (SCS0007)** on `Path.Combine` usage — suppress if inputs are validated
2. **Deserialization (SCS0018)** on `JsonSerializer.Deserialize` — suppress if using strict typing (not `object`)

### 3.2 DevSkim Configuration

Create `devskim.json` configuration:

```json
{
  "rules": {
    "DS126858": "disabled",  // Weak SSL/TLS (not applicable - no TLS in project)
    "DS137138": "disabled",  // SQL injection (no SQL)
    "DS104456": "enabled",   // Hardcoded credentials
    "DS173237": "enabled",   // Hardcoded IP addresses (review policy defaults)
    "DS114352": "enabled",   // Insecure random
    "DS134411": "enabled"    // Path traversal
  },
  "ignoreFiles": [
    "**/obj/**",
    "**/bin/**",
    "**/*.Designer.cs",
    "**/tests/**"
  ]
}
```

**Run command:**

```powershell
devskim analyze -I src/ -O devskim-results.sarif -f sarif --severity critical,important
```

### 3.3 Semgrep Configuration

Create `.semgrep.yml` at project root:

```yaml
rules:
  # Custom rule: Ensure WFP API return codes are checked
  - id: wfp-unchecked-return
    patterns:
      - pattern: |
          $FUNC(...);
      - metavariable-regex:
          metavariable: $FUNC
          regex: ^(FwpmEngineOpen|FwpmFilterAdd|FwpmFilterDeleteByKey|FwpmTransactionBegin|FwpmTransactionCommit|FwpmTransactionAbort)$
      - pattern-not: |
          $RET = $FUNC(...);
    message: "WFP API return code must be captured and checked"
    severity: ERROR
    languages: [csharp]

  # Custom rule: SafeHandle not disposed
  - id: safehandle-not-disposed
    patterns:
      - pattern: |
          $HANDLE = new $TYPE(...);
          ...
      - pattern-not: |
          using ($HANDLE = new $TYPE(...)) { ... }
      - pattern-not: |
          $HANDLE.Dispose();
      - metavariable-regex:
          metavariable: $TYPE
          regex: .*Handle$
    message: "SafeHandle should be disposed or used with 'using'"
    severity: WARNING
    languages: [csharp]

  # Use Semgrep's built-in C# security rules
  - id: use-semgrep-csharp
    pattern: ""
    message: ""
    severity: INFO
```

**Run command:**

```powershell
# Use Semgrep's auto config plus custom rules
semgrep --config=auto --config=.semgrep.yml src/ --sarif -o semgrep-results.sarif

# Or use specific rule packs
semgrep --config=p/csharp --config=p/security-audit src/
```

### 3.4 Roslyn Analyzer Configuration

Create/update `Directory.Build.props` at solution root:

```xml
<Project>
  <PropertyGroup>
    <!-- Enable all .NET analyzers -->
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-all</AnalysisLevel>
    <AnalysisMode>All</AnalysisMode>

    <!-- Treat warnings as errors in CI -->
    <TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>

    <!-- Nullable reference types -->
    <Nullable>enable</Nullable>

    <!-- Code analysis output -->
    <ErrorLog>$(MSBuildProjectDirectory)/analysis.sarif,version=2.1</ErrorLog>
  </PropertyGroup>

  <ItemGroup>
    <!-- Analyzers for all projects -->
    <PackageReference Include="Roslynator.Analyzers" Version="4.12.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>analyzers</IncludeAssets>
    </PackageReference>

    <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>analyzers</IncludeAssets>
    </PackageReference>

    <PackageReference Include="Microsoft.Interop.Analyzers" Version="7.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Key rules to enforce:**

| Category | Rule IDs | Action |
|----------|----------|--------|
| Security | CA2100, CA2109, CA2119, CA2153, CA3001-CA3012 | Error |
| Reliability | CA2000, CA2007, CA2008, CA2012, CA2013 | Error |
| Performance | CA1812, CA1822, CA1825, CA1826, CA1827-CA1829 | Warning |
| Design | CA1001, CA1010, CA1036, CA1051, CA1052 | Warning |
| Naming | CA1707, CA1708, CA1710, CA1711 | Suggestion |

---

## 4. Integration Plan

### 4.1 Local Development Integration

#### IDE Integration (Visual Studio / VS Code / Rider)

All Roslyn analyzers run automatically in the IDE when added via NuGet. No additional configuration needed.

**VS Code settings (.vscode/settings.json):**

```json
{
  "omnisharp.enableRoslynAnalyzers": true,
  "omnisharp.enableEditorConfigSupport": true,
  "csharp.analysis.analyzerConfig": ".globalconfig"
}
```

#### Local CLI Commands

Add these to a `scripts/Analyze-Local.ps1` script:

```powershell
#!/usr/bin/env pwsh
# scripts/Analyze-Local.ps1
# Run all static analysis locally

param(
    [switch]$Quick,      # Skip slow tools
    [switch]$Fix         # Auto-fix where possible
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== Static Analysis Suite ===" -ForegroundColor Cyan

# 1. Roslyn analyzers via build (always runs)
Write-Host "`n[1/6] Building with analyzers..." -ForegroundColor Yellow
dotnet build "$projectRoot/WfpTrafficControl.sln" -warnaserror -p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) { exit 1 }

# 2. Format check
Write-Host "`n[2/6] Checking code format..." -ForegroundColor Yellow
if ($Fix) {
    dotnet format "$projectRoot/WfpTrafficControl.sln"
} else {
    dotnet format "$projectRoot/WfpTrafficControl.sln" --verify-no-changes
}
if ($LASTEXITCODE -ne 0) {
    Write-Host "Format issues found. Run with -Fix to auto-fix." -ForegroundColor Red
    exit 1
}

# 3. NuGet vulnerability scan
Write-Host "`n[3/6] Scanning NuGet vulnerabilities..." -ForegroundColor Yellow
dotnet list "$projectRoot/WfpTrafficControl.sln" package --vulnerable --include-transitive
if ($LASTEXITCODE -ne 0) { exit 1 }

# 4. DevSkim (if installed)
Write-Host "`n[4/6] Running DevSkim..." -ForegroundColor Yellow
if (Get-Command devskim -ErrorAction SilentlyContinue) {
    devskim analyze -I "$projectRoot/src" -O "$projectRoot/devskim-results.sarif" -f sarif
} else {
    Write-Host "DevSkim not installed. Skipping." -ForegroundColor DarkYellow
}

# 5. Semgrep (skip in quick mode)
if (-not $Quick) {
    Write-Host "`n[5/6] Running Semgrep..." -ForegroundColor Yellow
    if (Get-Command semgrep -ErrorAction SilentlyContinue) {
        semgrep --config=auto --config="$projectRoot/.semgrep.yml" "$projectRoot/src" --sarif -o "$projectRoot/semgrep-results.sarif"
    } else {
        Write-Host "Semgrep not installed. Skipping." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "`n[5/6] Skipping Semgrep (quick mode)" -ForegroundColor DarkYellow
}

# 6. P/Invoke audit report
Write-Host "`n[6/6] Generating P/Invoke audit report..." -ForegroundColor Yellow
& "$PSScriptRoot/Find-PInvoke.ps1" -Path "$projectRoot/src" | Out-File "$projectRoot/pinvoke-audit.txt"
Write-Host "P/Invoke audit saved to pinvoke-audit.txt"

Write-Host "`n=== Analysis Complete ===" -ForegroundColor Green
```

### 4.2 MSBuild Integration

Add a custom target in `Directory.Build.targets`:

```xml
<Project>
  <!-- Run additional analysis post-build -->
  <Target Name="PostBuildAnalysis" AfterTargets="Build" Condition="'$(Configuration)' == 'Release'">
    <Message Text="Running post-build security analysis..." Importance="high" />

    <!-- Output SARIF for CI consumption -->
    <PropertyGroup>
      <ErrorLog>$(MSBuildProjectDirectory)/$(MSBuildProjectName).sarif,version=2.1</ErrorLog>
    </PropertyGroup>
  </Target>

  <!-- Fail on high-severity warnings in CI -->
  <PropertyGroup Condition="'$(CI)' == 'true'">
    <MSBuildWarningsAsErrors>$(MSBuildWarningsAsErrors);SCS0001;SCS0018;CA2000;CA2100</MSBuildWarningsAsErrors>
  </PropertyGroup>
</Project>
```

### 4.3 GitHub Actions CI Pipeline

Create `.github/workflows/static-analysis.yml`:

```yaml
name: Static Analysis

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  analyze:
    runs-on: windows-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore

      # 1. Build with analyzers
      - name: Build with Analyzers
        run: dotnet build -c Release -warnaserror /p:TreatWarningsAsErrors=true
        env:
          CI: true

      # 2. Format check
      - name: Check Formatting
        run: dotnet format --verify-no-changes

      # 3. NuGet vulnerability scan
      - name: Check Vulnerable Packages
        run: |
          dotnet list package --vulnerable --include-transitive 2>&1 | Tee-Object -Variable output
          if ($output -match "has the following vulnerable packages") {
            Write-Error "Vulnerable packages detected!"
            exit 1
          }
        shell: pwsh

      # 4. DevSkim
      - name: Install DevSkim
        run: dotnet tool install -g Microsoft.CST.DevSkim.CLI

      - name: Run DevSkim
        run: devskim analyze -I src/ -O devskim-results.sarif -f sarif --severity critical,important

      # 5. Upload SARIF results
      - name: Upload SARIF results
        uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: |
            devskim-results.sarif
            src/**/*.sarif

  semgrep:
    runs-on: ubuntu-latest
    container:
      image: returntocorp/semgrep

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Run Semgrep
        run: semgrep --config=auto --config=p/csharp --config=p/security-audit src/ --sarif -o semgrep-results.sarif

      - name: Upload SARIF
        uses: github/codeql-action/upload-sarif@v3
        if: always()
        with:
          sarif_file: semgrep-results.sarif

  sonarqube:
    runs-on: windows-latest
    # Only run if you have a SonarQube server
    if: ${{ vars.SONAR_HOST_URL != '' }}

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install SonarScanner
        run: dotnet tool install -g dotnet-sonarscanner

      - name: Begin Analysis
        run: |
          dotnet sonarscanner begin `
            /k:"wfp-traffic-control" `
            /d:sonar.host.url="${{ vars.SONAR_HOST_URL }}" `
            /d:sonar.token="${{ secrets.SONAR_TOKEN }}"
        shell: pwsh

      - name: Build
        run: dotnet build -c Release

      - name: End Analysis
        run: dotnet sonarscanner end /d:sonar.token="${{ secrets.SONAR_TOKEN }}"
```

### 4.4 Pre-Commit Hook (Optional)

Create `.githooks/pre-commit`:

```bash
#!/bin/sh
# Quick pre-commit checks (< 30 seconds)

echo "Running pre-commit analysis..."

# Format check (fast)
dotnet format --verify-no-changes
if [ $? -ne 0 ]; then
    echo "❌ Code formatting issues. Run 'dotnet format' to fix."
    exit 1
fi

# Build with analyzers (catches most issues)
dotnet build -c Debug -warnaserror --no-restore
if [ $? -ne 0 ]; then
    echo "❌ Build failed or analyzer warnings present."
    exit 1
fi

echo "✅ Pre-commit checks passed."
```

Install hook:

```powershell
git config core.hooksPath .githooks
```

---

## 5. Prioritized Execution Order

### Tier 1: Always Run (Every Build) — < 30 seconds

| Priority | Tool | Why First |
|----------|------|-----------|
| 1 | **Roslyn Analyzers** (via build) | Catches 80% of issues. Zero setup after NuGet install. Runs automatically. |
| 2 | **dotnet format --verify-no-changes** | Consistency. Fast. Catches obvious style drift. |
| 3 | **Nullable analysis** | Catches null reference issues at compile time. Already enabled in modern .NET. |

**Command:**
```powershell
dotnet build -warnaserror && dotnet format --verify-no-changes
```

### Tier 2: Before Commit (Local) — 1-2 minutes

| Priority | Tool | Why Second |
|----------|------|------------|
| 4 | **NuGet vulnerability scan** | Security-critical. Runs in seconds. No false positives. |
| 5 | **DevSkim** | Fast. Low false positive rate. Catches hardcoded secrets and dangerous patterns. |

**Command:**
```powershell
dotnet list package --vulnerable --include-transitive
devskim analyze -I src/ --severity critical,important
```

### Tier 3: CI Pipeline (Every PR) — 5-10 minutes

| Priority | Tool | Why Third |
|----------|------|-----------|
| 6 | **Semgrep** | Deeper analysis. Custom rules for WFP API patterns. Slower but thorough. |
| 7 | **Security Code Scan** | Comprehensive. Some false positives require triage. |
| 8 | **SARIF upload to GitHub** | Enables code scanning alerts in PR diffs. |

### Tier 4: Weekly/Release (Full Suite) — 30+ minutes

| Priority | Tool | Why Fourth |
|----------|------|------------|
| 9 | **SonarQube** | Full dashboard with trends. Duplication analysis. Requires server setup. |
| 10 | **Manual P/Invoke audit** | Cannot be automated. Critical for this project. |
| 11 | **BenchmarkDotNet regression check** | Performance baselines. Run after significant changes. |

---

## 6. Appendix: Rule Suppressions

### Creating Suppression Files

For project-wide suppressions, use `.globalconfig`:

```ini
# .globalconfig
is_global = true

# Suppress specific rules globally
dotnet_diagnostic.CA1014.severity = none  # Mark assemblies with CLSCompliant
dotnet_diagnostic.CA1716.severity = none  # Identifiers should not match keywords
dotnet_diagnostic.CA1724.severity = none  # Type names should not match namespaces

# Downgrade warnings that are noisy but worth reviewing
dotnet_diagnostic.CA1822.severity = suggestion  # Mark members as static
dotnet_diagnostic.RCS1036.severity = suggestion  # Remove redundant empty line
```

### Inline Suppressions

For specific false positives:

```csharp
// Suppress specific instance with justification
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
// Justification: Using parameterized queries, analyzer cannot detect this
connection.ExecuteCommand(query, parameters);
#pragma warning restore CA2100

// Or use attribute-based suppression
[SuppressMessage("Security", "SCS0007:Path Traversal",
    Justification = "Input validated by PolicyValidator.ValidatePolicyPath")]
public void LoadPolicy(string path) { ... }
```

### Suppression Audit

Periodically review all suppressions:

```powershell
# Find all pragma suppressions
Get-ChildItem -Path src/ -Recurse -Include "*.cs" |
    Select-String -Pattern "#pragma warning disable" |
    ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }

# Find all SuppressMessage attributes
Get-ChildItem -Path src/ -Recurse -Include "*.cs" |
    Select-String -Pattern "\[SuppressMessage" |
    ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
```

---

## 7. Configuring This Plan with Claude's Help

This plan can be largely implemented by Claude Code. Below is a breakdown of what Claude can automate versus what requires manual action.

### 7.1 What Claude Can Create/Modify

Claude can generate all configuration files and scripts described in this plan:

| File | Purpose | Section Reference |
|------|---------|-------------------|
| `.globalconfig` | Analyzer rule severity configuration | [3.1](#31-security-code-scan-configuration) |
| `Directory.Build.props` | Add analyzer NuGet packages to all projects | [3.4](#34-roslyn-analyzer-configuration) |
| `Directory.Build.targets` | MSBuild integration for CI | [4.2](#42-msbuild-integration) |
| `.semgrep.yml` | Custom Semgrep rules for WFP APIs | [3.3](#33-semgrep-configuration) |
| `devskim.json` | DevSkim rule configuration | [3.2](#32-devskim-configuration) |
| `scripts/Analyze-Local.ps1` | Local analysis runner script | [4.1](#41-local-development-integration) |
| `scripts/Find-PInvoke.ps1` | P/Invoke audit script | [2.3](#23-pinvoke-review-script) |
| `.github/workflows/static-analysis.yml` | GitHub Actions CI pipeline | [4.3](#43-github-actions-ci-pipeline) |
| `.githooks/pre-commit` | Pre-commit hook (optional) | [4.4](#44-pre-commit-hook-optional) |
| `.editorconfig` | Code style settings (if needed) | — |
| `.vscode/settings.json` | VS Code analyzer settings | [4.1](#41-local-development-integration) |

**After Claude creates these files:**

```powershell
# Restore packages to pull in new analyzers
dotnet restore

# Run local analysis
.\scripts\Analyze-Local.ps1
```

### 7.2 What You Must Do Manually

The following tasks require human action and cannot be automated by Claude:

#### Tool Installation (One-Time Setup)

```powershell
# DevSkim CLI
dotnet tool install -g Microsoft.CST.DevSkim.CLI

# Semgrep (choose one method)
pip install semgrep          # Python
scoop install semgrep        # Scoop (Windows)
winget install Semgrep.Semgrep  # WinGet

# Optional: SonarQube (requires Docker or dedicated server)
docker pull sonarqube:community
```

#### Server Configuration (If Using SonarQube)

1. Deploy SonarQube Community Edition (Docker or standalone)
2. Create a project in SonarQube dashboard
3. Generate authentication token
4. Add GitHub secrets: `SONAR_TOKEN`, `SONAR_HOST_URL`

#### Running Analysis and Triaging Results

- Execute `.\scripts\Analyze-Local.ps1` and review output
- Triage findings — determine which are true positives vs. false positives
- Add suppressions with justifications for confirmed false positives
- Fix confirmed issues

#### Manual Code Review (Cannot Be Automated)

The P/Invoke audit script (`Find-PInvoke.ps1`) locates all interop code, but **human judgment is required** to verify:

- [ ] Handle lifetime correctness
- [ ] Marshaling attribute accuracy
- [ ] SetLastError usage patterns
- [ ] Memory safety (pinning, allocation, cleanup)

This is particularly critical for this project given the security-sensitive nature of WFP integration.

#### Git Hook Activation

```powershell
# Enable the pre-commit hook (after Claude creates it)
git config core.hooksPath .githooks
```

### 7.3 Implementation Prompt

To have Claude implement this plan, use the following prompt:

```
Implement the static analysis configuration from docs/STATIC_ANALYSIS_PLAN.md.

Create the following files:
1. .globalconfig (analyzer rule configuration)
2. Directory.Build.props (add analyzer packages)
3. Directory.Build.targets (MSBuild integration)
4. .semgrep.yml (custom WFP rules)
5. scripts/Analyze-Local.ps1 (local analysis runner)
6. scripts/Find-PInvoke.ps1 (P/Invoke audit)
7. .github/workflows/static-analysis.yml (CI pipeline)

Do not modify any existing .csproj files directly — use Directory.Build.props
to apply settings solution-wide.

After creating files, verify the solution still builds:
  dotnet build
```

### 7.4 Expected Outcome

After implementation:

| Component | State |
|-----------|-------|
| Roslyn analyzers | Active on every build (warnings visible in IDE and CLI) |
| Security Code Scan | Active on every build |
| Roslynator | Active on every build |
| Microsoft.Interop.Analyzers | Active on every build |
| DevSkim | Available via `.\scripts\Analyze-Local.ps1` |
| Semgrep | Available via `.\scripts\Analyze-Local.ps1` |
| P/Invoke audit | Available via `.\scripts\Find-PInvoke.ps1` |
| CI pipeline | Ready (triggers on push/PR to main) |
| Pre-commit hook | Ready (activate with `git config core.hooksPath .githooks`) |

### 7.5 Post-Implementation Checklist

After Claude implements the configuration:

- [ ] Run `dotnet restore` to fetch analyzer packages
- [ ] Run `dotnet build` — expect analyzer warnings (this is expected on first run)
- [ ] Install DevSkim: `dotnet tool install -g Microsoft.CST.DevSkim.CLI`
- [ ] Install Semgrep: `pip install semgrep`
- [ ] Run `.\scripts\Analyze-Local.ps1` and review initial findings
- [ ] Triage findings and add suppressions where appropriate
- [ ] Run `.\scripts\Find-PInvoke.ps1` and conduct manual P/Invoke review
- [ ] Commit configuration files
- [ ] Verify CI pipeline runs on next push

---

## Quick Start Commands

```powershell
# Install global tools
dotnet tool install -g Microsoft.CST.DevSkim.CLI
pip install semgrep  # or: scoop install semgrep

# Run full local analysis
.\scripts\Analyze-Local.ps1

# Run quick check before commit
.\scripts\Analyze-Local.ps1 -Quick

# Run with auto-fix
.\scripts\Analyze-Local.ps1 -Fix

# Run just security tools
devskim analyze -I src/ --severity critical,important
semgrep --config=p/security-audit src/

# Check P/Invoke declarations
.\scripts\Find-PInvoke.ps1 -Path src/
```

---

## Related Documentation

- [025-testing-strategy.md](features/025-testing-strategy.md) — Integration and functional testing
- [019-ipc-security.md](features/019-ipc-security.md) — IPC security model
- [ARCHITECTURE_DIAGRAMS.md](ARCHITECTURE_DIAGRAMS.md) — System architecture
- [EXECUTIVE_SUMMARY.md](EXECUTIVE_SUMMARY.md) — Project overview
