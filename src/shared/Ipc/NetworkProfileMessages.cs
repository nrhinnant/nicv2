using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Network profile configuration for automatic policy switching.
/// </summary>
public class NetworkProfile
{
    /// <summary>
    /// Unique profile identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable profile name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of when this profile should be used.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Path to the policy file to apply when this profile is active.
    /// </summary>
    [JsonPropertyName("policyPath")]
    public string? PolicyPath { get; set; }

    /// <summary>
    /// Conditions that must match for this profile to activate.
    /// </summary>
    [JsonPropertyName("conditions")]
    public ProfileConditions Conditions { get; set; } = new();

    /// <summary>
    /// Priority for matching (higher wins when multiple profiles match).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Whether this profile is enabled for automatic switching.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether this is the default profile when no conditions match.
    /// </summary>
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}

/// <summary>
/// Conditions for network profile matching.
/// </summary>
public class ProfileConditions
{
    /// <summary>
    /// List of WiFi SSIDs to match (any match triggers).
    /// </summary>
    [JsonPropertyName("ssids")]
    public List<string> Ssids { get; set; } = new();

    /// <summary>
    /// List of DNS suffixes to match (any match triggers).
    /// </summary>
    [JsonPropertyName("dnsSuffixes")]
    public List<string> DnsSuffixes { get; set; } = new();

    /// <summary>
    /// List of network names to match (Windows network names).
    /// </summary>
    [JsonPropertyName("networkNames")]
    public List<string> NetworkNames { get; set; } = new();

    /// <summary>
    /// List of gateway IPs to match (any match triggers).
    /// </summary>
    [JsonPropertyName("gateways")]
    public List<string> Gateways { get; set; } = new();

    /// <summary>
    /// Network category to match (Public, Private, Domain).
    /// </summary>
    [JsonPropertyName("networkCategory")]
    public string? NetworkCategory { get; set; }

    /// <summary>
    /// Whether all conditions must match (AND) or any condition (OR).
    /// </summary>
    [JsonPropertyName("matchAll")]
    public bool MatchAll { get; set; }
}

/// <summary>
/// Information about the current network state.
/// </summary>
public class CurrentNetworkInfo
{
    /// <summary>
    /// Name of the connected network.
    /// </summary>
    [JsonPropertyName("networkName")]
    public string? NetworkName { get; set; }

    /// <summary>
    /// Network category (Public, Private, Domain).
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Connected WiFi SSID (if wireless).
    /// </summary>
    [JsonPropertyName("ssid")]
    public string? Ssid { get; set; }

    /// <summary>
    /// DNS suffix of the connection.
    /// </summary>
    [JsonPropertyName("dnsSuffix")]
    public string? DnsSuffix { get; set; }

    /// <summary>
    /// Default gateway IP address.
    /// </summary>
    [JsonPropertyName("gateway")]
    public string? Gateway { get; set; }

    /// <summary>
    /// Whether connected to the internet.
    /// </summary>
    [JsonPropertyName("isConnected")]
    public bool IsConnected { get; set; }

    /// <summary>
    /// Adapter name.
    /// </summary>
    [JsonPropertyName("adapterName")]
    public string? AdapterName { get; set; }
}

/// <summary>
/// Request to get all network profiles.
/// </summary>
public sealed class GetNetworkProfilesRequest : IpcRequest
{
    public const string RequestType = "get-network-profiles";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response containing all network profiles.
/// </summary>
public sealed class GetNetworkProfilesResponse : IpcResponse
{
    /// <summary>
    /// List of all configured network profiles.
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<NetworkProfile> Profiles { get; set; } = new();

    /// <summary>
    /// ID of the currently active profile (null if none).
    /// </summary>
    [JsonPropertyName("activeProfileId")]
    public string? ActiveProfileId { get; set; }

    public static GetNetworkProfilesResponse Success(List<NetworkProfile> profiles, string? activeProfileId = null)
    {
        return new GetNetworkProfilesResponse
        {
            Ok = true,
            Profiles = profiles,
            ActiveProfileId = activeProfileId
        };
    }

