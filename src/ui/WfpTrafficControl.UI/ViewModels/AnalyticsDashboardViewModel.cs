using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the connection analytics dashboard.
/// </summary>
public partial class AnalyticsDashboardViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IAnalyticsService _analyticsService;
    private readonly DispatcherTimer _refreshTimer;

    /// <summary>
    /// Available time ranges.
    /// </summary>
    public static string[] AvailableTimeRanges => new[] { "15 minutes", "1 hour", "6 hours", "24 hours" };

    /// <summary>
    /// Selected time range.
    /// </summary>
    [ObservableProperty]
    private string _selectedTimeRange = "1 hour";

    /// <summary>
    /// Whether auto-refresh is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _autoRefresh = true;

    /// <summary>
    /// Whether data is loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Collecting data...";

    /// <summary>
    /// Last refresh time.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastRefresh;

    // Summary stats
    [ObservableProperty]
    private int _totalConnections;

    [ObservableProperty]
    private int _tcpConnections;

    [ObservableProperty]
    private int _udpConnections;

    [ObservableProperty]
    private int _establishedConnections;

    [ObservableProperty]
    private int _listeningPorts;

    [ObservableProperty]
    private int _uniqueProcesses;

    [ObservableProperty]
    private string _topProcess = "None";

    [ObservableProperty]
    private int _topProcessCount;

    [ObservableProperty]
    private int _snapshotCount;

    // Chart data
    private readonly ObservableCollection<ObservablePoint> _tcpDataPoints = new();
    private readonly ObservableCollection<ObservablePoint> _udpDataPoints = new();

    /// <summary>
    /// Series for the connections over time chart.
    /// </summary>
    public ISeries[] ConnectionsSeries { get; }

    /// <summary>
    /// X axis for connections chart.
    /// </summary>
    public Axis[] ConnectionsXAxes { get; }

    /// <summary>
    /// Y axis for connections chart.
    /// </summary>
    public Axis[] ConnectionsYAxes { get; }

    /// <summary>
    /// Series for the process distribution pie chart.
    /// </summary>
    [ObservableProperty]
    private ISeries[] _processSeries = Array.Empty<ISeries>();

    /// <summary>
    /// Series for the state distribution pie chart.
    /// </summary>
    [ObservableProperty]
    private ISeries[] _stateSeries = Array.Empty<ISeries>();

    /// <summary>
    /// Top processes data.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ProcessAnalytics> _topProcesses = new();

    /// <summary>
    /// State distribution data.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<StateAnalytics> _stateDistribution = new();

    public AnalyticsDashboardViewModel(IServiceClient serviceClient, IAnalyticsService analyticsService)
    {
        _serviceClient = serviceClient;
        _analyticsService = analyticsService;

        // Setup connections chart series
        ConnectionsSeries = new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Name = "TCP",
                Values = _tcpDataPoints,
                Fill = null,
                GeometrySize = 4,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 }
            },
            new LineSeries<ObservablePoint>
            {
                Name = "UDP",
                Values = _udpDataPoints,
                Fill = null,
                GeometrySize = 4,
                Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 }
            }
        };

        ConnectionsXAxes = new Axis[]
        {
            new Axis
            {
                Name = "Time",
                Labeler = value => DateTime.FromOADate(value).ToString("HH:mm"),
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                NamePaint = new SolidColorPaint(SKColors.Gray)
            }
        };

        ConnectionsYAxes = new Axis[]
        {
            new Axis
            {
                Name = "Connections",
                MinLimit = 0,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                NamePaint = new SolidColorPaint(SKColors.Gray)
            }
        };

        // Setup refresh timer
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += async (_, _) => await CollectDataAsync();
    }

    partial void OnAutoRefreshChanged(bool value)
    {
        if (value)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        UpdateCharts();
    }

    /// <summary>
    /// Collects connection data and updates analytics.
    /// </summary>
    [RelayCommand]
    public async Task CollectDataAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var result = await _serviceClient.GetConnectionsAsync();

            if (result.IsSuccess && result.Value.Ok)
            {
                // Record snapshot to analytics service
                _analyticsService.RecordSnapshot(result.Value.Connections);

                // Update summary
                var summary = _analyticsService.GetSummary();
                TotalConnections = summary.TotalConnections;
                TcpConnections = summary.TcpConnections;
                UdpConnections = summary.UdpConnections;
                EstablishedConnections = summary.EstablishedConnections;
                ListeningPorts = summary.ListeningPorts;
                UniqueProcesses = summary.UniqueProcesses;
                TopProcess = summary.TopProcess;
                TopProcessCount = summary.TopProcessCount;
                SnapshotCount = _analyticsService.SnapshotCount;

                // Update charts
                UpdateCharts();

                LastRefresh = DateTime.Now;
                StatusMessage = $"Data collected: {TotalConnections} connections";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Error: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Clears all analytics data.
    /// </summary>
    [RelayCommand]
    private void ClearData()
    {
        _analyticsService.Clear();
        _tcpDataPoints.Clear();
        _udpDataPoints.Clear();
        TopProcesses.Clear();
        StateDistribution.Clear();
        ProcessSeries = Array.Empty<ISeries>();
        StateSeries = Array.Empty<ISeries>();
        SnapshotCount = 0;
        StatusMessage = "Data cleared";
    }

    private void UpdateCharts()
    {
        var range = GetTimeRangeFromSelection();

        // Update connections over time
        var dataPoints = _analyticsService.GetConnectionsOverTime(range);
        _tcpDataPoints.Clear();
        _udpDataPoints.Clear();

        foreach (var point in dataPoints)
        {
            var x = point.Timestamp.ToOADate();
            _tcpDataPoints.Add(new ObservablePoint(x, point.TcpCount));
            _udpDataPoints.Add(new ObservablePoint(x, point.UdpCount));
        }

        // Update top processes
        var topProcesses = _analyticsService.GetTopProcesses(10);
        TopProcesses.Clear();
        foreach (var p in topProcesses)
        {
            TopProcesses.Add(p);
        }

        // Update process pie chart
        ProcessSeries = topProcesses.Take(5).Select((p, i) => new PieSeries<double>
        {
            Name = p.ProcessName,
            Values = new[] { (double)p.ConnectionCount },
            Fill = new SolidColorPaint(GetPieColor(i))
        } as ISeries).ToArray();

        // Update state distribution
        var states = _analyticsService.GetStateDistribution();
        StateDistribution.Clear();
        foreach (var s in states)
        {
            StateDistribution.Add(s);
        }

        // Update state pie chart
        StateSeries = states.Take(5).Select((s, i) => new PieSeries<double>
        {
            Name = s.State,
            Values = new[] { (double)s.Count },
            Fill = new SolidColorPaint(GetStateColor(s.State))
        } as ISeries).ToArray();
    }

    private TimeSpan GetTimeRangeFromSelection()
    {
        return SelectedTimeRange switch
        {
            "15 minutes" => TimeSpan.FromMinutes(15),
            "1 hour" => TimeSpan.FromHours(1),
            "6 hours" => TimeSpan.FromHours(6),
            "24 hours" => TimeSpan.FromHours(24),
            _ => TimeSpan.FromHours(1)
        };
    }

    private static SKColor GetPieColor(int index)
    {
        var colors = new[]
        {
            SKColors.DodgerBlue,
            SKColors.Orange,
            SKColors.MediumSeaGreen,
            SKColors.Crimson,
            SKColors.MediumPurple,
            SKColors.Goldenrod,
            SKColors.Teal,
            SKColors.HotPink,
            SKColors.SlateGray,
            SKColors.Olive
        };
        return colors[index % colors.Length];
    }

    private static SKColor GetStateColor(string state)
    {
        return state.ToUpperInvariant() switch
        {
            "ESTABLISHED" => SKColors.MediumSeaGreen,
            "LISTEN" => SKColors.DodgerBlue,
            "TIME_WAIT" => SKColors.SlateGray,
            "CLOSE_WAIT" => SKColors.Orange,
            "SYN_SENT" => SKColors.MediumPurple,
            "SYN_RCVD" => SKColors.Teal,
            "FIN_WAIT1" => SKColors.Goldenrod,
            "FIN_WAIT2" => SKColors.HotPink,
            "*" => SKColors.Orange, // UDP
            _ => SKColors.Gray
        };
    }

    /// <summary>
    /// Starts auto-refresh if enabled.
    /// </summary>
    public void StartCollection()
    {
        if (AutoRefresh)
        {
            _refreshTimer.Start();
        }
    }

    /// <summary>
    /// Stops auto-refresh.
    /// </summary>
    public void StopCollection()
    {
        _refreshTimer.Stop();
    }
}
