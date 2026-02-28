namespace WfpTrafficControl.Shared;

/// <summary>
/// Single source of truth for all WFP-related constants used across the project.
/// WARNING: Do not change the GUIDs after deployment — they identify our WFP objects.
/// </summary>
public static class WfpConstants
{
    /// <summary>
    /// Unique identifier for our WFP provider.
    /// Used to tag all WFP objects we create for easy identification and cleanup.
    /// </summary>
    public static readonly Guid ProviderGuid = new("7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A");

    /// <summary>
    /// Unique identifier for our WFP sublayer.
    /// All our filters are added to this sublayer.
    /// </summary>
    public static readonly Guid SublayerGuid = new("B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C");

    /// <summary>
    /// Display name for our WFP provider (visible in WFP diagnostic tools).
    /// </summary>
    public const string ProviderName = "WfpTrafficControl Provider";

    /// <summary>
    /// Description for our WFP provider.
    /// </summary>
    public const string ProviderDescription = "Traffic control provider for WfpTrafficControl service";

    /// <summary>
    /// Display name for our WFP sublayer (visible in WFP diagnostic tools).
    /// </summary>
    public const string SublayerName = "WfpTrafficControl Sublayer";

    /// <summary>
    /// Description for our WFP sublayer.
    /// </summary>
    public const string SublayerDescription = "Sublayer containing all WfpTrafficControl filters";

    /// <summary>
    /// Windows service name used for registration and control.
    /// </summary>
    public const string ServiceName = "WfpTrafficControl";

    /// <summary>
    /// Display name shown in Services MMC snap-in.
    /// </summary>
    public const string ServiceDisplayName = "WFP Traffic Control Service";

    /// <summary>
    /// Named pipe name for CLI-to-service IPC communication.
    /// </summary>
    public const string PipeName = "WfpTrafficControl";

    /// <summary>
    /// Full pipe path for named pipe connections.
    /// </summary>
    public const string PipeFullPath = @"\\.\pipe\" + PipeName;

    /// <summary>
    /// Human-readable project name for display purposes.
    /// </summary>
    public const string ProjectName = "WfpTrafficControl";

    // ========================================
    // Demo Block Filter Constants
    // ========================================

    /// <summary>
    /// Unique identifier for our demo block filter.
    /// </summary>
    public static readonly Guid DemoBlockFilterGuid = new("D3E4F5A6-7B8C-4D9E-0F1A-2B3C4D5E6F7A");

    /// <summary>
    /// Display name for the demo block filter.
    /// </summary>
    public const string DemoBlockFilterName = "WfpTrafficControl Demo Block (1.1.1.1:443)";

    /// <summary>
    /// Description for the demo block filter.
    /// </summary>
    public const string DemoBlockFilterDescription = "Demo filter that blocks outbound TCP to 1.1.1.1:443";

    /// <summary>
    /// The remote IP address blocked by the demo filter (1.1.1.1 in host byte order).
    /// </summary>
    public const uint DemoBlockRemoteIp = 0x01010101; // 1.1.1.1

    /// <summary>
    /// The remote port blocked by the demo filter.
    /// </summary>
    public const ushort DemoBlockRemotePort = 443;

    /// <summary>
    /// TCP protocol number (IANA).
    /// </summary>
    public const byte ProtocolTcp = 6;

    /// <summary>
    /// UDP protocol number (IANA).
    /// </summary>
    public const byte ProtocolUdp = 17;

    // ========================================
    // LKG (Last Known Good) Policy Constants
    // ========================================

    /// <summary>
    /// Directory name under ProgramData for storing application data.
    /// Full path will be: %ProgramData%\WfpTrafficControl
    /// </summary>
    public const string DataDirectoryName = "WfpTrafficControl";

    /// <summary>
    /// File name for the LKG (Last Known Good) policy file.
    /// </summary>
    public const string LkgPolicyFileName = "lkg-policy.json";

    /// <summary>
    /// File name for the audit log file (JSON Lines format).
    /// </summary>
    public const string AuditLogFileName = "audit.log";

    // ========================================
    // IPC Protocol Constants
    // ========================================

    /// <summary>
    /// Current IPC protocol version.
    /// Increment when making breaking changes to the IPC message format.
    /// Clients and servers should validate this matches to ensure compatibility.
    /// </summary>
    public const int IpcProtocolVersion = 1;

    /// <summary>
    /// Minimum supported IPC protocol version.
    /// Requests with versions below this will be rejected.
    /// </summary>
    public const int IpcMinProtocolVersion = 1;

    /// <summary>
    /// Maximum message size for IPC requests/responses (64 KB).
    /// </summary>
    public const int IpcMaxMessageSize = 64 * 1024;

    /// <summary>
    /// Read timeout for IPC operations in milliseconds (30 seconds).
    /// </summary>
    public const int IpcReadTimeoutMs = 30_000;

    /// <summary>
    /// Connect timeout for IPC client in milliseconds (5 seconds).
    /// </summary>
    public const int IpcConnectTimeoutMs = 5_000;

    // Note: AutoApplyLkgOnStartup is now a runtime configuration setting.
    // Set "WfpTrafficControl:AutoApplyLkgOnStartup" to true in appsettings.json to enable.
    // Default is false (fail-open behavior for safety).

    // Cached paths to avoid repeated allocations (lazy-initialized)
    private static string? _dataDirectory;
    private static string? _lkgPolicyPath;
    private static string? _auditLogPath;

    /// <summary>
    /// Gets the full path to the application data directory.
    /// Cached after first call to avoid repeated string allocations.
    /// </summary>
    public static string GetDataDirectory()
    {
        return _dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            DataDirectoryName);
    }

    /// <summary>
    /// Gets the full path to the LKG policy file.
    /// Cached after first call to avoid repeated string allocations.
    /// </summary>
    public static string GetLkgPolicyPath()
    {
        return _lkgPolicyPath ??= Path.Combine(GetDataDirectory(), LkgPolicyFileName);
    }

    /// <summary>
    /// Gets the full path to the audit log file.
    /// Cached after first call to avoid repeated string allocations.
    /// </summary>
    public static string GetAuditLogPath()
    {
        return _auditLogPath ??= Path.Combine(GetDataDirectory(), AuditLogFileName);
    }
}
