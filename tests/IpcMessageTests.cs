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
    public void ParseRequestValidPingRequestReturnsPingRequest()
    {
        var json = """{"type":"ping"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<PingRequest>(result.Value);
        Assert.Equal(PingRequest.RequestType, result.Value.Type);
    }

    [Fact]
    public void ParseRequestPingRequestWithExtraFieldsReturnsPingRequest()
    {
        // Extra fields should be ignored
        var json = """{"type":"ping","extra":"ignored"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<PingRequest>(result.Value);
    }

    [Fact]
    public void ParseRequestEmptyStringReturnsError()
    {
        var result = IpcMessageParser.ParseRequest("");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("empty", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestWhitespaceOnlyReturnsError()
    {
        var result = IpcMessageParser.ParseRequest("   ");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void ParseRequestNullInputReturnsError()
    {
        var result = IpcMessageParser.ParseRequest(null!);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void ParseRequestInvalidJsonReturnsError()
    {
        var json = "not valid json";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("Invalid JSON", result.Error.Message);
    }

    [Fact]
    public void ParseRequestJsonArrayReturnsError()
    {
        var json = """["not","an","object"]""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("object", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestMissingTypeFieldReturnsError()
    {
        var json = """{"foo":"bar"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("type", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestTypeFieldNotStringReturnsError()
    {
        var json = """{"type":123}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("string", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestTypeFieldNullReturnsError()
    {
        var json = """{"type":null}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void ParseRequestUnknownTypeReturnsError()
    {
        var json = """{"type":"unknown"}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Contains("unknown", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestCaseInsensitiveTypeStillMatchesExact()
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
    public void SerializeResponsePingResponseSuccessContainsExpectedFields()
    {
        var response = PingResponse.Success("1.0.0");

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"serviceVersion\":\"1.0.0\"", json);
        Assert.Contains("\"time\":", json);
        Assert.DoesNotContain("\"error\"", json); // error should be omitted when null
    }

    [Fact]
    public void SerializeResponseErrorResponseContainsExpectedFields()
    {
        var response = new ErrorResponse("Test error message");

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("\"error\":\"Test error message\"", json);
    }

    [Fact]
    public void SerializeResponseAccessDeniedContainsAdminMessage()
    {
        var response = ErrorResponse.AccessDenied();

        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":false", json);
        Assert.Contains("Administrator", json);
    }

    [Fact]
    public void CreateErrorResponseReturnsValidJson()
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
    public void PingRequestTypeIsPing()
    {
        var request = new PingRequest();
        Assert.Equal("ping", request.Type);
        Assert.Equal("ping", PingRequest.RequestType);
    }

    [Fact]
    public void PingResponseSuccessSetsOkTrue()
    {
        var response = PingResponse.Success("1.2.3");

        Assert.True(response.Ok);
        Assert.Equal("1.2.3", response.ServiceVersion);
        Assert.NotNull(response.Time);
        Assert.Null(response.Error);
    }

    [Fact]
    public void PingResponseSuccessTimeIsIso8601()
    {
        var response = PingResponse.Success("1.0.0");

        // ISO 8601 format should be parseable
        Assert.True(DateTimeOffset.TryParse(response.Time, out var parsed));
        // Time should be recent (within last minute)
        Assert.True((DateTimeOffset.UtcNow - parsed).TotalMinutes < 1);
    }

    [Fact]
    public void ErrorResponseDefaultConstructorSetsOkFalse()
    {
        var response = new ErrorResponse();
        Assert.False(response.Ok);
    }

    [Fact]
    public void ErrorResponseWithMessageSetsError()
    {
        var response = new ErrorResponse("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void ErrorResponseAccessDeniedHasExpectedMessage()
    {
        var response = ErrorResponse.AccessDenied();

        Assert.False(response.Ok);
        Assert.Contains("Access denied", response.Error);
        Assert.Contains("Administrator", response.Error);
    }

    [Fact]
    public void ErrorResponseInvalidJsonIncludesDetails()
    {
        var response = ErrorResponse.InvalidJson("unexpected token");

        Assert.False(response.Ok);
        Assert.Contains("Invalid JSON", response.Error);
        Assert.Contains("unexpected token", response.Error);
    }

    [Fact]
    public void ErrorResponseUnknownRequestTypeIncludesType()
    {
        var response = ErrorResponse.UnknownRequestType("foobar");

        Assert.False(response.Ok);
        Assert.Contains("foobar", response.Error);
    }

    [Fact]
    public void ErrorResponseMissingRequestTypeHasExpectedMessage()
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
    public void PipeClientConnectServiceNotRunningReturnsServiceUnavailableError()
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
    public void PipeClientSendRequestWithoutConnectReturnsInvalidStateError()
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
    public void PipeClientDisposeCanBeCalledMultipleTimes()
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
    public void ParseRequestDeeplyNestedJsonHandledGracefully()
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
    public void ParseRequestVeryLongStringHandledGracefully()
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
    public void ParseRequestUnicodeCharactersHandledCorrectly()
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
    public void ParseRequestNullBytesInStringHandledGracefully()
    {
        // JSON with escaped null character
        var json = "{\"type\":\"ping\",\"extra\":\"\\u0000test\"}";

        var result = IpcMessageParser.ParseRequest(json);

        // Should parse - extra fields are ignored
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ParseRequestControlCharactersInStringHandledGracefully()
    {
        // JSON with various control characters
        var json = "{\"type\":\"ping\",\"extra\":\"\\t\\r\\n\\b\\f\"}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ParseRequestEmptyObjectReturnsMissingTypeError()
    {
        var json = "{}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("type", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestWhitespaceInTypeReturnsUnknownType()
    {
        var json = """{"type":" ping "}""";

        var result = IpcMessageParser.ParseRequest(json);

        // " ping " != "ping" so should be unknown
        Assert.True(result.IsFailure);
        Assert.Contains("Unknown request type", result.Error.Message);
    }

    [Fact]
    public void ParseRequestNumericTypeReturnsError()
    {
        var json = """{"type":42}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("string", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestBooleanTypeReturnsError()
    {
        var json = """{"type":true}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("string", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestArrayTypeReturnsError()
    {
        var json = """{"type":["ping"]}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequestObjectTypeReturnsError()
    {
        var json = """{"type":{"name":"ping"}}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequestTrailingCommaHandlesGracefully()
    {
        // Some JSON parsers are lenient about trailing commas
        var json = """{"type":"ping",}""";

        var result = IpcMessageParser.ParseRequest(json);

        // System.Text.Json is strict - this should fail
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequestSingleQuotesReturnsError()
    {
        // JSON requires double quotes
        var json = "{'type':'ping'}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("Invalid JSON", result.Error.Message);
    }

    [Fact]
    public void ParseRequestUnquotedKeysReturnsError()
    {
        var json = "{type:\"ping\"}";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ParseRequestCommentsInJsonReturnsError()
    {
        // JSON doesn't support comments
        var json = """{"type":"ping" /* comment */}""";

        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
    }
}

