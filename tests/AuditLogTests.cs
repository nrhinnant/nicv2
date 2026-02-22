using WfpTrafficControl.Shared.Audit;
using WfpTrafficControl.Shared.Ipc;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for AuditLogEntry serialization and factory methods.
/// </summary>
public class AuditLogEntryTests
{
    [Fact]
    public void ApplyStarted_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.ApplyStarted("cli", @"C:\policies\my-policy.json");

        Assert.Equal(AuditEventTypes.ApplyStarted, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
        Assert.Null(entry.Status);
        Assert.NotNull(entry.Details);
        Assert.Equal("my-policy.json", entry.Details!.PolicyFile);
    }

    [Fact]
    public void ApplyStarted_RedactsFullPath()
    {
        var entry = AuditLogEntry.ApplyStarted("cli", @"C:\Users\Admin\Documents\sensitive\policy.json");

        Assert.NotNull(entry.Details);
        Assert.Equal("policy.json", entry.Details!.PolicyFile);
        Assert.DoesNotContain("Admin", entry.Details.PolicyFile);
        Assert.DoesNotContain("sensitive", entry.Details.PolicyFile);
    }

    [Fact]
    public void ApplyFinished_Success_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.ApplyFinished("cli", 5, 2, 1, "1.0.0", 8);

        Assert.Equal(AuditEventTypes.ApplyFinished, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
        Assert.Equal(AuditStatus.Success, entry.Status);
        Assert.NotNull(entry.Details);
        Assert.Equal(5, entry.Details!.FiltersCreated);
        Assert.Equal(2, entry.Details.FiltersRemoved);
        Assert.Equal(1, entry.Details.RulesSkipped);
        Assert.Equal("1.0.0", entry.Details.PolicyVersion);
        Assert.Equal(8, entry.Details.TotalRules);
        Assert.Null(entry.ErrorCode);
        Assert.Null(entry.ErrorMessage);
    }

    [Fact]
    public void ApplyFailed_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.ApplyFailed("cli", "VALIDATION_FAILED", "Missing required field");

        Assert.Equal(AuditEventTypes.ApplyFinished, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
        Assert.Equal(AuditStatus.Failure, entry.Status);
        Assert.Equal("VALIDATION_FAILED", entry.ErrorCode);
        Assert.Equal("Missing required field", entry.ErrorMessage);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void RollbackStarted_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.RollbackStarted("cli");

        Assert.Equal(AuditEventTypes.RollbackStarted, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
        Assert.Null(entry.Status);
    }

    [Fact]
    public void RollbackFinished_Success_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.RollbackFinished("cli", 3);

        Assert.Equal(AuditEventTypes.RollbackFinished, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
        Assert.Equal(AuditStatus.Success, entry.Status);
        Assert.NotNull(entry.Details);
        Assert.Equal(3, entry.Details!.FiltersRemoved);
    }

    [Fact]
    public void RollbackFailed_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.RollbackFailed("cli", "WFP_ERROR", "Access denied");

        Assert.Equal(AuditEventTypes.RollbackFinished, entry.Event);
        Assert.Equal(AuditStatus.Failure, entry.Status);
        Assert.Equal("WFP_ERROR", entry.ErrorCode);
        Assert.Equal("Access denied", entry.ErrorMessage);
    }

    [Fact]
    public void TeardownStarted_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.TeardownStarted("cli");

        Assert.Equal(AuditEventTypes.TeardownStarted, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
    }

    [Fact]
    public void TeardownFinished_Success_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.TeardownFinished("cli", true, true);

