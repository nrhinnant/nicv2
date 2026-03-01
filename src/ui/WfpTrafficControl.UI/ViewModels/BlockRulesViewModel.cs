using System.Collections.ObjectModel;
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
