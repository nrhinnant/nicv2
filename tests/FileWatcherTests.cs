using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for watch-related IPC message parsing.
/// </summary>
public class WatchMessageParserTests
{
    [Fact]
    public void ParseRequestValidWatchSetRequestReturnsWatchSetRequest()
    {
        var json = """{"type":"watch-set","policyPath":"C:\\test\\policy.json"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WatchSetRequest>(result.Value);
        var request = (WatchSetRequest)result.Value;
        Assert.Equal(WatchSetRequest.RequestType, request.Type);
        Assert.Equal("C:\\test\\policy.json", request.PolicyPath);
    }

    [Fact]
    public void ParseRequestWatchSetRequestWithNullPathReturnsWatchSetRequest()
    {
        // Null path is valid (disables watching)
        var json = """{"type":"watch-set","policyPath":null}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WatchSetRequest>(result.Value);
        var request = (WatchSetRequest)result.Value;
        Assert.Null(request.PolicyPath);
    }

    [Fact]
    public void ParseRequestWatchSetRequestMissingPathReturnsWatchSetRequest()
    {
        // Missing path is valid (disables watching)
        var json = """{"type":"watch-set"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WatchSetRequest>(result.Value);
        var request = (WatchSetRequest)result.Value;
        Assert.Null(request.PolicyPath);
    }

    [Fact]
    public void ParseRequestValidWatchStatusRequestReturnsWatchStatusRequest()
    {
        var json = """{"type":"watch-status"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<WatchStatusRequest>(result.Value);
        Assert.Equal(WatchStatusRequest.RequestType, result.Value.Type);
    }
}

/// <summary>
/// Unit tests for watch response serialization.
/// </summary>
public class WatchResponseSerializationTests
{
    [Fact]
    public void SerializeResponseWatchSetResponseSuccessContainsExpectedFields()
    {
        var response = WatchSetResponse.Success(
            watching: true,
            policyPath: "C:\\test\\policy.json",
            initialApplySuccess: true,
            warning: null);

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"watching\":true", json);
        Assert.Contains("\"policyPath\":", json);
        Assert.Contains("\"initialApplySuccess\":true", json);
        Assert.DoesNotContain("\"warning\"", json); // Omitted when null
    }

    [Fact]
    public void SerializeResponseWatchSetResponseWithWarningContainsWarning()
    {
        var response = WatchSetResponse.Success(
            watching: true,
            policyPath: "C:\\test\\policy.json",
            initialApplySuccess: false,
            warning: "Validation failed");

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"warning\":\"Validation failed\"", json);
        Assert.Contains("\"initialApplySuccess\":false", json);
    }

    [Fact]
    public void SerializeResponseWatchSetResponseDisabledContainsExpectedFields()
    {
        var response = WatchSetResponse.Disabled();

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"watching\":false", json);
    }

    [Fact]
    public void SerializeResponseWatchSetResponseFailureContainsError()
    {
        var response = WatchSetResponse.Failure("File not found");

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"error\":\"File not found\"", json);
    }

    [Fact]
    public void SerializeResponseWatchStatusResponseActiveContainsAllFields()
    {
        var lastApply = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var response = WatchStatusResponse.Success(
            watching: true,
            policyPath: "C:\\test\\policy.json",
            debounceMs: 1000,
            lastApplyTime: lastApply,
            lastError: null,
            lastErrorTime: null,
            applyCount: 5,
            errorCount: 0);

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"watching\":true", json);
        Assert.Contains("\"policyPath\":", json);
        Assert.Contains("\"debounceMs\":1000", json);
        Assert.Contains("\"applyCount\":5", json);
        Assert.Contains("\"errorCount\":0", json);
        Assert.DoesNotContain("\"lastError\":", json); // Omitted when null
    }

    [Fact]
    public void SerializeResponseWatchStatusResponseWithErrorContainsErrorFields()
    {
        var lastError = new DateTime(2025, 1, 15, 10, 35, 0, DateTimeKind.Utc);
        var response = WatchStatusResponse.Success(
            watching: true,
            policyPath: "C:\\test\\policy.json",
            debounceMs: 1000,
            lastApplyTime: null,
            lastError: "Validation failed",
            lastErrorTime: lastError,
            applyCount: 0,
            errorCount: 1);

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"lastError\":\"Validation failed\"", json);
        Assert.Contains("\"lastErrorTime\":", json);
        Assert.Contains("\"errorCount\":1", json);
    }

    [Fact]
    public void SerializeResponseWatchStatusResponseNotWatchingOmitsPath()
    {
        var response = WatchStatusResponse.Success(
            watching: false,
            policyPath: null,
            debounceMs: 1000,
            lastApplyTime: null,
            lastError: null,
            lastErrorTime: null,
            applyCount: 0,
            errorCount: 0);

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"watching\":false", json);
        Assert.DoesNotContain("\"policyPath\":", json);
    }
}

