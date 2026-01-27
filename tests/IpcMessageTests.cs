using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for IPC message parsing and validation.
/// </summary>
public class IpcMessageParserTests
{
    [Fact]
    public void ParseRequest_ValidPingRequest_ReturnsPingRequest()
    {
        var json = """{"type":"ping"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<PingRequest>(result.Value);
        Assert.Equal(PingRequest.RequestType, result.Value.Type);
    }

    [Fact]
    public void ParseRequest_PingRequestWithExtraFields_ReturnsPingRequest()
    {
        // Extra fields should be ignored
        var json = """{"type":"ping","extra":"ignored"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<PingRequest>(result.Value);
    }

    [Fact]
    public void ParseRequest_EmptyString_ReturnsError()
    {
        var result = IpcMessageParser.ParseRequest("");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("empty", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_WhitespaceOnly_ReturnsError()
    {
        var result = IpcMessageParser.ParseRequest("   ");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void ParseRequest_NullInput_ReturnsError()
    {
        var result = IpcMessageParser.ParseRequest(null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void ParseRequest_InvalidJson_ReturnsError()
    {
        var json = "not valid json";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("Invalid JSON", result.Error.Message);
    }

    [Fact]
    public void ParseRequest_JsonArray_ReturnsError()
    {
        var json = """["not","an","object"]""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("object", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_MissingTypeField_ReturnsError()
    {
        var json = """{"foo":"bar"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("type", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_TypeFieldNotString_ReturnsError()
    {
        var json = """{"type":123}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("string", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_TypeFieldNull_ReturnsError()
    {
        var json = """{"type":null}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void ParseRequest_UnknownType_ReturnsError()
    {
        var json = """{"type":"unknown"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("unknown", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_CaseInsensitiveType_StillMatchesExact()
    {
        // Type matching should be case-sensitive ("ping" != "PING")
        var json = """{"type":"PING"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("Unknown request type", result.Error.Message);
    }
}

/// <summary>
/// Unit tests for IPC response serialization.
/// </summary>
public class IpcResponseSerializationTests
{
    [Fact]
    public void SerializeResponse_PingResponseSuccess_ContainsExpectedFields()
    {
        var response = PingResponse.Success("1.0.0");

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"serviceVersion\":\"1.0.0\"", json);
        Assert.Contains("\"time\":", json);
        Assert.DoesNotContain("\"error\"", json); // error should be omitted when null
    }

    [Fact]
    public void SerializeResponse_ErrorResponse_ContainsExpectedFields()
    {
        var response = new ErrorResponse("Test error message");

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"error\":\"Test error message\"", json);
    }

    [Fact]
    public void SerializeResponse_AccessDenied_ContainsAdminMessage()
    {
        var response = ErrorResponse.AccessDenied();

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("Administrator", json);
    }

    [Fact]
    public void CreateErrorResponse_ReturnsValidJson()
    {
        var json = IpcMessageParser.CreateErrorResponse("Something went wrong");

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"error\":\"Something went wrong\"", json);
    }
}

/// <summary>
/// Unit tests for request/response model classes.
/// </summary>
public class IpcMessageModelTests
{
    [Fact]
    public void PingRequest_Type_IsPing()
    {
        var request = new PingRequest();
        Assert.Equal("ping", request.Type);
        Assert.Equal("ping", PingRequest.RequestType);
    }

    [Fact]
    public void PingResponse_Success_SetsOkTrue()
    {
        var response = PingResponse.Success("1.2.3");

        Assert.True(response.Ok);
        Assert.Equal("1.2.3", response.ServiceVersion);
        Assert.NotNull(response.Time);
        Assert.Null(response.Error);
    }

    [Fact]
    public void PingResponse_Success_TimeIsIso8601()
    {
        var response = PingResponse.Success("1.0.0");

        // ISO 8601 format should be parseable
        Assert.True(DateTimeOffset.TryParse(response.Time, out var parsed));
        // Time should be recent (within last minute)
        Assert.True((DateTimeOffset.UtcNow - parsed).TotalMinutes < 1);
    }

    [Fact]
    public void ErrorResponse_DefaultConstructor_SetsOkFalse()
    {
        var response = new ErrorResponse();
        Assert.False(response.Ok);
    }

    [Fact]
    public void ErrorResponse_WithMessage_SetsError()
    {
        var response = new ErrorResponse("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void ErrorResponse_AccessDenied_HasExpectedMessage()
    {
        var response = ErrorResponse.AccessDenied();

        Assert.False(response.Ok);
        Assert.Contains("Access denied", response.Error);
        Assert.Contains("Administrator", response.Error);
    }

    [Fact]
    public void ErrorResponse_InvalidJson_IncludesDetails()
    {
        var response = ErrorResponse.InvalidJson("unexpected token");

        Assert.False(response.Ok);
        Assert.Contains("Invalid JSON", response.Error);
        Assert.Contains("unexpected token", response.Error);
    }

    [Fact]
    public void ErrorResponse_UnknownRequestType_IncludesType()
    {
        var response = ErrorResponse.UnknownRequestType("foobar");

        Assert.False(response.Ok);
        Assert.Contains("foobar", response.Error);
    }

    [Fact]
    public void ErrorResponse_MissingRequestType_HasExpectedMessage()
    {
        var response = ErrorResponse.MissingRequestType();

        Assert.False(response.Ok);
        Assert.Contains("type", response.Error, StringComparison.OrdinalIgnoreCase);
    }
}
