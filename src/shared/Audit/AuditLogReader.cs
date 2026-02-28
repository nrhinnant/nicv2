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
    /// Optimized to read from end of file without loading entire content.
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
            var lines = ReadLinesFromEnd(count);
            var entries = new List<AuditLogEntry>(Math.Min(count, lines.Count));

            // Lines are already in reverse order (newest first)
            foreach (var line in lines)
            {
                var entry = AuditLogEntry.FromJson(line);
                if (entry != null)
                {
                    entries.Add(entry);
                    if (entries.Count >= count)
                    {
                        break;
                    }
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
    /// Reads lines from the end of the file, returning them newest first.
    /// Uses backward scanning to avoid loading the entire file.
    /// </summary>
    /// <param name="maxLines">Maximum number of lines to read.</param>
    /// <returns>Lines in reverse order (newest first).</returns>
    private List<string> ReadLinesFromEnd(int maxLines)
    {
        const int BufferSize = 4096;
        var lines = new List<string>();

        using var stream = new FileStream(
            _logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        if (stream.Length == 0)
        {
            return lines;
        }

        // Use StreamReader for proper encoding handling, but read in chunks from end
        // For simplicity and correctness with UTF-8, we'll read backwards in chunks
        // and accumulate complete lines

        var buffer = new byte[BufferSize];
        var lineBuffer = new List<byte>();
        long position = stream.Length;

        while (position > 0 && lines.Count < maxLines)
        {
            // Calculate how much to read
            int bytesToRead = (int)Math.Min(BufferSize, position);
            position -= bytesToRead;
            stream.Seek(position, SeekOrigin.Begin);

            int bytesRead = stream.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0)
            {
                break;
            }

            // Process bytes in reverse order
            for (int i = bytesRead - 1; i >= 0 && lines.Count < maxLines; i--)
            {
                byte b = buffer[i];

                if (b == '\n')
                {
                    // Found end of a line, extract it
                    if (lineBuffer.Count > 0)
                    {
                        // Remove trailing CR if present
                        if (lineBuffer.Count > 0 && lineBuffer[lineBuffer.Count - 1] == '\r')
                        {
                            lineBuffer.RemoveAt(lineBuffer.Count - 1);
                        }

                        // Reverse the buffer since we built it backwards
                        lineBuffer.Reverse();
                        var lineText = System.Text.Encoding.UTF8.GetString(lineBuffer.ToArray());

                        if (!string.IsNullOrWhiteSpace(lineText))
                        {
                            lines.Add(lineText);
                        }
                        lineBuffer.Clear();
                    }
                }
                else if (b != '\r')
                {
                    // Add non-CR characters to buffer
                    lineBuffer.Add(b);
                }
            }
        }

        // Handle any remaining content (first line of file without leading newline)
        if (lineBuffer.Count > 0 && lines.Count < maxLines)
        {
            lineBuffer.Reverse();
            var lineText = System.Text.Encoding.UTF8.GetString(lineBuffer.ToArray());

            if (!string.IsNullOrWhiteSpace(lineText))
            {
                lines.Add(lineText);
            }
        }

        return lines;
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
    /// Optimized to count newlines directly without loading file content.
    /// </summary>
    public int GetEntryCount()
    {
        if (!File.Exists(_logPath))
        {
            return 0;
        }

        try
        {
            return CountLinesOptimized();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Counts lines in the file by scanning for newlines directly.
    /// Much more efficient than loading all content into memory.
    /// </summary>
    private int CountLinesOptimized()
    {
        using var stream = new FileStream(
            _logPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        if (stream.Length == 0)
        {
            return 0;
        }

        int lineCount = 0;
        byte[] buffer = new byte[4096];
        int bytesRead;
        bool lastCharWasNewline = false;
        bool hasContent = false;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                byte b = buffer[i];
                if (b == '\n')
                {
                    lineCount++;
                    lastCharWasNewline = true;
                }
                else if (b != '\r')
                {
                    hasContent = true;
                    lastCharWasNewline = false;
                }
            }
        }

        // If file doesn't end with newline but has content, count the last line
        if (hasContent && !lastCharWasNewline)
        {
            lineCount++;
        }

        return lineCount;
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
