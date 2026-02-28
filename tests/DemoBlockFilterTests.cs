using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Tests for demo block filter functionality using a mock IWfpEngine.
/// </summary>
public class DemoBlockFilterTests
{
    // ========================================
    // MockWfpEngine for demo block testing
    // ========================================

    private sealed class MockWfpEngine : IWfpEngine
    {
        public bool ProviderExistsValue { get; set; }
        public bool SublayerExistsValue { get; set; }
        public bool DemoBlockFilterExistsValue { get; set; }
        public Result EnsureResult { get; set; } = Result.Success();
        public Result RemoveResult { get; set; } = Result.Success();
        public Result AddDemoBlockResult { get; set; } = Result.Success();
        public Result RemoveDemoBlockResult { get; set; } = Result.Success();
        public Result<int> RemoveAllFiltersResult { get; set; } = Result<int>.Success(0);
        public int AddDemoBlockCallCount { get; private set; }
        public int RemoveDemoBlockCallCount { get; private set; }
        public int RemoveAllFiltersCallCount { get; private set; }

        public Result EnsureProviderAndSublayerExist()
        {
            if (EnsureResult.IsSuccess)
            {
                ProviderExistsValue = true;
                SublayerExistsValue = true;
            }
            return EnsureResult;
        }

        public Result RemoveProviderAndSublayer()
        {
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
    // Add Demo Block Filter Tests
    // ========================================

    [Fact]
    public void AddDemoBlockFilterWhenNotExistsCreatesFilter()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true,
            DemoBlockFilterExistsValue = false
        };

        // Act
        var result = engine.AddDemoBlockFilter();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(engine.DemoBlockFilterExistsValue);
        Assert.Equal(1, engine.AddDemoBlockCallCount);
    }

    [Fact]
    public void AddDemoBlockFilterWhenAlreadyExistsIsIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            DemoBlockFilterExistsValue = true
        };

        // Act - Call twice
        var result1 = engine.AddDemoBlockFilter();
        var result2 = engine.AddDemoBlockFilter();

        // Assert - Both should succeed
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(engine.DemoBlockFilterExistsValue);
        Assert.Equal(2, engine.AddDemoBlockCallCount);
    }

    [Fact]
    public void AddDemoBlockFilterOnFailureReturnsError()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            AddDemoBlockResult = Result.Failure(ErrorCodes.WfpError, "Failed to add filter")
        };

        // Act
        var result = engine.AddDemoBlockFilter();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("Failed to add filter", result.Error.Message);
        Assert.False(engine.DemoBlockFilterExistsValue); // Should not be set on failure
    }

    // ========================================
    // Remove Demo Block Filter Tests
    // ========================================

    [Fact]
    public void RemoveDemoBlockFilterWhenExistsRemovesFilter()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            DemoBlockFilterExistsValue = true
        };

        // Act
        var result = engine.RemoveDemoBlockFilter();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(engine.DemoBlockFilterExistsValue);
        Assert.Equal(1, engine.RemoveDemoBlockCallCount);
    }

    [Fact]
    public void RemoveDemoBlockFilterWhenNotExistsIsIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            DemoBlockFilterExistsValue = false
        };

        // Act - Call twice
        var result1 = engine.RemoveDemoBlockFilter();
        var result2 = engine.RemoveDemoBlockFilter();

        // Assert - Both should succeed
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(2, engine.RemoveDemoBlockCallCount);
    }

    [Fact]
    public void RemoveDemoBlockFilterOnFailureReturnsError()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            DemoBlockFilterExistsValue = true,
            RemoveDemoBlockResult = Result.Failure(ErrorCodes.WfpError, "Failed to remove filter")
        };

        // Act
        var result = engine.RemoveDemoBlockFilter();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("Failed to remove filter", result.Error.Message);
        Assert.True(engine.DemoBlockFilterExistsValue); // Should still exist on failure
    }

    // ========================================
    // Demo Block Filter Exists Tests
    // ========================================

    [Fact]
    public void DemoBlockFilterExistsWhenTrueReturnsTrue()
    {
        var engine = new MockWfpEngine { DemoBlockFilterExistsValue = true };
        var result = engine.DemoBlockFilterExists();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public void DemoBlockFilterExistsWhenFalseReturnsFalse()
    {
        var engine = new MockWfpEngine { DemoBlockFilterExistsValue = false };
        var result = engine.DemoBlockFilterExists();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    // ========================================
    // Remove All Filters Tests
    // ========================================

    [Fact]
    public void RemoveAllFiltersRemovesDemoBlockFilter()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            DemoBlockFilterExistsValue = true
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(engine.DemoBlockFilterExistsValue);
        Assert.Equal(1, engine.RemoveAllFiltersCallCount);
    }

    [Fact]
    public void RemoveAllFiltersWhenNoFiltersIsIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            DemoBlockFilterExistsValue = false
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, engine.RemoveAllFiltersCallCount);
    }

    // ========================================
    // Full Lifecycle Tests
    // ========================================

    [Fact]
    public void FullLifecycleEnableThenDisableWorks()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true,
            DemoBlockFilterExistsValue = false
        };

        // Act - Enable
        var enableResult = engine.AddDemoBlockFilter();
        Assert.True(enableResult.IsSuccess);
        Assert.True(engine.DemoBlockFilterExistsValue);

        // Act - Disable
        var disableResult = engine.RemoveDemoBlockFilter();
        Assert.True(disableResult.IsSuccess);
        Assert.False(engine.DemoBlockFilterExistsValue);
    }

    [Fact]
    public void FullLifecycleEnableThenRollbackWorks()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true,
            DemoBlockFilterExistsValue = false
        };

        // Act - Enable
        var enableResult = engine.AddDemoBlockFilter();
        Assert.True(enableResult.IsSuccess);
        Assert.True(engine.DemoBlockFilterExistsValue);

        // Act - Rollback
        var rollbackResult = engine.RemoveAllFilters();
        Assert.True(rollbackResult.IsSuccess);
        Assert.False(engine.DemoBlockFilterExistsValue);
    }
}

