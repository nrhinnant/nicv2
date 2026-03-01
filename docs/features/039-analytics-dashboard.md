# Connection Analytics Dashboard (P11)

## Overview

The Connection Analytics Dashboard provides visualizations and statistics showing connection patterns, top network-using processes, and connection state distribution. It transforms raw connection data into actionable insights for proactive security monitoring.

## Features

### Summary Cards

- **Total Connections**: Count of active TCP and UDP connections with breakdown
- **Established**: Count of active TCP sessions
- **Listening Ports**: Count of open server ports
- **Top Process**: Most network-active process with connection count

### Charts

#### Connections Over Time
- Line chart showing TCP and UDP connections over the selected time range
- Supports zooming on the X axis
- Auto-updates as new data is collected

#### Top Processes
- DataGrid showing the top 10 network-using processes
- Columns: Process Name, Total Connections, TCP, UDP, Percentage
- Color-coded TCP (blue) and UDP (orange) counts

#### Connection States
- Pie chart showing distribution of connection states
- Color-coded by state type (ESTABLISHED=green, LISTEN=blue, etc.)
- Itemized list with counts and percentages

### Data Collection

- **Auto-collect**: Automatically collect data every 5 seconds (toggleable)
- **Manual Collect**: Click "Collect Now" for immediate snapshot
- **Time Range**: Select from 15 minutes, 1 hour, 6 hours, or 24 hours
- **Clear Data**: Reset all collected analytics

## Implementation Details

### Architecture

The Analytics Dashboard uses an in-memory analytics service that:
1. Collects connection snapshots from the Connection Monitor service
2. Stores snapshots with configurable retention (default: 1000 snapshots, 24 hours)
3. Aggregates data into time buckets for charting
4. Computes statistics on demand from stored snapshots

### Data Storage

- Snapshots are stored in memory with automatic pruning
- Maximum snapshots: 1000 (configurable)
- Maximum age: 24 hours (configurable)
- Time buckets are dynamically sized based on selected range

### Charting Library

Uses LiveCharts2 (LiveChartsCore.SkiaSharpView.WPF) for:
- Line charts (connections over time)
- Pie charts (state distribution)
- Hardware-accelerated rendering via SkiaSharp

## Files

### New Files

- `src/ui/WfpTrafficControl.UI/Services/IAnalyticsService.cs` - Interface and DTOs
- `src/ui/WfpTrafficControl.UI/Services/AnalyticsService.cs` - Implementation
- `src/ui/WfpTrafficControl.UI/ViewModels/AnalyticsDashboardViewModel.cs` - ViewModel
- `src/ui/WfpTrafficControl.UI/Views/AnalyticsDashboardView.xaml(.cs)` - View
- `tests/UI/AnalyticsDashboardTests.cs` - Unit tests
- `docs/features/039-analytics-dashboard.md` - This documentation

### Modified Files

- `src/ui/WfpTrafficControl.UI/WfpTrafficControl.UI.csproj` - Added LiveCharts2 package
- `src/ui/WfpTrafficControl.UI/App.xaml.cs` - Registered services
- `src/ui/WfpTrafficControl.UI/ViewModels/MainViewModel.cs` - Added ViewModel property
- `src/ui/WfpTrafficControl.UI/MainWindow.xaml` - Added Analytics tab

## Usage

### Accessing the Dashboard

1. Open the WFP Traffic Control UI
2. Click the "Analytics" tab in the navigation bar
3. Data collection starts automatically if auto-collect is enabled

### Viewing Analytics

1. **Summary Cards**: View at-a-glance statistics at the top
2. **Connections Over Time**: Monitor connection trends in the main chart
3. **Top Processes**: Identify which applications use the most connections
4. **State Distribution**: Understand your connection state breakdown

### Configuring Data Collection

1. **Auto-collect**: Check/uncheck to enable/disable automatic collection
2. **Time Range**: Select the window of data to display (15min to 24h)
3. **Collect Now**: Manually trigger a data collection
4. **Clear Data**: Remove all stored analytics to start fresh

## Data Model

### ConnectionDataPoint
- `Timestamp`: When the data was collected
- `TcpCount`: Number of TCP connections
- `UdpCount`: Number of UDP connections
- `TotalCount`: Sum of TCP and UDP

### ProcessAnalytics
- `ProcessName`: Name of the process
- `ConnectionCount`: Total connections owned
- `TcpCount`: TCP connections owned
- `UdpCount`: UDP connections owned
- `Percentage`: Share of total connections

### StateAnalytics
- `State`: Connection state (ESTABLISHED, LISTEN, etc.)
- `Count`: Number of connections in this state
- `Percentage`: Share of total connections

### AnalyticsSummary
- `TotalConnections`: Total active connections
- `TcpConnections`: TCP connection count
- `UdpConnections`: UDP connection count
- `EstablishedConnections`: Active TCP sessions
- `ListeningPorts`: Open server ports
- `UniqueProcesses`: Distinct process count
- `TopProcess`: Most active process name
- `TopProcessCount`: Connection count for top process

## Performance Considerations

- Snapshots are stored in memory (not persisted)
- Automatic pruning prevents unbounded growth
- Charts use hardware acceleration via SkiaSharp
- Collection and aggregation are thread-safe

## Known Limitations

- Data is not persisted across application restarts
- Historical data beyond 24 hours is pruned
- Only current snapshot data is shown in pie charts
- No export functionality for analytics data

## Testing

Run the analytics dashboard tests:
```bash
dotnet test tests/Tests.csproj --filter "FullyQualifiedName~AnalyticsDashboardTests"
```

## Dependencies

- **P10 (Connection Monitor)**: Required for connection data source
- **LiveCharts2**: Charting library (LiveChartsCore.SkiaSharpView.WPF v2.0.0-rc2)