        Assert.Equal(AuditEventTypes.TeardownFinished, entry.Event);
        Assert.Equal(AuditStatus.Success, entry.Status);
        Assert.NotNull(entry.Details);
        Assert.True(entry.Details!.ProviderRemoved);
        Assert.True(entry.Details.SublayerRemoved);
    }

    [Fact]
    public void LkgRevertStarted_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.LkgRevertStarted("cli");

        Assert.Equal(AuditEventTypes.LkgRevertStarted, entry.Event);
        Assert.Equal(AuditSource.Cli, entry.Source);
    }

    [Fact]
    public void LkgRevertFinished_Success_CreatesCorrectEntry()
    {
        var entry = AuditLogEntry.LkgRevertFinished("cli", 4, 1, 0, "2.0.0", 4);

        Assert.Equal(AuditEventTypes.LkgRevertFinished, entry.Event);
        Assert.Equal(AuditStatus.Success, entry.Status);
        Assert.NotNull(entry.Details);
        Assert.Equal(4, entry.Details!.FiltersCreated);
        Assert.Equal(1, entry.Details.FiltersRemoved);
        Assert.Equal("2.0.0", entry.Details.PolicyVersion);
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var entry = AuditLogEntry.ApplyFinished("cli", 5, 2, 0, "1.0.0", 5);
        var json = entry.ToJson();

        Assert.NotNull(json);
        Assert.Contains("\"event\":\"apply-finished\"", json);
        Assert.Contains("\"status\":\"success\"", json);
        Assert.Contains("\"source\":\"cli\"", json);
        // Should be single line (no newlines)
        Assert.DoesNotContain("\n", json);
    }

    [Fact]
    public void ToJson_OmitsNullFields()
    {
        var entry = AuditLogEntry.RollbackStarted("cli");
        var json = entry.ToJson();

        Assert.DoesNotContain("\"status\"", json);
        Assert.DoesNotContain("\"errorCode\"", json);
        Assert.DoesNotContain("\"errorMessage\"", json);
        Assert.DoesNotContain("\"details\"", json);
    }

    [Fact]
    public void FromJson_ParsesValidEntry()
    {
        var json = """{"ts":"2026-01-31T10:00:00Z","event":"apply-finished","source":"cli","status":"success"}""";
        var entry = AuditLogEntry.FromJson(json);

        Assert.NotNull(entry);
        Assert.Equal("apply-finished", entry!.Event);
        Assert.Equal("cli", entry.Source);
        Assert.Equal("success", entry.Status);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsNull()
    {
        var entry = AuditLogEntry.FromJson("not valid json");

        Assert.Null(entry);
    }

    [Fact]
    public void FromJson_EmptyString_ReturnsNull()
    {
        var entry = AuditLogEntry.FromJson("");

        Assert.Null(entry);
    }

    [Fact]
    public void Timestamp_IsIso8601Utc()
    {
        var entry = AuditLogEntry.ApplyStarted("cli");

        Assert.NotNull(entry.Timestamp);
        Assert.True(DateTimeOffset.TryParse(entry.Timestamp, out var parsed));
        // ISO 8601 format with Z suffix indicates UTC (offset of 0)
        Assert.Equal(TimeSpan.Zero, parsed.Offset);
    }
}

/// <summary>
/// Unit tests for AuditLogWriter.
/// </summary>
public class AuditLogWriterTests : IDisposable
{
    private readonly string _testLogPath;
    private readonly AuditLogWriter _writer;

    public AuditLogWriterTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid()}.log");
        _writer = new AuditLogWriter(_testLogPath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testLogPath))
            {
                File.Delete(_testLogPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Write_CreatesFile()
    {
        var entry = AuditLogEntry.ApplyStarted("cli");

        _writer.Write(entry);

        Assert.True(File.Exists(_testLogPath));
    }

    [Fact]
    public void Write_AppendsEntries()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));
        _writer.Write(AuditLogEntry.ApplyFinished("cli", 1, 0, 0, "1.0", 1));

        var lines = File.ReadAllLines(_testLogPath);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void Write_NullEntry_DoesNotThrow()
    {
        _writer.Write(null!);

        // Should not create file for null entry
        Assert.False(File.Exists(_testLogPath));
    }

    [Fact]
    public void Write_EachLineIsSingleJson()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));
        _writer.Write(AuditLogEntry.RollbackStarted("cli"));

        var lines = File.ReadAllLines(_testLogPath);
        foreach (var line in lines)
        {
            Assert.DoesNotContain("\n", line);
            var entry = AuditLogEntry.FromJson(line);
            Assert.NotNull(entry);
        }
    }

    [Fact]
    public void LogPath_ReturnsConfiguredPath()
    {
        Assert.Equal(_testLogPath, _writer.LogPath);
    }
}