/// <summary>
/// Tests for Demo Block IPC messages.
/// </summary>
public class DemoBlockIpcMessageTests
{
    [Fact]
    public void DemoBlockEnableRequestHasCorrectType()
    {
        var request = new DemoBlockEnableRequest();
        Assert.Equal("demo-block-enable", request.Type);
    }

    [Fact]
    public void DemoBlockDisableRequestHasCorrectType()
    {
        var request = new DemoBlockDisableRequest();
        Assert.Equal("demo-block-disable", request.Type);
    }

    [Fact]
    public void DemoBlockStatusRequestHasCorrectType()
    {
        var request = new DemoBlockStatusRequest();
        Assert.Equal("demo-block-status", request.Type);
    }

    [Fact]
    public void RollbackRequestHasCorrectType()
    {
        var request = new RollbackRequest();
        Assert.Equal("rollback", request.Type);
    }

    [Fact]
    public void ParseRequestDemoBlockEnableRequestParsesCorrectly()
    {
        var json = "{\"type\":\"demo-block-enable\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<DemoBlockEnableRequest>(result.Value);
    }

    [Fact]
    public void ParseRequestDemoBlockDisableRequestParsesCorrectly()
    {
        var json = "{\"type\":\"demo-block-disable\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<DemoBlockDisableRequest>(result.Value);
    }

    [Fact]
    public void ParseRequestDemoBlockStatusRequestParsesCorrectly()
    {
        var json = "{\"type\":\"demo-block-status\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<DemoBlockStatusRequest>(result.Value);
    }

    [Fact]
    public void ParseRequestRollbackRequestParsesCorrectly()
    {
        var json = "{\"type\":\"rollback\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<RollbackRequest>(result.Value);
    }

    [Fact]
    public void DemoBlockEnableResponseSuccessSetsProperties()
    {
        var response = DemoBlockEnableResponse.Success(filterEnabled: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.FilterEnabled);
    }

    [Fact]
    public void DemoBlockEnableResponseFailureSetsError()
    {
        var response = DemoBlockEnableResponse.Failure("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void DemoBlockDisableResponseSuccessSetsProperties()
    {
        var response = DemoBlockDisableResponse.Success(filterDisabled: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.FilterDisabled);
    }

    [Fact]
    public void DemoBlockStatusResponseSuccessWhenActiveIncludesBlockedTarget()
    {
        var response = DemoBlockStatusResponse.Success(filterActive: true);

        Assert.True(response.Ok);
        Assert.True(response.FilterActive);
        Assert.NotNull(response.BlockedTarget);
        Assert.Contains("1.1.1.1", response.BlockedTarget);
    }

    [Fact]
    public void DemoBlockStatusResponseSuccessWhenInactiveNoBlockedTarget()
    {
        var response = DemoBlockStatusResponse.Success(filterActive: false);

        Assert.True(response.Ok);
        Assert.False(response.FilterActive);
        Assert.Null(response.BlockedTarget);
    }

    [Fact]
    public void RollbackResponseSuccessSetsProperties()
    {
        var response = RollbackResponse.Success(filtersRemoved: 3);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.Equal(3, response.FiltersRemoved);
    }

    [Fact]
    public void RollbackResponseFailureSetsError()
    {
        var response = RollbackResponse.Failure("Rollback failed");

        Assert.False(response.Ok);
        Assert.Equal("Rollback failed", response.Error);
    }

    [Fact]
    public void SerializeResponseDemoBlockEnableResponseProducesValidJson()
    {
        var response = DemoBlockEnableResponse.Success(filterEnabled: true);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filterEnabled\":true", json);
    }

    [Fact]
    public void SerializeResponseDemoBlockStatusResponseProducesValidJson()
    {
        var response = DemoBlockStatusResponse.Success(filterActive: true);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filterActive\":true", json);
        Assert.Contains("\"blockedTarget\":", json);
    }

    [Fact]
    public void SerializeResponseRollbackResponseProducesValidJson()
    {
        var response = RollbackResponse.Success(filtersRemoved: 5);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filtersRemoved\":5", json);
    }
}

/// <summary>
/// Tests for demo block filter constants.
/// </summary>
public class DemoBlockConstantsTests
{
    [Fact]
    public void DemoBlockFilterGuidIsValid()
    {
        Assert.NotEqual(Guid.Empty, WfpConstants.DemoBlockFilterGuid);
    }

    [Fact]
    public void DemoBlockFilterGuidIsDifferentFromOtherGuids()
    {
        Assert.NotEqual(WfpConstants.ProviderGuid, WfpConstants.DemoBlockFilterGuid);
        Assert.NotEqual(WfpConstants.SublayerGuid, WfpConstants.DemoBlockFilterGuid);
    }

    [Fact]
    public void DemoBlockFilterNameIsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(WfpConstants.DemoBlockFilterName));
    }

    [Fact]
    public void DemoBlockRemoteIpIs1111()
    {
        // 1.1.1.1 in host byte order: 0x01010101
        Assert.Equal(0x01010101u, WfpConstants.DemoBlockRemoteIp);
    }

    [Fact]
    public void DemoBlockRemotePortIs443()
    {
        Assert.Equal((ushort)443, WfpConstants.DemoBlockRemotePort);
    }

    [Fact]
    public void ProtocolTcpIs6()
    {
        Assert.Equal((byte)6, WfpConstants.ProtocolTcp);
    }
}
