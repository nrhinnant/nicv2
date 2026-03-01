using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.Shared.Logging;

/// <summary>
/// Syslog severity levels (RFC 5424).
/// </summary>
public enum SyslogSeverity
{
    Emergency = 0,
    Alert = 1,
    Critical = 2,
    Error = 3,
    Warning = 4,
    Notice = 5,
    Informational = 6,
    Debug = 7
}

/// <summary>
/// Event data for syslog export.
/// </summary>
public class SyslogEvent
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public SyslogSeverity Severity { get; set; } = SyslogSeverity.Informational;
    public string EventType { get; set; } = "unknown";
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Status { get; set; }
    public string? PolicyVersion { get; set; }
    public int? FiltersCreated { get; set; }
    public int? FiltersRemoved { get; set; }
    public string? ProcessPath { get; set; }
    public string? RemoteIp { get; set; }
    public int? RemotePort { get; set; }
    public string? Direction { get; set; }
    public string? Protocol { get; set; }
    public string? RuleId { get; set; }
    public Dictionary<string, string> AdditionalData { get; set; } = new();
}

/// <summary>
/// Exports audit events to a syslog server.
/// Supports UDP, TCP, and TLS transports with RFC 5424, CEF, and JSON formats.
/// </summary>
public sealed class SyslogExporter : IDisposable
{
    private SyslogConfig _config = new();
    private UdpClient? _udpClient;
    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private Stream? _stream;
    private readonly object _lock = new();
    private bool _disposed;
    private readonly string _hostname;

