using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for real-time connection monitoring.
/// </summary>
public partial class ConnectionMonitorViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly DispatcherTimer _refreshTimer;
    private ICollectionView? _connectionsView;

    /// <summary>
    /// Collection of active connections.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ConnectionDto> _connections = new();

    /// <summary>
    /// Whether auto-refresh is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _autoRefresh = false;

    /// <summary>
    /// Auto-refresh interval in seconds.
    /// </summary>
    [ObservableProperty]
    private int _refreshIntervalSeconds = 3;

    /// <summary>
    /// Whether to include TCP connections.
    /// </summary>
    [ObservableProperty]
    private bool _includeTcp = true;

    /// <summary>
    /// Whether to include UDP connections.
    /// </summary>
    [ObservableProperty]
    private bool _includeUdp = true;

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Status message.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Click Refresh to load connections";

    /// <summary>
    /// Total connection count.
    /// </summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>
    /// Filtered connection count.
    /// </summary>
    public int FilteredCount => _connectionsView?.Cast<object>().Count() ?? Connections.Count;

    /// <summary>
    /// Search text for filtering.
    /// </summary>
    [ObservableProperty]
    private string _searchText = "";

    /// <summary>
    /// Protocol filter.
    /// </summary>
    [ObservableProperty]
    private string _protocolFilter = "all";

    /// <summary>
    /// State filter (for TCP).
    /// </summary>
    [ObservableProperty]
    private string _stateFilter = "all";

    /// <summary>
    /// Selected connection.
    /// </summary>
    [ObservableProperty]
    private ConnectionDto? _selectedConnection;

    /// <summary>
    /// Last refresh timestamp.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastRefresh;

    /// <summary>
    /// Available protocol filters.
    /// </summary>
    public static string[] AvailableProtocolFilters => new[] { "all", "tcp", "udp" };

    /// <summary>
    /// Available state filters.
    /// </summary>
    public static string[] AvailableStateFilters => new[] { "all", "ESTABLISHED", "LISTEN", "TIME_WAIT", "CLOSE_WAIT" };

    /// <summary>
    /// Available refresh intervals in seconds.
    /// </summary>
    public static int[] AvailableRefreshIntervals => new[] { 1, 2, 3, 5, 10 };

    public ConnectionMonitorViewModel(IServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
        _refreshTimer = new DispatcherTimer();
        _refreshTimer.Tick += async (_, _) => await RefreshConnectionsAsync();
        SetupConnectionsView();
    }

    /// <summary>
    /// Gets the filtered view of connections.
    /// </summary>
    public ICollectionView ConnectionsView => _connectionsView ??= SetupConnectionsView();

    private ICollectionView SetupConnectionsView()
    {
        _connectionsView = CollectionViewSource.GetDefaultView(Connections);
        _connectionsView.Filter = FilterConnections;
        return _connectionsView;
    }

    private bool FilterConnections(object obj)
    {
        if (obj is not ConnectionDto conn)
            return false;

        // Apply protocol filter
        if (ProtocolFilter != "all" && !conn.Protocol.Equals(ProtocolFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Apply state filter
        if (StateFilter != "all" && !conn.State.Equals(StateFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Apply search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            return conn.Protocol.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   conn.State.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   conn.LocalIp.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   conn.LocalPort.ToString().Contains(searchLower) ||
                   conn.RemoteIp.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   conn.RemotePort.ToString().Contains(searchLower) ||
                   (conn.ProcessName?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (conn.ProcessPath?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   conn.ProcessId.ToString().Contains(searchLower);
        }

        return true;
    }

    private void RefreshFilter()
    {
        _connectionsView?.Refresh();
        OnPropertyChanged(nameof(FilteredCount));
    }

    partial void OnSearchTextChanged(string value) => RefreshFilter();
    partial void OnProtocolFilterChanged(string value) => RefreshFilter();
    partial void OnStateFilterChanged(string value) => RefreshFilter();

    partial void OnAutoRefreshChanged(bool value)
    {
        if (value)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        _refreshTimer.Interval = TimeSpan.FromSeconds(value);
    }

    /// <summary>
    /// Refreshes the connection list.
    /// </summary>
    [RelayCommand]
    public async Task RefreshConnectionsAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Loading connections...";

        try
        {
            var result = await _serviceClient.GetConnectionsAsync(IncludeTcp, IncludeUdp);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                TotalCount = response.Count;
                LastRefresh = response.Timestamp;

                // Update connections
                Connections.Clear();
                foreach (var conn in response.Connections)
                {
                    Connections.Add(conn);
                }

                RefreshFilter();
                StatusMessage = $"Showing {FilteredCount} of {TotalCount} connections";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to load: {error}";
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
    /// Clears filters.
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        ProtocolFilter = "all";
        StateFilter = "all";
    }

    /// <summary>
    /// Copies selected connection details to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyConnectionDetails()
    {
        if (SelectedConnection == null)
            return;

        var conn = SelectedConnection;
        var text = $"Protocol: {conn.Protocol}\n" +
                   $"State: {conn.State}\n" +
                   $"Local: {conn.LocalEndpoint}\n" +
                   $"Remote: {conn.RemoteEndpoint}\n" +
                   $"Process: {conn.ProcessName ?? "Unknown"} (PID: {conn.ProcessId})\n" +
                   (conn.ProcessPath != null ? $"Path: {conn.ProcessPath}\n" : "");

        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard access can fail
        }
    }

    /// <summary>
    /// Stops the auto-refresh timer when the ViewModel is disposed.
    /// </summary>
    public void Cleanup()
    {
        _refreshTimer.Stop();
    }
}
