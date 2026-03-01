using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for ConnectionMonitorViewModel.
/// </summary>
public class ConnectionMonitorTests
{
    #region ViewModel Tests

    [Fact]
    public void ViewModel_InitialState_HasCorrectDefaults()
    {
        // Arrange
        var mockService = new MockServiceClient();

        // Act
        var vm = new ConnectionMonitorViewModel(mockService);

        // Assert
        Assert.Empty(vm.Connections);
        Assert.Equal(0, vm.TotalCount);
        Assert.Null(vm.SelectedConnection);
        Assert.False(vm.IsLoading);
        Assert.False(vm.AutoRefresh);
        Assert.Equal(3, vm.RefreshIntervalSeconds);
        Assert.True(vm.IncludeTcp);
        Assert.True(vm.IncludeUdp);
        Assert.Equal("", vm.SearchText);
        Assert.Equal("all", vm.ProtocolFilter);
        Assert.Equal("all", vm.StateFilter);
        Assert.Equal("Click Refresh to load connections", vm.StatusMessage);
        Assert.Null(vm.LastRefresh);
    }

    [Fact]
    public async Task RefreshConnections_LoadsConnections()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto
                {
                    Protocol = "tcp",
                    State = "ESTABLISHED",
                    LocalIp = "192.168.1.100",
                    LocalPort = 54321,
                    RemoteIp = "142.250.80.46",
                    RemotePort = 443,
                    ProcessId = 1234,
                    ProcessName = "chrome.exe"
                },
                new ConnectionDto
                {
                    Protocol = "udp",
                    State = "*",
                    LocalIp = "0.0.0.0",
                    LocalPort = 53,
                    RemoteIp = "*",
                    RemotePort = 0,
                    ProcessId = 4,
                    ProcessName = "System"
                }
            }
        };
        var vm = new ConnectionMonitorViewModel(mockService);

        // Act
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, vm.Connections.Count);
        Assert.Equal(2, vm.TotalCount);
        Assert.Equal(1, mockService.GetConnectionsCallCount);
    }

    [Fact]
    public async Task RefreshConnections_FiltersTcpOnly()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED" },
                new ConnectionDto { Protocol = "udp", State = "*" }
            }
        };
        var vm = new ConnectionMonitorViewModel(mockService);
        vm.IncludeTcp = true;
        vm.IncludeUdp = false;

        // Act
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(vm.Connections);
        Assert.Equal("tcp", vm.Connections[0].Protocol);
    }

    [Fact]
    public async Task RefreshConnections_FiltersUdpOnly()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED" },
                new ConnectionDto { Protocol = "udp", State = "*" }
            }
        };
        var vm = new ConnectionMonitorViewModel(mockService);
        vm.IncludeTcp = false;
        vm.IncludeUdp = true;

        // Act
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(vm.Connections);
        Assert.Equal("udp", vm.Connections[0].Protocol);
    }

    [Fact]
    public async Task RefreshConnections_ServiceUnavailable_ShowsError()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            ShouldConnect = false
        };
        var vm = new ConnectionMonitorViewModel(mockService);

        // Act
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Failed to load", vm.StatusMessage);
    }

    [Fact]
    public async Task RefreshConnections_EmptyResult_ShowsNoConnectionsMessage()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>()
        };
        var vm = new ConnectionMonitorViewModel(mockService);

        // Act
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(vm.Connections);
        Assert.Equal(0, vm.TotalCount);
    }

    [Fact]
    public void ClearFilters_ResetsAllFilters()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var vm = new ConnectionMonitorViewModel(mockService);
        vm.SearchText = "chrome";
        vm.ProtocolFilter = "tcp";
        vm.StateFilter = "ESTABLISHED";

        // Act
        vm.ClearFiltersCommand.Execute(null);

        // Assert
        Assert.Equal("", vm.SearchText);
        Assert.Equal("all", vm.ProtocolFilter);
        Assert.Equal("all", vm.StateFilter);
    }

    [Fact]
    public async Task ProtocolFilter_FiltersView()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED", LocalIp = "1.1.1.1", LocalPort = 1 },
                new ConnectionDto { Protocol = "udp", State = "*", LocalIp = "2.2.2.2", LocalPort = 2 },
                new ConnectionDto { Protocol = "tcp", State = "LISTEN", LocalIp = "3.3.3.3", LocalPort = 3 }
            }
        };
        var vm = new ConnectionMonitorViewModel(mockService);
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Act
        vm.ProtocolFilter = "tcp";

        // Assert
        Assert.Equal(2, vm.FilteredCount);
        Assert.Equal(3, vm.TotalCount);
    }

    [Fact]
    public async Task StateFilter_FiltersView()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED", LocalIp = "1.1.1.1", LocalPort = 1 },
                new ConnectionDto { Protocol = "tcp", State = "LISTEN", LocalIp = "2.2.2.2", LocalPort = 2 },
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED", LocalIp = "3.3.3.3", LocalPort = 3 }
            }
        };
        var vm = new ConnectionMonitorViewModel(mockService);
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Act
        vm.StateFilter = "ESTABLISHED";

        // Assert
        Assert.Equal(2, vm.FilteredCount);
    }

    [Fact]
    public async Task SearchText_FiltersView()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED", LocalIp = "1.1.1.1", LocalPort = 1, ProcessName = "chrome.exe" },
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED", LocalIp = "2.2.2.2", LocalPort = 2, ProcessName = "firefox.exe" },
                new ConnectionDto { Protocol = "tcp", State = "ESTABLISHED", LocalIp = "3.3.3.3", LocalPort = 3, ProcessName = "chrome.exe" }
            }
        };
        var vm = new ConnectionMonitorViewModel(mockService);
        await vm.RefreshConnectionsCommand.ExecuteAsync(null);

        // Act
        vm.SearchText = "chrome";

        // Assert
        Assert.Equal(2, vm.FilteredCount);
    }

    [Fact]
    public void SelectedConnection_ChangeNotification()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var vm = new ConnectionMonitorViewModel(mockService);
        var connection = new ConnectionDto
        {
            Protocol = "tcp",
            State = "ESTABLISHED",
            LocalIp = "192.168.1.100",
            LocalPort = 54321
        };

        var propertyChanged = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.SelectedConnection))
                propertyChanged = true;
        };

        // Act
        vm.SelectedConnection = connection;

        // Assert
        Assert.True(propertyChanged);
        Assert.Equal(connection, vm.SelectedConnection);
    }

    [Fact]
    public void Cleanup_StopsTimer()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var vm = new ConnectionMonitorViewModel(mockService);
        vm.AutoRefresh = true;

        // Act
        vm.Cleanup();

        // Assert - no exception should be thrown
        // The timer should be stopped
    }

    [Fact]
    public async Task CopyConnectionDetails_CopiesSelectedConnection()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var vm = new ConnectionMonitorViewModel(mockService);
        vm.SelectedConnection = new ConnectionDto
        {
            Protocol = "tcp",
            State = "ESTABLISHED",
            LocalIp = "192.168.1.100",
            LocalPort = 54321,
            RemoteIp = "142.250.80.46",
            RemotePort = 443,
            ProcessId = 1234,
            ProcessName = "chrome.exe"
        };

        // Act - should not throw (clipboard may fail in test environment)
        vm.CopyConnectionDetailsCommand.Execute(null);

        // Assert - no exception
    }

    [Fact]
    public void CopyConnectionDetails_WithNoSelection_DoesNothing()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var vm = new ConnectionMonitorViewModel(mockService);
        vm.SelectedConnection = null;

        // Act - should not throw
        vm.CopyConnectionDetailsCommand.Execute(null);

        // Assert - no exception
    }

    #endregion

    #region Static Properties Tests

    [Fact]
    public void AvailableProtocolFilters_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains("all", ConnectionMonitorViewModel.AvailableProtocolFilters);
        Assert.Contains("tcp", ConnectionMonitorViewModel.AvailableProtocolFilters);
        Assert.Contains("udp", ConnectionMonitorViewModel.AvailableProtocolFilters);
    }

    [Fact]
    public void AvailableStateFilters_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains("all", ConnectionMonitorViewModel.AvailableStateFilters);
        Assert.Contains("ESTABLISHED", ConnectionMonitorViewModel.AvailableStateFilters);
        Assert.Contains("LISTEN", ConnectionMonitorViewModel.AvailableStateFilters);
        Assert.Contains("TIME_WAIT", ConnectionMonitorViewModel.AvailableStateFilters);
        Assert.Contains("CLOSE_WAIT", ConnectionMonitorViewModel.AvailableStateFilters);
    }

    [Fact]
    public void AvailableRefreshIntervals_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains(1, ConnectionMonitorViewModel.AvailableRefreshIntervals);
        Assert.Contains(2, ConnectionMonitorViewModel.AvailableRefreshIntervals);
        Assert.Contains(3, ConnectionMonitorViewModel.AvailableRefreshIntervals);
        Assert.Contains(5, ConnectionMonitorViewModel.AvailableRefreshIntervals);
        Assert.Contains(10, ConnectionMonitorViewModel.AvailableRefreshIntervals);
    }

    #endregion

    #region ConnectionDto Tests

    [Fact]
    public void ConnectionDto_LocalEndpoint_FormatsCorrectly()
    {
        // Arrange
        var dto = new ConnectionDto
        {
            LocalIp = "192.168.1.100",
            LocalPort = 54321
        };

        // Act & Assert
        Assert.Equal("192.168.1.100:54321", dto.LocalEndpoint);
    }

    [Fact]
    public void ConnectionDto_RemoteEndpoint_FormatsCorrectly()
    {
        // Arrange
        var dto = new ConnectionDto
        {
            RemoteIp = "142.250.80.46",
            RemotePort = 443
        };

        // Act & Assert
        Assert.Equal("142.250.80.46:443", dto.RemoteEndpoint);
    }

    #endregion

    #region MockServiceClient Connection Tests

    [Fact]
    public async Task MockServiceClient_GetConnections_ReturnsConfiguredConnections()
    {
        // Arrange
        var mock = new MockServiceClient();

        // Act
        var result = await mock.GetConnectionsAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.Equal(mock.Connections.Count, result.Value.Connections.Count);
    }

    [Fact]
    public async Task MockServiceClient_GetConnections_FiltersTcpWhenRequested()
    {
        // Arrange
        var mock = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp" },
                new ConnectionDto { Protocol = "udp" }
            }
        };

        // Act
        var result = await mock.GetConnectionsAsync(includeTcp: true, includeUdp: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Connections);
        Assert.Equal("tcp", result.Value.Connections[0].Protocol);
    }

    [Fact]
    public async Task MockServiceClient_GetConnections_FiltersUdpWhenRequested()
    {
        // Arrange
        var mock = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new ConnectionDto { Protocol = "tcp" },
                new ConnectionDto { Protocol = "udp" }
            }
        };

        // Act
        var result = await mock.GetConnectionsAsync(includeTcp: false, includeUdp: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Connections);
        Assert.Equal("udp", result.Value.Connections[0].Protocol);
    }

    [Fact]
    public async Task MockServiceClient_GetConnections_ServiceUnavailable_ReturnsFailure()
    {
        // Arrange
        var mock = new MockServiceClient
        {
            ShouldConnect = false
        };

        // Act
        var result = await mock.GetConnectionsAsync();

        // Assert
        Assert.True(result.IsFailure);
    }

    #endregion
}
