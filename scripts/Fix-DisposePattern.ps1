#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes CA1063/CA1816 warnings by marking IDisposable test classes as sealed
    and adding GC.SuppressFinalize to Dispose methods.

.PARAMETER Path
    Path to process. Defaults to tests directory.

.PARAMETER WhatIf
    Show what would be changed without making changes.
#>

param(
    [string]$Path,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

# Files to process
$files = @()
if ($Path) {
    $files = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse
} else {
    $files = Get-ChildItem -Path (Join-Path $root "tests") -Filter "*.cs" -Recurse
}

$totalChanges = 0

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content
    $changes = 0

    # Pattern 1: Add 'sealed' to public classes that implement IDisposable
    # Match: public class ClassName : IDisposable
    # Or: public class ClassName : SomeBase, IDisposable
    $pattern1 = '(?<!sealed\s)public class (\w+)\s*:\s*([^{]*IDisposable[^{]*)\{'
    if ($content -match $pattern1) {
        $content = $content -replace $pattern1, 'public sealed class $1 : $2{'
        $changes++
    }

    # Pattern 2: Add GC.SuppressFinalize to Dispose() methods that don't have it
    # This is trickier - we need to find Dispose() methods and add the call
    # Look for: public void Dispose() { ... } without GC.SuppressFinalize
    $disposePattern = '(public void Dispose\(\)\s*\{)'
    $suppressPattern = 'GC\.SuppressFinalize\(this\)'

    # Check if file has Dispose() without GC.SuppressFinalize
    if ($content -match $disposePattern -and $content -notmatch $suppressPattern) {
        # Add GC.SuppressFinalize at the end of Dispose methods
        # Find Dispose() { and add after the opening brace
        $content = $content -replace '(public void Dispose\(\)\s*\{)(\s*)', "`$1`$2GC.SuppressFinalize(this);`$2"
        $changes++
    }

    if ($changes -gt 0) {
        $totalChanges += $changes
        if ($WhatIf) {
            Write-Host "Would update: $($file.Name) ($changes changes)" -ForegroundColor Cyan
        } else {
            if ($content -ne $originalContent) {
                Set-Content -Path $file.FullName -Value $content -NoNewline
                Write-Host "Updated: $($file.Name) ($changes changes)" -ForegroundColor Green
            }
        }
    }
}

Write-Host ""
if ($WhatIf) {
    Write-Host "Would make $totalChanges changes" -ForegroundColor Yellow
} else {
    Write-Host "Made $totalChanges changes" -ForegroundColor Green
}
