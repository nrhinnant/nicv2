using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for AnalyticsDashboardViewModel and AnalyticsService.
/// </summary>
public class AnalyticsDashboardTests
{
    #region AnalyticsService Tests

    [Fact]
    public void AnalyticsService_InitialState_IsEmpty()
    {
        // Arrange
        var service = new AnalyticsService();

        // Assert
        Assert.Equal(0, service.SnapshotCount);
        Assert.Empty(service.GetTopProcesses());
        Assert.Empty(service.GetStateDistribution());
        Assert.Empty(service.GetConnectionsOverTime(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void AnalyticsService_RecordSnapshot_IncrementsCount()
    {
        // Arrange
        var service = new AnalyticsService();
        var connections = CreateSampleConnections();

        // Act
        service.RecordSnapshot(connections);

        // Assert
        Assert.Equal(1, service.SnapshotCount);
    }

    [Fact]
    public void AnalyticsService_RecordMultipleSnapshots_TracksAll()
    {
        // Arrange
        var service = new AnalyticsService();
        var connections = CreateSampleConnections();

        // Act
        for (int i = 0; i < 5; i++)
        {
            service.RecordSnapshot(connections);
        }

        // Assert
        Assert.Equal(5, service.SnapshotCount);
    }

    [Fact]
    public void AnalyticsService_GetSummary_ReturnsCorrectValues()
    {
        // Arrange
        var service = new AnalyticsService();
        var connections = new List<ConnectionDto>
        {
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "LISTEN", ProcessName = "nginx.exe" },
            new() { Protocol = "udp", State = "*", ProcessName = "dns.exe" }
        };

        // Act
        service.RecordSnapshot(connections);
        var summary = service.GetSummary();

        // Assert
        Assert.Equal(4, summary.TotalConnections);
        Assert.Equal(3, summary.TcpConnections);
        Assert.Equal(1, summary.UdpConnections);
        Assert.Equal(2, summary.EstablishedConnections);
        Assert.Equal(1, summary.ListeningPorts);
        Assert.Equal(3, summary.UniqueProcesses);
        Assert.Equal("chrome.exe", summary.TopProcess);
        Assert.Equal(2, summary.TopProcessCount);
    }

    [Fact]
    public void AnalyticsService_GetTopProcesses_ReturnsCorrectOrder()
    {
        // Arrange
        var service = new AnalyticsService();
        var connections = new List<ConnectionDto>
        {
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "LISTEN", ProcessName = "nginx.exe" },
            new() { Protocol = "tcp", State = "LISTEN", ProcessName = "nginx.exe" },
            new() { Protocol = "udp", State = "*", ProcessName = "dns.exe" }
        };

        // Act
        service.RecordSnapshot(connections);
        var topProcesses = service.GetTopProcesses(3);

        // Assert
        Assert.Equal(3, topProcesses.Count);
        Assert.Equal("chrome.exe", topProcesses[0].ProcessName);
        Assert.Equal(3, topProcesses[0].ConnectionCount);
        Assert.Equal("nginx.exe", topProcesses[1].ProcessName);
        Assert.Equal(2, topProcesses[1].ConnectionCount);
        Assert.Equal("dns.exe", topProcesses[2].ProcessName);
        Assert.Equal(1, topProcesses[2].ConnectionCount);
    }

    [Fact]
    public void AnalyticsService_GetTopProcesses_CalculatesPercentages()
    {
        // Arrange
        var service = new AnalyticsService();
        var connections = new List<ConnectionDto>
        {
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "firefox.exe" },
            new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "firefox.exe" }
        };

        // Act
        service.RecordSnapshot(connections);
        var topProcesses = service.GetTopProcesses();

