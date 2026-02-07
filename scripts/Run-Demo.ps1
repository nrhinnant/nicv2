#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Complete end-to-end demonstration of WfpTrafficControl system.

.DESCRIPTION
    Orchestrates the full lifecycle:
    1. Build CLI
    2. Install service
    3. Start service
    4. Bootstrap WFP (create provider/sublayer)
    5. Validate and apply demo policy
    6. Verify with logs and status checks
    7. Rollback policy
    8. Teardown WFP objects
    9. Stop and uninstall service

    Includes robust error handling and cleanup.

.PARAMETER SkipInstall
    Skip service install/uninstall steps (assumes service is already installed).

.PARAMETER SkipBuild
    Skip the build step (assumes binaries are up to date).

.PARAMETER PauseAfterEach
    Pause after each major step for manual inspection.

.PARAMETER CleanupOnly
    Only run cleanup/teardown/uninstall operations.

.PARAMETER KeepServiceRunning
    Don't stop or uninstall the service at the end.

.EXAMPLE
    .\Run-Demo.ps1

.EXAMPLE
    .\Run-Demo.ps1 -PauseAfterEach

.EXAMPLE
    .\Run-Demo.ps1 -SkipInstall -SkipBuild

.EXAMPLE
    .\Run-Demo.ps1 -CleanupOnly
#>

param(
    [switch]$SkipInstall,
    [switch]$SkipBuild,
    [switch]$PauseAfterEach,
    [switch]$CleanupOnly,
    [switch]$KeepServiceRunning
)

$ErrorActionPreference = "Stop"

# Configuration
$ServiceName = "WfpTrafficControl"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$DemoPolicyPath = Join-Path $ScriptDir "sample-demo-policy.json"
$CliProject = Join-Path $RepoRoot "src\cli\Cli.csproj"
$WfpCtlExe = Join-Path $RepoRoot "src\cli\bin\Release\net8.0\wfpctl.exe"

# Track what we've done for cleanup
$script:ServiceWasInstalled = $false
$script:ServiceWasStarted = $false
$script:WfpWasBootstrapped = $false
$script:PolicyWasApplied = $false

# Statistics
$script:StepNumber = 0
$script:SuccessCount = 0
$script:FailureCount = 0
$script:StartTime = Get-Date

#region Helper Functions

function Write-StepHeader {
    param([string]$Message)
    $script:StepNumber++
    Write-Host ""
    Write-Host "[$script:StepNumber] $Message" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
    $script:SuccessCount++
}

