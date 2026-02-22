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

/// <summary>
/// Integration tests for CLI error handling when service is not running.
/// These tests verify the PipeClient error handling without needing an actual service.
/// </summary>
/// <remarks>
/// <para><strong>Related VM Integration Test Scenarios (require manual testing):</strong></para>
/// <list type="bullet">
///   <item><description>7.2.1 End-to-End: Service install → apply policy → verify traffic blocked → uninstall</description></item>
///   <item><description>7.3.1 Security: Non-admin CLI connection rejection (requires non-elevated process)</description></item>
///   <item><description>7.3.2 Security: Rate limiting enforcement across multiple rapid connections</description></item>
/// </list>
/// <para>
/// These scenarios require a Windows VM with:
/// - Administrator privileges for WFP operations
/// - The service installed and running
/// - Network connectivity for traffic blocking verification
/// </para>
/// <para>
/// See /scripts/Smoke-Test.ps1 for the manual test script.
/// See /docs/features/025-testing-strategy.md for the full testing strategy.
/// </para>
/// </remarks>
public class CliServiceConnectionTests
{
    [Fact]
    public void PipeClient_Connect_ServiceNotRunning_ReturnsServiceUnavailableError()
    {
        // Arrange - create a client that will fail to connect (no service running)
        using var client = new WfpTrafficControl.Cli.PipeClient();

        // Act
        var result = client.Connect();

        // Assert - should fail with ServiceUnavailable error
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.ServiceUnavailable, result.Error.Code);
        Assert.Contains("service", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PipeClient_SendRequest_WithoutConnect_ReturnsInvalidStateError()
    {
        // Arrange - create a client but don't connect
        using var client = new WfpTrafficControl.Cli.PipeClient();
        var request = new PingRequest();

        // Act
        var result = client.SendRequest<PingResponse>(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidState, result.Error.Code);
        Assert.Contains("Not connected", result.Error.Message);
    }

    [Fact]
    public void PipeClient_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var client = new WfpTrafficControl.Cli.PipeClient();

        // Act - should not throw
        client.Dispose();
        var exception = Record.Exception(() => client.Dispose());

        // Assert
        Assert.Null(exception);
    }
}

/// <summary>
/// Additional malformed IPC request edge case tests for security robustness.
/// </summary>
public class IpcMalformedRequestEdgeCaseTests
{
    [Fact]
    public void ParseRequest_DeeplyNestedJson_HandledGracefully()
    {
        // Create deeply nested JSON that could cause stack overflow
        var nested = "{";
        for (int i = 0; i < 50; i++)
        {
            nested += "\"a\":{";
        }
        nested += "\"type\":\"ping\"";
        for (int i = 0; i < 51; i++)
        {
            nested += "}";
        }

        var result = IpcMessageParser.ParseRequest(nested);

        // Should either parse or fail cleanly without exception
        // The type is deeply nested so won't be found at root level
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequest_VeryLongString_HandledGracefully()
    {
        // Create a request with a very long string value
        var longString = new string('x', 100000);
        var json = $"{{\"type\":\"apply\",\"policyPath\":\"{longString}\"}}";

        var result = IpcMessageParser.ParseRequest(json);

        // Should parse successfully (just a long path)
        Assert.True(result.IsSuccess);
        Assert.IsType<ApplyRequest>(result.Value);
    }

    [Fact]
    public void ParseRequest_UnicodeCharacters_HandledCorrectly()
    {
        var json = """{"type":"apply","policyPath":"C:\\путь\\策略.json"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<ApplyRequest>(result.Value);
        var applyRequest = (ApplyRequest)result.Value;
        Assert.Contains("путь", applyRequest.PolicyPath);
        Assert.Contains("策略", applyRequest.PolicyPath);
    }

    [Fact]
    public void ParseRequest_NullBytesInString_HandledGracefully()
    {
        // JSON with escaped null character
        var json = "{\"type\":\"ping\",\"extra\":\"\\u0000test\"}";

        var result = IpcMessageParser.ParseRequest(json);

        // Should parse - extra fields are ignored
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ParseRequest_ControlCharactersInString_HandledGracefully()
    {
        // JSON with various control characters
        var json = "{\"type\":\"ping\",\"extra\":\"\\t\\r\\n\\b\\f\"}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ParseRequest_EmptyObject_ReturnsMissingTypeError()
    {
        var json = "{}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("type", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_WhitespaceInType_ReturnsUnknownType()
    {
        var json = """{"type":" ping "}""";

        var result = IpcMessageParser.ParseRequest(json);

        // " ping " != "ping" so should be unknown
        Assert.True(result.IsFailure);
        Assert.Contains("Unknown request type", result.Error.Message);
    }

    [Fact]
    public void ParseRequest_NumericType_ReturnsError()
    {
        var json = """{"type":42}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("string", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_BooleanType_ReturnsError()
    {
        var json = """{"type":true}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("string", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequest_ArrayType_ReturnsError()
    {
        var json = """{"type":["ping"]}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequest_ObjectType_ReturnsError()
    {
        var json = """{"type":{"name":"ping"}}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequest_TrailingComma_HandlesGracefully()
    {
        // Some JSON parsers are lenient about trailing commas
        var json = """{"type":"ping",}""";

        var result = IpcMessageParser.ParseRequest(json);

        // System.Text.Json is strict - this should fail
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequest_SingleQuotes_ReturnsError()
    {
        // JSON requires double quotes
        var json = "{'type':'ping'}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid JSON", result.Error.Message);
    }

    [Fact]
    public void ParseRequest_UnquotedKeys_ReturnsError()
    {
        var json = "{type:\"ping\"}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequest_CommentsInJson_ReturnsError()
    {
        // JSON doesn't support comments
        var json = """{"type":"ping" /* comment */}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }
}