    public SyslogExporter()
    {
        _hostname = Environment.MachineName;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public SyslogConfig Config
    {
        get
        {
            lock (_lock)
            {
                return _config;
            }
        }
    }

    /// <summary>
    /// Updates the syslog configuration.
    /// </summary>
    public void Configure(SyslogConfig config)
    {
        lock (_lock)
        {
            // Close existing connections if config changed
            if (_config.Host != config.Host ||
                _config.Port != config.Port ||
                _config.Protocol != config.Protocol)
            {
                CloseConnections();
            }

            _config = config;
        }
    }

    /// <summary>
    /// Sends a syslog event.
    /// </summary>
    public Result<bool> Send(SyslogEvent evt)
    {
        lock (_lock)
        {
            if (!_config.Enabled)
            {
                return Result<bool>.Success(false);
            }

            try
            {
                var message = FormatMessage(evt);
                var bytes = Encoding.UTF8.GetBytes(message);

                switch (_config.Protocol)
                {
                    case SyslogProtocol.Udp:
                        return SendUdp(bytes);
                    case SyslogProtocol.Tcp:
                        return SendTcp(bytes);
                    case SyslogProtocol.Tls:
                        return SendTls(bytes);
                    default:
                        return Result<bool>.Failure(ErrorCodes.InvalidArgument, "Unknown protocol");
                }
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure(ErrorCodes.NetworkError, $"Failed to send syslog: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tests the syslog connection by sending a test message.
    /// </summary>
    public Result<int> TestConnection()
    {
        lock (_lock)
        {
            if (!_config.Enabled)
            {
                return Result<int>.Failure(ErrorCodes.InvalidArgument, "Syslog export is not enabled");
            }

            var testEvent = new SyslogEvent
            {
                Timestamp = DateTime.UtcNow,
                Severity = SyslogSeverity.Notice,
                EventType = "test",
                Message = "Test message from WFP Traffic Control"
            };

            var start = DateTime.UtcNow;
            var result = Send(testEvent);
            var elapsed = DateTime.UtcNow - start;

            if (result.IsFailure)
            {
                return Result<int>.Failure(result.Error.Code, result.Error.Message);
            }

            return Result<int>.Success((int)elapsed.TotalMilliseconds);
        }
    }

    private string FormatMessage(SyslogEvent evt)
    {
        return _config.Format switch
        {
            SyslogFormat.Rfc5424 => FormatRfc5424(evt),
            SyslogFormat.Cef => FormatCef(evt),
            SyslogFormat.Json => FormatJson(evt),
            _ => FormatRfc5424(evt)
        };
    }

    private string FormatRfc5424(SyslogEvent evt)
    {
        // PRI = Facility * 8 + Severity
        var pri = (_config.Facility * 8) + (int)evt.Severity;

        // RFC 5424 format:
        // <PRI>VERSION TIMESTAMP HOSTNAME APP-NAME PROCID MSGID STRUCTURED-DATA MSG
        var timestamp = evt.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var structuredData = BuildStructuredData(evt);

        return $"<{pri}>1 {timestamp} {_hostname} {_config.AppName} - {evt.EventType} {structuredData} {evt.Message}";
    }

    private string BuildStructuredData(SyslogEvent evt)
    {
        var sb = new StringBuilder();
        sb.Append($"[wfp@0 eventType=\"{evt.EventType}\"");

        if (!string.IsNullOrEmpty(evt.Source))
            sb.Append($" source=\"{EscapeSdValue(evt.Source)}\"");
        if (!string.IsNullOrEmpty(evt.Status))
            sb.Append($" status=\"{EscapeSdValue(evt.Status)}\"");
        if (!string.IsNullOrEmpty(evt.PolicyVersion))
            sb.Append($" policyVersion=\"{EscapeSdValue(evt.PolicyVersion)}\"");
        if (evt.FiltersCreated.HasValue)
            sb.Append($" filtersCreated=\"{evt.FiltersCreated}\"");
        if (evt.FiltersRemoved.HasValue)
            sb.Append($" filtersRemoved=\"{evt.FiltersRemoved}\"");
        if (!string.IsNullOrEmpty(evt.ProcessPath))
            sb.Append($" processPath=\"{EscapeSdValue(evt.ProcessPath)}\"");
        if (!string.IsNullOrEmpty(evt.RemoteIp))
            sb.Append($" remoteIp=\"{EscapeSdValue(evt.RemoteIp)}\"");
        if (evt.RemotePort.HasValue)
            sb.Append($" remotePort=\"{evt.RemotePort}\"");
        if (!string.IsNullOrEmpty(evt.Direction))
            sb.Append($" direction=\"{EscapeSdValue(evt.Direction)}\"");
        if (!string.IsNullOrEmpty(evt.Protocol))
            sb.Append($" protocol=\"{EscapeSdValue(evt.Protocol)}\"");
        if (!string.IsNullOrEmpty(evt.RuleId))
            sb.Append($" ruleId=\"{EscapeSdValue(evt.RuleId)}\"");

        foreach (var kvp in evt.AdditionalData)
        {
            sb.Append($" {kvp.Key}=\"{EscapeSdValue(kvp.Value)}\"");
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string EscapeSdValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("]", "\\]");
    }

    private string FormatCef(SyslogEvent evt)
    {
        // CEF format:
        // CEF:Version|Device Vendor|Device Product|Device Version|Signature ID|Name|Severity|Extension
        var severity = MapSeverityToCef(evt.Severity);
        var timestamp = evt.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var extension = new StringBuilder();
        extension.Append($"rt={timestamp}");
        extension.Append($" dhost={_hostname}");

        if (!string.IsNullOrEmpty(evt.Source))
            extension.Append($" src={EscapeCefValue(evt.Source)}");
        if (!string.IsNullOrEmpty(evt.Status))
            extension.Append($" outcome={EscapeCefValue(evt.Status)}");
        if (!string.IsNullOrEmpty(evt.PolicyVersion))
            extension.Append($" cs1={EscapeCefValue(evt.PolicyVersion)} cs1Label=PolicyVersion");
        if (evt.FiltersCreated.HasValue)
            extension.Append($" cn1={evt.FiltersCreated} cn1Label=FiltersCreated");
        if (evt.FiltersRemoved.HasValue)
            extension.Append($" cn2={evt.FiltersRemoved} cn2Label=FiltersRemoved");
        if (!string.IsNullOrEmpty(evt.ProcessPath))
            extension.Append($" sproc={EscapeCefValue(evt.ProcessPath)}");
        if (!string.IsNullOrEmpty(evt.RemoteIp))
            extension.Append($" dst={EscapeCefValue(evt.RemoteIp)}");
        if (evt.RemotePort.HasValue)
            extension.Append($" dpt={evt.RemotePort}");
        if (!string.IsNullOrEmpty(evt.Direction))
            extension.Append($" deviceDirection={(evt.Direction.ToLowerInvariant() == "inbound" ? "0" : "1")}");
        if (!string.IsNullOrEmpty(evt.Protocol))
            extension.Append($" proto={EscapeCefValue(evt.Protocol)}");
        if (!string.IsNullOrEmpty(evt.RuleId))
            extension.Append($" cs2={EscapeCefValue(evt.RuleId)} cs2Label=RuleId");
        if (!string.IsNullOrEmpty(evt.Message))
            extension.Append($" msg={EscapeCefValue(evt.Message)}");

        // PRI header for syslog
        var pri = (_config.Facility * 8) + (int)evt.Severity;
        return $"<{pri}>CEF:0|WfpTrafficControl|WFP Traffic Control|1.0|{evt.EventType}|{EscapeCefPipe(evt.Message)}|{severity}|{extension}";
    }

    private static int MapSeverityToCef(SyslogSeverity severity)
    {
        // CEF uses 0-10 scale
        return severity switch
        {
            SyslogSeverity.Emergency => 10,
            SyslogSeverity.Alert => 9,
            SyslogSeverity.Critical => 8,
            SyslogSeverity.Error => 7,
            SyslogSeverity.Warning => 5,
            SyslogSeverity.Notice => 4,
            SyslogSeverity.Informational => 3,
            SyslogSeverity.Debug => 1,
            _ => 5
        };
    }

    private static string EscapeCefValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("=", "\\=");
    }

    private static string EscapeCefPipe(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("|", "\\|");
    }

    private string FormatJson(SyslogEvent evt)
    {
        var obj = new Dictionary<string, object?>
        {
            ["timestamp"] = evt.Timestamp.ToString("o"),
            ["severity"] = evt.Severity.ToString().ToLowerInvariant(),
            ["eventType"] = evt.EventType,
            ["message"] = evt.Message,
            ["hostname"] = _hostname,
            ["appName"] = _config.AppName
        };

        if (!string.IsNullOrEmpty(evt.Source)) obj["source"] = evt.Source;
        if (!string.IsNullOrEmpty(evt.Status)) obj["status"] = evt.Status;
        if (!string.IsNullOrEmpty(evt.PolicyVersion)) obj["policyVersion"] = evt.PolicyVersion;
        if (evt.FiltersCreated.HasValue) obj["filtersCreated"] = evt.FiltersCreated;
        if (evt.FiltersRemoved.HasValue) obj["filtersRemoved"] = evt.FiltersRemoved;
        if (!string.IsNullOrEmpty(evt.ProcessPath)) obj["processPath"] = evt.ProcessPath;
        if (!string.IsNullOrEmpty(evt.RemoteIp)) obj["remoteIp"] = evt.RemoteIp;
        if (evt.RemotePort.HasValue) obj["remotePort"] = evt.RemotePort;
        if (!string.IsNullOrEmpty(evt.Direction)) obj["direction"] = evt.Direction;
        if (!string.IsNullOrEmpty(evt.Protocol)) obj["protocol"] = evt.Protocol;
        if (!string.IsNullOrEmpty(evt.RuleId)) obj["ruleId"] = evt.RuleId;

        foreach (var kvp in evt.AdditionalData)
        {
            obj[kvp.Key] = kvp.Value;
        }

        var json = System.Text.Json.JsonSerializer.Serialize(obj);

        // Add syslog PRI header
        var pri = (_config.Facility * 8) + (int)evt.Severity;
        return $"<{pri}>{json}";
    }

    private Result<bool> SendUdp(byte[] bytes)
    {
        try
        {
            _udpClient ??= new UdpClient();

            _udpClient.Send(bytes, bytes.Length, _config.Host, _config.Port);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _udpClient?.Dispose();
            _udpClient = null;
            return Result<bool>.Failure(ErrorCodes.NetworkError, $"UDP send failed: {ex.Message}");
        }
    }

    private Result<bool> SendTcp(byte[] bytes)
    {
        try
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                _tcpClient?.Dispose();
                _tcpClient = new TcpClient();
                _tcpClient.Connect(_config.Host, _config.Port);
                _stream = _tcpClient.GetStream();
            }

            // TCP syslog uses newline as message delimiter
            _stream!.Write(bytes, 0, bytes.Length);
            _stream.WriteByte((byte)'\n');
            _stream.Flush();

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            CloseConnections();
            return Result<bool>.Failure(ErrorCodes.NetworkError, $"TCP send failed: {ex.Message}");
        }
    }

    private Result<bool> SendTls(byte[] bytes)
    {
        try
        {
            if (_tcpClient == null || !_tcpClient.Connected)
            {
                _tcpClient?.Dispose();
                _sslStream?.Dispose();

                _tcpClient = new TcpClient();
                _tcpClient.Connect(_config.Host, _config.Port);

                _sslStream = new SslStream(
                    _tcpClient.GetStream(),
                    false,
                    _config.VerifyCertificate ? null : (sender, cert, chain, errors) => true);

                _sslStream.AuthenticateAsClient(_config.Host);
                _stream = _sslStream;
            }

            _stream!.Write(bytes, 0, bytes.Length);
            _stream.WriteByte((byte)'\n');
            _stream.Flush();

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            CloseConnections();
            return Result<bool>.Failure(ErrorCodes.NetworkError, $"TLS send failed: {ex.Message}");
        }
    }

    private void CloseConnections()
    {
        _sslStream?.Dispose();
        _sslStream = null;
        _stream = null;
        _tcpClient?.Dispose();
        _tcpClient = null;
        _udpClient?.Dispose();
        _udpClient = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            CloseConnections();
        }
    }
}
