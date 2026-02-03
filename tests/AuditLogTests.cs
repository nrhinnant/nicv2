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
