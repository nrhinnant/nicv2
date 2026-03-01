using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Syslog transport protocol.
/// </summary>
public enum SyslogProtocol
{
    Udp,
    Tcp,
    Tls
}

/// <summary>
/// Syslog message format.
/// </summary>
public enum SyslogFormat
{
    /// <summary>
    /// RFC 5424 structured syslog.
    /// </summary>
    Rfc5424,

    /// <summary>
    /// Common Event Format (CEF) for SIEM integration.
    /// </summary>
    Cef,

    /// <summary>
    /// JSON format.
    /// </summary>
    Json
}

/// <summary>
/// Syslog configuration.
/// </summary>
public class SyslogConfig
{
    /// <summary>
    /// Whether syslog export is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>
    /// Syslog server hostname or IP address.
    /// </summary>
    [JsonPropertyName("host")]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Syslog server port.
    /// </summary>
    [JsonPropertyName("port")]
    public int Port { get; set; } = 514;

    /// <summary>
    /// Transport protocol (UDP, TCP, TLS).
    /// </summary>
    [JsonPropertyName("protocol")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SyslogProtocol Protocol { get; set; } = SyslogProtocol.Udp;

    /// <summary>
    /// Message format (RFC5424, CEF, JSON).
    /// </summary>
    [JsonPropertyName("format")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SyslogFormat Format { get; set; } = SyslogFormat.Rfc5424;

    /// <summary>
    /// Facility code (default: 16 = local0).
    /// </summary>
    [JsonPropertyName("facility")]
    public int Facility { get; set; } = 16;

    /// <summary>
    /// Application name for syslog messages.
    /// </summary>
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = "WfpTrafficControl";

    /// <summary>
    /// Whether to verify TLS certificates (for TLS protocol).
    /// </summary>
    [JsonPropertyName("verifyCertificate")]
    public bool VerifyCertificate { get; set; } = true;
}

/// <summary>
/// Request to get syslog configuration.
/// </summary>
public sealed class GetSyslogConfigRequest : IpcRequest
{
    public const string RequestType = "get-syslog-config";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response containing syslog configuration.
/// </summary>
public sealed class GetSyslogConfigResponse : IpcResponse
{
    /// <summary>
    /// Current syslog configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public SyslogConfig Config { get; set; } = new();

    public static GetSyslogConfigResponse Success(SyslogConfig config)
    {
        return new GetSyslogConfigResponse
        {
            Ok = true,
            Config = config
        };
    }

    public static GetSyslogConfigResponse Failure(string error)
    {
        return new GetSyslogConfigResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Request to set syslog configuration.
/// </summary>
public sealed class SetSyslogConfigRequest : IpcRequest
{
    public const string RequestType = "set-syslog-config";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// New syslog configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public SyslogConfig Config { get; set; } = new();
}

/// <summary>
/// Response for setting syslog configuration.
/// </summary>
public sealed class SetSyslogConfigResponse : IpcResponse
{
    public static SetSyslogConfigResponse Success()
    {
        return new SetSyslogConfigResponse { Ok = true };
    }

    public static SetSyslogConfigResponse Failure(string error)
    {
        return new SetSyslogConfigResponse { Ok = false, Error = error };
    }
}

/// <summary>
/// Request to test syslog connection.
/// </summary>
public sealed class TestSyslogRequest : IpcRequest
{
    public const string RequestType = "test-syslog";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response for syslog connection test.
/// </summary>
public sealed class TestSyslogResponse : IpcResponse
{
    /// <summary>
    /// Whether the test message was sent successfully.
    /// </summary>
    [JsonPropertyName("sent")]
    public bool Sent { get; set; }

    /// <summary>
    /// Round-trip time in milliseconds (if applicable).
    /// </summary>
    [JsonPropertyName("rttMs")]
    public int? RttMs { get; set; }

    public static TestSyslogResponse Success(int? rttMs = null)
    {
        return new TestSyslogResponse
        {
            Ok = true,
            Sent = true,
            RttMs = rttMs
        };
    }

    public static TestSyslogResponse NotEnabled()
    {
        return new TestSyslogResponse
        {
            Ok = true,
            Sent = false,
            Error = "Syslog export is not enabled"
        };
    }

    public static TestSyslogResponse Failure(string error)
    {
        return new TestSyslogResponse
        {
            Ok = false,
            Sent = false,
            Error = error
        };
    }
}
