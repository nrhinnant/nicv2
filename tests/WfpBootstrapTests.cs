using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Tests for WFP bootstrap functionality using a mock IWfpEngine.
/// These tests verify the idempotency and error handling logic without
/// requiring actual WFP API access.
/// </summary>
public class WfpBootstrapTests
{
    // ========================================
    // MockWfpEngine for testing
    // ========================================

    private class MockWfpEngine : IWfpEngine
    {
        public bool ProviderExistsValue { get; set; }
        public bool SublayerExistsValue { get; set; }
        public bool DemoBlockFilterExistsValue { get; set; }
        public Result EnsureResult { get; set; } = Result.Success();
        public Result RemoveResult { get; set; } = Result.Success();
        public Result AddDemoBlockResult { get; set; } = Result.Success();
        public Result RemoveDemoBlockResult { get; set; } = Result.Success();
        public Result<int> RemoveAllFiltersResult { get; set; } = Result<int>.Success(0);
        public int EnsureCallCount { get; private set; }
        public int RemoveCallCount { get; private set; }
        public int AddDemoBlockCallCount { get; private set; }
        public int RemoveDemoBlockCallCount { get; private set; }
        public int RemoveAllFiltersCallCount { get; private set; }

        public Result EnsureProviderAndSublayerExist()
        {
            EnsureCallCount++;
            if (EnsureResult.IsSuccess)
            {
                ProviderExistsValue = true;
                SublayerExistsValue = true;
            }
            return EnsureResult;
        }

        public Result RemoveProviderAndSublayer()
        {
            RemoveCallCount++;
            if (RemoveResult.IsSuccess)
            {
                ProviderExistsValue = false;
                SublayerExistsValue = false;
            }
            return RemoveResult;
        }

        public Result<bool> ProviderExists() => Result<bool>.Success(ProviderExistsValue);
        public Result<bool> SublayerExists() => Result<bool>.Success(SublayerExistsValue);

        public Result AddDemoBlockFilter()
        {
            AddDemoBlockCallCount++;
            if (AddDemoBlockResult.IsSuccess)
            {
                DemoBlockFilterExistsValue = true;
            }
            return AddDemoBlockResult;
        }

        public Result RemoveDemoBlockFilter()
        {
            RemoveDemoBlockCallCount++;
            if (RemoveDemoBlockResult.IsSuccess)
            {
                DemoBlockFilterExistsValue = false;
            }
            return RemoveDemoBlockResult;
        }

        public Result<bool> DemoBlockFilterExists() => Result<bool>.Success(DemoBlockFilterExistsValue);

        public Result<int> RemoveAllFilters()
        {
            RemoveAllFiltersCallCount++;
            if (RemoveAllFiltersResult.IsSuccess)
            {
                DemoBlockFilterExistsValue = false;
            }
            return RemoveAllFiltersResult;
        }

        public Result<ApplyResult> ApplyFilters(List<CompiledFilter> filters)
        {
            return Result<ApplyResult>.Success(new ApplyResult { FiltersCreated = filters?.Count ?? 0, FiltersRemoved = 0 });
        }
    }

    // ========================================
    // Bootstrap Tests
    // ========================================

