using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for WfpErrorTranslator.
/// These tests verify error code translation without requiring actual WFP calls.
/// </summary>
public class WfpErrorTranslatorTests
{
    // Win32 error codes
    private const uint ERROR_SUCCESS = 0;
    private const uint ERROR_ACCESS_DENIED = 5;
    private const uint ERROR_INVALID_PARAMETER = 87;
    private const uint ERROR_NOT_FOUND = 1168;

    // WFP-specific error codes
    private const uint FWP_E_ALREADY_EXISTS = 0x80320009;
    private const uint FWP_E_IN_USE = 0x80320006;
    private const uint FWP_E_PROVIDER_NOT_FOUND = 0x80320008;
    private const uint FWP_E_SUBLAYER_NOT_FOUND = 0x8032000A;
    private const uint FWP_E_FILTER_NOT_FOUND = 0x8032000B;
    private const uint FWP_E_NOT_FOUND = 0x80320002;
    private const uint FWP_E_SESSION_ABORTED = 0x80320017;
    private const uint FWP_E_INVALID_PARAMETER = 0x80320035;

    #region IsSuccess Tests

    [Fact]
    public void IsSuccessWithZeroReturnsTrue()
    {
        Assert.True(WfpErrorTranslator.IsSuccess(ERROR_SUCCESS));
    }

    [Fact]
    public void IsSuccessWithNonZeroReturnsFalse()
    {
        Assert.False(WfpErrorTranslator.IsSuccess(ERROR_ACCESS_DENIED));
        Assert.False(WfpErrorTranslator.IsSuccess(FWP_E_NOT_FOUND));
    }

    #endregion

    #region TranslateError Tests

