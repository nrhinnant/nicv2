# test-apply-policy.ps1
# Manual test script for policy apply functionality
# Phase 12: Compile Outbound TCP Rules
#
# Prerequisites:
# - Service must be running: sc start WfpTrafficControl
# - Run as Administrator
#
# Usage:
# .\test-apply-policy.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Phase 12: Policy Apply Test Script ===" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$TestPolicyPath = Join-Path $ScriptDir "test-block-policy.json"
$WfpCtl = Join-Path $RepoRoot "bin\wfpctl.exe"

# Check if CLI exists
if (-not (Test-Path $WfpCtl)) {
    Write-Host "ERROR: wfpctl.exe not found at: $WfpCtl" -ForegroundColor Red
    Write-Host "Please build the project first: dotnet build" -ForegroundColor Yellow
    exit 1
}

# Step 1: Check service status
Write-Host "Step 1: Checking service status..." -ForegroundColor Yellow
try {
    & $WfpCtl status
    Write-Host "  Service is running" -ForegroundColor Green
}
catch {
    Write-Host "  ERROR: Service may not be running. Start with: sc start WfpTrafficControl" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 2: Validate the test policy
Write-Host "Step 2: Validating test policy..." -ForegroundColor Yellow
if (-not (Test-Path $TestPolicyPath)) {
    Write-Host "  ERROR: Test policy not found at: $TestPolicyPath" -ForegroundColor Red
    exit 1
}

& $WfpCtl validate $TestPolicyPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Policy validation failed" -ForegroundColor Red
    exit 1
}
Write-Host "  Policy is valid" -ForegroundColor Green
Write-Host ""

# Step 3: Test connectivity BEFORE applying policy
Write-Host "Step 3: Testing connectivity BEFORE policy apply..." -ForegroundColor Yellow
Write-Host "  Testing: curl https://1.1.1.1 (should succeed)"
try {
    $response = Invoke-WebRequest -Uri "https://1.1.1.1" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
    Write-Host "  SUCCESS: Connection succeeded (Status: $($response.StatusCode))" -ForegroundColor Green
}
catch [System.Net.WebException] {
    $statusCode = $_.Exception.Response.StatusCode
    if ($statusCode) {
        Write-Host "  SUCCESS: Got HTTP response (Status: $statusCode)" -ForegroundColor Green
    }
    else {
        Write-Host "  WARNING: Connection failed (this is unexpected at this point)" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  WARNING: Connection test inconclusive: $_" -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Apply the test policy
Write-Host "Step 4: Applying test policy..." -ForegroundColor Yellow
& $WfpCtl apply $TestPolicyPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Policy apply failed" -ForegroundColor Red
    exit 1
}
Write-Host "  Policy applied successfully" -ForegroundColor Green
Write-Host ""

# Step 5: Test that 1.1.1.1:443 is now blocked
Write-Host "Step 5: Testing connectivity AFTER policy apply (should be BLOCKED)..." -ForegroundColor Yellow
Write-Host "  Testing: curl https://1.1.1.1 (should timeout/fail)"
try {
    $response = Invoke-WebRequest -Uri "https://1.1.1.1" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
    Write-Host "  FAIL: Connection succeeded but should have been blocked!" -ForegroundColor Red
    $testPassed = $false
}
catch [System.Net.WebException] {
    $statusCode = $_.Exception.Response.StatusCode
    if ($statusCode) {
        Write-Host "  FAIL: Got HTTP response ($statusCode) but should have been blocked!" -ForegroundColor Red
        $testPassed = $false
    }
    else {
        Write-Host "  SUCCESS: Connection was blocked (as expected)" -ForegroundColor Green
        $testPassed = $true
    }
}
catch {
    Write-Host "  SUCCESS: Connection failed/blocked: $_" -ForegroundColor Green
    $testPassed = $true
}
Write-Host ""

# Step 6: Rollback (remove all filters)
Write-Host "Step 6: Rolling back policy (removing filters)..." -ForegroundColor Yellow
& $WfpCtl rollback
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: Rollback failed" -ForegroundColor Red
    exit 1
}
Write-Host "  Rollback completed" -ForegroundColor Green
Write-Host ""

# Step 7: Test that connectivity is restored
Write-Host "Step 7: Testing connectivity AFTER rollback (should succeed again)..." -ForegroundColor Yellow
Write-Host "  Testing: curl https://1.1.1.1 (should succeed)"
try {
    $response = Invoke-WebRequest -Uri "https://1.1.1.1" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
    Write-Host "  SUCCESS: Connection restored (Status: $($response.StatusCode))" -ForegroundColor Green
}
catch [System.Net.WebException] {
    $statusCode = $_.Exception.Response.StatusCode
    if ($statusCode) {
        Write-Host "  SUCCESS: Got HTTP response (Status: $statusCode)" -ForegroundColor Green
    }
    else {
        Write-Host "  WARNING: Connection still blocked after rollback" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "  WARNING: Connection test inconclusive: $_" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
if ($testPassed) {
    Write-Host "PASSED: Block rule worked correctly" -ForegroundColor Green
}
else {
    Write-Host "FAILED: Block rule did not work as expected" -ForegroundColor Red
}
Write-Host ""
Write-Host "Manual verification steps:" -ForegroundColor Yellow
Write-Host "1. Run: wfpctl apply $TestPolicyPath"
Write-Host "2. Try: curl -v --connect-timeout 5 https://1.1.1.1 (should fail)"
Write-Host "3. Run: wfpctl rollback"
Write-Host "4. Try: curl -v https://1.1.1.1 (should succeed)"