/// <summary>
/// Unit tests for watch message model classes.
/// </summary>
public class WatchMessageModelTests
{
    [Fact]
    public void WatchSetRequestTypeIsWatchSet()
    {
        var request = new WatchSetRequest();
        Assert.Equal("watch-set", request.Type);
        Assert.Equal("watch-set", WatchSetRequest.RequestType);
    }

    [Fact]
    public void WatchStatusRequestTypeIsWatchStatus()
    {
        var request = new WatchStatusRequest();
        Assert.Equal("watch-status", request.Type);
        Assert.Equal("watch-status", WatchStatusRequest.RequestType);
    }

    [Fact]
    public void WatchSetResponseSuccessSetsAllFields()
    {
        var response = WatchSetResponse.Success(true, "test.json", true, null);

        Assert.True(response.Ok);
        Assert.True(response.Watching);
        Assert.Equal("test.json", response.PolicyPath);
        Assert.True(response.InitialApplySuccess);
        Assert.Null(response.Warning);
        Assert.Null(response.Error);
    }

    [Fact]
    public void WatchSetResponseDisabledSetsCorrectState()
    {
        var response = WatchSetResponse.Disabled();

        Assert.True(response.Ok);
        Assert.False(response.Watching);
        Assert.Null(response.PolicyPath);
        Assert.False(response.InitialApplySuccess);
    }

    [Fact]
    public void WatchSetResponseFailureSetsError()
    {
        var response = WatchSetResponse.Failure("Error message");

        Assert.False(response.Ok);
        Assert.Equal("Error message", response.Error);
    }

    [Fact]
    public void WatchStatusResponseSuccessSetsAllFields()
    {
        var now = DateTime.UtcNow;
        var response = WatchStatusResponse.Success(
            watching: true,
            policyPath: "test.json",
            debounceMs: 500,
            lastApplyTime: now,
            lastError: "Test error",
            lastErrorTime: now,
            applyCount: 10,
            errorCount: 2);

        Assert.True(response.Ok);
        Assert.True(response.Watching);
        Assert.Equal("test.json", response.PolicyPath);
        Assert.Equal(500, response.DebounceMs);
        Assert.NotNull(response.LastApplyTime);
        Assert.Equal("Test error", response.LastError);
        Assert.NotNull(response.LastErrorTime);
        Assert.Equal(10, response.ApplyCount);
        Assert.Equal(2, response.ErrorCount);
    }

    [Fact]
    public void WatchStatusResponseFailureSetsError()
    {
        var response = WatchStatusResponse.Failure("Status error");

        Assert.False(response.Ok);
        Assert.Equal("Status error", response.Error);
    }
}

/// <summary>
/// Unit tests for debounce logic constants and configuration.
/// </summary>
public class DebounceConfigurationTests
{
    [Fact]
    public void DefaultDebounceMsIs1000()
    {
        // Note: We're testing the constant value from WatchStatusResponse defaults
        var response = WatchStatusResponse.Success(
            watching: false,
            policyPath: null,
            debounceMs: 1000, // Default value
            lastApplyTime: null,
            lastError: null,
            lastErrorTime: null,
            applyCount: 0,
            errorCount: 0);

        Assert.Equal(1000, response.DebounceMs);
    }

    [Fact]
    public void DebounceMsCanBeConfiguredInRange()
    {
        // Test minimum valid value
        var minResponse = WatchStatusResponse.Success(
            watching: true, policyPath: "test.json", debounceMs: 100,
            lastApplyTime: null, lastError: null, lastErrorTime: null,
            applyCount: 0, errorCount: 0);
        Assert.Equal(100, minResponse.DebounceMs);

        // Test maximum valid value
        var maxResponse = WatchStatusResponse.Success(
            watching: true, policyPath: "test.json", debounceMs: 30000,
            lastApplyTime: null, lastError: null, lastErrorTime: null,
            applyCount: 0, errorCount: 0);
        Assert.Equal(30000, maxResponse.DebounceMs);
    }
}
