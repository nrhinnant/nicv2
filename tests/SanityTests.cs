using WfpTrafficControl.Shared;
using Xunit;

namespace WfpTrafficControl.Tests;

public class SanityTests
{
    [Fact]
    public void ProjectName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Placeholder.ProjectName));
    }

    [Fact]
    public void ProjectName_HasExpectedValue()
    {
        Assert.Equal("WfpTrafficControl", Placeholder.ProjectName);
    }
}

public class WfpConstantsTests
{
    [Fact]
    public void ProviderGuid_IsNotEmpty()
    {
        Assert.NotEqual(Guid.Empty, WfpConstants.ProviderGuid);
    }

    [Fact]
    public void SublayerGuid_IsNotEmpty()
    {
        Assert.NotEqual(Guid.Empty, WfpConstants.SublayerGuid);
    }

    [Fact]
    public void ProviderGuid_IsStable()
    {
        // This test ensures the GUID never changes — changing it would orphan WFP objects
        var expected = new Guid("7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A");
        Assert.Equal(expected, WfpConstants.ProviderGuid);
    }

    [Fact]
    public void SublayerGuid_IsStable()
    {
        // This test ensures the GUID never changes — changing it would orphan WFP objects
        var expected = new Guid("B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C");
        Assert.Equal(expected, WfpConstants.SublayerGuid);
    }

    [Fact]
    public void ProviderGuid_DiffersFromSublayerGuid()
    {
        Assert.NotEqual(WfpConstants.ProviderGuid, WfpConstants.SublayerGuid);
    }

    [Fact]
    public void ServiceName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(WfpConstants.ServiceName));
    }

    [Fact]
    public void PipeName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(WfpConstants.PipeName));
    }

    [Fact]
    public void PipeFullPath_ContainsPipeName()
    {
        Assert.Contains(WfpConstants.PipeName, WfpConstants.PipeFullPath);
    }
}

public class ResultTests
{
    [Fact]
    public void Result_Success_IsSuccess()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Result_Success_HasValue()
    {
        var result = Result<int>.Success(42);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Result_Failure_IsFailure()
    {
        var result = Result<int>.Failure(ErrorCodes.InvalidArgument, "test error");
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Result_Failure_HasError()
    {
        var result = Result<int>.Failure(ErrorCodes.InvalidArgument, "test error");
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Equal("test error", result.Error.Message);
    }

    [Fact]
    public void Result_AccessValueOnFailure_Throws()
    {
        var result = Result<int>.Failure(ErrorCodes.Unknown, "error");
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Result_AccessErrorOnSuccess_Throws()
    {
        var result = Result<int>.Success(42);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Result_ImplicitConversion_FromValue()
    {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Result_ImplicitConversion_FromError()
    {
        Result<int> result = new Error(ErrorCodes.Unknown, "error");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Result_Match_CallsCorrectBranch()
    {
        var success = Result<int>.Success(42);
        var failure = Result<int>.Failure(ErrorCodes.Unknown, "error");

        var successResult = success.Match(v => $"value:{v}", e => $"error:{e.Code}");
        var failureResult = failure.Match(v => $"value:{v}", e => $"error:{e.Code}");

        Assert.Equal("value:42", successResult);
        Assert.Equal("error:UNKNOWN", failureResult);
    }

    [Fact]
    public void NonGenericResult_Success_IsSuccess()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void NonGenericResult_Failure_IsFailure()
    {
        var result = Result.Failure(ErrorCodes.Unknown, "error");
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
    }
}

public class ErrorTests
{
    [Fact]
    public void Error_Constructor_SetsProperties()
    {
        var error = new Error("CODE", "message");
        Assert.Equal("CODE", error.Code);
        Assert.Equal("message", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void Error_Constructor_WithException()
    {
        var ex = new InvalidOperationException("inner");
        var error = new Error("CODE", "message", ex);
        Assert.Same(ex, error.Exception);
    }

    [Fact]
    public void Error_ToString_FormatsCorrectly()
    {
        var error = new Error("CODE", "message");
        Assert.Equal("[CODE] message", error.ToString());
    }

    [Fact]
    public void Error_NullCode_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Error(null!, "message"));
    }

    [Fact]
    public void Error_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Error("CODE", null!));
    }
}
