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

    // Logs Configuration
    public int LogEntryCount { get; set; } = 5;
    public int LogTotalCount { get; set; } = 100;
    public string LogFilePath { get; set; } = @"C:\ProgramData\WfpTrafficControl\audit.log";
    public int? LastLogsTail { get; set; }
    public int? LastLogsSinceMinutes { get; set; }

    // Watch Configuration
    public bool WatchIsWatching { get; set; } = false;
    public string? WatchPolicyPath { get; set; }
    public int WatchApplyCount { get; set; } = 0;
    public int WatchErrorCount { get; set; } = 0;
    public string? WatchLastError { get; set; }
    public string? LastWatchSetPath { get; set; }

    // Bootstrap/Teardown Configuration
    public bool BootstrapProviderExists { get; set; } = true;
    public bool BootstrapSublayerExists { get; set; } = true;
    public bool TeardownProviderRemoved { get; set; } = true;
    public bool TeardownSublayerRemoved { get; set; } = true;

    // Simulate Configuration
    public bool SimulatePolicyLoaded { get; set; } = true;
    public string SimulatePolicyVersion { get; set; } = "1.0.0";
    public bool SimulateWouldAllow { get; set; } = true;
    public string? SimulateMatchedRuleId { get; set; } = "test-rule";
    public string? SimulateMatchedAction { get; set; } = "allow";
    public bool SimulateUsedDefaultAction { get; set; } = false;
    public SimulateRequest? LastSimulateRequest { get; private set; }

    // Block Rules Configuration
    public bool BlockRulesPolicyLoaded { get; set; } = true;
    public string BlockRulesPolicyVersion { get; set; } = "1.0.0";
    public List<BlockRuleDto> BlockRules { get; set; } = new()
    {
        new BlockRuleDto
        {
            Id = "block-telnet",
            Direction = "outbound",
            Protocol = "tcp",
            RemotePorts = "23",
            Comment = "Block outbound Telnet",
            Priority = 100,
            Enabled = true,
            Summary = "Outbound TCP to port 23"
        },
        new BlockRuleDto
        {
            Id = "block-ftp",
            Direction = "both",
            Protocol = "tcp",
            RemotePorts = "20,21",
            Comment = "Block FTP connections",
            Priority = 100,
            Enabled = true,
            Summary = "Both directions TCP to ports 20,21"
        }
    };

    // Call tracking
    public int PingCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }
    public int RollbackCallCount { get; private set; }
    public int GetLkgCallCount { get; private set; }
    public int RevertToLkgCallCount { get; private set; }
    public int GetLogsCallCount { get; private set; }
    public int ValidateCallCount { get; private set; }
    public int WatchSetCallCount { get; private set; }
    public int WatchStatusCallCount { get; private set; }
    public int BootstrapCallCount { get; private set; }
    public int TeardownCallCount { get; private set; }
    public int GetBlockRulesCallCount { get; private set; }
    public int SimulateCallCount { get; private set; }
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
        LastLogsTail = tail;
        LastLogsSinceMinutes = sinceMinutes;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<AuditLogsResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        var entries = new List<AuditLogEntryDto>();
        for (int i = 0; i < LogEntryCount; i++)
        {
            entries.Add(new AuditLogEntryDto
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-i).ToString("o"),
                Event = i % 2 == 0 ? "apply-finished" : "rollback-finished",
                Source = "cli",
                Status = "success",
                FiltersCreated = FilterCount,
                PolicyVersion = "1.0.0"
            });
        }

        return Task.FromResult(Result<AuditLogsResponse>.Success(new AuditLogsResponse
        {
            Ok = ShouldSucceed,
            Entries = entries,
            Count = entries.Count,
            TotalCount = LogTotalCount,
            LogPath = LogFilePath,
            Error = ShouldSucceed ? null : ErrorMessage
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

    public Task<Result<WatchSetResponse>> WatchSetAsync(string? policyPath, CancellationToken ct = default)
    {
        WatchSetCallCount++;
        LastWatchSetPath = policyPath;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<WatchSetResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        // Simulate enabling/disabling watch
        if (policyPath != null)
        {
            WatchIsWatching = true;
            WatchPolicyPath = policyPath;
        }
        else
        {
            WatchIsWatching = false;
            WatchPolicyPath = null;
        }

        return Task.FromResult(Result<WatchSetResponse>.Success(new WatchSetResponse
        {
            Ok = ShouldSucceed,
            Watching = WatchIsWatching,
            PolicyPath = WatchPolicyPath,
            InitialApplySuccess = ShouldSucceed,
            Warning = null,
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<WatchStatusResponse>> WatchStatusAsync(CancellationToken ct = default)
    {
        WatchStatusCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<WatchStatusResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<WatchStatusResponse>.Success(new WatchStatusResponse
        {
            Ok = true,
            Watching = WatchIsWatching,
            PolicyPath = WatchPolicyPath,
            DebounceMs = 2000,
            LastApplyTime = WatchIsWatching ? DateTime.UtcNow.ToString("o") : null,
            LastError = WatchLastError,
            LastErrorTime = WatchLastError != null ? DateTime.UtcNow.ToString("o") : null,
            ApplyCount = WatchApplyCount,
            ErrorCount = WatchErrorCount
        }));
    }

    public Task<Result<BootstrapResponse>> BootstrapAsync(CancellationToken ct = default)
    {
        BootstrapCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<BootstrapResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<BootstrapResponse>.Success(new BootstrapResponse
        {
            Ok = ShouldSucceed,
            ProviderExists = BootstrapProviderExists,
            SublayerExists = BootstrapSublayerExists,
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<TeardownResponse>> TeardownAsync(CancellationToken ct = default)
    {
        TeardownCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<TeardownResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        return Task.FromResult(Result<TeardownResponse>.Success(new TeardownResponse
        {
            Ok = ShouldSucceed,
            ProviderRemoved = TeardownProviderRemoved,
            SublayerRemoved = TeardownSublayerRemoved,
            Error = ShouldSucceed ? null : ErrorMessage
        }));
    }

    public Task<Result<BlockRulesResponse>> GetBlockRulesAsync(CancellationToken ct = default)
    {
        GetBlockRulesCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<BlockRulesResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        if (!BlockRulesPolicyLoaded)
        {
            return Task.FromResult(Result<BlockRulesResponse>.Success(
                BlockRulesResponse.NoPolicyLoaded()));
        }

        return Task.FromResult(Result<BlockRulesResponse>.Success(
            BlockRulesResponse.Success(BlockRules, BlockRulesPolicyVersion)));
    }

    public Task<Result<SimulateResponse>> SimulateAsync(
        string direction,
        string protocol,
        string? remoteIp,
        int? remotePort,
        string? processPath = null,
        string? localIp = null,
        int? localPort = null,
        CancellationToken ct = default)
    {
        SimulateCallCount++;
        LastSimulateRequest = new SimulateRequest
        {
            Direction = direction,
            Protocol = protocol,
            RemoteIp = remoteIp,
            RemotePort = remotePort,
            ProcessPath = processPath,
            LocalIp = localIp,
            LocalPort = localPort
        };

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<SimulateResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        if (!SimulatePolicyLoaded)
        {
            return Task.FromResult(Result<SimulateResponse>.Success(
                SimulateResponse.NoPolicyLoaded()));
        }

        var response = SimulateResponse.Success(
            wouldAllow: SimulateWouldAllow,
            matchedRuleId: SimulateUsedDefaultAction ? null : SimulateMatchedRuleId,
            matchedAction: SimulateUsedDefaultAction ? null : SimulateMatchedAction,
            matchedRuleComment: null,
            usedDefaultAction: SimulateUsedDefaultAction,
            defaultAction: "allow",
            evaluationTrace: new List<SimulateEvaluationStep>
            {
                new SimulateEvaluationStep
                {
                    RuleId = SimulateMatchedRuleId ?? "test-rule",
                    Action = SimulateMatchedAction ?? "allow",
                    Matched = !SimulateUsedDefaultAction,
                    Reason = SimulateUsedDefaultAction ? "No match" : "All criteria matched",
                    Priority = 100
                }
            },
            policyVersion: SimulatePolicyVersion);

        return Task.FromResult(Result<SimulateResponse>.Success(response));
    }

    // Policy History Configuration
    public List<PolicyHistoryEntryDto> HistoryEntries { get; set; } = new()
    {
        new PolicyHistoryEntryDto
        {
            Id = "20250301-120000-001",
            AppliedAt = DateTime.UtcNow.AddHours(-1),
            PolicyVersion = "1.0.0",
            RuleCount = 5,
            Source = "CLI",
            FiltersCreated = 5,
            FiltersRemoved = 0
        },
        new PolicyHistoryEntryDto
        {
            Id = "20250301-100000-001",
            AppliedAt = DateTime.UtcNow.AddHours(-3),
            PolicyVersion = "0.9.0",
            RuleCount = 3,
            Source = "UI",
            FiltersCreated = 3,
            FiltersRemoved = 2
        }
    };
    public int HistoryTotalCount { get; set; } = 10;
    public int GetPolicyHistoryCallCount { get; private set; }
    public int RevertToHistoryCallCount { get; private set; }
    public int GetPolicyFromHistoryCallCount { get; private set; }
    public string? LastHistoryEntryId { get; private set; }

    public Task<Result<PolicyHistoryResponse>> GetPolicyHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        GetPolicyHistoryCallCount++;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<PolicyHistoryResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        var entries = HistoryEntries.Take(limit).ToList();
        return Task.FromResult(Result<PolicyHistoryResponse>.Success(
            PolicyHistoryResponse.Success(entries, HistoryTotalCount)));
    }

    public Task<Result<PolicyHistoryRevertResponse>> RevertToHistoryAsync(string entryId, CancellationToken ct = default)
    {
        RevertToHistoryCallCount++;
        LastHistoryEntryId = entryId;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<PolicyHistoryRevertResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        var entry = HistoryEntries.FirstOrDefault(e => e.Id == entryId);
        if (entry == null)
        {
            return Task.FromResult(Result<PolicyHistoryRevertResponse>.Success(
                PolicyHistoryRevertResponse.NotFound(entryId)));
        }

        return Task.FromResult(Result<PolicyHistoryRevertResponse>.Success(
            PolicyHistoryRevertResponse.Success(
                entry.FiltersCreated,
                entry.FiltersRemoved,
                0,
                entry.PolicyVersion,
                entry.RuleCount,
                entryId)));
    }

    public Task<Result<PolicyHistoryGetResponse>> GetPolicyFromHistoryAsync(string entryId, CancellationToken ct = default)
    {
        GetPolicyFromHistoryCallCount++;
        LastHistoryEntryId = entryId;

        if (!ShouldConnect)
        {
            return Task.FromResult(Result<PolicyHistoryGetResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Service not running"));
        }

        var entry = HistoryEntries.FirstOrDefault(e => e.Id == entryId);
        if (entry == null)
        {
            return Task.FromResult(Result<PolicyHistoryGetResponse>.Success(
                PolicyHistoryGetResponse.NotFound(entryId)));
        }

        var policyJson = "{\"version\":\"" + entry.PolicyVersion + "\",\"defaultAction\":\"allow\",\"rules\":[]}";
        return Task.FromResult(Result<PolicyHistoryGetResponse>.Success(
            PolicyHistoryGetResponse.Success(entry, policyJson)));
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
        WatchSetCallCount = 0;
        WatchStatusCallCount = 0;
        BootstrapCallCount = 0;
        TeardownCallCount = 0;
        GetBlockRulesCallCount = 0;
        SimulateCallCount = 0;
        GetPolicyHistoryCallCount = 0;
        RevertToHistoryCallCount = 0;
        GetPolicyFromHistoryCallCount = 0;
        LastApplyPath = null;
        LastValidateJson = null;
        LastLogsTail = null;
        LastLogsSinceMinutes = null;
        LastWatchSetPath = null;
        LastSimulateRequest = null;
        LastHistoryEntryId = null;
    }
}
