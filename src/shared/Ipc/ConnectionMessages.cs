using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to get active network connections.
/// </summary>
public sealed class GetConnectionsRequest : IpcRequest
{
    public const string RequestType = "get-connections";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// Whether to include TCP connections.
    /// </summary>
    [JsonPropertyName("includeTcp")]
    public bool IncludeTcp { get; set; } = true;

    /// <summary>
    /// Whether to include UDP connections.
    /// </summary>
    [JsonPropertyName("includeUdp")]
    public bool IncludeUdp { get; set; } = true;
}

/// <summary>
/// Response containing active network connections.
/// </summary>
public sealed class GetConnectionsResponse : IpcResponse
{
    /// <summary>
    /// List of active connections.
    /// </summary>
    [JsonPropertyName("connections")]
    public List<ConnectionDto> Connections { get; set; } = new();

    /// <summary>
    /// Total count of connections.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// Timestamp when the snapshot was taken.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    public static GetConnectionsResponse Success(List<ConnectionDto> connections)
    {
        return new GetConnectionsResponse
        {
            Ok = true,
            Connections = connections,
            Count = connections.Count,
            Timestamp = DateTime.UtcNow
        };
    }

    public static GetConnectionsResponse Failure(string error)
    {
        return new GetConnectionsResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// DTO representing a network connection.
/// </summary>
public sealed class ConnectionDto
{
    /// <summary>
    /// Protocol (tcp or udp).
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Connection state (for TCP: ESTABLISHED, LISTEN, etc.).
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Local IP address.
    /// </summary>
    [JsonPropertyName("localIp")]
    public string LocalIp { get; set; } = string.Empty;

    /// <summary>
    /// Local port.
    /// </summary>
    [JsonPropertyName("localPort")]
    public int LocalPort { get; set; }

    /// <summary>
    /// Remote IP address.
    /// </summary>
    [JsonPropertyName("remoteIp")]
    public string RemoteIp { get; set; } = string.Empty;

    /// <summary>
    /// Remote port.
    /// </summary>
    [JsonPropertyName("remotePort")]
    public int RemotePort { get; set; }

    /// <summary>
    /// Process ID owning the connection.
    /// </summary>
    [JsonPropertyName("processId")]
    public int ProcessId { get; set; }

    /// <summary>
    /// Process name (e.g., "chrome.exe").
    /// </summary>
    [JsonPropertyName("processName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessName { get; set; }

    /// <summary>
    /// Full process path.
    /// </summary>
    [JsonPropertyName("processPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessPath { get; set; }

    /// <summary>
    /// Gets a display string for the local endpoint.
    /// </summary>
    [JsonIgnore]
    public string LocalEndpoint => $"{LocalIp}:{LocalPort}";

    /// <summary>
    /// Gets a display string for the remote endpoint.
    /// </summary>
    [JsonIgnore]
    public string RemoteEndpoint => $"{RemoteIp}:{RemotePort}";
}