/// <summary>
/// Unit tests for AuditLogReader.
/// </summary>
public class AuditLogReaderTests : IDisposable
{
    private readonly string _testLogPath;
    private readonly AuditLogWriter _writer;
    private readonly AuditLogReader _reader;

    public AuditLogReaderTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"audit_read_test_{Guid.NewGuid()}.log");
        _writer = new AuditLogWriter(_testLogPath);
        _reader = new AuditLogReader(_testLogPath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testLogPath))
            {
                File.Delete(_testLogPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void ReadTail_NoFile_ReturnsEmpty()
    {
        var entries = _reader.ReadTail(10);

        Assert.Empty(entries);
    }

    [Fact]
    public void ReadTail_ReturnsNewestFirst()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));
        Thread.Sleep(10); // Ensure different timestamps
        _writer.Write(AuditLogEntry.ApplyFinished("cli", 1, 0, 0, "1.0", 1));

        var entries = _reader.ReadTail(10);

        Assert.Equal(2, entries.Count);
        Assert.Equal(AuditEventTypes.ApplyFinished, entries[0].Event); // Newest first
        Assert.Equal(AuditEventTypes.ApplyStarted, entries[1].Event);
    }

    [Fact]
    public void ReadTail_LimitsResults()
    {
        for (int i = 0; i < 10; i++)
        {
            _writer.Write(AuditLogEntry.RollbackStarted("cli"));
        }

        var entries = _reader.ReadTail(3);

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void ReadTail_ZeroCount_ReturnsEmpty()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        var entries = _reader.ReadTail(0);

        Assert.Empty(entries);
    }

    [Fact]
    public void ReadTail_NegativeCount_ReturnsEmpty()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        var entries = _reader.ReadTail(-5);

        Assert.Empty(entries);
    }

    [Fact]
    public void ReadSince_ReturnsRecentEntries()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        var entries = _reader.ReadSince(5); // Last 5 minutes

        Assert.Single(entries);
    }

    [Fact]
    public void ReadSince_ZeroMinutes_ReturnsEmpty()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        var entries = _reader.ReadSince(0);

        Assert.Empty(entries);
    }

    [Fact]
    public void GetEntryCount_ReturnsCorrectCount()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));
        _writer.Write(AuditLogEntry.ApplyFinished("cli", 1, 0, 0, "1.0", 1));
        _writer.Write(AuditLogEntry.RollbackStarted("cli"));

        var count = _reader.GetEntryCount();

        Assert.Equal(3, count);
    }

    [Fact]
    public void GetEntryCount_NoFile_ReturnsZero()
    {
        var count = _reader.GetEntryCount();

        Assert.Equal(0, count);
    }

    [Fact]
    public void LogFileExists_ReturnsFalseWhenNoFile()
    {
        Assert.False(_reader.LogFileExists);
    }

    [Fact]
    public void LogFileExists_ReturnsTrueWhenFileExists()
    {
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        Assert.True(_reader.LogFileExists);
    }
}

/// <summary>
/// Unit tests for NullAuditLogWriter.
/// </summary>
public class NullAuditLogWriterTests
{
    [Fact]
    public void Write_DoesNotThrow()
    {
        var writer = NullAuditLogWriter.Instance;

        writer.Write(AuditLogEntry.ApplyStarted("cli"));
        // Should not throw
    }

    [Fact]
    public void LogPath_ReturnsEmptyString()
    {
        var writer = NullAuditLogWriter.Instance;

        Assert.Equal(string.Empty, writer.LogPath);
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var writer1 = NullAuditLogWriter.Instance;
        var writer2 = NullAuditLogWriter.Instance;

        Assert.Same(writer1, writer2);
    }
}

