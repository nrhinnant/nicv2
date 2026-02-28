#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes CA1707 warnings by removing underscores from method names.

.DESCRIPTION
    This script processes C# test and benchmark files to rename methods
    that use underscore-separated naming (e.g., Method_Name_Test) to
    PascalCase (e.g., MethodNameTest).

.PARAMETER Path
    Path to process. Defaults to tests and benchmarks directories.

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
    $files += Get-ChildItem -Path (Join-Path $root "tests") -Filter "*.cs" -Recurse
    $files += Get-ChildItem -Path (Join-Path $root "benchmarks") -Filter "*.cs" -Recurse
}

$totalChanges = 0

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content

    # Pattern to match method declarations with underscores
    # Matches: public void Method_Name() or public async Task Method_Name()
    # Also matches [Fact], [Theory], [Benchmark] attributed methods

    # Find all method names with underscores
    $methodPattern = '(?<=(?:public|private|protected|internal)\s+(?:async\s+)?(?:void|Task|Task<[^>]+>|[A-Za-z_][A-Za-z0-9_<>]*)\s+)([A-Za-z_][A-Za-z0-9]*(?:_[A-Za-z0-9]+)+)(?=\s*[\(<])'

    $matches = [regex]::Matches($content, $methodPattern)

    if ($matches.Count -gt 0) {
        Write-Host "Processing: $($file.Name) - Found $($matches.Count) methods with underscores" -ForegroundColor Yellow

        # Build replacement map (to avoid replacing same name multiple times differently)
        $replacements = @{}
        foreach ($match in $matches) {
            $oldName = $match.Value
            if (-not $replacements.ContainsKey($oldName)) {
                # Convert underscore_separated to PascalCase
                # Split by underscore, capitalize each part, join
                $parts = $oldName -split '_'
                $newName = ($parts | ForEach-Object {
                    if ($_.Length -gt 0) {
                        $_.Substring(0,1).ToUpper() + $_.Substring(1)
                    }
                }) -join ''

                $replacements[$oldName] = $newName
            }
        }

        # Apply replacements
        foreach ($oldName in $replacements.Keys) {
            $newName = $replacements[$oldName]
            if ($WhatIf) {
                Write-Host "  Would rename: $oldName -> $newName" -ForegroundColor Cyan
            } else {
                # Replace the method name wherever it appears (declaration and calls)
                $content = $content -replace "\b$([regex]::Escape($oldName))\b", $newName
            }
            $totalChanges++
        }

        if (-not $WhatIf -and $content -ne $originalContent) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            Write-Host "  Updated: $($file.Name)" -ForegroundColor Green
        }
    }
}

Write-Host ""
if ($WhatIf) {
    Write-Host "Would make $totalChanges method name changes" -ForegroundColor Yellow
} else {
    Write-Host "Made $totalChanges method name changes" -ForegroundColor Green
}
