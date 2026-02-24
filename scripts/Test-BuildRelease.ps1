#Requires -Version 5.1
#Requires -Modules Pester
<#
.SYNOPSIS
    Tests for the Build-Release.ps1 script.

.DESCRIPTION
    Pester tests for validating Build-Release.ps1 parameter validation,
    helper functions, and expected behavior.

.EXAMPLE
    Invoke-Pester -Path .\Test-BuildRelease.ps1
#>

BeforeAll {
    $ScriptDir = Split-Path -Parent $PSCommandPath
    $RepoRoot = Split-Path -Parent $ScriptDir
    $BuildReleaseScript = Join-Path $ScriptDir "Build-Release.ps1"
}

Describe "Build-Release.ps1" {
    Context "Script Validation" {
        It "Script file exists" {
            Test-Path $BuildReleaseScript | Should -Be $true
        }

        It "Script has valid PowerShell syntax" {
            $errors = $null
            [System.Management.Automation.PSParser]::Tokenize((Get-Content $BuildReleaseScript -Raw), [ref]$errors)
            $errors.Count | Should -Be 0
        }

        It "Script has required comment-based help" {
            $help = Get-Help $BuildReleaseScript -Full
            $help.Synopsis | Should -Not -BeNullOrEmpty
            $help.Description | Should -Not -BeNullOrEmpty
        }
    }

    Context "Version Parameter Validation" {
        BeforeAll {
            $versionParam = (Get-Command $BuildReleaseScript).Parameters['Version']
            $validatePattern = $versionParam.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidatePatternAttribute] }
            $pattern = $validatePattern.RegexPattern
        }

        It "Has ValidatePattern attribute" {
            $validatePattern | Should -Not -BeNullOrEmpty
        }

        It "Pattern accepts valid two-part version (1.0)" {
            "1.0" -match $pattern | Should -Be $true
        }

        It "Pattern accepts valid three-part version (1.0.0)" {
            "1.0.0" -match $pattern | Should -Be $true
        }

        It "Pattern accepts valid version (10.20.30)" {
            "10.20.30" -match $pattern | Should -Be $true
        }

        It "Pattern rejects invalid version format (no dots)" {
            "100" -match $pattern | Should -Be $false
        }

        It "Pattern rejects invalid version format (letters)" {
            "1.0.0-beta" -match $pattern | Should -Be $false
        }

        It "Pattern rejects invalid version format (four parts)" {
            "1.0.0.0" -match $pattern | Should -Be $false
        }
    }

    Context "Configuration Parameter Validation" {
        BeforeAll {
            $configParam = (Get-Command $BuildReleaseScript).Parameters['Configuration']
            $validateSet = $configParam.Attributes | Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] }
        }

        It "Has ValidateSet attribute" {
            $validateSet | Should -Not -BeNullOrEmpty
        }

        It "Accepts 'Release' configuration" {
            $validateSet.ValidValues -contains 'Release' | Should -Be $true
        }

        It "Accepts 'Debug' configuration" {
            $validateSet.ValidValues -contains 'Debug' | Should -Be $true
        }

        It "Does not accept 'Invalid' configuration" {
            $validateSet.ValidValues -contains 'Invalid' | Should -Be $false
        }
    }

    Context "Switch Parameters" {
        It "Accepts -SkipTests switch" {
            (Get-Command $BuildReleaseScript).Parameters.ContainsKey('SkipTests') | Should -Be $true
            (Get-Command $BuildReleaseScript).Parameters['SkipTests'].SwitchParameter | Should -Be $true
        }

        It "Accepts -SkipMsi switch" {
            (Get-Command $BuildReleaseScript).Parameters.ContainsKey('SkipMsi') | Should -Be $true
            (Get-Command $BuildReleaseScript).Parameters['SkipMsi'].SwitchParameter | Should -Be $true
        }

        It "Accepts -SkipArchive switch" {
            (Get-Command $BuildReleaseScript).Parameters.ContainsKey('SkipArchive') | Should -Be $true
            (Get-Command $BuildReleaseScript).Parameters['SkipArchive'].SwitchParameter | Should -Be $true
        }
    }

    Context "Helper Functions (extracted for testing)" {
        BeforeAll {
            # Extract and define helper functions for isolated testing
            function Get-MajorMinorVersion {
                param([string]$VersionString)
                $parts = $VersionString -split '\.'
                if ($parts.Count -ge 2) {
                    return "$($parts[0]).$($parts[1])"
                }
                return $VersionString
            }

            function Format-Size {
                param([long]$Bytes)
                if ($null -eq $Bytes -or $Bytes -eq 0) { return "0 bytes" }
                if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
                if ($Bytes -ge 1MB) { return "{0:N2} MB" -f ($Bytes / 1MB) }
                if ($Bytes -ge 1KB) { return "{0:N2} KB" -f ($Bytes / 1KB) }
                return "$Bytes bytes"
            }
        }

        Context "Get-MajorMinorVersion" {
            It "Extracts major.minor from X.Y.Z" {
                Get-MajorMinorVersion "1.2.3" | Should -Be "1.2"
            }

            It "Extracts major.minor from X.Y" {
                Get-MajorMinorVersion "1.2" | Should -Be "1.2"
            }

            It "Returns single part version unchanged" {
                Get-MajorMinorVersion "1" | Should -Be "1"
            }

            It "Handles larger version numbers" {
                Get-MajorMinorVersion "10.20.30" | Should -Be "10.20"
            }
        }

        Context "Format-Size" {
            It "Formats zero bytes" {
                Format-Size 0 | Should -Be "0 bytes"
            }

            It "Formats null as zero bytes" {
                Format-Size $null | Should -Be "0 bytes"
            }

            It "Formats bytes" {
                Format-Size 500 | Should -Be "500 bytes"
            }

            It "Formats kilobytes" {
                Format-Size 2048 | Should -Match "^\d+\.\d+ KB$"
            }

            It "Formats megabytes" {
                Format-Size (5 * 1MB) | Should -Match "^\d+\.\d+ MB$"
            }

            It "Formats gigabytes" {
                Format-Size (2 * 1GB) | Should -Match "^\d+\.\d+ GB$"
            }
        }
    }

    Context "Required Files" {
        It "Solution file exists" {
            Test-Path (Join-Path $RepoRoot "WfpTrafficControl.sln") | Should -Be $true
        }

        It "Service project exists" {
            Test-Path (Join-Path $RepoRoot "src\service\Service.csproj") | Should -Be $true
        }

        It "CLI project exists" {
            Test-Path (Join-Path $RepoRoot "src\cli\Cli.csproj") | Should -Be $true
        }

        It "Tests project exists" {
            Test-Path (Join-Path $RepoRoot "tests\Tests.csproj") | Should -Be $true
        }
    }

    Context "Scripts to Copy" {
        $requiredScripts = @(
            "Install-Service.ps1"
            "Uninstall-Service.ps1"
            "Start-Service.ps1"
            "Stop-Service.ps1"
        )

        foreach ($script in $requiredScripts) {
            It "Required script exists: $script" {
                Test-Path (Join-Path $ScriptDir $script) | Should -Be $true
            }
        }
    }

    Context "Documentation to Copy" {
        It "EXECUTIVE_SUMMARY.md exists" {
            Test-Path (Join-Path $RepoRoot "docs\EXECUTIVE_SUMMARY.md") | Should -Be $true
        }

        It "022-how-it-works.md exists" {
            Test-Path (Join-Path $RepoRoot "docs\features\022-how-it-works.md") | Should -Be $true
        }

        It "023-troubleshooting.md exists" {
            Test-Path (Join-Path $RepoRoot "docs\features\023-troubleshooting.md") | Should -Be $true
        }

        It "License.rtf exists" {
            Test-Path (Join-Path $RepoRoot "installer\WfpTrafficControl.Installer\License.rtf") | Should -Be $true
        }
    }
}

# Summary if run directly
if ($MyInvocation.InvocationName -ne '.') {
    Write-Host "`nTo run these tests:" -ForegroundColor Cyan
    Write-Host "  Invoke-Pester -Path '$PSCommandPath'" -ForegroundColor Gray
    Write-Host "`nTo run with detailed output:" -ForegroundColor Cyan
    Write-Host "  Invoke-Pester -Path '$PSCommandPath' -Output Detailed" -ForegroundColor Gray
}