/// <summary>
/// Unit tests for AuditLogWriter ACL protection behavior.
/// </summary>
/// <remarks>
/// These tests verify that ACL protection works correctly and doesn't break
/// normal audit log operations. Since tests don't run as LocalSystem, the
/// ACL protection is skipped, which is the expected behavior.
/// </remarks>
public class AuditLogWriterAclTests : IDisposable
{
    private readonly string _testLogPath;
    private readonly AuditLogWriter _writer;

    public AuditLogWriterAclTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"audit_acl_test_{Guid.NewGuid()}.log");
        _writer = new AuditLogWriter(_testLogPath);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testLogPath))
            {
                File.Delete(_testLogPath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Write_FileRemainsAccessible_WhenNotLocalSystem()
    {
        // When not running as LocalSystem, ACL protection is skipped
        // File should remain fully accessible to the current user
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        // Verify file is readable
        var content = File.ReadAllText(_testLogPath);
        Assert.NotEmpty(content);

        // Verify file is writable (can append more)
        _writer.Write(AuditLogEntry.ApplyFinished("cli", 1, 0, 0, "1.0", 1));
        var lines = File.ReadAllLines(_testLogPath);
        Assert.Equal(2, lines.Length);

        // Verify file is deletable
        File.Delete(_testLogPath);
        Assert.False(File.Exists(_testLogPath));
    }

    [Fact]
    public void Write_MultipleWrites_DoNotReapplyAcl()
    {
        // ACL should only be applied once (on first write)
        // Subsequent writes should not attempt to reapply
        for (int i = 0; i < 10; i++)
        {
            _writer.Write(AuditLogEntry.ApplyStarted("cli"));
        }

        // If ACL was reapplied each time and failed, we'd see issues
        var lines = File.ReadAllLines(_testLogPath);
        Assert.Equal(10, lines.Length);
    }

    [Fact]
    public void Write_NewWriterInstance_WorksAfterFirstInstanceWrote()
    {
        // First writer creates file
        _writer.Write(AuditLogEntry.ApplyStarted("cli"));

        // Second writer instance should be able to append
        var writer2 = new AuditLogWriter(_testLogPath);
        writer2.Write(AuditLogEntry.RollbackStarted("cli"));

        var lines = File.ReadAllLines(_testLogPath);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Write_ConcurrentWrites_AllSucceed()
    {
        // Test that concurrent writes from multiple threads work correctly
        var tasks = new Task[5];
        for (int i = 0; i < 5; i++)
        {
            int iteration = i;
            tasks[i] = Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    _writer.Write(AuditLogEntry.ApplyStarted($"cli-{iteration}"));
                }
            });
        }

        await Task.WhenAll(tasks);

        var lines = File.ReadAllLines(_testLogPath);
        Assert.Equal(50, lines.Length);
    }

    [Fact]
    public async Task Write_ConcurrentWritesFromMultipleInstances_TracksFailures()
    {
        // Use a dedicated file for this test to avoid interference
        var testPath = Path.Combine(Path.GetTempPath(), $"audit_multi_{Guid.NewGuid():N}.log");
        try
        {
            // Test concurrent writes from multiple AuditLogWriter instances
            // Note: Multiple instances have separate write locks, so file-level contention may occur.
            // This test verifies that write failures are tracked and that most writes succeed.
            var writers = new AuditLogWriter[3];
            for (int i = 0; i < writers.Length; i++)
            {
                writers[i] = new AuditLogWriter(testPath);
            }

            var tasks = new Task[writers.Length];
            for (int i = 0; i < writers.Length; i++)
            {
                int writerIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        writers[writerIndex].Write(AuditLogEntry.ApplyStarted($"writer-{writerIndex}"));
                    }
                });
            }

            await Task.WhenAll(tasks);

            var lines = File.ReadAllLines(testPath);
            var totalFailures = writers.Sum(w => w.FailedWriteCount);

            // Verify: lines written + failures = total attempted writes
            Assert.Equal(30, lines.Length + totalFailures);

            // Most writes should succeed
            Assert.True(lines.Length >= 15, $"Expected at least 15 successful writes, got {lines.Length}");
        }
        finally
        {
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }
        }
    }

    [Fact]
    public void Write_FailedWriteCount_InitiallyZero()
    {
        Assert.Equal(0, _writer.FailedWriteCount);
    }

    [Fact]
    public void Write_ToInvalidPath_IncrementsFailedWriteCount()
    {
        // Create a writer with an invalid path (contains invalid characters)
        var invalidPath = "Z:\\<invalid>\\path\\audit.log";
        var badWriter = new AuditLogWriter(invalidPath);

        // Try to write - should fail but not throw
        badWriter.Write(AuditLogEntry.ApplyStarted("test"));

        // FailedWriteCount should increment
        Assert.True(badWriter.FailedWriteCount >= 1);
    }

    [Fact]
    public async Task Write_HighConcurrency_NoDataLoss()
    {
        // Use a dedicated file for this stress test
        var testPath = Path.Combine(Path.GetTempPath(), $"audit_stress_{Guid.NewGuid():N}.log");
        var stressWriter = new AuditLogWriter(testPath);
        try
        {
            // Stress test with high concurrency
            const int tasksCount = 10;
            const int writesPerTask = 50;
            var tasks = new Task[tasksCount];

            for (int i = 0; i < tasksCount; i++)
            {
                int iteration = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < writesPerTask; j++)
                    {
                        stressWriter.Write(AuditLogEntry.ApplyStarted($"stress-{iteration}-{j}"));
                    }
                });
            }

            await Task.WhenAll(tasks);

            var lines = File.ReadAllLines(testPath);
            Assert.Equal(tasksCount * writesPerTask, lines.Length);
            Assert.Equal(0, stressWriter.FailedWriteCount);
        }
        finally
        {
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }
        }
    }

    [Fact]
    public void Write_WhileFileLockedByReader_StillSucceeds()
    {
        // Write initial entry
        _writer.Write(AuditLogEntry.ApplyStarted("initial"));

        // Open file for reading (shared read access)
        using (var reader = new FileStream(_testLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            // Writer should still be able to write while reader holds file
            _writer.Write(AuditLogEntry.ApplyStarted("while-reading"));
        }

        var lines = File.ReadAllLines(_testLogPath);
        Assert.Equal(2, lines.Length);
    }
}

