using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Tests for the panic rollback functionality (RemoveAllFilters).
/// Uses a mock IWfpEngine to verify the enumeration + delete flow logic.
/// </summary>
public class PanicRollbackTests
{
    // ========================================
    // MockWfpEngine for rollback testing
    // ========================================

    private class MockWfpEngine : IWfpEngine
    {
        public bool ProviderExistsValue { get; set; } = true;
        public bool SublayerExistsValue { get; set; } = true;
        public bool DemoBlockFilterExistsValue { get; set; }
        public int FilterCount { get; set; }
        public Result EnsureResult { get; set; } = Result.Success();
        public Result RemoveResult { get; set; } = Result.Success();
        public Result AddDemoBlockResult { get; set; } = Result.Success();
        public Result RemoveDemoBlockResult { get; set; } = Result.Success();
        public Result<int>? RemoveAllFiltersResult { get; set; }
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
            if (AddDemoBlockResult.IsSuccess)
            {
                DemoBlockFilterExistsValue = true;
                FilterCount++;
            }
            return AddDemoBlockResult;
        }

        public Result RemoveDemoBlockFilter()
        {
            if (RemoveDemoBlockResult.IsSuccess)
            {
                if (DemoBlockFilterExistsValue)
                {
                    DemoBlockFilterExistsValue = false;
                    FilterCount--;
                }
            }
            return RemoveDemoBlockResult;
        }

        public Result<bool> DemoBlockFilterExists() => Result<bool>.Success(DemoBlockFilterExistsValue);

        public Result<int> RemoveAllFilters()
        {
            RemoveAllFiltersCallCount++;

            // If a specific result is set, use it
            if (RemoveAllFiltersResult.HasValue)
            {
                if (RemoveAllFiltersResult.Value.IsSuccess)
                {
                    DemoBlockFilterExistsValue = false;
                    FilterCount = 0;
                }
                return RemoveAllFiltersResult.Value;
            }

            // Default behavior: remove all filters and return count
            var removedCount = FilterCount;
            FilterCount = 0;
            DemoBlockFilterExistsValue = false;
            return Result<int>.Success(removedCount);
        }

        public Result<ApplyResult> ApplyFilters(List<CompiledFilter> filters)
        {
            return Result<ApplyResult>.Success(new ApplyResult { FiltersCreated = filters?.Count ?? 0, FiltersRemoved = 0 });
        }
    }

    // ========================================
    // RemoveAllFilters Tests
    // ========================================

    [Fact]
    public void RemoveAllFilters_WhenNoFilters_ReturnsZero()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 0,
            DemoBlockFilterExistsValue = false
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Equal(1, engine.RemoveAllFiltersCallCount);
    }

    [Fact]
    public void RemoveAllFilters_WithOneFilter_ReturnsOne()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 1,
            DemoBlockFilterExistsValue = true
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.False(engine.DemoBlockFilterExistsValue);
        Assert.Equal(0, engine.FilterCount);
    }

    [Fact]
    public void RemoveAllFilters_WithMultipleFilters_ReturnsCorrectCount()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 5,
            DemoBlockFilterExistsValue = true
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
        Assert.False(engine.DemoBlockFilterExistsValue);
        Assert.Equal(0, engine.FilterCount);
    }

    [Fact]
    public void RemoveAllFilters_OnEnumerationFailure_ReturnsError()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 2,
            RemoveAllFiltersResult = Result<int>.Failure(ErrorCodes.WfpError, "Failed to enumerate filters")
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("enumerate", result.Error.Message);
    }

    [Fact]
    public void RemoveAllFilters_OnDeletionFailure_ReturnsError()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 2,
            RemoveAllFiltersResult = Result<int>.Failure(ErrorCodes.WfpError, "Failed to delete filter 12345")
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("delete", result.Error.Message);
    }

    [Fact]
    public void RemoveAllFilters_MultipleCalls_IsIdempotent()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 3,
            DemoBlockFilterExistsValue = true
        };

        // Act - First call removes filters
        var result1 = engine.RemoveAllFilters();
        Assert.True(result1.IsSuccess);
        Assert.Equal(3, result1.Value);

        // Act - Second call finds no filters
        var result2 = engine.RemoveAllFilters();
        Assert.True(result2.IsSuccess);
        Assert.Equal(0, result2.Value);

        // Assert
        Assert.Equal(2, engine.RemoveAllFiltersCallCount);
        Assert.Equal(0, engine.FilterCount);
    }

    [Fact]
    public void RemoveAllFilters_PreservesSublayerAndProvider()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            ProviderExistsValue = true,
            SublayerExistsValue = true,
            FilterCount = 2
        };

        // Act
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(engine.ProviderExistsValue);
        Assert.True(engine.SublayerExistsValue);
    }

    // ========================================
    // Lifecycle Tests
    // ========================================

    [Fact]
    public void FullLifecycle_AddFiltersThenRollback_RemovesAll()
    {
        // Arrange
        var engine = new MockWfpEngine();

        // Act - Bootstrap
        engine.EnsureProviderAndSublayerExist();

        // Act - Add filter
        engine.AddDemoBlockFilter();
        Assert.True(engine.DemoBlockFilterExistsValue);
        Assert.Equal(1, engine.FilterCount);

        // Act - Rollback
        var result = engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.False(engine.DemoBlockFilterExistsValue);
        Assert.Equal(0, engine.FilterCount);
        Assert.True(engine.ProviderExistsValue);
        Assert.True(engine.SublayerExistsValue);
    }

    [Fact]
    public void FullLifecycle_RollbackThenTeardown_CleansEverything()
    {
        // Arrange
        var engine = new MockWfpEngine
        {
            FilterCount = 2,
            DemoBlockFilterExistsValue = true
        };

        // Act - Rollback first (removes filters)
        var rollbackResult = engine.RemoveAllFilters();
        Assert.True(rollbackResult.IsSuccess);
        Assert.Equal(2, rollbackResult.Value);

        // Act - Teardown (removes provider/sublayer)
        var teardownResult = engine.RemoveProviderAndSublayer();
        Assert.True(teardownResult.IsSuccess);

        // Assert - Everything is gone
        Assert.False(engine.DemoBlockFilterExistsValue);
        Assert.Equal(0, engine.FilterCount);
        Assert.False(engine.ProviderExistsValue);
        Assert.False(engine.SublayerExistsValue);
    }
}

