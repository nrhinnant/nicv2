using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Interface for communicating with the WFP Traffic Control service.
/// </summary>
public interface IServiceClient
{
    /// <summary>
    /// Gets whether the client is currently connected to the service.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Pings the service to check if it's running.
    /// </summary>
    Task<Result<PingResponse>> PingAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies a policy file to the service.
    /// </summary>
    /// <param name="policyPath">Full path to the policy JSON file.</param>
    Task<Result<ApplyResponse>> ApplyAsync(string policyPath, CancellationToken ct = default);

    /// <summary>
    /// Executes a rollback, removing all filters.
    /// </summary>
    Task<Result<RollbackResponse>> RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets information about the Last Known Good policy.
    /// </summary>
    Task<Result<LkgShowResponse>> GetLkgAsync(CancellationToken ct = default);

    /// <summary>
    /// Reverts to the Last Known Good policy.
    /// </summary>
    Task<Result<LkgRevertResponse>> RevertToLkgAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets recent audit log entries.
    /// </summary>
    /// <param name="tail">Number of recent entries to return.</param>
    /// <param name="sinceMinutes">Return entries from the last N minutes (ignored if tail > 0).</param>
    Task<Result<AuditLogsResponse>> GetLogsAsync(int? tail = null, int? sinceMinutes = null, CancellationToken ct = default);

    /// <summary>
    /// Validates a policy JSON string.
    /// </summary>
    /// <param name="policyJson">The policy JSON to validate.</param>
    Task<Result<ValidateResponse>> ValidateAsync(string policyJson, CancellationToken ct = default);

    /// <summary>
    /// Sets (enables or disables) file watching for hot reload.
    /// </summary>
    /// <param name="policyPath">Path to watch, or null to disable watching.</param>
    Task<Result<WatchSetResponse>> WatchSetAsync(string? policyPath, CancellationToken ct = default);

    /// <summary>
    /// Gets the current file watch status.
    /// </summary>
    Task<Result<WatchStatusResponse>> WatchStatusAsync(CancellationToken ct = default);
}
