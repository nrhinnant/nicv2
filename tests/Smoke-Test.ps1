#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Smoke test for WfpTrafficControl service lifecycle.

.DESCRIPTION
    Tests the full service lifecycle: install, start, verify running, stop, uninstall.
    Run this in a development VM to validate service hosting works correctly.

.PARAMETER InstallPath
    Temporary installation path for the test.
    Default: C:\WfpTrafficControlTest

.PARAMETER KeepOnFailure
    If specified, does not uninstall on test failure (for debugging).

.EXAMPLE
    .\Smoke-Test.ps1

.EXAMPLE
    .\Smoke-Test.ps1 -KeepOnFailure
#>

param(
    [string]$InstallPath = "C:\WfpTrafficControlTest",
    [switch]$KeepOnFailure
)

$ErrorActionPreference = "Stop"

$ServiceName = "WfpTrafficControl"
$TestsPassed = 0
$TestsFailed = 0
$TestResults = @()

function Write-TestHeader {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " WfpTrafficControl Service Smoke Test" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Install Path: $InstallPath" -ForegroundColor Gray
    Write-Host "Timestamp:    $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host ""
}

function Write-Test {
    param([string]$Name, [string]$Status, [string]$Message = "")

    $script:TestResults += @{
        Name = $Name
        Status = $Status
        Message = $Message
    }

    if ($Status -eq "PASS") {
        $script:TestsPassed++
        Write-Host "[PASS] " -ForegroundColor Green -NoNewline
        Write-Host $Name -ForegroundColor White
    } elseif ($Status -eq "FAIL") {
        $script:TestsFailed++
        Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
        Write-Host $Name -ForegroundColor White
        if ($Message) {
            Write-Host "       $Message" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[....] " -ForegroundColor Gray -NoNewline
        Write-Host $Name -ForegroundColor White
    }
}

function Write-TestSummary {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Test Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Passed: $TestsPassed" -ForegroundColor Green
    Write-Host "Failed: $TestsFailed" -ForegroundColor $(if ($TestsFailed -gt 0) { "Red" } else { "Green" })
    Write-Host "Total:  $($TestsPassed + $TestsFailed)" -ForegroundColor White
    Write-Host ""

    if ($TestsFailed -eq 0) {
        Write-Host "All tests passed!" -ForegroundColor Green
    } else {
        Write-Host "Some tests failed." -ForegroundColor Red
    }
    Write-Host ""
}

function Cleanup {
    param([bool]$Force = $false)

    if (-not $Force -and $KeepOnFailure -and $TestsFailed -gt 0) {
        Write-Host ""
        Write-Host "Keeping service installed for debugging (-KeepOnFailure specified)" -ForegroundColor Yellow
        Write-Host "Run manually: .\scripts\Uninstall-Service.ps1 -InstallPath '$InstallPath' -RemoveFiles" -ForegroundColor Gray
        return
    }

    Write-Host ""
    Write-Host "Cleaning up..." -ForegroundColor Gray

    # Stop service if running
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc -and $svc.Status -eq 'Running') {
        sc.exe stop $ServiceName 2>&1 | Out-Null
        Start-Sleep -Seconds 2
    }

    # Delete service
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
        sc.exe delete $ServiceName 2>&1 | Out-Null
        Start-Sleep -Seconds 1
    }

    # Remove files
    if (Test-Path $InstallPath) {
        Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Cleanup complete." -ForegroundColor Gray
}