/// <summary>
/// Unit tests for AuditLogsRequest IPC message parsing.
/// </summary>
public class AuditLogsMessageTests
{
    [Fact]
    public void ParseRequest_ValidAuditLogsRequest_ReturnsRequest()
    {
        var json = """{"type":"audit-logs","tail":20}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<AuditLogsRequest>(result.Value);
        var request = (AuditLogsRequest)result.Value;
        Assert.Equal(20, request.Tail);
        Assert.Equal(0, request.SinceMinutes);
    }

    [Fact]
    public void ParseRequest_AuditLogsWithSince_ReturnsRequest()
    {
        var json = """{"type":"audit-logs","sinceMinutes":60}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<AuditLogsRequest>(result.Value);
        var request = (AuditLogsRequest)result.Value;
        Assert.Equal(0, request.Tail);
        Assert.Equal(60, request.SinceMinutes);
    }

    [Fact]
    public void ParseRequest_AuditLogsNoParams_ReturnsRequest()
    {
        var json = """{"type":"audit-logs"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<AuditLogsRequest>(result.Value);
    }

    [Fact]
    public void AuditLogsResponse_Success_HasExpectedFields()
    {
        var entries = new List<AuditLogEntryDto>
        {
            new() { Event = "apply-finished", Status = "success" }
        };

        var response = AuditLogsResponse.Success(entries, 100, @"C:\logs\audit.log");

        Assert.True(response.Ok);
        Assert.Single(response.Entries);
        Assert.Equal(1, response.Count);
        Assert.Equal(100, response.TotalCount);
        Assert.Equal(@"C:\logs\audit.log", response.LogPath);
    }

    [Fact]
    public void AuditLogsResponse_Failure_HasErrorMessage()
    {
        var response = AuditLogsResponse.Failure("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
        Assert.Empty(response.Entries);
    }
}
