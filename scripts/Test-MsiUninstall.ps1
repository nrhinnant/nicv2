#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Comprehensive test suite for MSI uninstallation verification.

.DESCRIPTION
    Validates that all components installed by the WfpTrafficControl MSI
    have been completely removed after uninstallation:

    - Windows Service (WfpTrafficControl)
    - Installation files (C:\Program Files\WfpTrafficControl\)
    - System PATH entry
    - WFP artifacts (Provider, Sublayer, Filters)
    - Registry entries

    This script should be run AFTER uninstalling the MSI to verify
    complete cleanup. It can also be run BEFORE installation to establish
    a clean baseline.

.PARAMETER Mode
    Test mode:
    - "PostUninstall" (default): Verify everything is removed after uninstall
    - "PreInstall": Verify clean state before installation
    - "PostInstall": Verify everything is installed correctly

.PARAMETER InstallPath
    Installation path to check. Default: C:\Program Files\WfpTrafficControl

.PARAMETER Detailed
    Show detailed output for each check.

.EXAMPLE
    # After uninstalling MSI, verify complete removal
    .\Test-MsiUninstall.ps1

.EXAMPLE
    # Verify clean state before installation
    .\Test-MsiUninstall.ps1 -Mode PreInstall

.EXAMPLE
    # Verify installation completed correctly
    .\Test-MsiUninstall.ps1 -Mode PostInstall -Detailed

.NOTES
    Must be run as Administrator to check WFP state and service registry.
#>

[CmdletBinding()]
param(
    [ValidateSet("PostUninstall", "PreInstall", "PostInstall")]
    [string]$Mode = "PostUninstall",

    [string]$InstallPath = "C:\Program Files\WfpTrafficControl",

    [switch]$Detailed
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Constants
# ============================================================================

$ServiceName = "WfpTrafficControl"
$ProviderGuid = "7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A"
$SublayerGuid = "B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C"

# Files that should be installed/removed
$ExpectedFiles = @(
    "WfpTrafficControl.Service.exe"
    "wfpctl.exe"
    "Shared.dll"
    "appsettings.json"
    "WfpTrafficControl.Service.deps.json"
    "WfpTrafficControl.Service.runtimeconfig.json"
    "wfpctl.deps.json"
    "wfpctl.runtimeconfig.json"
    "sample-policy.json"
)

# ============================================================================
# Test Result Tracking
# ============================================================================

$script:TestResults = @{
    Passed = 0
    Failed = 0
    Warnings = 0
    Details = [System.Collections.ArrayList]::new()
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message,
        [bool]$IsWarning = $false
    )

    $result = @{
        Name = $TestName
        Passed = $Passed
        Message = $Message
        IsWarning = $IsWarning
    }
    $null = $script:TestResults.Details.Add($result)

    if ($Passed) {
        $script:TestResults.Passed++
        Write-Host "[PASS] " -ForegroundColor Green -NoNewline
        Write-Host "$TestName" -ForegroundColor White
        if ($Detailed -and $Message) {
            Write-Host "       $Message" -ForegroundColor Gray
        }
    }
    elseif ($IsWarning) {
        $script:TestResults.Warnings++
        Write-Host "[WARN] " -ForegroundColor Yellow -NoNewline
        Write-Host "$TestName" -ForegroundColor White
        Write-Host "       $Message" -ForegroundColor Yellow
    }
    else {
        $script:TestResults.Failed++
        Write-Host "[FAIL] " -ForegroundColor Red -NoNewline
        Write-Host "$TestName" -ForegroundColor White
        Write-Host "       $Message" -ForegroundColor Red
    }
}

# ============================================================================
# Test Functions
# ============================================================================