/// <summary>
/// Tests for Rollback IPC messages with the new filter count.
/// </summary>
public class RollbackIpcMessageTests
{
    [Fact]
    public void RollbackResponse_Success_WithZeroFilters()
    {
        var response = RollbackResponse.Success(filtersRemoved: 0);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.Equal(0, response.FiltersRemoved);
    }

    [Fact]
    public void RollbackResponse_Success_WithMultipleFilters()
    {
        var response = RollbackResponse.Success(filtersRemoved: 10);

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.Equal(10, response.FiltersRemoved);
    }

    [Fact]
    public void RollbackResponse_Failure_PreservesErrorMessage()
    {
        var response = RollbackResponse.Failure("Failed to enumerate filters in sublayer");

        Assert.False(response.Ok);
        Assert.Equal("Failed to enumerate filters in sublayer", response.Error);
        Assert.Equal(0, response.FiltersRemoved);
    }

    [Fact]
    public void SerializeResponse_RollbackResponse_WithZero_ProducesValidJson()
    {
        var response = RollbackResponse.Success(filtersRemoved: 0);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filtersRemoved\":0", json);
    }

    [Fact]
    public void SerializeResponse_RollbackResponse_WithCount_ProducesValidJson()
    {
        var response = RollbackResponse.Success(filtersRemoved: 42);
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filtersRemoved\":42", json);
    }

    [Fact]
    public void ParseRequest_RollbackRequest_ParsesCorrectly()
    {
        var json = "{\"type\":\"rollback\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<RollbackRequest>(result.Value);
        Assert.Equal("rollback", result.Value.Type);
    }
}

/// <summary>
/// Tests for IWfpEngine.RemoveAllFilters interface contract.
/// </summary>
public class RemoveAllFiltersInterfaceTests
{
    [Fact]
    public void IWfpEngine_RemoveAllFilters_ReturnsResultInt()
    {
        var method = typeof(IWfpEngine).GetMethod("RemoveAllFilters");
        Assert.NotNull(method);
        Assert.Equal(typeof(Result<int>), method!.ReturnType);
    }

    [Fact]
    public void IWfpEngine_RemoveAllFilters_TakesNoParameters()
    {
        var method = typeof(IWfpEngine).GetMethod("RemoveAllFilters");
        Assert.NotNull(method);
        Assert.Empty(method!.GetParameters());
    }
}
