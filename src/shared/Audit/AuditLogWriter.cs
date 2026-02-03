namespace WfpTrafficControl.Shared.Audit;

/// <summary>
/// Thread-safe audit log writer that appends JSON Lines to the audit log file.
/// </summary>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly string _logPath;
    private readonly object _writeLock = new();

    /// <summary>
    /// Creates a new audit log writer using the default audit log path.
    /// </summary>
    public AuditLogWriter()
        : this(WfpConstants.GetAuditLogPath())
    {
    }

    /// <summary>
    /// Creates a new audit log writer with a custom log path.
    /// </summary>
    public AuditLogWriter(string logPath)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
    }

    /// <summary>
    /// Gets the path to the audit log file.
    /// </summary>
    public string LogPath => _logPath;

    /// <summary>
    /// Writes an audit log entry to the log file.
    /// Thread-safe and handles directory creation and disk errors gracefully.
    /// </summary>
    public void Write(AuditLogEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        try
        {
            var json = entry.ToJson();
            WriteLineToFile(json);
        }
        catch (IOException)
        {
            // Log write failure - don't crash the service
            // Callers can log this if they have access to a logger
        }
        catch (UnauthorizedAccessException)
        {
            // Log write failure - don't crash the service
        }
        catch
        {
            // Catch all other errors to ensure audit logging never crashes the service
        }
    }

    /// <summary>
    /// Writes a line to the audit log file with proper locking.
    /// </summary>
    private void WriteLineToFile(string line)
    {
        lock (_writeLock)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Append the line with a newline character
            // Using FileShare.Read to allow readers while writing
            using var stream = new FileStream(
                _logPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.WriteLine(line);
        }
    }
}

/// <summary>
/// Interface for audit log writing, allows for testing and dependency injection.
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Writes an audit log entry.
    /// </summary>
    void Write(AuditLogEntry entry);

    /// <summary>
    /// Gets the path to the audit log file.
    /// </summary>
    string LogPath { get; }
}

/// <summary>
/// Null audit log writer for testing or when logging is disabled.
/// </summary>
public sealed class NullAuditLogWriter : IAuditLogWriter
{
    public static readonly NullAuditLogWriter Instance = new();

    public string LogPath => string.Empty;

    public void Write(AuditLogEntry entry)
    {
        // No-op
    }
}