    [Fact]
    public void BootstrapFromCleanStateCreatesProviderAndSublayer()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = false,
            SublayerExistsValue = false
        };

        // Act
        var result = engine.EnsureProviderAndSublayerExist();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(engine.ProviderExistsValue);
        Assert.True(engine.SublayerExistsValue);
        Assert.Equal(1, engine.EnsureCallCount);
    }

    [Fact]
    public void BootstrapWhenAlreadyExistsIsIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true
        };

        // Act - Call twice
        var result1 = engine.EnsureProviderAndSublayerExist();
        var result2 = engine.EnsureProviderAndSublayerExist();

        // Assert - Both should succeed
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(engine.ProviderExistsValue);
        Assert.True(engine.SublayerExistsValue);
        Assert.Equal(2, engine.EnsureCallCount);
    }

    [Fact]
    public void BootstrapOnFailureReturnsError()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            EnsureResult = Result.Failure(ErrorCodes.WfpError, "Failed to open engine")
        };

        // Act
        var result = engine.EnsureProviderAndSublayerExist();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("Failed to open engine", result.Error.Message);
    }

    // ========================================
    // Teardown Tests
    // ========================================

    [Fact]
    public void TeardownWhenExistsRemovesProviderAndSublayer()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true
        };

        // Act
        var result = engine.RemoveProviderAndSublayer();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(engine.ProviderExistsValue);
        Assert.False(engine.SublayerExistsValue);
        Assert.Equal(1, engine.RemoveCallCount);
    }

    [Fact]
    public void TeardownWhenNotExistsIsIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = false,
            SublayerExistsValue = false
        };

        // Act - Call twice
        var result1 = engine.RemoveProviderAndSublayer();
        var result2 = engine.RemoveProviderAndSublayer();

        // Assert - Both should succeed
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(2, engine.RemoveCallCount);
    }

    [Fact]
    public void TeardownOnFiltersInUseReturnsError()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true,
            RemoveResult = Result.Failure(ErrorCodes.WfpError, "Cannot remove sublayer: filters still exist")
        };

        // Act
        var result = engine.RemoveProviderAndSublayer();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("filters still exist", result.Error.Message);
    }

    // ========================================
    // ProviderExists / SublayerExists Tests
    // ========================================

    [Fact]
    public void ProviderExistsWhenTrueReturnsTrue()
    {
        var engine = new MockWfpEngine { ProviderExistsValue = true };
        var result = engine.ProviderExists();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public void ProviderExistsWhenFalseReturnsFalse()
    {
        var engine = new MockWfpEngine { ProviderExistsValue = false };
        var result = engine.ProviderExists();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public void SublayerExistsWhenTrueReturnsTrue()
    {
        var engine = new MockWfpEngine { SublayerExistsValue = true };
        var result = engine.SublayerExists();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public void SublayerExistsWhenFalseReturnsFalse()
    {
        var engine = new MockWfpEngine { SublayerExistsValue = false };
        var result = engine.SublayerExists();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    // ========================================
    // Bootstrap + Teardown Sequence Tests
    // ========================================

    [Fact]
    public void FullLifecycleBootstrapThenTeardownWorks()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = false,
            SublayerExistsValue = false
        };

        // Act - Bootstrap
        var bootstrapResult = engine.EnsureProviderAndSublayerExist();
        Assert.True(bootstrapResult.IsSuccess);
        Assert.True(engine.ProviderExistsValue);
        Assert.True(engine.SublayerExistsValue);

        // Act - Teardown
        var teardownResult = engine.RemoveProviderAndSublayer();
        Assert.True(teardownResult.IsSuccess);
        Assert.False(engine.ProviderExistsValue);
        Assert.False(engine.SublayerExistsValue);
    }

    [Fact]
    public void FullLifecycleMultipleBootstrapsAndTeardownsAreIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = false,
            SublayerExistsValue = false
        };

        // Act - Multiple bootstraps
        Assert.True(engine.EnsureProviderAndSublayerExist().IsSuccess);
        Assert.True(engine.EnsureProviderAndSublayerExist().IsSuccess);
        Assert.True(engine.EnsureProviderAndSublayerExist().IsSuccess);

        // Verify state
        Assert.True(engine.ProviderExistsValue);
        Assert.True(engine.SublayerExistsValue);

        // Act - Multiple teardowns
        Assert.True(engine.RemoveProviderAndSublayer().IsSuccess);
        Assert.True(engine.RemoveProviderAndSublayer().IsSuccess);
        Assert.True(engine.RemoveProviderAndSublayer().IsSuccess);

        // Verify state
        Assert.False(engine.ProviderExistsValue);
        Assert.False(engine.SublayerExistsValue);

        // Verify call counts
        Assert.Equal(3, engine.EnsureCallCount);
        Assert.Equal(3, engine.RemoveCallCount);
    }
}

/// <summary>
/// Tests for Bootstrap/Teardown IPC messages.
/// </summary>
public class WfpBootstrapIpcMessageTests
{
    [Fact]
    public void BootstrapRequestHasCorrectType()
    {
        var request = new BootstrapRequest();
        Assert.Equal("bootstrap", request.Type);
    }

