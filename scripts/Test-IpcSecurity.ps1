#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Integration test for IPC security features (pipe ACL, message size, rate limiting).

.DESCRIPTION
    This script tests the WfpTrafficControl IPC security model at the VM level,
    confirming what IpcSecurityTests.cs validates at the unit level:
    1. Pipe connectivity as admin (baseline sanity)
    2. Oversized message rejection (> 64KB, bypassing CLI client-side limits)
    3. Rate limiting (10 requests per 10-second window per identity)
    4. Non-admin access denial (optional, gated on -TestNonAdmin)

    Requires the WfpTrafficControl service to be running.

.PARAMETER WfpctlPath
    Path to wfpctl.exe. Defaults to .\src\cli\bin\Debug\net8.0\wfpctl.exe

.PARAMETER TestNonAdmin
    If specified, enables the non-admin access test. Requires -NonAdminUser.

.PARAMETER NonAdminUser
    Local user account name (non-admin) for the access denial test.
    Only used when -TestNonAdmin is specified.

.EXAMPLE
    .\scripts\Test-IpcSecurity.ps1

.EXAMPLE
    .\scripts\Test-IpcSecurity.ps1 -TestNonAdmin -NonAdminUser "testuser"

.NOTES
    Must be run as Administrator.
    The WfpTrafficControl service must be running.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$WfpctlPath = ".\src\cli\bin\Debug\net8.0\wfpctl.exe",

    [Parameter()]
    [switch]$TestNonAdmin,

    [Parameter()]
    [string]$NonAdminUser
)

$ErrorActionPreference = "Stop"

# ANSI color codes for output
$Green = "`e[32m"
$Red = "`e[31m"
$Yellow = "`e[33m"
$Cyan = "`e[36m"
$Reset = "`e[0m"

function Write-TestStep {
    param([string]$Message)
    Write-Host "${Cyan}[TEST]${Reset} $Message"
}

function Write-TestPass {
    param([string]$Message)
    Write-Host "${Green}[PASS]${Reset} $Message"
}

function Write-TestFail {
    param([string]$Message)
    Write-Host "${Red}[FAIL]${Reset} $Message"
}

function Write-TestWarn {
    param([string]$Message)
    Write-Host "${Yellow}[WARN]${Reset} $Message"
}

function Invoke-Wfpctl {
    param([string[]]$Arguments)

    $output = & $WfpctlPath @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    return @{
        Output = $output -join "`n"
        ExitCode = $exitCode
        Success = ($exitCode -eq 0)
    }
}

# ========================================
# Main Test Script
# ========================================

Write-Host ""
Write-Host "=============================================="
Write-Host "  WfpTrafficControl IPC Security Test"
Write-Host "=============================================="
Write-Host ""

# Verify wfpctl exists
if (-not (Test-Path $WfpctlPath)) {
    Write-TestFail "wfpctl not found at: $WfpctlPath"
    Write-Host "Build the solution first: dotnet build"
    exit 1
}

$WfpctlPath = Resolve-Path $WfpctlPath

$testsPassed = 0
$testsFailed = 0
$testsSkipped = 0

