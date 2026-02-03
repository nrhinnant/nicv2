namespace WfpTrafficControl.Shared.Audit;

/// <summary>
/// Reads and queries audit log entries from the audit log file.
/// </summary>
public sealed class AuditLogReader
{
    private readonly string _logPath;

    /// <summary>
    /// Creates a new audit log reader using the default audit log path.
    /// </summary>
    public AuditLogReader()
        : this(WfpConstants.GetAuditLogPath())
    {
    }

    /// <summary>
    /// Creates a new audit log reader with a custom log path.
    /// </summary>
    public AuditLogReader(string logPath)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
    }

    /// <summary>
    /// Gets the path to the audit log file.
    /// </summary>
    public string LogPath => _logPath;

    /// <summary>
    /// Checks if the audit log file exists.
    /// </summary>
    public bool LogFileExists => File.Exists(_logPath);

    /// <summary>
    /// Reads the last N entries from the audit log.
    /// </summary>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <returns>List of audit log entries, newest first.</returns>
    public List<AuditLogEntry> ReadTail(int count)
    {
        if (count <= 0)
        {
            return new List<AuditLogEntry>();
        }

        if (!File.Exists(_logPath))
        {
            return new List<AuditLogEntry>();
        }

        try
        {
            // Read all lines and take the last N
            // For large files, this could be optimized to read from end
            var lines = ReadLinesShared();
            var entries = new List<AuditLogEntry>();

            // Process from end to get newest first
            for (int i = lines.Count - 1; i >= 0 && entries.Count < count; i--)
            {
                var entry = AuditLogEntry.FromJson(lines[i]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }
        catch
        {
            return new List<AuditLogEntry>();
        }
    }

    /// <summary>
    /// Reads entries from the last N minutes.
    /// </summary>
    /// <param name="minutes">Number of minutes to look back.</param>
    /// <returns>List of audit log entries within the time window, newest first.</returns>
    public List<AuditLogEntry> ReadSince(int minutes)
    {
        if (minutes <= 0)
        {
            return new List<AuditLogEntry>();
        }

        if (!File.Exists(_logPath))
        {
            return new List<AuditLogEntry>();
        }

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutes);
        var entries = new List<AuditLogEntry>();

        try
        {
            var lines = ReadLinesShared();

            // Process from end to get newest first
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var entry = AuditLogEntry.FromJson(lines[i]);
                if (entry == null)
                {
                    continue;
                }

                // Parse the timestamp
                if (!DateTimeOffset.TryParse(entry.Timestamp, out var entryTime))
                {
                    continue;
                }

                // Stop when we've gone past the cutoff
                if (entryTime < cutoff)
                {
                    break;
                }

                entries.Add(entry);
            }

            return entries;
        }
        catch
        {
            return new List<AuditLogEntry>();
        }
    }

    /// <summary>
    /// Reads all entries from the audit log.
    /// </summary>
    /// <returns>List of all audit log entries, newest first.</returns>
    public List<AuditLogEntry> ReadAll()
    {
        if (!File.Exists(_logPath))
        {
            return new List<AuditLogEntry>();
        }

        try
        {
            var lines = ReadLinesShared();
            var entries = new List<AuditLogEntry>();

            // Process from end to get newest first
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var entry = AuditLogEntry.FromJson(lines[i]);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }
        catch
        {
            return new List<AuditLogEntry>();
        }
    }

    /// <summary>
    /// Gets the total number of entries in the audit log.
    /// </summary>
    public int GetEntryCount()
    {
        if (!File.Exists(_logPath))
        {
            return 0;
        }

        try
        {
            var lines = ReadLinesShared();
            return lines.Count(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Reads all lines from the log file with shared read access.
    /// Allows reading while the service is writing.
    /// </summary>
    private List<string> ReadLinesShared()
    {
        var lines = new List<string>();

        using var stream = new FileStream(
            _logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