        // Assert
        Assert.Equal(2, topProcesses.Count);
        Assert.Equal(50.0, topProcesses[0].Percentage);
        Assert.Equal(50.0, topProcesses[1].Percentage);
    }

    [Fact]
    public void AnalyticsService_GetStateDistribution_ReturnsCorrectCounts()
    {
        // Arrange
        var service = new AnalyticsService();
        var connections = new List<ConnectionDto>
        {
            new() { Protocol = "tcp", State = "ESTABLISHED" },
            new() { Protocol = "tcp", State = "ESTABLISHED" },
            new() { Protocol = "tcp", State = "LISTEN" },
            new() { Protocol = "tcp", State = "TIME_WAIT" },
            new() { Protocol = "udp", State = "*" }
        };

        // Act
        service.RecordSnapshot(connections);
        var distribution = service.GetStateDistribution();

        // Assert
        Assert.Equal(4, distribution.Count);
        Assert.Contains(distribution, s => s.State == "ESTABLISHED" && s.Count == 2);
        Assert.Contains(distribution, s => s.State == "LISTEN" && s.Count == 1);
        Assert.Contains(distribution, s => s.State == "TIME_WAIT" && s.Count == 1);
        Assert.Contains(distribution, s => s.State == "*" && s.Count == 1);
    }

    [Fact]
    public void AnalyticsService_Clear_RemovesAllData()
    {
        // Arrange
        var service = new AnalyticsService();
        service.RecordSnapshot(CreateSampleConnections());

        // Act
        service.Clear();

        // Assert
        Assert.Equal(0, service.SnapshotCount);
        Assert.Empty(service.GetTopProcesses());
    }

    [Fact]
    public void AnalyticsService_MaxSnapshots_PrunesOldData()
    {
        // Arrange
        var service = new AnalyticsService(maxSnapshots: 3);
        var connections = CreateSampleConnections();

        // Act - Add more than max
        for (int i = 0; i < 5; i++)
        {
            service.RecordSnapshot(connections);
        }

        // Assert
        Assert.Equal(3, service.SnapshotCount);
    }

    [Fact]
    public void AnalyticsService_GetConnectionsOverTime_ReturnsDataPoints()
    {
        // Arrange
        var service = new AnalyticsService();
        service.RecordSnapshot(CreateSampleConnections(tcpCount: 5, udpCount: 2));

        // Act
        var dataPoints = service.GetConnectionsOverTime(TimeSpan.FromHours(1));

        // Assert
        Assert.NotEmpty(dataPoints);
        Assert.True(dataPoints[0].TcpCount > 0);
    }

    #endregion

    #region AnalyticsDashboardViewModel Tests

    [Fact]
    public void ViewModel_InitialState_HasCorrectDefaults()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var analyticsService = new AnalyticsService();

        // Act
        var vm = new AnalyticsDashboardViewModel(mockService, analyticsService);

        // Assert
        Assert.True(vm.AutoRefresh);
        Assert.Equal("1 hour", vm.SelectedTimeRange);
        Assert.Equal(0, vm.TotalConnections);
        Assert.Equal("None", vm.TopProcess);
        Assert.Equal(0, vm.SnapshotCount);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task ViewModel_CollectData_UpdatesStats()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
                new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
                new() { Protocol = "udp", State = "*", ProcessName = "dns.exe" }
            }
        };
        var analyticsService = new AnalyticsService();
        var vm = new AnalyticsDashboardViewModel(mockService, analyticsService);

        // Act
        await vm.CollectDataCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(3, vm.TotalConnections);
        Assert.Equal(2, vm.TcpConnections);
        Assert.Equal(1, vm.UdpConnections);
        Assert.Equal(1, vm.SnapshotCount);
    }

    [Fact]
    public async Task ViewModel_CollectData_UpdatesTopProcess()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = new List<ConnectionDto>
            {
                new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
                new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
                new() { Protocol = "tcp", State = "ESTABLISHED", ProcessName = "chrome.exe" },
                new() { Protocol = "tcp", State = "LISTEN", ProcessName = "nginx.exe" }
            }
        };
        var analyticsService = new AnalyticsService();
        var vm = new AnalyticsDashboardViewModel(mockService, analyticsService);

        // Act
        await vm.CollectDataCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal("chrome.exe", vm.TopProcess);
        Assert.Equal(3, vm.TopProcessCount);
    }

    [Fact]
    public async Task ViewModel_CollectData_ServiceUnavailable_ShowsError()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            ShouldConnect = false
        };
        var analyticsService = new AnalyticsService();
        var vm = new AnalyticsDashboardViewModel(mockService, analyticsService);

        // Act
        await vm.CollectDataCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Error", vm.StatusMessage);
    }

    [Fact]
    public void ViewModel_ClearData_ResetsStats()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var analyticsService = new AnalyticsService();
        analyticsService.RecordSnapshot(CreateSampleConnections());
        var vm = new AnalyticsDashboardViewModel(mockService, analyticsService);

        // Act
        vm.ClearDataCommand.Execute(null);

        // Assert
        Assert.Equal(0, vm.SnapshotCount);
    }

    [Fact]
    public void ViewModel_AvailableTimeRanges_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains("15 minutes", AnalyticsDashboardViewModel.AvailableTimeRanges);
        Assert.Contains("1 hour", AnalyticsDashboardViewModel.AvailableTimeRanges);
        Assert.Contains("6 hours", AnalyticsDashboardViewModel.AvailableTimeRanges);
        Assert.Contains("24 hours", AnalyticsDashboardViewModel.AvailableTimeRanges);
    }

    [Fact]
    public async Task ViewModel_MultipleCollects_AccumulatesSnapshots()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            Connections = CreateSampleConnections()
        };
        var analyticsService = new AnalyticsService();
        var vm = new AnalyticsDashboardViewModel(mockService, analyticsService);

        // Act
        await vm.CollectDataCommand.ExecuteAsync(null);
        await vm.CollectDataCommand.ExecuteAsync(null);
        await vm.CollectDataCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(3, vm.SnapshotCount);
    }

    #endregion

    #region Helper Methods

    private static List<ConnectionDto> CreateSampleConnections(int tcpCount = 3, int udpCount = 1)
    {
        var connections = new List<ConnectionDto>();

        for (int i = 0; i < tcpCount; i++)
        {
            connections.Add(new ConnectionDto
            {
                Protocol = "tcp",
                State = i == 0 ? "LISTEN" : "ESTABLISHED",
                LocalIp = "127.0.0.1",
                LocalPort = 8000 + i,
                RemoteIp = "0.0.0.0",
                RemotePort = 0,
                ProcessName = "test.exe",
                ProcessId = 1000 + i
            });
        }

        for (int i = 0; i < udpCount; i++)
        {
            connections.Add(new ConnectionDto
            {
                Protocol = "udp",
                State = "*",
                LocalIp = "0.0.0.0",
                LocalPort = 53 + i,
                RemoteIp = "*",
                RemotePort = 0,
                ProcessName = "dns.exe",
                ProcessId = 2000 + i
            });
        }

        return connections;
    }

    #endregion
}
