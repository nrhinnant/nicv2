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
}