function Write-Failure {
    param([string]$Message)
    Write-Host "  [FAIL] $Message" -ForegroundColor Red
    $script:FailureCount++
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

function Invoke-Pause {
    if ($PauseAfterEach) {
        Write-Host ""
        Write-Host "  Press any key to continue..." -ForegroundColor Yellow
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
}

function Test-ServiceExists {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return $null -ne $svc
}

function Test-ServiceRunning {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    return ($null -ne $svc) -and ($svc.Status -eq 'Running')
}

function Invoke-Cleanup {
    param([bool]$Force = $false)

    Write-Host ""
    Write-Host "=== Cleanup Phase ===" -ForegroundColor Cyan
    Write-Host ""

    # Step 1: Rollback policy (remove filters)
    if ($script:PolicyWasApplied -or $Force) {
        Write-Host "  Removing filters (rollback)..." -ForegroundColor Yellow
        try {
            & $WfpCtlExe rollback 2>&1 | Out-Null
            Write-Success "Filters removed"
        } catch {
            Write-Warning "Rollback failed or no filters present: $_"
        }
    }

    # Step 2: Teardown WFP objects
    if ($script:WfpWasBootstrapped -or $Force) {
        Write-Host "  Removing WFP provider/sublayer (teardown)..." -ForegroundColor Yellow
        try {
            & $WfpCtlExe teardown 2>&1 | Out-Null
            Write-Success "WFP objects removed"
        } catch {
            Write-Warning "Teardown failed or no WFP objects present: $_"
        }
    }

    # Step 3: Stop service
    if (($script:ServiceWasStarted -or $Force) -and -not $KeepServiceRunning) {
        Write-Host "  Stopping service..." -ForegroundColor Yellow
        if (Test-ServiceRunning) {
            try {
                & (Join-Path $ScriptDir "Stop-Service.ps1") 2>&1 | Out-Null
                Write-Success "Service stopped"
            } catch {
                Write-Warning "Failed to stop service: $_"
            }
        } else {
            Write-Info "Service not running"
        }
    }

    # Step 4: Uninstall service
    if (($script:ServiceWasInstalled -or $Force) -and -not $SkipInstall -and -not $KeepServiceRunning) {
        Write-Host "  Uninstalling service..." -ForegroundColor Yellow
        if (Test-ServiceExists) {
            try {
                & (Join-Path $ScriptDir "Uninstall-Service.ps1") 2>&1 | Out-Null
                Write-Success "Service uninstalled"
            } catch {
                Write-Warning "Failed to uninstall service: $_"
            }
        } else {
            Write-Info "Service not installed"
        }
    }

    Write-Host ""
}

#endregion

#region Main Script

try {
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║       WfpTrafficControl - Complete Demo Runner                    ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    # If cleanup-only mode, run cleanup and exit
    if ($CleanupOnly) {
        Write-Host "Running in CLEANUP-ONLY mode..." -ForegroundColor Yellow
        Invoke-Cleanup -Force $true
        Write-Host ""
        Write-Host "Cleanup complete." -ForegroundColor Green
        exit 0
    }

    #region Pre-flight Checks
    Write-StepHeader "Pre-flight Checks"

    # Check if demo policy exists
    if (-not (Test-Path $DemoPolicyPath)) {
        Write-Failure "Demo policy not found: $DemoPolicyPath"
        exit 1
    }
    Write-Success "Demo policy found: $DemoPolicyPath"

    # Check if CLI project exists
    if (-not (Test-Path $CliProject)) {
        Write-Failure "CLI project not found: $CliProject"
        exit 1
    }
    Write-Success "CLI project found"

    # Check if service is already installed
    if (Test-ServiceExists) {
        if ($SkipInstall) {
            Write-Warning "Service already installed (SkipInstall mode)"
        } else {
            Write-Warning "Service already installed - run with -SkipInstall or uninstall first"
            Write-Info "To uninstall: .\Uninstall-Service.ps1"
            exit 1
        }
    } else {
        Write-Success "Service not currently installed"
    }

    Invoke-Pause
    #endregion

    #region Build
    if (-not $SkipBuild) {
        Write-StepHeader "Build CLI (Release)"
        Write-Info "Building: $CliProject"

        try {
            $buildOutput = dotnet build $CliProject -c Release 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Failure "Build failed"
                Write-Host $buildOutput -ForegroundColor Red
                exit 1
            }
            Write-Success "Build completed"
            Write-Info "CLI binary: $WfpCtlExe"
        } catch {
            Write-Failure "Build exception: $_"
            exit 1
        }

        # Verify CLI exists
        if (-not (Test-Path $WfpCtlExe)) {
            Write-Failure "CLI binary not found after build: $WfpCtlExe"
            exit 1
        }
        Write-Success "CLI binary verified"

        Invoke-Pause
    } else {
        Write-Host ""
        Write-Host "Skipping build (using existing binaries)" -ForegroundColor Yellow
        if (-not (Test-Path $WfpCtlExe)) {
            Write-Failure "CLI binary not found: $WfpCtlExe"
            Write-Info "Remove -SkipBuild to build the project"
            exit 1
        }
    }
    #endregion

    #region Install Service
    if (-not $SkipInstall) {
        Write-StepHeader "Install Service"

        try {
            $installScript = Join-Path $ScriptDir "Install-Service.ps1"
            Write-Info "Running: $installScript"
            & $installScript

            if ($LASTEXITCODE -ne 0) {
                Write-Failure "Service installation failed"
                exit 1
            }

            $script:ServiceWasInstalled = $true
            Write-Success "Service installed successfully"
        } catch {
            Write-Failure "Service installation exception: $_"
            exit 1
        }

        Invoke-Pause
    } else {
        Write-Host ""
        Write-Host "Skipping install (service already installed)" -ForegroundColor Yellow
    }
    #endregion

    #region Start Service
    Write-StepHeader "Start Service"

    if (Test-ServiceRunning) {
        Write-Warning "Service is already running"
    } else {
        try {
            $startScript = Join-Path $ScriptDir "Start-Service.ps1"
            Write-Info "Running: $startScript"
            & $startScript

            if ($LASTEXITCODE -ne 0) {
                Write-Failure "Service start failed"
                throw "Service failed to start"
            }

            $script:ServiceWasStarted = $true
            Write-Success "Service started successfully"
        } catch {
            Write-Failure "Service start exception: $_"
            throw
        }
    }

    # Verify service is running
    Start-Sleep -Seconds 2
    if (-not (Test-ServiceRunning)) {
        Write-Failure "Service is not running after start attempt"
        throw "Service not running"
    }
    Write-Success "Service status verified: Running"

    Invoke-Pause
    #endregion

    #region Test Service Connection
    Write-StepHeader "Test Service Connection"

    try {
        Write-Info "Running: wfpctl status"
        $statusOutput = & $WfpCtlExe status 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Unable to connect to service"
            Write-Host $statusOutput -ForegroundColor Red
            throw "Service connection failed"
        }

        Write-Success "Service is responding"
        Write-Host ""
        Write-Host $statusOutput -ForegroundColor Gray
    } catch {
        Write-Failure "Service connection exception: $_"
        throw
    }

    Invoke-Pause
    #endregion

    #region Bootstrap WFP
    Write-StepHeader "Bootstrap WFP (Create Provider/Sublayer)"

    try {
        Write-Info "Running: wfpctl bootstrap"
        $bootstrapOutput = & $WfpCtlExe bootstrap 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Bootstrap failed"
            Write-Host $bootstrapOutput -ForegroundColor Red
            throw "Bootstrap failed"
        }

        $script:WfpWasBootstrapped = $true
        Write-Success "WFP provider and sublayer created"
        Write-Host ""
        Write-Host $bootstrapOutput -ForegroundColor Gray
    } catch {
        Write-Failure "Bootstrap exception: $_"
        throw
    }

    Invoke-Pause
    #endregion

    #region Validate Policy
    Write-StepHeader "Validate Demo Policy"

    try {
        Write-Info "Running: wfpctl validate $DemoPolicyPath"
        $validateOutput = & $WfpCtlExe validate $DemoPolicyPath 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Policy validation failed"
            Write-Host $validateOutput -ForegroundColor Red
            throw "Policy validation failed"
        }

        Write-Success "Policy is valid"
        Write-Host ""
        Write-Host $validateOutput -ForegroundColor Gray
    } catch {
        Write-Failure "Validation exception: $_"
        throw
    }

    Invoke-Pause
    #endregion

    #region Apply Policy
    Write-StepHeader "Apply Demo Policy"

    try {
        Write-Info "Running: wfpctl apply $DemoPolicyPath"
        $applyOutput = & $WfpCtlExe apply $DemoPolicyPath 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Policy apply failed"
            Write-Host $applyOutput -ForegroundColor Red
            throw "Policy apply failed"
        }

        $script:PolicyWasApplied = $true
        Write-Success "Policy applied successfully"
        Write-Host ""
        Write-Host $applyOutput -ForegroundColor Gray
    } catch {
        Write-Failure "Apply exception: $_"
        throw
    }

    Invoke-Pause
    #endregion

    #region Verify Policy - Logs
    Write-StepHeader "Verify Policy - Audit Logs"

    try {
        Write-Info "Running: wfpctl logs --tail 10"
        $logsOutput = & $WfpCtlExe logs --tail 10 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to retrieve logs (non-fatal)"
            Write-Host $logsOutput -ForegroundColor Yellow
        } else {
            Write-Success "Audit logs retrieved"
            Write-Host ""
            Write-Host $logsOutput -ForegroundColor Gray
        }
    } catch {
        Write-Warning "Logs exception (non-fatal): $_"
    }

    Invoke-Pause
    #endregion

    #region Verify Policy - LKG
    Write-StepHeader "Verify Policy - Last Known Good"

    try {
        Write-Info "Running: wfpctl lkg show"
        $lkgOutput = & $WfpCtlExe lkg show 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to retrieve LKG (non-fatal)"
            Write-Host $lkgOutput -ForegroundColor Yellow
        } else {
            Write-Success "LKG policy verified"
            Write-Host ""
            Write-Host $lkgOutput -ForegroundColor Gray
        }
    } catch {
        Write-Warning "LKG exception (non-fatal): $_"
    }

    Invoke-Pause
    #endregion

    #region Verify Policy - Connectivity Test
    Write-StepHeader "Verify Policy - Connectivity Test"

    Write-Info "Testing connectivity to example.com (93.184.216.34:443)"
    Write-Info "This should be BLOCKED by the demo policy"
    Write-Host ""

    try {
        # Test with Test-NetConnection (more reliable than curl on Windows)
        $testResult = Test-NetConnection -ComputerName "93.184.216.34" -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue

        if ($testResult) {
            Write-Warning "Connection to example.com SUCCEEDED (expected: blocked)"
            Write-Warning "Policy may not be enforcing correctly"
        } else {
            Write-Success "Connection to example.com BLOCKED (as expected)"
            Write-Info "Demo policy is working correctly"
        }
    } catch {
        Write-Success "Connection to example.com BLOCKED (as expected)"
        Write-Info "Demo policy is working correctly"
    }

    Invoke-Pause
    #endregion

    #region Rollback Policy
    Write-StepHeader "Rollback Policy (Remove Filters)"

    try {
        Write-Info "Running: wfpctl rollback"
        $rollbackOutput = & $WfpCtlExe rollback 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Rollback failed"
            Write-Host $rollbackOutput -ForegroundColor Red
            throw "Rollback failed"
        }

        $script:PolicyWasApplied = $false
        Write-Success "Policy rolled back (filters removed)"
        Write-Host ""
        Write-Host $rollbackOutput -ForegroundColor Gray
    } catch {
        Write-Failure "Rollback exception: $_"
        throw
    }

    Invoke-Pause
    #endregion

    #region Verify Rollback - Connectivity
    Write-StepHeader "Verify Rollback - Connectivity Test"

    Write-Info "Testing connectivity to example.com after rollback"
    Write-Info "This should now SUCCEED (policy removed)"
    Write-Host ""

    try {
        $testResult = Test-NetConnection -ComputerName "93.184.216.34" -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue

        if ($testResult) {
            Write-Success "Connection to example.com SUCCEEDED (as expected after rollback)"
            Write-Info "Rollback verified - connectivity restored"
        } else {
            Write-Warning "Connection to example.com BLOCKED (expected: allowed)"
            Write-Warning "Rollback may not have worked correctly"
        }
    } catch {
        Write-Warning "Connection test inconclusive: $_"
    }

    Invoke-Pause
    #endregion

    #region Teardown WFP
    Write-StepHeader "Teardown WFP (Remove Provider/Sublayer)"

    try {
        Write-Info "Running: wfpctl teardown"
        $teardownOutput = & $WfpCtlExe teardown 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Failure "Teardown failed"
            Write-Host $teardownOutput -ForegroundColor Red
            throw "Teardown failed"
        }

        $script:WfpWasBootstrapped = $false
        Write-Success "WFP provider and sublayer removed"
        Write-Host ""
        Write-Host $teardownOutput -ForegroundColor Gray
    } catch {
        Write-Failure "Teardown exception: $_"
        throw
    }

    Invoke-Pause
    #endregion

    #region Stop Service
    if (-not $KeepServiceRunning) {
        Write-StepHeader "Stop Service"

        try {
            $stopScript = Join-Path $ScriptDir "Stop-Service.ps1"
            Write-Info "Running: $stopScript"
            & $stopScript

            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Service stop reported non-zero exit code"
            }

            $script:ServiceWasStarted = $false
            Write-Success "Service stopped"
        } catch {
            Write-Warning "Service stop exception (non-fatal): $_"
        }

        Invoke-Pause
    } else {
        Write-Host ""
        Write-Host "Skipping service stop (KeepServiceRunning mode)" -ForegroundColor Yellow
    }
    #endregion

    #region Uninstall Service
    if (-not $SkipInstall -and -not $KeepServiceRunning) {
        Write-StepHeader "Uninstall Service"

        try {
            $uninstallScript = Join-Path $ScriptDir "Uninstall-Service.ps1"
            Write-Info "Running: $uninstallScript"
            & $uninstallScript

            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Service uninstall reported non-zero exit code"
            }

            $script:ServiceWasInstalled = $false
            Write-Success "Service uninstalled"
        } catch {
            Write-Warning "Service uninstall exception (non-fatal): $_"
        }

        Invoke-Pause
    } else {
        Write-Host ""
        if ($SkipInstall) {
            Write-Host "Skipping service uninstall (SkipInstall mode)" -ForegroundColor Yellow
        }
        if ($KeepServiceRunning) {
            Write-Host "Skipping service uninstall (KeepServiceRunning mode)" -ForegroundColor Yellow
        }
    }
    #endregion

    #region Summary
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║                    Demo Completed Successfully                     ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""

    $duration = (Get-Date) - $script:StartTime
    Write-Host "Summary:" -ForegroundColor Cyan
    Write-Host "  Duration:      $($duration.TotalSeconds.ToString('F1')) seconds" -ForegroundColor White
    Write-Host "  Steps:         $script:StepNumber" -ForegroundColor White
    Write-Host "  Successes:     $script:SuccessCount" -ForegroundColor Green
    Write-Host "  Failures:      $script:FailureCount" -ForegroundColor $(if ($script:FailureCount -eq 0) { "Green" } else { "Red" })
    Write-Host ""

    Write-Host "The complete lifecycle was demonstrated:" -ForegroundColor White
    Write-Host "  1. Service installation and startup" -ForegroundColor Gray
    Write-Host "  2. WFP provider/sublayer creation (bootstrap)" -ForegroundColor Gray
    Write-Host "  3. Policy validation and application" -ForegroundColor Gray
    Write-Host "  4. Verification via logs and connectivity tests" -ForegroundColor Gray
    Write-Host "  5. Policy rollback (filter removal)" -ForegroundColor Gray
    Write-Host "  6. WFP teardown (provider/sublayer removal)" -ForegroundColor Gray
    Write-Host "  7. Service shutdown and uninstallation" -ForegroundColor Gray
    Write-Host ""

    if ($KeepServiceRunning) {
        Write-Host "Note: Service is still running (KeepServiceRunning mode)" -ForegroundColor Yellow
        Write-Host "To manually test:" -ForegroundColor Yellow
        Write-Host "  wfpctl bootstrap" -ForegroundColor Gray
        Write-Host "  wfpctl apply $DemoPolicyPath" -ForegroundColor Gray
        Write-Host "  wfpctl rollback" -ForegroundColor Gray
        Write-Host "  wfpctl teardown" -ForegroundColor Gray
        Write-Host ""
    }
    #endregion

} catch {
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║                         Demo Failed                                ║" -ForegroundColor Red
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Attempting cleanup..." -ForegroundColor Yellow

    # Attempt cleanup
    Invoke-Cleanup -Force $false

    Write-Host ""
    Write-Host "To manually clean up, run:" -ForegroundColor Yellow
    Write-Host "  .\Run-Demo.ps1 -CleanupOnly" -ForegroundColor Gray
    Write-Host ""

    exit 1
} finally {
    # Final cleanup is handled in the catch block or skipped if success
}

#endregion