# Main test sequence
try {
    Write-TestHeader

    # Determine paths
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $RepoRoot = Split-Path -Parent $ScriptDir
    $ScriptsDir = Join-Path $RepoRoot "scripts"
    $ServiceProject = Join-Path $RepoRoot "src\service\Service.csproj"
    $ExeName = "WfpTrafficControl.Service.exe"
    $ExePath = Join-Path $InstallPath $ExeName

    # Ensure clean state
    Write-Host "Ensuring clean state..." -ForegroundColor Gray
    Cleanup -Force $true
    Write-Host ""

    # ================================================================
    # TEST 1: Build and publish service
    # ================================================================
    Write-Host "--- Test 1: Build and Publish ---" -ForegroundColor Cyan

    try {
        $buildOutput = dotnet publish $ServiceProject -c Release -r win-x64 --self-contained false -o "$InstallPath" 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }

        if (Test-Path $ExePath) {
            Write-Test "Build and publish succeeded" "PASS"
        } else {
            Write-Test "Build and publish succeeded" "FAIL" "Executable not found at $ExePath"
        }
    } catch {
        Write-Test "Build and publish succeeded" "FAIL" $_.Exception.Message
        throw
    }

    # ================================================================
    # TEST 2: Install service
    # ================================================================
    Write-Host ""
    Write-Host "--- Test 2: Install Service ---" -ForegroundColor Cyan

    try {
        $binPath = "`"$ExePath`""
        sc.exe create $ServiceName binPath= $binPath start= demand displayname= "WFP Traffic Control Service (Test)" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "sc.exe create failed with exit code $LASTEXITCODE"
        }

        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc) {
            Write-Test "Service installed" "PASS"
        } else {
            Write-Test "Service installed" "FAIL" "Service not found after creation"
        }
    } catch {
        Write-Test "Service installed" "FAIL" $_.Exception.Message
        throw
    }

    # ================================================================
    # TEST 3: Start service
    # ================================================================
    Write-Host ""
    Write-Host "--- Test 3: Start Service ---" -ForegroundColor Cyan

    try {
        sc.exe start $ServiceName 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "sc.exe start failed with exit code $LASTEXITCODE"
        }

        # Wait for service to start (max 10 seconds)
        $timeout = 10
        $elapsed = 0
        while ($elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
            $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($svc.Status -eq 'Running') {
                break
            }
        }

        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc.Status -eq 'Running') {
            Write-Test "Service started" "PASS"
        } else {
            Write-Test "Service started" "FAIL" "Service status: $($svc.Status)"
        }
    } catch {
        Write-Test "Service started" "FAIL" $_.Exception.Message
        # Don't throw - continue with remaining tests
    }

    # ================================================================
    # TEST 4: Verify service is running
    # ================================================================
    Write-Host ""
    Write-Host "--- Test 4: Verify Running ---" -ForegroundColor Cyan

    try {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -eq 'Running') {
            Write-Test "Service status is Running" "PASS"

            # Check process is actually running
            $proc = Get-Process -Name "WfpTrafficControl.Service" -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Test "Service process exists" "PASS"
            } else {
                Write-Test "Service process exists" "FAIL" "Process not found"
            }
        } else {
            Write-Test "Service status is Running" "FAIL" "Service status: $($svc.Status)"
            Write-Test "Service process exists" "FAIL" "Skipped (service not running)"
        }
    } catch {
        Write-Test "Service status is Running" "FAIL" $_.Exception.Message
    }

    # ================================================================
    # TEST 5: Stop service
    # ================================================================
    Write-Host ""
    Write-Host "--- Test 5: Stop Service ---" -ForegroundColor Cyan

    try {
        sc.exe stop $ServiceName 2>&1 | Out-Null

        # Wait for service to stop (max 10 seconds)
        $timeout = 10
        $elapsed = 0
        while ($elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
            $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if ($svc.Status -eq 'Stopped') {
                break
            }
        }

        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($svc.Status -eq 'Stopped') {
            Write-Test "Service stopped" "PASS"
        } else {
            Write-Test "Service stopped" "FAIL" "Service status: $($svc.Status)"
        }
    } catch {
        Write-Test "Service stopped" "FAIL" $_.Exception.Message
    }

    # ================================================================
    # TEST 6: Uninstall service
    # ================================================================
    Write-Host ""
    Write-Host "--- Test 6: Uninstall Service ---" -ForegroundColor Cyan

    try {
        sc.exe delete $ServiceName 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "sc.exe delete failed with exit code $LASTEXITCODE"
        }

        Start-Sleep -Seconds 1

        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if (-not $svc) {
            Write-Test "Service uninstalled" "PASS"
        } else {
            Write-Test "Service uninstalled" "FAIL" "Service still exists"
        }
    } catch {
        Write-Test "Service uninstalled" "FAIL" $_.Exception.Message
    }

    # Final cleanup (remove files)
    if (Test-Path $InstallPath) {
        Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-TestSummary

    if ($TestsFailed -gt 0) {
        exit 1
    }
    exit 0

} catch {
    Write-Host ""
    Write-Host "Test execution failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""

    Cleanup

    Write-TestSummary
    exit 1
}
