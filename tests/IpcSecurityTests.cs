using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for IPC security features including protocol versioning,
/// message size validation, and error responses.
/// </summary>
public class IpcSecurityTests
{
    #region Protocol Version Parsing Tests

    [Fact]
    public void ParseRequest_WithProtocolVersion_ExtractsVersion()
    {
        var json = """{"type":"ping","protocolVersion":1}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ProtocolVersion);
    }

    [Fact]
    public void ParseRequest_WithoutProtocolVersion_DefaultsToZero()
    {
        var json = """{"type":"ping"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ProtocolVersion);
    }

    [Fact]
    public void ParseRequest_WithProtocolVersionNull_DefaultsToZero()
    {
        // JSON null for protocolVersion - should be treated as not present
        var json = """{"type":"ping","protocolVersion":null}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ProtocolVersion);
    }

    [Fact]
    public void ParseRequest_WithProtocolVersionString_DefaultsToZero()
    {
        // Wrong type (string instead of number) - should be ignored
        var json = """{"type":"ping","protocolVersion":"1"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ProtocolVersion);
    }

    [Fact]
    public void ParseRequest_WithLargeProtocolVersion_ParsesCorrectly()
    {
        var json = """{"type":"ping","protocolVersion":999}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.Value.ProtocolVersion);
    }

    #endregion

    #region Protocol Version Validation Tests

    [Fact]
    public void ValidateProtocolVersion_CurrentVersion_ReturnsNull()
    {
        var request = new PingRequest { ProtocolVersion = WfpConstants.IpcProtocolVersion };

        var error = IpcMessageParser.ValidateProtocolVersion(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateProtocolVersion_MinVersion_ReturnsNull()
    {
        var request = new PingRequest { ProtocolVersion = WfpConstants.IpcMinProtocolVersion };

        var error = IpcMessageParser.ValidateProtocolVersion(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateProtocolVersion_ZeroVersion_ReturnsNullForBackwardCompatibility()
    {
        // Version 0 means client didn't send version - allow for backward compatibility
        var request = new PingRequest { ProtocolVersion = 0 };

        var error = IpcMessageParser.ValidateProtocolVersion(request);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateProtocolVersion_BelowMinVersion_ReturnsError()
    {
        // Version below minimum (but not 0) should be rejected
        // Note: This test assumes IpcMinProtocolVersion > 0. If it equals 1,
        // there's no valid "below minimum but not zero" case currently.
        // We'll test with a hypothetical negative version which is always invalid.
        var request = new PingRequest { ProtocolVersion = -1 };

        var error = IpcMessageParser.ValidateProtocolVersion(request);

        Assert.NotNull(error);
        Assert.False(error.Ok);
        Assert.Contains("Protocol version mismatch", error.Error);
    }

    [Fact]
    public void ValidateProtocolVersion_AboveMaxVersion_ReturnsError()
    {
        var request = new PingRequest { ProtocolVersion = WfpConstants.IpcProtocolVersion + 100 };

        var error = IpcMessageParser.ValidateProtocolVersion(request);

        Assert.NotNull(error);
        Assert.False(error.Ok);
        Assert.Contains("Protocol version mismatch", error.Error);
        Assert.Contains("Please update the CLI", error.Error);
    }

    [Fact]
    public void ValidateProtocolVersion_ErrorIncludesVersionInfo()
    {
        var request = new PingRequest { ProtocolVersion = 999 };

        var error = IpcMessageParser.ValidateProtocolVersion(request);

        Assert.NotNull(error);
        Assert.Contains("999", error.Error);
        Assert.Contains(WfpConstants.IpcMinProtocolVersion.ToString(), error.Error);
        Assert.Contains(WfpConstants.IpcProtocolVersion.ToString(), error.Error);
    }

    #endregion

    #region Message Size Validation Tests

    [Fact]
    public void ValidateMessageSize_ValidSize_ReturnsNull()
    {
        var error = IpcMessageParser.ValidateMessageSize(1024);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateMessageSize_MaxSize_ReturnsNull()
    {
        var error = IpcMessageParser.ValidateMessageSize(WfpConstants.IpcMaxMessageSize);

        Assert.Null(error);
    }

    [Fact]
    public void ValidateMessageSize_OneByteOverMax_ReturnsError()
    {
        var error = IpcMessageParser.ValidateMessageSize(WfpConstants.IpcMaxMessageSize + 1);

        Assert.NotNull(error);
        Assert.False(error.Ok);
        Assert.Contains("too large", error.Error);
    }

    [Fact]
    public void ValidateMessageSize_ZeroSize_ReturnsError()
    {
        var error = IpcMessageParser.ValidateMessageSize(0);

        Assert.NotNull(error);
        Assert.False(error.Ok);
    }

    [Fact]
    public void ValidateMessageSize_NegativeSize_ReturnsError()
    {
        var error = IpcMessageParser.ValidateMessageSize(-1);

        Assert.NotNull(error);
        Assert.False(error.Ok);
    }

    [Fact]
    public void ValidateMessageSize_ErrorIncludesSizeInfo()
    {
        var size = WfpConstants.IpcMaxMessageSize + 1000;
        var error = IpcMessageParser.ValidateMessageSize(size);

        Assert.NotNull(error);
        Assert.Contains(size.ToString(), error.Error);
        Assert.Contains(WfpConstants.IpcMaxMessageSize.ToString(), error.Error);
    }

    #endregion

    #region Error Response Factory Tests

    [Fact]
    public void ErrorResponse_ProtocolVersionMismatch_ContainsExpectedInfo()
    {
        var response = ErrorResponse.ProtocolVersionMismatch(5, 1, 3);

        Assert.False(response.Ok);
        Assert.Contains("5", response.Error);
        Assert.Contains("1", response.Error);
        Assert.Contains("3", response.Error);
        Assert.Contains("Protocol version mismatch", response.Error);
    }

    [Fact]
    public void ErrorResponse_RequestTooLarge_ContainsExpectedInfo()
    {
        var response = ErrorResponse.RequestTooLarge(100000, 65536);

        Assert.False(response.Ok);
        Assert.Contains("100000", response.Error);
        Assert.Contains("65536", response.Error);
        Assert.Contains("too large", response.Error);
    }

    [Fact]
    public void ErrorResponse_RequestTimeout_ContainsExpectedMessage()
    {
        var response = ErrorResponse.RequestTimeout();

        Assert.False(response.Ok);
        Assert.Contains("timed out", response.Error);
    }

    #endregion

    #region Response Protocol Version Tests

    [Fact]
    public void IpcResponse_IncludesServerProtocolVersion()
    {
        var response = PingResponse.Success("1.0.0");

        Assert.Equal(WfpConstants.IpcProtocolVersion, response.ProtocolVersion);
    }

    [Fact]
    public void ErrorResponse_IncludesServerProtocolVersion()
    {
        var response = new ErrorResponse("Test error");

        Assert.Equal(WfpConstants.IpcProtocolVersion, response.ProtocolVersion);
    }

    [Fact]
    public void SerializedResponse_ContainsProtocolVersion()
    {
        var response = PingResponse.Success("1.0.0");
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"protocolVersion\":", json);
        Assert.Contains(WfpConstants.IpcProtocolVersion.ToString(), json);
    }

    #endregion

    #region Constants Validation Tests

    [Fact]
    public void IpcConstants_AreReasonable()
    {
        // Protocol version should be positive
        Assert.True(WfpConstants.IpcProtocolVersion >= 1);

        // Min version should be <= current version
        Assert.True(WfpConstants.IpcMinProtocolVersion <= WfpConstants.IpcProtocolVersion);

        // Max message size should be reasonable (at least 1KB, at most 1MB)
        Assert.True(WfpConstants.IpcMaxMessageSize >= 1024);
        Assert.True(WfpConstants.IpcMaxMessageSize <= 1024 * 1024);

        // Timeouts should be reasonable (at least 1 second)
        Assert.True(WfpConstants.IpcReadTimeoutMs >= 1000);
        Assert.True(WfpConstants.IpcConnectTimeoutMs >= 1000);
    }

    #endregion
}