try {

    # ========================================
    # Test 1: Pipe connectivity (admin baseline)
    # ========================================
    Write-TestStep "Test 1: Verifying pipe connectivity as admin (wfpctl status)..."

    $statusResult = Invoke-Wfpctl @("status")
    if ($statusResult.Success) {
        Write-TestPass "wfpctl status succeeded as admin (baseline confirmed)"
        $testsPassed++
    } else {
        Write-TestFail "wfpctl status failed — service may not be running"
        Write-Host $statusResult.Output
        Write-Host ""
        Write-Host "Start the service first:"
        Write-Host "  .\scripts\Start-Service.ps1"
        Write-Host "  -- OR --"
        Write-Host "  dotnet run --project src/service"
        $testsFailed++
        # Cannot continue without a running service
        Write-Host ""
        Write-Host "${Red}Aborting: service must be running for IPC tests.${Reset}"
        exit 1
    }
    Write-Host ""

    # ========================================
    # Test 2: Oversized message rejection
    # ========================================
    Write-TestStep "Test 2: Sending oversized message (> 64KB) directly to pipe..."

    $pipeName = "WfpTrafficControl"
    $maxMessageSize = 64 * 1024  # 64KB — the server's limit
    $oversizedLength = $maxMessageSize + 1024  # 65KB + 1KB = clearly over limit

    try {
        # Connect to the named pipe directly (bypassing wfpctl client-side checks)
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(
            ".",
            $pipeName,
            [System.IO.Pipes.PipeDirection]::InOut
        )
        $pipe.Connect(5000)  # 5-second timeout

        # Send a length prefix indicating an oversized message
        # The server reads 4 bytes (little-endian int32) as the message length,
        # then validates it against IpcMaxMessageSize before reading the body.
        $lengthBytes = [BitConverter]::GetBytes([int]$oversizedLength)
        $pipe.Write($lengthBytes, 0, 4)
        $pipe.Flush()

        # The server should reject the oversized length and send an error response.
        # Try to read the response (4-byte length prefix + JSON body).
        $responseLengthBytes = New-Object byte[] 4
        $bytesRead = $pipe.Read($responseLengthBytes, 0, 4)

        if ($bytesRead -eq 4) {
            $responseLength = [BitConverter]::ToInt32($responseLengthBytes, 0)
            if ($responseLength -gt 0 -and $responseLength -le $maxMessageSize) {
                $responseBytes = New-Object byte[] $responseLength
                $totalRead = 0
                while ($totalRead -lt $responseLength) {
                    $read = $pipe.Read($responseBytes, $totalRead, $responseLength - $totalRead)
                    if ($read -eq 0) { break }
                    $totalRead += $read
                }
                $responseJson = [System.Text.Encoding]::UTF8.GetString($responseBytes, 0, $totalRead)

                if ($responseJson -match "too large" -or $responseJson -match "size" -or $responseJson -match '"ok"\s*:\s*false') {
                    Write-TestPass "Server rejected oversized message with error response"
                    Write-Host "  Response: $responseJson"
                    $testsPassed++
                } else {
                    Write-TestFail "Server responded but did not indicate size rejection"
                    Write-Host "  Response: $responseJson"
                    $testsFailed++
                }
            } else {
                Write-TestFail "Server sent unexpected response length: $responseLength"
                $testsFailed++
            }
        } elseif ($bytesRead -eq 0) {
            # Server disconnected — this is also acceptable behavior for oversized messages
            Write-TestPass "Server disconnected cleanly when oversized message was sent"
            $testsPassed++
        } else {
            Write-TestFail "Unexpected partial read from server: $bytesRead bytes"
            $testsFailed++
        }

        $pipe.Dispose()
    }
    catch [TimeoutException] {
        Write-TestFail "Could not connect to pipe (timeout) — is the service running?"
        $testsFailed++
    }
    catch {
        # A broken pipe or connection reset is acceptable — server rejected us
        $errorMsg = $_.Exception.Message
        if ($errorMsg -match "Broken pipe" -or $errorMsg -match "pipe has been ended" -or $errorMsg -match "forcibly closed") {
            Write-TestPass "Server rejected oversized message (connection terminated)"
            $testsPassed++
        } else {
            Write-TestFail "Unexpected error during oversized message test: $errorMsg"
            $testsFailed++
        }
    }
    Write-Host ""

    # ========================================
    # Test 3: Rate limiting
    # ========================================
    Write-TestStep "Test 3: Rate limiting — sending 20 rapid requests..."
    Write-Host "  (Rate limiter allows 10 requests per 10-second window per identity)"

    $successCount = 0
    $rateLimitedCount = 0
    $otherFailureCount = 0

    for ($i = 1; $i -le 20; $i++) {
        $result = Invoke-Wfpctl @("status")

        if ($result.Success) {
            $successCount++
        } elseif ($result.Output -match "Rate limit") {
            $rateLimitedCount++
        } else {
            $otherFailureCount++
        }
    }

    Write-Host "  Results: $successCount succeeded, $rateLimitedCount rate-limited, $otherFailureCount other failures"

    # Expect: approximately 10 successes and 10 rate-limited.
    # Use tolerant bounds: at least 8 successes (first window), at least 8 rate-limited.
    # The tolerance accounts for timing variance on slow VMs.
    if ($successCount -ge 8 -and $rateLimitedCount -ge 8 -and $otherFailureCount -eq 0) {
        Write-TestPass "Rate limiting working correctly ($successCount succeeded, $rateLimitedCount rate-limited)"
        $testsPassed++
    } elseif ($successCount -ge 8 -and $rateLimitedCount -ge 5 -and $otherFailureCount -eq 0) {
        Write-TestWarn "Rate limiting partially working ($successCount succeeded, $rateLimitedCount rate-limited)"
        Write-Host "  Some requests may have spilled into a new rate limit window"
        $testsPassed++
    } elseif ($otherFailureCount -gt 0) {
        Write-TestFail "Unexpected failures during rate limit test ($otherFailureCount non-rate-limit errors)"
        $testsFailed++
    } else {
        Write-TestFail "Rate limiting not working as expected ($successCount succeeded, $rateLimitedCount rate-limited)"
        $testsFailed++
    }

    # Wait for rate limit window to expire before any subsequent tests
    Write-Host "  Waiting 11 seconds for rate limit window to expire..."
    Start-Sleep -Seconds 11
    Write-Host ""

    # ========================================
    # Test 4: Non-admin access (optional, gated)
    # ========================================
    if ($TestNonAdmin) {
        if ([string]::IsNullOrWhiteSpace($NonAdminUser)) {
            Write-TestFail "Test 4: -TestNonAdmin requires -NonAdminUser parameter"
            Write-Host "  Usage: .\scripts\Test-IpcSecurity.ps1 -TestNonAdmin -NonAdminUser 'testuser'"
            $testsFailed++
        } else {
            Write-TestStep "Test 4: Non-admin access — attempting wfpctl status as '$NonAdminUser'..."

            try {
                $credential = Get-Credential -UserName $NonAdminUser -Message "Enter password for non-admin user '$NonAdminUser'"

                # Run wfpctl status as the non-admin user.
                # Note: Start-Process with -Credential cannot use -NoNewWindow on Windows.
                # We redirect stdout/stderr to temp files to capture output.
                $tempStdout = [System.IO.Path]::GetTempFileName()
                $tempStderr = [System.IO.Path]::GetTempFileName()
                $process = Start-Process -FilePath $WfpctlPath -ArgumentList "status" `
                    -Credential $credential -PassThru -Wait `
                    -RedirectStandardOutput $tempStdout `
                    -RedirectStandardError $tempStderr -ErrorAction Stop

                $stdoutContent = Get-Content $tempStdout -Raw -ErrorAction SilentlyContinue
                $stderrContent = Get-Content $tempStderr -Raw -ErrorAction SilentlyContinue
                Remove-Item $tempStdout -Force -ErrorAction SilentlyContinue
                Remove-Item $tempStderr -Force -ErrorAction SilentlyContinue
                $combinedOutput = "$stdoutContent $stderrContent".Trim()

                if ($process.ExitCode -ne 0) {
                    if ($combinedOutput -match "Access" -or $combinedOutput -match "denied" -or $combinedOutput -match "Unauthorized") {
                        Write-TestPass "Non-admin user correctly denied access (exit code $($process.ExitCode))"
                        Write-Host "  Error: $combinedOutput"
                        $testsPassed++
                    } else {
                        Write-TestPass "Non-admin user rejected (exit code $($process.ExitCode))"
                        Write-Host "  Output: $combinedOutput"
                        $testsPassed++
                    }
                } else {
                    Write-TestFail "Non-admin user was able to run wfpctl status (should have been denied)"
                    $testsFailed++
                }
            }
            catch {
                $errorMsg = $_.Exception.Message
                if ($errorMsg -match "Access" -or $errorMsg -match "denied" -or $errorMsg -match "logon") {
                    Write-TestPass "Non-admin user correctly denied access at OS level"
                    Write-Host "  Error: $errorMsg"
                    $testsPassed++
                } else {
                    Write-TestFail "Unexpected error during non-admin test: $errorMsg"
                    $testsFailed++
                }
            }
        }
    } else {
        Write-TestWarn "Test 4: Non-admin access test SKIPPED (not requested)"
        Write-Host "  To run this test, provide -TestNonAdmin and -NonAdminUser parameters:"
        Write-Host "  .\scripts\Test-IpcSecurity.ps1 -TestNonAdmin -NonAdminUser 'testuser'"
        Write-Host "  The user must be a local non-admin account on this VM."
        $testsSkipped++
    }
    Write-Host ""

}
finally {
    # No WFP state was modified by these tests, so no cleanup needed.
    # The rate limit window will expire naturally.
}

# ========================================
# Summary
# ========================================
Write-Host "=============================================="
Write-Host "  IPC Security Test Summary"
Write-Host "=============================================="
Write-Host ""
Write-Host "  Passed:  ${Green}$testsPassed${Reset}"
Write-Host "  Failed:  ${Red}$testsFailed${Reset}"
Write-Host "  Skipped: ${Yellow}$testsSkipped${Reset}"
Write-Host ""

if ($testsFailed -eq 0) {
    Write-Host "${Green}All executed tests passed!${Reset}"
    exit 0
} else {
    Write-Host "${Red}Some tests failed.${Reset}"
    exit 1
}