    [Fact]
    public void TeardownRequestHasCorrectType()
    {
        var request = new TeardownRequest();
        Assert.Equal("teardown", request.Type);
    }

    [Fact]
    public void ParseRequestBootstrapRequestParsesCorrectly()
    {
        var json = "{\"type\":\"bootstrap\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<BootstrapRequest>(result.Value);
    }

    [Fact]
    public void ParseRequestTeardownRequestParsesCorrectly()
    {
        var json = "{\"type\":\"teardown\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<TeardownRequest>(result.Value);
    }

    [Fact]
    public void BootstrapResponseSuccessSetsProperties()
    {
        var response = BootstrapResponse.Success(providerExists: true, sublayerExists: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.ProviderExists);
        Assert.True(response.SublayerExists);
    }

    [Fact]
    public void BootstrapResponseFailureSetsError()
    {
        var response = BootstrapResponse.Failure("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void TeardownResponseSuccessSetsProperties()
    {
        var response = TeardownResponse.Success(providerRemoved: true, sublayerRemoved: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.ProviderRemoved);
        Assert.True(response.SublayerRemoved);
    }

    [Fact]
    public void TeardownResponseFailureSetsError()
    {
        var response = TeardownResponse.Failure("Filters still exist");

        Assert.False(response.Ok);
        Assert.Equal("Filters still exist", response.Error);
    }

    [Fact]
    public void SerializeResponseBootstrapResponseProducesValidJson()
    {
        var response = BootstrapResponse.Success(providerExists: true, sublayerExists: false);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"providerExists\":true", json);
        Assert.Contains("\"sublayerExists\":false", json);
    }

    [Fact]
    public void SerializeResponseTeardownResponseProducesValidJson()
    {
        var response = TeardownResponse.Success(providerRemoved: false, sublayerRemoved: true);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"providerRemoved\":false", json);
        Assert.Contains("\"sublayerRemoved\":true", json);
    }
}

/// <summary>
/// Tests for IWfpEngine interface contract.
/// </summary>
public class IWfpEngineInterfaceTests
{
    [Fact]
    public void IWfpEngineHasAllRequiredMethods()
    {
        // Verify interface has all expected methods
        var interfaceType = typeof(IWfpEngine);

        Assert.NotNull(interfaceType.GetMethod("EnsureProviderAndSublayerExist"));
        Assert.NotNull(interfaceType.GetMethod("RemoveProviderAndSublayer"));
        Assert.NotNull(interfaceType.GetMethod("ProviderExists"));
        Assert.NotNull(interfaceType.GetMethod("SublayerExists"));
        Assert.NotNull(interfaceType.GetMethod("AddDemoBlockFilter"));
        Assert.NotNull(interfaceType.GetMethod("RemoveDemoBlockFilter"));
        Assert.NotNull(interfaceType.GetMethod("DemoBlockFilterExists"));
        Assert.NotNull(interfaceType.GetMethod("RemoveAllFilters"));
    }

    [Fact]
    public void IWfpEngineEnsureProviderAndSublayerExistReturnsResult()
    {
        var method = typeof(IWfpEngine).GetMethod("EnsureProviderAndSublayerExist");
        Assert.Equal(typeof(Result), method!.ReturnType);
    }

    [Fact]
    public void IWfpEngineRemoveProviderAndSublayerReturnsResult()
    {
        var method = typeof(IWfpEngine).GetMethod("RemoveProviderAndSublayer");
        Assert.Equal(typeof(Result), method!.ReturnType);
    }

    [Fact]
    public void IWfpEngineProviderExistsReturnsResultBool()
    {
        var method = typeof(IWfpEngine).GetMethod("ProviderExists");
        Assert.Equal(typeof(Result<bool>), method!.ReturnType);
    }

    [Fact]
    public void IWfpEngineSublayerExistsReturnsResultBool()
    {
        var method = typeof(IWfpEngine).GetMethod("SublayerExists");
        Assert.Equal(typeof(Result<bool>), method!.ReturnType);
    }
}