function Test-ServiceRemoved {
    <#
    .SYNOPSIS
        Verify Windows service is not registered.
    #>
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

    if ($Mode -eq "PostInstall") {
        # Should exist
        if ($service) {
            Write-TestResult "Windows Service Registered" $true "Service '$ServiceName' is registered"
            # Also check if running
            if ($service.Status -eq 'Running') {
                Write-TestResult "Windows Service Running" $true "Service is running"
            }
            else {
                Write-TestResult "Windows Service Running" $false "Service is not running (status: $($service.Status))" -IsWarning:$true
            }
        }
        else {
            Write-TestResult "Windows Service Registered" $false "Service '$ServiceName' is NOT registered"
        }
    }
    else {
        # Should NOT exist
        if ($service) {
            Write-TestResult "Windows Service Removed" $false "Service '$ServiceName' still exists (status: $($service.Status))"
        }
        else {
            Write-TestResult "Windows Service Removed" $true "Service '$ServiceName' is not registered"
        }
    }
}

function Test-ServiceRegistryRemoved {
    <#
    .SYNOPSIS
        Verify service registry entries are removed.
    #>
    $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
    $exists = Test-Path $regPath

    if ($Mode -eq "PostInstall") {
        if ($exists) {
            Write-TestResult "Service Registry Entry" $true "Registry key exists at $regPath"
        }
        else {
            Write-TestResult "Service Registry Entry" $false "Registry key missing at $regPath"
        }
    }
    else {
        if ($exists) {
            # Get additional info
            $regInfo = Get-ItemProperty $regPath -ErrorAction SilentlyContinue
            $imagePath = $regInfo.ImagePath
            Write-TestResult "Service Registry Removed" $false "Registry key still exists at $regPath (ImagePath: $imagePath)"
        }
        else {
            Write-TestResult "Service Registry Removed" $true "No service registry entry found"
        }
    }
}

function Test-InstallationFilesRemoved {
    <#
    .SYNOPSIS
        Verify installation directory and files are removed.
    #>
    $folderExists = Test-Path $InstallPath

    if ($Mode -eq "PostInstall") {
        # Folder should exist with expected files
        if ($folderExists) {
            Write-TestResult "Installation Folder Exists" $true "Folder exists at $InstallPath"

            foreach ($file in $ExpectedFiles) {
                $filePath = Join-Path $InstallPath $file
                if (Test-Path $filePath) {
                    Write-TestResult "File Installed: $file" $true ""
                }
                else {
                    Write-TestResult "File Installed: $file" $false "File not found: $filePath"
                }
            }
        }
        else {
            Write-TestResult "Installation Folder Exists" $false "Folder not found at $InstallPath"
        }
    }
    else {
        # Folder should NOT exist
        if ($folderExists) {
            $remainingFiles = Get-ChildItem -Path $InstallPath -Recurse -Force -ErrorAction SilentlyContinue
            $fileList = ($remainingFiles | ForEach-Object { $_.Name }) -join ", "

            if ($remainingFiles.Count -gt 0) {
                Write-TestResult "Installation Folder Removed" $false "Folder still exists with $($remainingFiles.Count) files: $fileList"

                # Detail each remaining file
                if ($Detailed) {
                    foreach ($file in $remainingFiles) {
                        Write-Host "       - $($file.FullName)" -ForegroundColor Gray
                    }
                }
            }
            else {
                Write-TestResult "Installation Folder Removed" $false "Empty folder still exists at $InstallPath" -IsWarning:$true
            }
        }
        else {
            Write-TestResult "Installation Folder Removed" $true "Installation folder does not exist"
        }

        # Also check for individual expected files (in case folder was recreated)
        foreach ($file in $ExpectedFiles) {
            $filePath = Join-Path $InstallPath $file
            if (Test-Path $filePath) {
                Write-TestResult "File Removed: $file" $false "File still exists: $filePath"
            }
        }
    }
}

