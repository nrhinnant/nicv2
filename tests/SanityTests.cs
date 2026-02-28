using WfpTrafficControl.Shared;
using Xunit;

namespace WfpTrafficControl.Tests;

public class SanityTests
{
    [Fact]
    public void ProjectNameIsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(WfpConstants.ProjectName));
    }

    [Fact]
    public void ProjectNameHasExpectedValue()
    {
        Assert.Equal("WfpTrafficControl", WfpConstants.ProjectName);
    }
}

public class WfpConstantsTests
{
    [Fact]
    public void ProviderGuidIsNotEmpty()
    {
        Assert.NotEqual(Guid.Empty, WfpConstants.ProviderGuid);
    }

    [Fact]
    public void SublayerGuidIsNotEmpty()
    {
        Assert.NotEqual(Guid.Empty, WfpConstants.SublayerGuid);
    }

    [Fact]
    public void ProviderGuidIsStable()
    {
        // This test ensures the GUID never changes — changing it would orphan WFP objects
        var expected = new Guid("7A3F8E2D-1B4C-4D5E-9F6A-0C8B7D2E3F1A");
        Assert.Equal(expected, WfpConstants.ProviderGuid);
    }

    [Fact]
    public void SublayerGuidIsStable()
    {
        // This test ensures the GUID never changes — changing it would orphan WFP objects
        var expected = new Guid("B2C4D6E8-3A5F-4E7D-8C9B-1D2E3F4A5B6C");
        Assert.Equal(expected, WfpConstants.SublayerGuid);
    }

    [Fact]
    public void ProviderGuidDiffersFromSublayerGuid()
    {
        Assert.NotEqual(WfpConstants.ProviderGuid, WfpConstants.SublayerGuid);
    }

    [Fact]
    public void ServiceNameIsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(WfpConstants.ServiceName));
    }

    [Fact]
    public void PipeNameIsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(WfpConstants.PipeName));
    }

    [Fact]
    public void PipeFullPathContainsPipeName()
    {
        Assert.Contains(WfpConstants.PipeName, WfpConstants.PipeFullPath);
    }
}

public class ResultTests
{
    [Fact]
    public void ResultSuccessIsSuccess()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void ResultSuccessHasValue()
    {
        var result = Result<int>.Success(42);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ResultFailureIsFailure()
    {
        var result = Result<int>.Failure(ErrorCodes.InvalidArgument, "test error");
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ResultFailureHasError()
    {
        var result = Result<int>.Failure(ErrorCodes.InvalidArgument, "test error");
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
        Assert.Equal("test error", result.Error.Message);
    }

    [Fact]
    public void ResultAccessValueOnFailureThrows()
    {
        var result = Result<int>.Failure(ErrorCodes.Unknown, "error");
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ResultAccessErrorOnSuccessThrows()
    {
        var result = Result<int>.Success(42);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void ResultImplicitConversionFromValue()
    {
        Result<int> result = 42;
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ResultImplicitConversionFromError()
    {
        Result<int> result = new Error(ErrorCodes.Unknown, "error");
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ResultMatchCallsCorrectBranch()
    {
        var success = Result<int>.Success(42);
        var failure = Result<int>.Failure(ErrorCodes.Unknown, "error");

        var successResult = success.Match(v => $"value:{v}", e => $"error:{e.Code}");
        var failureResult = failure.Match(v => $"value:{v}", e => $"error:{e.Code}");

        Assert.Equal("value:42", successResult);
        Assert.Equal("error:UNKNOWN", failureResult);
    }

    [Fact]
    public void NonGenericResultSuccessIsSuccess()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void NonGenericResultFailureIsFailure()
    {
        var result = Result.Failure(ErrorCodes.Unknown, "error");
        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
    }
}

public class ErrorTests
{
    [Fact]
    public void ErrorConstructorSetsProperties()
    {
        var error = new Error("CODE", "message");
        Assert.Equal("CODE", error.Code);
        Assert.Equal("message", error.Message);
        Assert.Null(error.Exception);
    }

    [Fact]
    public void ErrorConstructorWithException()
    {
        var ex = new InvalidOperationException("inner");
        var error = new Error("CODE", "message", ex);
        Assert.Same(ex, error.Exception);
    }

    [Fact]
    public void ErrorToStringFormatsCorrectly()
    {
        var error = new Error("CODE", "message");
        Assert.Equal("[CODE] message", error.ToString());
    }

    [Fact]
    public void ErrorNullCodeThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new Error(null!, "message"));
    }

    [Fact]
    public void ErrorNullMessageThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new Error("CODE", null!));
    }
}
