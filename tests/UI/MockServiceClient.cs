using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Mock service client for testing ViewModels.
/// </summary>
public class MockServiceClient : IServiceClient
{
    public bool IsConnected => ShouldConnect;

    // Configuration
    public bool ShouldConnect { get; set; } = true;
    public bool ShouldSucceed { get; set; } = true;
    public string ServiceVersion { get; set; } = "1.0.0-test";
    public int FilterCount { get; set; } = 5;
    public string ErrorMessage { get; set; } = "Mock error";

    // LKG Configuration
    public bool LkgExists { get; set; } = true;
    public bool LkgIsCorrupt { get; set; } = false;
    public string LkgPolicyVersion { get; set; } = "1.0.0";
    public int LkgRuleCount { get; set; } = 3;

    // Validation Configuration
    public bool ValidationIsValid { get; set; } = true;
    public List<ValidationErrorDto> ValidationErrors { get; set; } = new();

    // Call tracking
    public int PingCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }
    public int RollbackCallCount { get; private set; }
    public int GetLkgCallCount { get; private set; }
    public int RevertToLkgCallCount { get; private set; }
    public int GetLogsCallCount { get; private set; }
    public int ValidateCallCount { get; private set; }
    public string? LastApplyPath { get; private set; }
    public string? LastValidateJson { get; private set; }

    public Task<Result<PingResponse>> PingAsync(CancellationToken ct = default)
    {
        PingCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<PingResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<PingResponse>.Success(new PingResponse
        {
            Ok = ShouldSucceed,
            ServiceVersion = ServiceVersion,
            Time = DateTime.UtcNow.ToString("o"),
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<ApplyResponse>> ApplyAsync(string policyPath, CancellationToken ct = default)
    {
        ApplyCallCount++;
        LastApplyPath = policyPath;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<ApplyResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<ApplyResponse>.Success(new ApplyResponse
        {
            Ok = ShouldSucceed,
            FiltersCreated = FilterCount,
            FiltersRemoved = 0,
            RulesSkipped = 0,
            PolicyVersion = "1.0.0",
            TotalRules = FilterCount,
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<RollbackResponse>> RollbackAsync(CancellationToken ct = default)
    {
        RollbackCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<RollbackResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<RollbackResponse>.Success(new RollbackResponse
        {
            Ok = ShouldSucceed,
            FiltersRemoved = FilterCount,
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<LkgShowResponse>> GetLkgAsync(CancellationToken ct = default)
    {
        GetLkgCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<LkgShowResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<LkgShowResponse>.Success(new LkgShowResponse
        {
            Ok = true,
            Exists = LkgExists,
            IsCorrupt = LkgIsCorrupt,
            PolicyVersion = LkgPolicyVersion,
            RuleCount = LkgRuleCount,
            SavedAt = DateTime.UtcNow.ToString("o")
        }));
    }

    public Task<Result<LkgRevertResponse>> RevertToLkgAsync(CancellationToken ct = default)
    {
        RevertToLkgCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<LkgRevertResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<LkgRevertResponse>.Success(new LkgRevertResponse
        {
            Ok = ShouldSucceed,
            LkgFound = LkgExists,
            PolicyVersion = LkgPolicyVersion,
            TotalRules = LkgRuleCount,
            FiltersCreated = LkgRuleCount,
            FiltersRemoved = 0,
            RulesSkipped = 0,
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<AuditLogsResponse>> GetLogsAsync(int? tail = null, int? sinceMinutes = null, CancellationToken ct = default)
    {
        GetLogsCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<AuditLogsResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        var entries = new List<AuditLogEntryDto>
        {
            new()
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Event = "apply-finished",
                Status = "success",
                FiltersCreated = FilterCount
            }
        };

        return Task.FromResult(Result<AuditLogsResponse>.Success(new AuditLogsResponse
        {
            Ok = true,
            Entries = entries,
            Count = entries.Count,
            TotalCount = entries.Count
        }));
    }

    public Task<Result<ValidateResponse>> ValidateAsync(string policyJson, CancellationToken ct = default)
    {
        ValidateCallCount++;
        LastValidateJson = policyJson;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<ValidateResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<ValidateResponse>.Success(new ValidateResponse
        {
            Ok = true,
            Valid = ValidationIsValid,
            RuleCount = 1,
            Version = "1.0.0",
            Errors = ValidationErrors
        }));
    }

    public void Reset()
    {
        PingCallCount = 0;
        ApplyCallCount = 0;
        RollbackCallCount = 0;
        GetLkgCallCount = 0;
        RevertToLkgCallCount = 0;
        GetLogsCallCount = 0;
        ValidateCallCount = 0;
        LastApplyPath = null;
        LastValidateJson = null;
    }
}
