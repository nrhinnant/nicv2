using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WfpTrafficControl.Shared.Audit;

/// <summary>
/// Thread-safe audit log writer that appends JSON Lines to the audit log file.
/// </summary>
/// <remarks>
/// <para><strong>Security Model:</strong></para>
/// <para>
/// This writer applies protective ACLs to the audit log file to prevent tampering.
/// Once the ACL is applied, the file has the following permissions:
/// </para>
/// <list type="bullet">
///   <item><description>LocalSystem: Full Control (the service account)</description></item>
///   <item><description>Administrators: Read + AppendData only (cannot delete or overwrite existing entries)</description></item>
///   <item><description>Users: No access</description></item>
/// </list>
/// <para>
/// This append-only model ensures that audit log entries cannot be tampered with or deleted
/// by administrators, while still allowing them to read logs and allowing the service to
/// perform log rotation if needed.
/// </para>
/// <para>
/// ACL protection is applied on the first successful write and is non-fatal if it fails
/// (the log continues to work, just without protection).
/// </para>
/// </remarks>
public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly string _logPath;
    private readonly object _writeLock = new();
    private readonly long _maxFileSize;
    private readonly int _maxRotatedFiles;
    private bool _aclApplied;
    private long _failedWriteCount;
    private long _rotationCount;
    private int _writesSinceLastSizeCheck;

    /// <summary>
    /// Default maximum file size before rotation (10 MB).
    /// </summary>
    public const long DefaultMaxFileSize = 10 * 1024 * 1024;

    /// <summary>
    /// Default number of rotated log files to keep.
    /// </summary>
    public const int DefaultMaxRotatedFiles = 5;

    /// <summary>
    /// Number of writes between file size checks (optimization to avoid checking on every write).
    /// </summary>
    private const int WritesBetweenSizeChecks = 50;

    /// <summary>
    /// Well-known SID for LocalSystem account.
    /// </summary>
    private static readonly SecurityIdentifier LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>
    /// Well-known SID for Administrators group.
    /// </summary>
    private static readonly SecurityIdentifier AdministratorsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);

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
        : this(logPath, DefaultMaxFileSize, DefaultMaxRotatedFiles)
    {
    }

    /// <summary>
    /// Creates a new audit log writer with custom rotation settings.
    /// </summary>
    /// <param name="logPath">Path to the audit log file.</param>
    /// <param name="maxFileSize">Maximum file size in bytes before rotation (default: 10 MB).</param>
    /// <param name="maxRotatedFiles">Maximum number of rotated files to keep (default: 5).</param>
    public AuditLogWriter(string logPath, long maxFileSize, int maxRotatedFiles)
    {
        _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));

        if (maxFileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFileSize), "Must be positive");
        if (maxRotatedFiles < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRotatedFiles), "Cannot be negative");

        _maxFileSize = maxFileSize;
        _maxRotatedFiles = maxRotatedFiles;
    }

    /// <summary>
    /// Gets the path to the audit log file.
    /// </summary>
    public string LogPath => _logPath;

    /// <summary>
    /// Gets the number of failed write attempts since this writer was created.
    /// Useful for diagnostics and monitoring.
    /// </summary>
    public long FailedWriteCount => Interlocked.Read(ref _failedWriteCount);

    /// <summary>
    /// Gets the number of log rotations performed since this writer was created.
    /// </summary>
    public long RotationCount => Interlocked.Read(ref _rotationCount);

    /// <summary>
    /// Gets the maximum file size before rotation.
    /// </summary>
    public long MaxFileSize => _maxFileSize;

    /// <summary>
    /// Gets the maximum number of rotated files to keep.
    /// </summary>
    public int MaxRotatedFiles => _maxRotatedFiles;

    /// <summary>
    /// Writes an audit log entry to the log file.
    /// Thread-safe and handles directory creation and disk errors gracefully.
    /// </summary>
    /// <remarks>
    /// Write failures are logged via <see cref="Debug.WriteLine"/> and tracked
    /// in <see cref="FailedWriteCount"/>. The method never throws to ensure
    /// audit logging doesn't crash the service.
    /// </remarks>
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
        catch (IOException ex)
        {
            Interlocked.Increment(ref _failedWriteCount);
            Debug.WriteLine($"[AuditLogWriter] ERROR: Failed to write audit log entry (I/O error): {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Interlocked.Increment(ref _failedWriteCount);
            Debug.WriteLine($"[AuditLogWriter] ERROR: Failed to write audit log entry (access denied): {ex.Message}");
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedWriteCount);
            Debug.WriteLine($"[AuditLogWriter] ERROR: Failed to write audit log entry: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a line to the audit log file with proper locking.
    /// Performs log rotation if the file exceeds the maximum size.
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

            // Check for rotation periodically (not on every write for performance)
            _writesSinceLastSizeCheck++;
            if (_writesSinceLastSizeCheck >= WritesBetweenSizeChecks)
            {
                _writesSinceLastSizeCheck = 0;
                CheckAndRotateIfNeeded();
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

            // Apply ACL protection on first successful write
            if (!_aclApplied && File.Exists(_logPath))
            {
                EnsureLogFileAcl();
            }
        }
    }

    /// <summary>
    /// Checks if the log file exceeds the maximum size and rotates if needed.
    /// Must be called while holding _writeLock.
    /// </summary>
    private void CheckAndRotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                return;
            }

            var fileInfo = new FileInfo(_logPath);
            if (fileInfo.Length < _maxFileSize)
            {
                return;
            }

            RotateLogFiles();
        }
        catch (Exception ex)
        {
            // Non-fatal: log rotation failure should not break audit logging
            Debug.WriteLine($"[AuditLogWriter] WARNING: Failed to check/rotate log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Rotates log files by renaming current to .1, .1 to .2, etc.
    /// Deletes the oldest file if it exceeds _maxRotatedFiles.
    /// Must be called while holding _writeLock.
    /// </summary>
    private void RotateLogFiles()
    {
        try
        {
            // Delete the oldest rotated file if it exists and we're at max
            var oldestPath = $"{_logPath}.{_maxRotatedFiles}";
            if (File.Exists(oldestPath))
            {
                File.Delete(oldestPath);
            }

            // Shift existing rotated files: .4 -> .5, .3 -> .4, etc.
            for (int i = _maxRotatedFiles - 1; i >= 1; i--)
            {
                var sourcePath = $"{_logPath}.{i}";
                var destPath = $"{_logPath}.{i + 1}";

                if (File.Exists(sourcePath))
                {
                    // Delete destination if it exists (shouldn't happen but be safe)
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    File.Move(sourcePath, destPath);
                }
            }

            // Rotate current file to .1
            var rotatedPath = $"{_logPath}.1";
            if (File.Exists(rotatedPath))
            {
                File.Delete(rotatedPath);
            }
            File.Move(_logPath, rotatedPath);

            // Reset ACL flag so it gets applied to the new file
            _aclApplied = false;

            Interlocked.Increment(ref _rotationCount);
            Debug.WriteLine($"[AuditLogWriter] Log file rotated: {_logPath} -> {rotatedPath}");
        }
        catch (Exception ex)
        {
            // Non-fatal: log rotation failure should not break audit logging
            Debug.WriteLine($"[AuditLogWriter] WARNING: Failed to rotate log files: {ex.Message}");
        }
    }

    /// <summary>
    /// Forces a log rotation check and rotation if needed.
    /// Useful for testing or manual rotation triggers.
    /// </summary>
    public void ForceRotationCheck()
    {
        lock (_writeLock)
        {
            CheckAndRotateIfNeeded();
        }
    }

    /// <summary>
    /// Applies protective ACLs to the audit log file to prevent tampering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method configures the file with an append-only security model for administrators:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>LocalSystem: Full Control (service account needs complete access)</description></item>
    ///   <item><description>Administrators: Read + AppendData only (can view logs, add new entries, but cannot modify or delete existing entries)</description></item>
    ///   <item><description>Users: No access (implicit deny by not granting any rights)</description></item>
    /// </list>
    /// <para>
    /// The method disables ACL inheritance to prevent the file from inheriting broader
    /// permissions from the parent directory. This ensures our restrictive permissions
    /// cannot be bypassed via inherited rules.
    /// </para>
    /// <para>
    /// ACLs are only applied when running as LocalSystem (the service account). When running
    /// as any other account (e.g., during testing or development), ACL protection is skipped
    /// to avoid breaking normal file access patterns.
    /// </para>
    /// <para>
    /// If ACL application fails (e.g., insufficient privileges, file locked), a warning
    /// is logged but the operation continues. Audit logging remains functional without
    /// the protection as a defense-in-depth measure.
    /// </para>
    /// </remarks>
    private void EnsureLogFileAcl()
    {
        if (_aclApplied)
        {
            return;
        }

        try
        {
            // Only apply ACLs when running as LocalSystem (the service account)
            // This prevents issues when running tests or during development
            using var currentIdentity = WindowsIdentity.GetCurrent();
            if (currentIdentity.User == null || !currentIdentity.User.Equals(LocalSystemSid))
            {
                // Not running as LocalSystem - skip ACL protection
                _aclApplied = true;
                return;
            }

            // Get the current file security
            var fileInfo = new FileInfo(_logPath);
            var fileSecurity = fileInfo.GetAccessControl();

            // Disable inheritance and remove all inherited rules
            // This ensures we have full control over the permissions
            fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // Remove all existing access rules to start fresh
            var existingRules = fileSecurity.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                targetType: typeof(SecurityIdentifier));

            foreach (FileSystemAccessRule rule in existingRules)
            {
                fileSecurity.RemoveAccessRule(rule);
            }

            // Grant LocalSystem full control (the service account)
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                LocalSystemSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            // Grant Administrators read and append-only access
            // FileSystemRights.Read allows reading the file contents
            // FileSystemRights.AppendData allows adding to the end (but not modifying existing content)
            // FileSystemRights.Synchronize is required for synchronous I/O operations (standard Windows requirement)
            // Note: We intentionally do NOT grant WriteData (overwrite), Delete, or ChangePermissions
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                AdministratorsSid,
                FileSystemRights.Read | FileSystemRights.AppendData | FileSystemRights.Synchronize,
                AccessControlType.Allow));

            // Users get no access (no rule = no access since we removed all rules)

            // Apply the security descriptor to the file
            fileInfo.SetAccessControl(fileSecurity);

            _aclApplied = true;

            // Log success (best effort - we're in the audit writer so we can't use it recursively)
            Console.WriteLine($"[AuditLogWriter] ACL protection applied to audit log: {_logPath}");
        }
        catch (UnauthorizedAccessException ex)
        {
            // Non-fatal: log warning but continue
            Console.WriteLine($"[AuditLogWriter] WARNING: Failed to apply ACL protection to audit log (insufficient privileges): {ex.Message}");
            _aclApplied = true; // Don't retry on every write
        }
        catch (IOException ex)
        {
            // Non-fatal: log warning but continue (file might be locked)
            Console.WriteLine($"[AuditLogWriter] WARNING: Failed to apply ACL protection to audit log (I/O error): {ex.Message}");
            _aclApplied = true; // Don't retry on every write
        }
        catch (Exception ex)
        {
            // Non-fatal: catch all other errors
            Console.WriteLine($"[AuditLogWriter] WARNING: Failed to apply ACL protection to audit log: {ex.Message}");
            _aclApplied = true; // Don't retry on every write
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