function Test-PathEntryRemoved {
    <#
    .SYNOPSIS
        Verify system PATH no longer contains installation folder.
    #>
    $systemPath = [Environment]::GetEnvironmentVariable("PATH", "Machine")
    $pathEntries = $systemPath -split ";"
    $containsInstallPath = $pathEntries | Where-Object { $_.TrimEnd('\') -eq $InstallPath.TrimEnd('\') }

    if ($Mode -eq "PostInstall") {
        if ($containsInstallPath) {
            Write-TestResult "System PATH Entry" $true "PATH contains $InstallPath"
        }
        else {
            Write-TestResult "System PATH Entry" $false "PATH does not contain $InstallPath" -IsWarning:$true
        }
    }
    else {
        if ($containsInstallPath) {
            Write-TestResult "PATH Entry Removed" $false "System PATH still contains: $InstallPath"
        }
        else {
            Write-TestResult "PATH Entry Removed" $true "Installation path not in system PATH"
        }
    }
}

function Test-WfpProviderRemoved {
    <#
    .SYNOPSIS
        Verify WFP provider is removed using netsh.
    #>
    try {
        $output = netsh wfp show providers 2>&1

        # Check if our provider GUID appears in the output
        $providerFound = $output | Select-String -Pattern $ProviderGuid -Quiet

        if ($Mode -eq "PostInstall") {
            # After applying policy, provider should exist
            if ($providerFound) {
                Write-TestResult "WFP Provider Registered" $true "Provider GUID $ProviderGuid found"
            }
            else {
                # Provider may not exist if no policy has been applied yet
                Write-TestResult "WFP Provider Registered" $true "Provider not yet created (normal if no policy applied)" -IsWarning:$true
            }
        }
        else {
            if ($providerFound) {
                Write-TestResult "WFP Provider Removed" $false "WFP Provider still exists (GUID: $ProviderGuid)"
            }
            else {
                Write-TestResult "WFP Provider Removed" $true "WFP Provider not found"
            }
        }
    }
    catch {
        Write-TestResult "WFP Provider Check" $false "Failed to query WFP providers: $_" -IsWarning:$true
    }
}

function Test-WfpSublayerRemoved {
    <#
    .SYNOPSIS
        Verify WFP sublayer is removed using netsh.
    #>
    try {
        $output = netsh wfp show sublayers 2>&1

        # Check if our sublayer GUID appears in the output
        $sublayerFound = $output | Select-String -Pattern $SublayerGuid -Quiet

        if ($Mode -eq "PostInstall") {
            if ($sublayerFound) {
                Write-TestResult "WFP Sublayer Registered" $true "Sublayer GUID $SublayerGuid found"
            }
            else {
                Write-TestResult "WFP Sublayer Registered" $true "Sublayer not yet created (normal if no policy applied)" -IsWarning:$true
            }
        }
        else {
            if ($sublayerFound) {
                Write-TestResult "WFP Sublayer Removed" $false "WFP Sublayer still exists (GUID: $SublayerGuid)"
            }
            else {
                Write-TestResult "WFP Sublayer Removed" $true "WFP Sublayer not found"
            }
        }
    }
    catch {
        Write-TestResult "WFP Sublayer Check" $false "Failed to query WFP sublayers: $_" -IsWarning:$true
    }
}

function Test-WfpFiltersRemoved {
    <#
    .SYNOPSIS
        Verify WFP filters from our provider are removed.
    #>
    try {
        $output = netsh wfp show filters 2>&1

        # Check if any filters reference our provider GUID
        $filtersFound = $output | Select-String -Pattern $ProviderGuid -Quiet

        if ($Mode -eq "PostInstall") {
            # This is informational - filters exist if policy is applied
            if ($filtersFound) {
                Write-TestResult "WFP Filters Active" $true "Filters from our provider are active"
            }
            else {
                Write-TestResult "WFP Filters Active" $true "No filters yet (normal if no policy applied)" -IsWarning:$true
            }
        }
        else {
            if ($filtersFound) {
                # Count the filters
                $filterMatches = $output | Select-String -Pattern $ProviderGuid
                $filterCount = ($filterMatches | Measure-Object).Count
                Write-TestResult "WFP Filters Removed" $false "Found $filterCount WFP filter references to our provider"
            }
            else {
                Write-TestResult "WFP Filters Removed" $true "No WFP filters from our provider found"
            }
        }
    }
    catch {
        Write-TestResult "WFP Filters Check" $false "Failed to query WFP filters: $_" -IsWarning:$true
    }
}

function Test-EventLogSource {
    <#
    .SYNOPSIS
        Check for event log source registration.
    #>
    try {
        $sourceExists = [System.Diagnostics.EventLog]::SourceExists($ServiceName)

        if ($Mode -eq "PostInstall") {
            # Event log source might be created on first run
            if ($sourceExists) {
                Write-TestResult "Event Log Source" $true "Event log source '$ServiceName' is registered"
            }
            else {
                Write-TestResult "Event Log Source" $true "Event log source not yet created (normal)" -IsWarning:$true
            }
        }
        else {
            # Source might persist (Windows doesn't always clean these up)
            if ($sourceExists) {
                Write-TestResult "Event Log Source" $false "Event log source '$ServiceName' still registered" -IsWarning:$true
            }
            else {
                Write-TestResult "Event Log Source Removed" $true "Event log source not found"
            }
        }
    }
    catch {
        Write-TestResult "Event Log Source Check" $false "Failed to check event log: $_" -IsWarning:$true
    }
}

function Test-MsiProductRegistration {
    <#
    .SYNOPSIS
        Check if product is still registered in Windows Installer.
    #>
    try {
        $products = Get-WmiObject -Class Win32_Product -Filter "Name LIKE '%WfpTrafficControl%'" -ErrorAction SilentlyContinue

        if ($Mode -eq "PostInstall") {
            if ($products) {
                $productInfo = $products | Select-Object -First 1
                Write-TestResult "MSI Product Registered" $true "Product: $($productInfo.Name) v$($productInfo.Version)"
            }
            else {
                Write-TestResult "MSI Product Registered" $false "Product not found in Windows Installer database"
            }
        }
        else {
            if ($products) {
                foreach ($product in $products) {
                    Write-TestResult "MSI Product Removed" $false "Product still registered: $($product.Name) ($($product.IdentifyingNumber))"
                }
            }
            else {
                Write-TestResult "MSI Product Removed" $true "No product found in Windows Installer database"
            }
        }
    }
    catch {
        # Win32_Product query can be slow and sometimes fails
        Write-TestResult "MSI Product Check" $false "Failed to query Windows Installer: $_" -IsWarning:$true
    }
}

function Test-ProcessesRunning {
    <#
    .SYNOPSIS
        Check if any WfpTrafficControl processes are running.
    #>
    $processes = Get-Process -Name "WfpTrafficControl.Service", "wfpctl" -ErrorAction SilentlyContinue

    if ($Mode -eq "PostInstall") {
        $serviceProc = $processes | Where-Object { $_.Name -eq "WfpTrafficControl.Service" }
        if ($serviceProc) {
            Write-TestResult "Service Process Running" $true "Service process is running (PID: $($serviceProc.Id))"
        }
        else {
            Write-TestResult "Service Process Running" $false "Service process not running" -IsWarning:$true
        }
    }
    else {
        if ($processes) {
            foreach ($proc in $processes) {
                Write-TestResult "Process Stopped" $false "Process still running: $($proc.Name) (PID: $($proc.Id))"
            }
        }
        else {
            Write-TestResult "Processes Stopped" $true "No WfpTrafficControl processes running"
        }
    }
}

function Test-NamedPipeRemoved {
    <#
    .SYNOPSIS
        Check if the named pipe is still active.
    #>
    $pipeName = "WfpTrafficControl"
    $pipeExists = Get-ChildItem "\\.\pipe\" -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq $pipeName }

    if ($Mode -eq "PostInstall") {
        if ($pipeExists) {
            Write-TestResult "Named Pipe Active" $true "Named pipe '$pipeName' is active"
        }
        else {
            Write-TestResult "Named Pipe Active" $false "Named pipe '$pipeName' not found (service may not be running)" -IsWarning:$true
        }
    }
    else {
        if ($pipeExists) {
            Write-TestResult "Named Pipe Removed" $false "Named pipe '$pipeName' still exists"
        }
        else {
            Write-TestResult "Named Pipe Removed" $true "Named pipe not active"
        }
    }
}

# ============================================================================
# Main Execution
# ============================================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " WfpTrafficControl MSI Uninstall Verification" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Mode:         $Mode" -ForegroundColor White
Write-Host "Install Path: $InstallPath" -ForegroundColor White
Write-Host "Timestamp:    $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor White
Write-Host ""

switch ($Mode) {
    "PostUninstall" {
        Write-Host "Verifying complete removal of all components..." -ForegroundColor Yellow
    }
    "PreInstall" {
        Write-Host "Verifying clean state before installation..." -ForegroundColor Yellow
    }
    "PostInstall" {
        Write-Host "Verifying installation completed correctly..." -ForegroundColor Yellow
    }
}
Write-Host ""

# Run all tests
Write-Host "--- Service Tests ---" -ForegroundColor Cyan
Test-ServiceRemoved
Test-ServiceRegistryRemoved
Test-ProcessesRunning
Test-NamedPipeRemoved
Write-Host ""

Write-Host "--- File System Tests ---" -ForegroundColor Cyan
Test-InstallationFilesRemoved
Test-PathEntryRemoved
Write-Host ""

Write-Host "--- WFP Artifact Tests ---" -ForegroundColor Cyan
Test-WfpProviderRemoved
Test-WfpSublayerRemoved
Test-WfpFiltersRemoved
Write-Host ""

Write-Host "--- Registry & Installer Tests ---" -ForegroundColor Cyan
Test-EventLogSource
Test-MsiProductRegistration
Write-Host ""

# ============================================================================
# Summary
# ============================================================================

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Test Summary" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$totalTests = $script:TestResults.Passed + $script:TestResults.Failed + $script:TestResults.Warnings
Write-Host "Total Tests:  $totalTests" -ForegroundColor White
Write-Host "Passed:       $($script:TestResults.Passed)" -ForegroundColor Green
Write-Host "Failed:       $($script:TestResults.Failed)" -ForegroundColor $(if ($script:TestResults.Failed -gt 0) { "Red" } else { "White" })
Write-Host "Warnings:     $($script:TestResults.Warnings)" -ForegroundColor $(if ($script:TestResults.Warnings -gt 0) { "Yellow" } else { "White" })
Write-Host ""

if ($script:TestResults.Failed -gt 0) {
    Write-Host "RESULT: INCOMPLETE CLEANUP DETECTED" -ForegroundColor Red
    Write-Host ""
    Write-Host "The following issues were found:" -ForegroundColor Red

    foreach ($result in $script:TestResults.Details | Where-Object { -not $_.Passed -and -not $_.IsWarning }) {
        Write-Host "  - $($result.Name): $($result.Message)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Recommended actions:" -ForegroundColor Yellow
    Write-Host "  1. Stop any running WfpTrafficControl processes" -ForegroundColor Gray
    Write-Host "  2. Run 'wfpctl teardown' if available" -ForegroundColor Gray
    Write-Host "  3. Manually delete $InstallPath" -ForegroundColor Gray
    Write-Host "  4. Remove service: sc delete WfpTrafficControl" -ForegroundColor Gray
    Write-Host ""

    exit 1
}
elseif ($script:TestResults.Warnings -gt 0) {
    Write-Host "RESULT: CLEANUP COMPLETE WITH WARNINGS" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Minor issues found (may be acceptable):" -ForegroundColor Yellow

    foreach ($result in $script:TestResults.Details | Where-Object { $_.IsWarning }) {
        Write-Host "  - $($result.Name): $($result.Message)" -ForegroundColor Yellow
    }
    Write-Host ""

    exit 0
}
else {
    Write-Host "RESULT: ALL CHECKS PASSED" -ForegroundColor Green

    switch ($Mode) {
        "PostUninstall" {
            Write-Host "All components have been successfully removed." -ForegroundColor Green
        }
        "PreInstall" {
            Write-Host "System is clean and ready for installation." -ForegroundColor Green
        }
        "PostInstall" {
            Write-Host "Installation completed successfully." -ForegroundColor Green
        }
    }
    Write-Host ""

    exit 0
}
