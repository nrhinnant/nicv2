using WfpTrafficControl.UI.Services;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for TrayIconService.
/// Note: These tests focus on state logic. Full integration testing
/// with actual tray icons requires a running Windows GUI environment.
/// </summary>
public sealed class TrayIconServiceTests : IDisposable
{
    private readonly TrayIconService _service;

    public TrayIconServiceTests()
    {
        _service = new TrayIconService();
    }

    public void Dispose()
    {
        _service.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void InitialState_IsDisconnected()
    {
        // Assert
        Assert.Equal(TrayIconState.Disconnected, _service.CurrentState);
        Assert.False(_service.IsVisible);
    }

    [Fact]
    public void Initialize_SetsIconVisible()
    {
        // Act
        _service.Initialize();

        // Assert
        Assert.True(_service.IsVisible);
        Assert.Equal(TrayIconState.Disconnected, _service.CurrentState);
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotThrow()
    {
        // Act - calling Initialize twice should be safe
        _service.Initialize();
        _service.Initialize();

        // Assert
        Assert.True(_service.IsVisible);
    }

    [Fact]
    public void UpdateState_WhenDisconnected_SetsDisconnectedState()
    {
        // Arrange
        _service.Initialize();

        // Act
        _service.UpdateState(isConnected: false, filterCount: 0, serviceVersion: "1.0.0");

        // Assert
        Assert.Equal(TrayIconState.Disconnected, _service.CurrentState);
    }

    [Fact]
    public void UpdateState_WhenConnectedNoFilters_SetsNoPolicyState()
    {
        // Arrange
        _service.Initialize();

        // Act
        _service.UpdateState(isConnected: true, filterCount: 0, serviceVersion: "1.0.0");

        // Assert
        Assert.Equal(TrayIconState.NoPolicy, _service.CurrentState);
    }

    [Fact]
    public void UpdateState_WhenConnectedWithFilters_SetsActiveState()
    {
        // Arrange
        _service.Initialize();

        // Act
        _service.UpdateState(isConnected: true, filterCount: 5, serviceVersion: "1.0.0");

        // Assert
        Assert.Equal(TrayIconState.Active, _service.CurrentState);
    }

    [Fact]
    public void UpdateState_StateTransitions_UpdateCorrectly()
    {
        // Arrange
        _service.Initialize();

        // Act & Assert - transition through all states
        _service.UpdateState(isConnected: true, filterCount: 10, serviceVersion: "1.0.0");
        Assert.Equal(TrayIconState.Active, _service.CurrentState);

        _service.UpdateState(isConnected: true, filterCount: 0, serviceVersion: "1.0.0");
        Assert.Equal(TrayIconState.NoPolicy, _service.CurrentState);

        _service.UpdateState(isConnected: false, filterCount: 0, serviceVersion: "1.0.0");
        Assert.Equal(TrayIconState.Disconnected, _service.CurrentState);

        _service.UpdateState(isConnected: true, filterCount: 3, serviceVersion: "1.0.0");
        Assert.Equal(TrayIconState.Active, _service.CurrentState);
    }

    [Fact]
    public void UpdateState_BeforeInitialize_DoesNotThrow()
    {
        // Act - should not throw even if not initialized
        _service.UpdateState(isConnected: true, filterCount: 5, serviceVersion: "1.0.0");

        // Assert - state should still be tracked
        Assert.Equal(TrayIconState.Disconnected, _service.CurrentState); // Not updated because not initialized
    }

    [Fact]
    public void Hide_SetsIconNotVisible()
    {
        // Arrange
        _service.Initialize();
        Assert.True(_service.IsVisible);

        // Act
        _service.Hide();

        // Assert
        Assert.False(_service.IsVisible);
    }

    [Fact]
    public void Dispose_HidesIcon()
    {
        // Arrange
        _service.Initialize();
        Assert.True(_service.IsVisible);

        // Act
        _service.Dispose();

        // Assert
        Assert.False(_service.IsVisible);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        _service.Initialize();

        // Act - double dispose should be safe
        _service.Dispose();
        _service.Dispose();

        // Assert - no exception
        Assert.False(_service.IsVisible);
    }

    [Fact]
    public void ShowWindowRequested_EventCanBeSubscribed()
    {
        // Arrange
        var eventRaised = false;
        _service.ShowWindowRequested += (_, _) => eventRaised = true;
        _service.Initialize();

        // Assert - just verify subscription doesn't throw
        Assert.False(eventRaised);
    }

    [Fact]
    public void ExitRequested_EventCanBeSubscribed()
    {
        // Arrange
        var eventRaised = false;
        _service.ExitRequested += (_, _) => eventRaised = true;
        _service.Initialize();

        // Assert - just verify subscription doesn't throw
        Assert.False(eventRaised);
    }

    [Fact]
    public void RefreshRequested_EventCanBeSubscribed()
    {
        // Arrange
        var eventRaised = false;
        _service.RefreshRequested += (_, _) => eventRaised = true;
        _service.Initialize();

        // Assert - just verify subscription doesn't throw
        Assert.False(eventRaised);
    }

    [Fact]
    public void ShowNotification_BeforeInitialize_DoesNotThrow()
    {
        // Act - should not throw even if not initialized
        _service.ShowNotification("Test", "Test message");

        // Assert - no exception means success
        Assert.True(true);
    }

    [Fact]
    public void ShowNotification_AfterInitialize_DoesNotThrow()
    {
        // Arrange
        _service.Initialize();

        // Act
        _service.ShowNotification("Test", "Test message", isError: false);
        _service.ShowNotification("Error", "Error message", isError: true);

        // Assert - no exception means success
        Assert.True(true);
    }
}

/// <summary>
/// Tests for TrayIconState enum values.
/// </summary>
public class TrayIconStateTests
{
    [Theory]
    [InlineData(TrayIconState.Disconnected)]
    [InlineData(TrayIconState.NoPolicy)]
    [InlineData(TrayIconState.Active)]
    [InlineData(TrayIconState.Error)]
    public void TrayIconState_HasExpectedValues(TrayIconState state)
    {
        // Assert - verify all expected enum values exist
        Assert.True(Enum.IsDefined(typeof(TrayIconState), state));
    }
}
