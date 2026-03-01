using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Analytics data point for connections over time.
/// </summary>
public class ConnectionDataPoint
{
    public DateTime Timestamp { get; set; }
    public int TcpCount { get; set; }
    public int UdpCount { get; set; }
    public int TotalCount => TcpCount + UdpCount;
}

/// <summary>
/// Analytics data for a process.
/// </summary>
public class ProcessAnalytics
{
    public string ProcessName { get; set; } = "Unknown";
    public int ConnectionCount { get; set; }
    public int TcpCount { get; set; }
    public int UdpCount { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Analytics data for connection states.
/// </summary>
public class StateAnalytics
{
    public string State { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Summary statistics for analytics.
/// </summary>
public class AnalyticsSummary
{
    public int TotalConnections { get; set; }
    public int TcpConnections { get; set; }
    public int UdpConnections { get; set; }
    public int EstablishedConnections { get; set; }
    public int ListeningPorts { get; set; }
    public int UniqueProcesses { get; set; }
    public string TopProcess { get; set; } = "None";
    public int TopProcessCount { get; set; }
}

/// <summary>
/// Analytics snapshot at a point in time.
/// </summary>
public class AnalyticsSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<ConnectionDto> Connections { get; set; } = new();
    public AnalyticsSummary Summary { get; set; } = new();
}

/// <summary>
/// Service for collecting and aggregating connection analytics.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Gets connection data points over a time range.
    /// </summary>
    IReadOnlyList<ConnectionDataPoint> GetConnectionsOverTime(TimeSpan range);

    /// <summary>
    /// Gets top processes by connection count.
    /// </summary>
    IReadOnlyList<ProcessAnalytics> GetTopProcesses(int count = 10);

    /// <summary>
    /// Gets connection state distribution.
    /// </summary>
    IReadOnlyList<StateAnalytics> GetStateDistribution();

    /// <summary>
    /// Gets analytics summary.
    /// </summary>
    AnalyticsSummary GetSummary();

    /// <summary>
    /// Records a new snapshot of connections.
    /// </summary>
    void RecordSnapshot(IEnumerable<ConnectionDto> connections);

    /// <summary>
    /// Clears all analytics data.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the number of stored snapshots.
    /// </summary>
    int SnapshotCount { get; }
}
