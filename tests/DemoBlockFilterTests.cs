using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Native;
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

    private class MockWfpEngine : IWfpEngine
    {
        public bool ProviderExistsValue { get; set; }
        public bool SublayerExistsValue { get; set; }
        public bool DemoBlockFilterExistsValue { get; set; }
        public Result EnsureResult { get; set; } = Result.Success();
        public Result RemoveResult { get; set; } = Result.Success();
        public Result AddDemoBlockResult { get; set; } = Result.Success();
        public Result RemoveDemoBlockResult { get; set; } = Result.Success();
        public Result RemoveAllFiltersResult { get; set; } = Result.Success();
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

        public Result RemoveAllFilters()
        {
            RemoveAllFiltersCallCount++;
            if (RemoveAllFiltersResult.IsSuccess)
            {
                DemoBlockFilterExistsValue = false;
            }
            return RemoveAllFiltersResult;
        }
    }

    // ========================================
    // Add Demo Block Filter Tests
    // ========================================

    [Fact]
    public void AddDemoBlockFilter_WhenNotExists_CreatesFilter()
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
    public void AddDemoBlockFilter_WhenAlreadyExists_IsIdempotent()
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
    public void AddDemoBlockFilter_OnFailure_ReturnsError()
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
    public void RemoveDemoBlockFilter_WhenExists_RemovesFilter()
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
    public void RemoveDemoBlockFilter_WhenNotExists_IsIdempotent()
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
    public void RemoveDemoBlockFilter_OnFailure_ReturnsError()
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
    public void DemoBlockFilterExists_WhenTrue_ReturnsTrue()
    {
        var engine = new MockWfpEngine { DemoBlockFilterExistsValue = true };
        var result = engine.DemoBlockFilterExists();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public void DemoBlockFilterExists_WhenFalse_ReturnsFalse()
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
    public void RemoveAllFilters_RemovesDemoBlockFilter()
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
    public void RemoveAllFilters_WhenNoFilters_IsIdempotent()
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
    public void FullLifecycle_EnableThenDisable_Works()
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
    public void FullLifecycle_EnableThenRollback_Works()
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
    public void DemoBlockEnableRequest_HasCorrectType()
    {
        var request = new DemoBlockEnableRequest();
        Assert.Equal("demo-block-enable", request.Type);
    }

    [Fact]
    public void DemoBlockDisableRequest_HasCorrectType()
    {
        var request = new DemoBlockDisableRequest();
        Assert.Equal("demo-block-disable", request.Type);
    }

    [Fact]
    public void DemoBlockStatusRequest_HasCorrectType()
    {
        var request = new DemoBlockStatusRequest();
        Assert.Equal("demo-block-status", request.Type);
    }

    [Fact]
    public void RollbackRequest_HasCorrectType()
    {
        var request = new RollbackRequest();
        Assert.Equal("rollback", request.Type);
    }

    [Fact]
    public void ParseRequest_DemoBlockEnableRequest_ParsesCorrectly()
    {
        var json = "{\"type\":\"demo-block-enable\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<DemoBlockEnableRequest>(result.Value);
    }

    [Fact]
    public void ParseRequest_DemoBlockDisableRequest_ParsesCorrectly()
    {
        var json = "{\"type\":\"demo-block-disable\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<DemoBlockDisableRequest>(result.Value);
    }

    [Fact]
    public void ParseRequest_DemoBlockStatusRequest_ParsesCorrectly()
    {
        var json = "{\"type\":\"demo-block-status\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<DemoBlockStatusRequest>(result.Value);
    }

    [Fact]
    public void ParseRequest_RollbackRequest_ParsesCorrectly()
    {
        var json = "{\"type\":\"rollback\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<RollbackRequest>(result.Value);
    }

    [Fact]
    public void DemoBlockEnableResponse_Success_SetsProperties()
    {
        var response = DemoBlockEnableResponse.Success(filterEnabled: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.FilterEnabled);
    }

    [Fact]
    public void DemoBlockEnableResponse_Failure_SetsError()
    {
        var response = DemoBlockEnableResponse.Failure("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void DemoBlockDisableResponse_Success_SetsProperties()
    {
        var response = DemoBlockDisableResponse.Success(filterDisabled: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.FilterDisabled);
    }

    [Fact]
    public void DemoBlockStatusResponse_Success_WhenActive_IncludesBlockedTarget()
    {
        var response = DemoBlockStatusResponse.Success(filterActive: true);

        Assert.True(response.Ok);
        Assert.True(response.FilterActive);
        Assert.NotNull(response.BlockedTarget);
        Assert.Contains("1.1.1.1", response.BlockedTarget);
    }

    [Fact]
    public void DemoBlockStatusResponse_Success_WhenInactive_NoBlockedTarget()
    {
        var response = DemoBlockStatusResponse.Success(filterActive: false);

        Assert.True(response.Ok);
        Assert.False(response.FilterActive);
        Assert.Null(response.BlockedTarget);
    }

    [Fact]
    public void RollbackResponse_Success_SetsProperties()
    {
        var response = RollbackResponse.Success(filtersRemoved: true);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.True(response.FiltersRemoved);
    }

    [Fact]
    public void RollbackResponse_Failure_SetsError()
    {
        var response = RollbackResponse.Failure("Rollback failed");

        Assert.False(response.Ok);
        Assert.Equal("Rollback failed", response.Error);
    }

    [Fact]
    public void SerializeResponse_DemoBlockEnableResponse_ProducesValidJson()
    {
        var response = DemoBlockEnableResponse.Success(filterEnabled: true);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filterEnabled\":true", json);
    }

    [Fact]
    public void SerializeResponse_DemoBlockStatusResponse_ProducesValidJson()
    {
        var response = DemoBlockStatusResponse.Success(filterActive: true);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filterActive\":true", json);
        Assert.Contains("\"blockedTarget\":", json);
    }

    [Fact]
    public void SerializeResponse_RollbackResponse_ProducesValidJson()
    {
        var response = RollbackResponse.Success(filtersRemoved: true);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filtersRemoved\":true", json);
    }
}

/// <summary>
/// Tests for demo block filter constants.
/// </summary>
public class DemoBlockConstantsTests
{
    [Fact]
    public void DemoBlockFilterGuid_IsValid()
    {
        Assert.NotEqual(Guid.Empty, WfpConstants.DemoBlockFilterGuid);
    }

    [Fact]
    public void DemoBlockFilterGuid_IsDifferentFromOtherGuids()
    {
        Assert.NotEqual(WfpConstants.ProviderGuid, WfpConstants.DemoBlockFilterGuid);
        Assert.NotEqual(WfpConstants.SublayerGuid, WfpConstants.DemoBlockFilterGuid);
    }

    [Fact]
    public void DemoBlockFilterName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(WfpConstants.DemoBlockFilterName));
    }

    [Fact]
    public void DemoBlockRemoteIp_Is_1_1_1_1()
    {
        // 1.1.1.1 in host byte order: 0x01010101
        Assert.Equal(0x01010101u, WfpConstants.DemoBlockRemoteIp);
    }

    [Fact]
    public void DemoBlockRemotePort_Is443()
    {
        Assert.Equal((ushort)443, WfpConstants.DemoBlockRemotePort);
    }

    [Fact]
    public void ProtocolTcp_Is6()
    {
        Assert.Equal((byte)6, WfpConstants.ProtocolTcp);
    }
}