    public static GetNetworkProfilesResponse Failure(string error)
    {
        return new GetNetworkProfilesResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Request to save a network profile (create or update).
/// </summary>
public sealed class SaveNetworkProfileRequest : IpcRequest
{
    public const string RequestType = "save-network-profile";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// The profile to save.
    /// </summary>
    [JsonPropertyName("profile")]
    public NetworkProfile Profile { get; set; } = new();
}

/// <summary>
/// Response for saving a network profile.
/// </summary>
public sealed class SaveNetworkProfileResponse : IpcResponse
{
    public static SaveNetworkProfileResponse Success()
    {
        return new SaveNetworkProfileResponse { Ok = true };
    }

    public static SaveNetworkProfileResponse Failure(string error)
    {
        return new SaveNetworkProfileResponse { Ok = false, Error = error };
    }
}

/// <summary>
/// Request to delete a network profile.
/// </summary>
public sealed class DeleteNetworkProfileRequest : IpcRequest
{
    public const string RequestType = "delete-network-profile";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// ID of the profile to delete.
    /// </summary>
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = string.Empty;
}

/// <summary>
/// Response for deleting a network profile.
/// </summary>
public sealed class DeleteNetworkProfileResponse : IpcResponse
{
    public static DeleteNetworkProfileResponse Success()
    {
        return new DeleteNetworkProfileResponse { Ok = true };
    }

    public static DeleteNetworkProfileResponse Failure(string error)
    {
        return new DeleteNetworkProfileResponse { Ok = false, Error = error };
    }
}

/// <summary>
/// Request to get current network information.
/// </summary>
public sealed class GetCurrentNetworkRequest : IpcRequest
{
    public const string RequestType = "get-current-network";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response containing current network information.
/// </summary>
public sealed class GetCurrentNetworkResponse : IpcResponse
{
    /// <summary>
    /// Information about the current network.
    /// </summary>
    [JsonPropertyName("network")]
    public CurrentNetworkInfo Network { get; set; } = new();

    /// <summary>
    /// ID of the profile that matches current network (null if none).
    /// </summary>
    [JsonPropertyName("matchingProfileId")]
    public string? MatchingProfileId { get; set; }

    public static GetCurrentNetworkResponse Success(CurrentNetworkInfo network, string? matchingProfileId = null)
    {
        return new GetCurrentNetworkResponse
        {
            Ok = true,
            Network = network,
            MatchingProfileId = matchingProfileId
        };
    }

    public static GetCurrentNetworkResponse Failure(string error)
    {
        return new GetCurrentNetworkResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Request to activate a specific profile manually.
/// </summary>
public sealed class ActivateProfileRequest : IpcRequest
{
    public const string RequestType = "activate-profile";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// ID of the profile to activate (null for auto-detection).
    /// </summary>
    [JsonPropertyName("profileId")]
    public string? ProfileId { get; set; }
}

/// <summary>
/// Response for activating a profile.
/// </summary>
public sealed class ActivateProfileResponse : IpcResponse
{
    /// <summary>
    /// ID of the activated profile.
    /// </summary>
    [JsonPropertyName("activatedProfileId")]
    public string? ActivatedProfileId { get; set; }

    /// <summary>
    /// Whether the policy was applied successfully.
    /// </summary>
    [JsonPropertyName("policyApplied")]
    public bool PolicyApplied { get; set; }

    public static ActivateProfileResponse Success(string? profileId, bool policyApplied)
    {
        return new ActivateProfileResponse
        {
            Ok = true,
            ActivatedProfileId = profileId,
            PolicyApplied = policyApplied
        };
    }

    public static ActivateProfileResponse Failure(string error)
    {
        return new ActivateProfileResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Request to enable or disable automatic profile switching.
/// </summary>
public sealed class SetAutoSwitchRequest : IpcRequest
{
    public const string RequestType = "set-auto-switch";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// Whether automatic switching should be enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

/// <summary>
/// Response for setting auto-switch.
/// </summary>
public sealed class SetAutoSwitchResponse : IpcResponse
{
    public static SetAutoSwitchResponse Success()
    {
        return new SetAutoSwitchResponse { Ok = true };
    }

    public static SetAutoSwitchResponse Failure(string error)
    {
        return new SetAutoSwitchResponse { Ok = false, Error = error };
    }
}

/// <summary>
/// Request to get auto-switch status.
/// </summary>
public sealed class GetAutoSwitchStatusRequest : IpcRequest
{
    public const string RequestType = "get-auto-switch-status";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response containing auto-switch status.
/// </summary>
public sealed class GetAutoSwitchStatusResponse : IpcResponse
{
    /// <summary>
    /// Whether automatic switching is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// ID of the currently active profile.
    /// </summary>
    [JsonPropertyName("activeProfileId")]
    public string? ActiveProfileId { get; set; }

    /// <summary>
    /// Name of the currently active profile.
    /// </summary>
    [JsonPropertyName("activeProfileName")]
    public string? ActiveProfileName { get; set; }

    public static GetAutoSwitchStatusResponse Success(bool enabled, string? activeProfileId, string? activeProfileName)
    {
        return new GetAutoSwitchStatusResponse
        {
            Ok = true,
            Enabled = enabled,
            ActiveProfileId = activeProfileId,
            ActiveProfileName = activeProfileName
        };
    }

    public static GetAutoSwitchStatusResponse Failure(string error)
    {
        return new GetAutoSwitchStatusResponse
        {
            Ok = false,
            Error = error
        };
    }
}