    [Fact]
    public void TranslateErrorWithSuccessReturnsNull()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_SUCCESS);
        Assert.Null(error);
    }

    [Fact]
    public void TranslateErrorWithAccessDeniedReturnsAccessDeniedError()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_ACCESS_DENIED);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.AccessDenied, error.Code);
        Assert.Contains("Access denied", error.Message);
        Assert.Contains("administrator", error.Message);
    }

    [Fact]
    public void TranslateErrorWithInvalidParameterReturnsInvalidArgumentError()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_INVALID_PARAMETER);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Invalid parameter", error.Message);
    }

    [Fact]
    public void TranslateErrorWithNotFoundReturnsNotFoundError()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_NOT_FOUND);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.NotFound, error.Code);
        Assert.Contains("not found", error.Message);
    }

    [Fact]
    public void TranslateErrorWithFwpAlreadyExistsReturnsWfpError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_ALREADY_EXISTS);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.WfpError, error.Code);
        Assert.Contains("already exists", error.Message);
    }

    [Fact]
    public void TranslateErrorWithFwpInUseReturnsWfpError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_IN_USE);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.WfpError, error.Code);
        Assert.Contains("in use", error.Message);
    }

    [Fact]
    public void TranslateErrorWithFwpProviderNotFoundReturnsNotFoundError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_PROVIDER_NOT_FOUND);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.NotFound, error.Code);
        Assert.Contains("provider", error.Message.ToLower());
        Assert.Contains("not found", error.Message.ToLower());
    }

    [Fact]
    public void TranslateErrorWithFwpSublayerNotFoundReturnsNotFoundError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_SUBLAYER_NOT_FOUND);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.NotFound, error.Code);
        Assert.Contains("sublayer", error.Message.ToLower());
        Assert.Contains("not found", error.Message.ToLower());
    }

    [Fact]
    public void TranslateErrorWithFwpFilterNotFoundReturnsNotFoundError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_FILTER_NOT_FOUND);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.NotFound, error.Code);
        Assert.Contains("filter", error.Message.ToLower());
        Assert.Contains("not found", error.Message.ToLower());
    }

    [Fact]
    public void TranslateErrorWithFwpNotFoundReturnsNotFoundError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_NOT_FOUND);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.NotFound, error.Code);
        Assert.Contains("not found", error.Message.ToLower());
    }

    [Fact]
    public void TranslateErrorWithFwpSessionAbortedReturnsWfpError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_SESSION_ABORTED);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.WfpError, error.Code);
        Assert.Contains("session", error.Message.ToLower());
        Assert.Contains("aborted", error.Message.ToLower());
    }

    [Fact]
    public void TranslateErrorWithFwpInvalidParameterReturnsInvalidArgumentError()
    {
        var error = WfpErrorTranslator.TranslateError(FWP_E_INVALID_PARAMETER);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Invalid parameter", error.Message);
    }

    [Fact]
    public void TranslateErrorWithUnknownErrorReturnsWfpErrorWithHexCode()
    {
        const uint unknownError = 0xDEADBEEF;
        var error = WfpErrorTranslator.TranslateError(unknownError);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.WfpError, error.Code);
        // The error message contains the hex code (case-insensitive check)
        Assert.Contains("DEADBEEF", error.Message.ToUpper());
    }

    [Fact]
    public void TranslateErrorWithContextIncludesContextInMessage()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_ACCESS_DENIED, "opening WFP engine");

        Assert.NotNull(error);
        Assert.StartsWith("opening WFP engine:", error.Message);
    }

    [Fact]
    public void TranslateErrorWithNullContextOmitsContext()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_ACCESS_DENIED, null);

        Assert.NotNull(error);
        Assert.DoesNotContain(":", error.Message.Split(' ')[0]);
    }

    [Fact]
    public void TranslateErrorWithEmptyContextOmitsContext()
    {
        var error = WfpErrorTranslator.TranslateError(ERROR_ACCESS_DENIED, "");

        Assert.NotNull(error);
        Assert.StartsWith("Access denied", error.Message);
    }

    #endregion

    #region ToFailedResult<T> Tests

    [Fact]
    public void ToFailedResultTWithErrorReturnsFailedResult()
    {
        var result = WfpErrorTranslator.ToFailedResult<int>(ERROR_ACCESS_DENIED);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AccessDenied, result.Error.Code);
    }

    [Fact]
    public void ToFailedResultTWithContextIncludesContextInError()
    {
        var result = WfpErrorTranslator.ToFailedResult<int>(ERROR_ACCESS_DENIED, "test operation");

        Assert.True(result.IsFailure);
        Assert.Contains("test operation", result.Error.Message);
    }

    [Fact]
    public void ToFailedResultTWithSuccessThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            WfpErrorTranslator.ToFailedResult<int>(ERROR_SUCCESS));
    }

    #endregion

    #region ToFailedResult (non-generic) Tests

    [Fact]
    public void ToFailedResultWithErrorReturnsFailedResult()
    {
        var result = WfpErrorTranslator.ToFailedResult(ERROR_ACCESS_DENIED);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AccessDenied, result.Error.Code);
    }

    [Fact]
    public void ToFailedResultWithContextIncludesContextInError()
    {
        var result = WfpErrorTranslator.ToFailedResult(ERROR_ACCESS_DENIED, "test operation");

        Assert.True(result.IsFailure);
        Assert.Contains("test operation", result.Error.Message);
    }

    [Fact]
    public void ToFailedResultWithSuccessThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            WfpErrorTranslator.ToFailedResult(ERROR_SUCCESS));
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData(1u)]      // ERROR_INVALID_FUNCTION
    [InlineData(2u)]      // ERROR_FILE_NOT_FOUND
    [InlineData(3u)]      // ERROR_PATH_NOT_FOUND
    [InlineData(50u)]     // ERROR_NOT_SUPPORTED
    [InlineData(1450u)]   // ERROR_NO_SYSTEM_RESOURCES
    public void TranslateErrorWithVariousWin32ErrorsReturnsWfpError(uint errorCode)
    {
        var error = WfpErrorTranslator.TranslateError(errorCode);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.WfpError, error.Code);
        Assert.Contains($"0x{errorCode:X8}", error.Message);
    }

    [Fact]
    public void TranslateErrorWithMaxUintHandlesGracefully()
    {
        var error = WfpErrorTranslator.TranslateError(uint.MaxValue);

        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.WfpError, error.Code);
    }

    #endregion
}
