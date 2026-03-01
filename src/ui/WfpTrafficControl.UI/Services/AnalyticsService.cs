using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Service for collecting and aggregating connection analytics.
/// Stores snapshots in memory with configurable retention.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly object _lock = new();
    private readonly List<AnalyticsSnapshot> _snapshots = new();
    private readonly int _maxSnapshots;
    private readonly TimeSpan _maxAge;

    /// <summary>
    /// Creates a new analytics service.
    /// </summary>
    /// <param name="maxSnapshots">Maximum number of snapshots to retain.</param>
    /// <param name="maxAge">Maximum age of snapshots to retain.</param>
    public AnalyticsService(int maxSnapshots = 1000, TimeSpan? maxAge = null)
    {
        _maxSnapshots = maxSnapshots;
        _maxAge = maxAge ?? TimeSpan.FromHours(24);
    }

    /// <inheritdoc />
    public int SnapshotCount
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count;
            }
        }
    }

    /// <inheritdoc />
    public void RecordSnapshot(IEnumerable<ConnectionDto> connections)
    {
        var connectionList = connections.ToList();
        var snapshot = new AnalyticsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Connections = connectionList,
            Summary = ComputeSummary(connectionList)
        };

        lock (_lock)
        {
            _snapshots.Add(snapshot);
            PruneOldSnapshots();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ConnectionDataPoint> GetConnectionsOverTime(TimeSpan range)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - range;
            var relevantSnapshots = _snapshots
                .Where(s => s.Timestamp >= cutoff)
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (relevantSnapshots.Count == 0)
                return Array.Empty<ConnectionDataPoint>();

            // Aggregate into time buckets
            var bucketSize = DetermineBucketSize(range);
            var dataPoints = new List<ConnectionDataPoint>();
            var bucketStart = relevantSnapshots[0].Timestamp;
            var currentBucket = new List<AnalyticsSnapshot>();

            foreach (var snapshot in relevantSnapshots)
            {
                if (snapshot.Timestamp >= bucketStart + bucketSize)
                {
                    // Finalize current bucket
                    if (currentBucket.Count > 0)
                    {
                        dataPoints.Add(AggregateBucket(bucketStart, currentBucket));
                    }

                    // Start new bucket
                    bucketStart = snapshot.Timestamp;
                    currentBucket.Clear();
                }
                currentBucket.Add(snapshot);
            }

            // Finalize last bucket
            if (currentBucket.Count > 0)
            {
                dataPoints.Add(AggregateBucket(bucketStart, currentBucket));
            }

            return dataPoints;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ProcessAnalytics> GetTopProcesses(int count = 10)
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
                return Array.Empty<ProcessAnalytics>();

            // Use most recent snapshot
            var latestSnapshot = _snapshots[^1];
            var processGroups = latestSnapshot.Connections
                .GroupBy(c => c.ProcessName ?? "Unknown")
                .Select(g => new ProcessAnalytics
                {
                    ProcessName = g.Key,
                    ConnectionCount = g.Count(),
                    TcpCount = g.Count(c => c.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase)),
                    UdpCount = g.Count(c => c.Protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
                })
                .OrderByDescending(p => p.ConnectionCount)
                .Take(count)
                .ToList();

            var total = latestSnapshot.Connections.Count;
            foreach (var p in processGroups)
            {
                p.Percentage = total > 0 ? (double)p.ConnectionCount / total * 100 : 0;
            }

            return processGroups;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<StateAnalytics> GetStateDistribution()
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
                return Array.Empty<StateAnalytics>();

            // Use most recent snapshot
            var latestSnapshot = _snapshots[^1];
            var stateGroups = latestSnapshot.Connections
                .GroupBy(c => c.State)
                .Select(g => new StateAnalytics
                {
                    State = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(s => s.Count)
                .ToList();

            var total = latestSnapshot.Connections.Count;
            foreach (var s in stateGroups)
            {
                s.Percentage = total > 0 ? (double)s.Count / total * 100 : 0;
            }

            return stateGroups;
        }
    }

    /// <inheritdoc />
    public AnalyticsSummary GetSummary()
    {
        lock (_lock)
        {
            if (_snapshots.Count == 0)
                return new AnalyticsSummary();

            return _snapshots[^1].Summary;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _snapshots.Clear();
        }
    }

    private static AnalyticsSummary ComputeSummary(List<ConnectionDto> connections)
    {
        var summary = new AnalyticsSummary
        {
            TotalConnections = connections.Count,
            TcpConnections = connections.Count(c => c.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase)),
            UdpConnections = connections.Count(c => c.Protocol.Equals("udp", StringComparison.OrdinalIgnoreCase)),
            EstablishedConnections = connections.Count(c => c.State.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase)),
            ListeningPorts = connections.Count(c => c.State.Equals("LISTEN", StringComparison.OrdinalIgnoreCase)),
            UniqueProcesses = connections.Select(c => c.ProcessName ?? "Unknown").Distinct().Count()
        };

        var topProcess = connections
            .GroupBy(c => c.ProcessName ?? "Unknown")
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (topProcess != null)
        {
            summary.TopProcess = topProcess.Key;
            summary.TopProcessCount = topProcess.Count();
        }

        return summary;
    }

    private void PruneOldSnapshots()
    {
        // Remove snapshots exceeding max count
        while (_snapshots.Count > _maxSnapshots)
        {
            _snapshots.RemoveAt(0);
        }

        // Remove snapshots exceeding max age
        var cutoff = DateTime.UtcNow - _maxAge;
        _snapshots.RemoveAll(s => s.Timestamp < cutoff);
    }

    private static TimeSpan DetermineBucketSize(TimeSpan range)
    {
        // Choose bucket size based on range to get ~20-50 data points
        if (range <= TimeSpan.FromMinutes(30))
            return TimeSpan.FromMinutes(1);
        if (range <= TimeSpan.FromHours(1))
            return TimeSpan.FromMinutes(2);
        if (range <= TimeSpan.FromHours(6))
            return TimeSpan.FromMinutes(10);
        if (range <= TimeSpan.FromHours(24))
            return TimeSpan.FromMinutes(30);
        return TimeSpan.FromHours(1);
    }

    private static ConnectionDataPoint AggregateBucket(DateTime timestamp, List<AnalyticsSnapshot> snapshots)
    {
        // Average the values in the bucket
        var avgTcp = (int)snapshots.Average(s => s.Summary.TcpConnections);
        var avgUdp = (int)snapshots.Average(s => s.Summary.UdpConnections);

        return new ConnectionDataPoint
        {
            Timestamp = timestamp,
            TcpCount = avgTcp,
            UdpCount = avgUdp
        };
    }
}
