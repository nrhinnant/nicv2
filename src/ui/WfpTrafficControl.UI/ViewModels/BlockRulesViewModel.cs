using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Block Rules tab showing current block rules from the active policy.
/// Note: This is a simplified view showing policy rules, not actual blocked connection events.
/// A full blocked connection log would require ETW event tracing integration.
/// </summary>
public partial class BlockRulesViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;

    // Block rules data
    [ObservableProperty]
    private ObservableCollection<BlockRuleDto> _blockRules = new();

    [ObservableProperty]
    private int _totalRuleCount;

    [ObservableProperty]
    private string? _policyVersion;

    [ObservableProperty]
    private bool _policyLoaded;

    // Loading state
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Click Refresh to load block rules";

    // Selected rule for details
    [ObservableProperty]
    private BlockRuleDto? _selectedRule;

    // Search/Filter
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _directionFilter = "all";

    [ObservableProperty]
    private string _protocolFilter = "all";

    private ICollectionView? _blockRulesView;

    /// <summary>
    /// Explanation shown in UI about the simplified nature of this view.
    /// </summary>
    public string SimplificationNotice =>
        "This view shows block rules defined in your policy, not actual blocked connection events. " +
        "Real-time blocked connection logging would require Windows ETW event tracing integration. " +
        "Use this to review what traffic your policy will block when matching rules are triggered.";

    public BlockRulesViewModel(IServiceClient serviceClient)
    {
        _serviceClient = serviceClient;
        SetupBlockRulesView();
    }

    /// <summary>
    /// Gets the filtered view of block rules for data binding.
    /// </summary>
    public ICollectionView BlockRulesView => _blockRulesView ??= SetupBlockRulesView();

    /// <summary>
    /// Gets the count of visible (filtered) block rules.
    /// </summary>
    public int FilteredRuleCount => _blockRulesView?.Cast<object>().Count() ?? BlockRules.Count;

    /// <summary>
    /// Available direction filters.
    /// </summary>
    public static string[] AvailableDirectionFilters => new[] { "all", "inbound", "outbound", "both" };

    /// <summary>
    /// Available protocol filters.
    /// </summary>
    public static string[] AvailableProtocolFilters => new[] { "all", "tcp", "udp", "any" };

    private ICollectionView SetupBlockRulesView()
    {
        _blockRulesView = CollectionViewSource.GetDefaultView(BlockRules);
        _blockRulesView.Filter = FilterBlockRules;
        return _blockRulesView;
    }

    private bool FilterBlockRules(object obj)
    {
        if (obj is not BlockRuleDto rule)
            return false;

        // Apply direction filter
        if (DirectionFilter != "all" && !rule.Direction.Equals(DirectionFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Apply protocol filter
        if (ProtocolFilter != "all" && !(rule.Protocol?.Equals(ProtocolFilter, StringComparison.OrdinalIgnoreCase) ?? false))
            return false;

        // Apply search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            return rule.Id.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   rule.Direction.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   (rule.Protocol?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.Process?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.RemoteIp?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.RemotePorts?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.Summary?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.Comment?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }

    private void RefreshBlockRulesFilter()
    {
        _blockRulesView?.Refresh();
        OnPropertyChanged(nameof(FilteredRuleCount));
    }

    partial void OnSearchTextChanged(string value) => RefreshBlockRulesFilter();
    partial void OnDirectionFilterChanged(string value) => RefreshBlockRulesFilter();
    partial void OnProtocolFilterChanged(string value) => RefreshBlockRulesFilter();

    /// <summary>
    /// Clears the search filter.
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        DirectionFilter = "all";
        ProtocolFilter = "all";
    }

    /// <summary>
    /// Initializes the view by loading block rules.
    /// </summary>
    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RefreshBlockRulesAsync();
    }

    /// <summary>
    /// Refreshes the block rules from the service.
    /// </summary>
    [RelayCommand]
    public async Task RefreshBlockRulesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading block rules...";

        try
        {
            var result = await _serviceClient.GetBlockRulesAsync();

            BlockRules.Clear();

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                PolicyLoaded = response.PolicyLoaded;
                PolicyVersion = response.PolicyVersion;
                TotalRuleCount = response.Count;

                if (!response.PolicyLoaded)
                {
                    StatusMessage = "No policy is currently loaded. Apply a policy to see block rules.";
                }
                else if (response.Count == 0)
                {
                    StatusMessage = "Policy loaded but contains no block rules.";
                }
                else
                {
                    foreach (var rule in response.Rules)
                    {
                        BlockRules.Add(rule);
                    }

                    StatusMessage = $"Showing {response.Count} block rule(s) from policy v{response.PolicyVersion}";
                }
            }
            else
            {
                PolicyLoaded = false;
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to load block rules: {errorMsg}";
            }
        }
        catch (Exception ex)
        {
            PolicyLoaded = false;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Copies the selected rule's summary to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyRuleSummary()
    {
        if (SelectedRule == null)
            return;

        var text = $"Rule: {SelectedRule.Id}\n" +
                   $"Summary: {SelectedRule.Summary}\n" +
                   $"Direction: {SelectedRule.Direction}\n" +
                   $"Protocol: {SelectedRule.Protocol ?? "any"}\n" +
                   (SelectedRule.Process != null ? $"Process: {SelectedRule.Process}\n" : "") +
                   (SelectedRule.RemoteIp != null ? $"Remote IP: {SelectedRule.RemoteIp}\n" : "") +
                   (SelectedRule.RemotePorts != null ? $"Remote Ports: {SelectedRule.RemotePorts}\n" : "") +
                   (SelectedRule.LocalIp != null ? $"Local IP: {SelectedRule.LocalIp}\n" : "") +
                   (SelectedRule.LocalPorts != null ? $"Local Ports: {SelectedRule.LocalPorts}\n" : "") +
                   (SelectedRule.Comment != null ? $"Comment: {SelectedRule.Comment}\n" : "");

        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard access can fail in some scenarios
        }
    }
}
