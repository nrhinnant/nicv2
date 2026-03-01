using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Policy;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Policy Diff dialog.
/// </summary>
public partial class PolicyDiffViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly PolicyDiffService _diffService;

    // Left policy
    [ObservableProperty]
    private Policy? _leftPolicy;

    [ObservableProperty]
    private string _leftPolicyName = "(No policy loaded)";

    [ObservableProperty]
    private string _leftPolicyPath = "";

    // Right policy
    [ObservableProperty]
    private Policy? _rightPolicy;

    [ObservableProperty]
    private string _rightPolicyName = "(No policy loaded)";

    [ObservableProperty]
    private string _rightPolicyPath = "";

    // Diff results
    [ObservableProperty]
    private PolicyDiffResult? _diffResult;

    [ObservableProperty]
    private string _diffSummary = "Load two policies to compare";

    [ObservableProperty]
    private bool _hasChanges;

    // Unified diff items for display
    [ObservableProperty]
    private ObservableCollection<DiffItemViewModel> _diffItems = new();

    // Loading state
    [ObservableProperty]
    private bool _isLoading;

    public PolicyDiffViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _diffService = new PolicyDiffService();
    }

    /// <summary>
    /// Loads a policy file for the left side.
    /// </summary>
    [RelayCommand]
    private async Task LoadLeftPolicyAsync()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            "Select Left Policy (Baseline)");

        if (string.IsNullOrEmpty(filePath))
            return;

        await LoadPolicyAsync(filePath, isLeft: true);
    }

    /// <summary>
    /// Loads a policy file for the right side.
    /// </summary>
    [RelayCommand]
    private async Task LoadRightPolicyAsync()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            "Select Right Policy (New)");

        if (string.IsNullOrEmpty(filePath))
            return;

        await LoadPolicyAsync(filePath, isLeft: false);
    }

    /// <summary>
    /// Swaps left and right policies.
    /// </summary>
    [RelayCommand]
    private void SwapPolicies()
    {
        (LeftPolicy, RightPolicy) = (RightPolicy, LeftPolicy);
        (LeftPolicyName, RightPolicyName) = (RightPolicyName, LeftPolicyName);
        (LeftPolicyPath, RightPolicyPath) = (RightPolicyPath, LeftPolicyPath);

        ComputeDiff();
    }

    /// <summary>
    /// Clears both policies.
    /// </summary>
    [RelayCommand]
    private void ClearAll()
    {
        LeftPolicy = null;
        LeftPolicyName = "(No policy loaded)";
        LeftPolicyPath = "";

        RightPolicy = null;
        RightPolicyName = "(No policy loaded)";
        RightPolicyPath = "";

        DiffResult = null;
        DiffSummary = "Load two policies to compare";
        HasChanges = false;
        DiffItems.Clear();
    }

    private async Task LoadPolicyAsync(string filePath, bool isLeft)
    {
        IsLoading = true;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var policy = Policy.FromJson(json);

            if (policy == null)
            {
                _dialogService.ShowError("Failed to parse policy file. Invalid JSON format.", "Load Failed");
                return;
            }

            var name = Path.GetFileName(filePath);
            var rulesCount = policy.Rules.Count;

            if (isLeft)
            {
                LeftPolicy = policy;
                LeftPolicyName = $"{name} ({rulesCount} rules, v{policy.Version})";
                LeftPolicyPath = filePath;
            }
            else
            {
                RightPolicy = policy;
                RightPolicyName = $"{name} ({rulesCount} rules, v{policy.Version})";
                RightPolicyPath = filePath;
            }

            ComputeDiff();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to load policy file:\n\n{ex.Message}", "Load Failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ComputeDiff()
    {
        if (LeftPolicy == null && RightPolicy == null)
        {
            DiffResult = null;
            DiffSummary = "Load two policies to compare";
            HasChanges = false;
            DiffItems.Clear();
            return;
        }

        if (LeftPolicy == null || RightPolicy == null)
        {
            DiffResult = null;
            DiffSummary = "Load both policies to see comparison";
            HasChanges = false;
            DiffItems.Clear();
            return;
        }

        DiffResult = _diffService.Compare(LeftPolicy, RightPolicy);
        DiffSummary = DiffResult.Summary;
        HasChanges = DiffResult.HasChanges;

        // Build unified diff view
        DiffItems.Clear();

        // Add metadata changes
        if (DiffResult.VersionChanged)
        {
            DiffItems.Add(new DiffItemViewModel
            {
                ChangeType = DiffChangeType.Modified,
                RuleId = "(metadata)",
                Description = $"Version: {DiffResult.OldVersion} → {DiffResult.NewVersion}",
                Details = "Policy version changed"
            });
        }

        if (DiffResult.DefaultActionChanged)
        {
            DiffItems.Add(new DiffItemViewModel
            {
                ChangeType = DiffChangeType.Modified,
                RuleId = "(metadata)",
                Description = $"Default action: {DiffResult.OldDefaultAction ?? "(none)"} → {DiffResult.NewDefaultAction ?? "(none)"}",
                Details = "Default action changed"
            });
        }

        // Add added rules
        foreach (var diff in DiffResult.AddedRules)
        {
            DiffItems.Add(new DiffItemViewModel
            {
                ChangeType = DiffChangeType.Added,
                RuleId = diff.Rule.Id,
                Description = FormatRuleSummary(diff.Rule),
                Details = $"+ Added rule: {diff.Rule.Action} {diff.Rule.Direction} {diff.Rule.Protocol}"
            });
        }

        // Add removed rules
        foreach (var diff in DiffResult.RemovedRules)
        {
            DiffItems.Add(new DiffItemViewModel
            {
                ChangeType = DiffChangeType.Removed,
                RuleId = diff.Rule.Id,
                Description = FormatRuleSummary(diff.Rule),
                Details = $"- Removed rule: {diff.Rule.Action} {diff.Rule.Direction} {diff.Rule.Protocol}"
            });
        }

        // Add modified rules
        foreach (var diff in DiffResult.ModifiedRules)
        {
            DiffItems.Add(new DiffItemViewModel
            {
                ChangeType = DiffChangeType.Modified,
                RuleId = diff.NewRule.Id,
                Description = FormatRuleSummary(diff.NewRule),
                Details = string.Join("\n", diff.ChangedFields.Select(f => $"  ~ {f}"))
            });
        }

        // Add unchanged rules (optional, can be toggled)
        foreach (var diff in DiffResult.UnchangedRules)
        {
            DiffItems.Add(new DiffItemViewModel
            {
                ChangeType = DiffChangeType.Unchanged,
                RuleId = diff.Rule.Id,
                Description = FormatRuleSummary(diff.Rule),
                Details = "No changes"
            });
        }
    }

    private static string FormatRuleSummary(Rule rule)
    {
        var parts = new List<string>
        {
            rule.Action.ToUpperInvariant(),
            rule.Direction,
            rule.Protocol
        };

        if (!string.IsNullOrEmpty(rule.Process))
            parts.Add(Path.GetFileName(rule.Process));

        if (rule.Remote != null)
        {
            if (!string.IsNullOrEmpty(rule.Remote.Ip))
                parts.Add(rule.Remote.Ip);
            if (!string.IsNullOrEmpty(rule.Remote.Ports))
                parts.Add($":{rule.Remote.Ports}");
        }

        return string.Join(" ", parts);
    }
}

/// <summary>
/// Type of change in a diff.
/// </summary>
public enum DiffChangeType
{
    Added,
    Removed,
    Modified,
    Unchanged
}

/// <summary>
/// ViewModel for a single item in the diff view.
/// </summary>
public partial class DiffItemViewModel : ObservableObject
{
    [ObservableProperty]
    private DiffChangeType _changeType;

    [ObservableProperty]
    private string _ruleId = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _details = "";

    /// <summary>
    /// Gets the display character for the change type.
    /// </summary>
    public string ChangeIndicator => ChangeType switch
    {
        DiffChangeType.Added => "+",
        DiffChangeType.Removed => "-",
        DiffChangeType.Modified => "~",
        DiffChangeType.Unchanged => " ",
        _ => "?"
    };
}
