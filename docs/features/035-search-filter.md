# Search/Filter Throughout (P6)

## Overview

Search and filter functionality has been added to all list views in the WFP Traffic Control UI. This improves usability as policies and logs grow in size, allowing users to quickly find specific rules, log entries, or blocked connections.

## Features

### Policy Editor - Rules Search

- **Search**: Type in the search box to filter rules by ID, action, direction, protocol, process path, IP addresses, ports, or comments
- **Action Filter**: Use the dropdown to show only "allow" or "block" rules
- **Debounced Input**: 300ms delay prevents excessive filtering while typing
- **Count Display**: Shows "filtered/total" rule count (e.g., "Rules (3/10)")

**Searchable Fields:**
- Rule ID
- Action (allow/block)
- Direction (inbound/outbound/both)
- Protocol (tcp/udp/any)
- Process path
- Remote IP/CIDR
- Remote ports
- Local IP/CIDR
- Local ports
- Comment

### Logs View - Log Search & Filters

- **Search**: Filter log entries by timestamp, event, source, status, policy version, or error message
- **Event Type Filter**: Filter by event type (all, apply, rollback, startup, shutdown)
- **Status Filter**: Filter by status (all, success, failure)
- **Count Display**: Shows "Showing X of Y entries"

### Block Rules View - Block Rules Search & Filters

- **Search**: Filter block rules by ID, direction, protocol, process, remote IP, ports, summary, or comment
- **Direction Filter**: Filter by direction (all, inbound, outbound, both)
- **Protocol Filter**: Filter by protocol (all, tcp, udp, any)
- **Count Display**: Shows "Showing X of Y rules"

## Implementation Details

### Reusable SearchFilterControl

A reusable `SearchFilterControl` user control was created to provide consistent search UI:

```
src/ui/WfpTrafficControl.UI/Controls/SearchFilterControl.xaml
src/ui/WfpTrafficControl.UI/Controls/SearchFilterControl.xaml.cs
```

Features:
- Search text box with clear button
- Placeholder text when empty
- Automatic clear button visibility based on text content
- Two-way binding support for search text

### ICollectionView Filtering

All ViewModels use WPF's `ICollectionView` for efficient filtering:

```csharp
// Setup collection view
_rulesView = CollectionViewSource.GetDefaultView(Rules);
_rulesView.Filter = FilterRules;

// Filter predicate
private bool FilterRules(object obj)
{
    if (obj is not RuleViewModel rule)
        return false;

    // Apply filters...
    return matchesAllFilters;
}

// Refresh on property change
partial void OnSearchTextChanged(string value) => RefreshRulesFilter();
```

### Debouncing

Search input is debounced using XAML binding delay:
```xml
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged, Delay=300}" />
```

This prevents excessive filtering while the user is typing.

## Files Modified

### New Files
- `src/ui/WfpTrafficControl.UI/Controls/SearchFilterControl.xaml`
- `src/ui/WfpTrafficControl.UI/Controls/SearchFilterControl.xaml.cs`
- `src/ui/WfpTrafficControl.UI/Converters/StringNotEmptyConverter.cs`

### Modified Files
- `src/ui/WfpTrafficControl.UI/ViewModels/PolicyEditorViewModel.cs` - Added search/filter
- `src/ui/WfpTrafficControl.UI/Views/PolicyEditorView.xaml` - Added search UI
- `src/ui/WfpTrafficControl.UI/ViewModels/LogsViewModel.cs` - Added search/filter
- `src/ui/WfpTrafficControl.UI/Views/LogsView.xaml` - Added search/filter UI
- `src/ui/WfpTrafficControl.UI/ViewModels/BlockRulesViewModel.cs` - Added search/filter
- `src/ui/WfpTrafficControl.UI/Views/BlockRulesView.xaml` - Added search/filter UI

## Usage

### Searching
1. Type in the search box
2. Results filter automatically after a 300ms delay
3. Click the X button or clear the text to reset

### Filtering
1. Select a filter value from the dropdown
2. Results update immediately
3. Select "all" to remove the filter

### Combining Search and Filters
Search and filters can be combined - results must match both the search text AND the selected filter values.

## Known Limitations

- Search is case-insensitive
- Search matches anywhere in the field (contains), not just prefix
- No advanced query syntax (e.g., "process:chrome")
- Filter preferences are not persisted between sessions

## Testing

Run the UI tests to verify search/filter functionality:
```bash
dotnet test tests/UI.Tests/
```
