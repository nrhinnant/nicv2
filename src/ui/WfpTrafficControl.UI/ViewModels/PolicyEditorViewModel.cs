using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Policy;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Policy Editor screen.
/// </summary>
public partial class PolicyEditorViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;

    // Current policy
    [ObservableProperty]
    private Policy? _currentPolicy;

    [ObservableProperty]
    private string _currentFilePath = "";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private bool _hasPolicy;

    // Policy metadata
    [ObservableProperty]
    private string _policyVersion = "";

    [ObservableProperty]
    private string _defaultAction = "allow";

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.UtcNow;

    // Rules
    [ObservableProperty]
    private ObservableCollection<RuleViewModel> _rules = new();

    [ObservableProperty]
    private RuleViewModel? _selectedRule;

    // Validation
    [ObservableProperty]
    private bool _isValid = true;

    [ObservableProperty]
    private string _validationMessage = "";

    [ObservableProperty]
    private ObservableCollection<string> _validationErrors = new();

    // Loading states
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isValidating;

    [ObservableProperty]
    private bool _isApplying;

    public PolicyEditorViewModel(IServiceClient serviceClient, IDialogService dialogService)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    /// <summary>
    /// Creates a new empty policy.
    /// </summary>
    [RelayCommand]
    private void NewPolicy()
    {
        if (HasUnsavedChanges)
        {
            if (!_dialogService.Confirm(
                    "You have unsaved changes. Create a new policy anyway?",
                    "Unsaved Changes"))
            {
                return;
            }
        }

        CurrentPolicy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        LoadPolicyToUI(CurrentPolicy);
        CurrentFilePath = "";
        HasUnsavedChanges = false;
        HasPolicy = true;
    }

    /// <summary>
    /// Opens a policy file.
    /// </summary>
    [RelayCommand]
    private async Task LoadPolicyAsync()
    {
        if (HasUnsavedChanges)
        {
            if (!_dialogService.Confirm(
                    "You have unsaved changes. Load a different policy anyway?",
                    "Unsaved Changes"))
            {
                return;
            }
        }

        var filePath = _dialogService.ShowOpenFileDialog(
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            "Open Policy File");

        if (string.IsNullOrEmpty(filePath))
            return;

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

            CurrentPolicy = policy;
            LoadPolicyToUI(policy);
            CurrentFilePath = filePath;
            HasUnsavedChanges = false;
            HasPolicy = true;

            // Validate the loaded policy
            await ValidatePolicyAsync();
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

    /// <summary>
    /// Saves the current policy to a file.
    /// </summary>
    [RelayCommand]
    private async Task SavePolicyAsync()
    {
        if (CurrentPolicy == null)
        {
            _dialogService.ShowWarning("No policy to save. Create or load a policy first.", "No Policy");
            return;
        }

        string? filePath = CurrentFilePath;

        if (string.IsNullOrEmpty(filePath))
        {
            filePath = _dialogService.ShowSaveFileDialog(
                "JSON files (*.json)|*.json|All files (*.*)|*.*",
                "policy.json",
                "Save Policy File");

            if (string.IsNullOrEmpty(filePath))
                return;
        }

        IsLoading = true;

        try
        {
            SaveUIToPolicy();
            var json = CurrentPolicy.ToJson(indented: true);
            await File.WriteAllTextAsync(filePath, json);

            CurrentFilePath = filePath;
            HasUnsavedChanges = false;

            _dialogService.ShowSuccess($"Policy saved to:\n{filePath}", "Save Successful");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to save policy file:\n\n{ex.Message}", "Save Failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the policy to a new file.
    /// </summary>
    [RelayCommand]
    private async Task SavePolicyAsAsync()
    {
        if (CurrentPolicy == null)
        {
            _dialogService.ShowWarning("No policy to save. Create or load a policy first.", "No Policy");
            return;
        }

        var filePath = _dialogService.ShowSaveFileDialog(
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Path.GetFileName(CurrentFilePath) ?? "policy.json",
            "Save Policy As");

        if (string.IsNullOrEmpty(filePath))
            return;

        // Temporarily clear path to force save to new location
        var oldPath = CurrentFilePath;
        CurrentFilePath = filePath;
        await SavePolicyAsync();

        if (HasUnsavedChanges)
        {
            // Save failed, restore old path
            CurrentFilePath = oldPath;
        }
    }

    /// <summary>
    /// Validates the current policy.
    /// </summary>
    [RelayCommand]
    private async Task ValidatePolicyAsync()
    {
        if (CurrentPolicy == null)
        {
            _dialogService.ShowWarning("No policy to validate. Create or load a policy first.", "No Policy");
            return;
        }

        IsValidating = true;
        ValidationErrors.Clear();

        try
        {
            SaveUIToPolicy();
            var json = CurrentPolicy.ToJson();

            var result = await _serviceClient.ValidateAsync(json);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                IsValid = response.Valid;

                if (response.Valid)
                {
                    ValidationMessage = $"Policy is valid. {response.RuleCount} rule(s).";
                }
                else
                {
                    ValidationMessage = $"Policy has {response.Errors.Count} error(s).";
                    foreach (var error in response.Errors)
                    {
                        ValidationErrors.Add($"[{error.Path}] {error.Message}");
                    }
                }
            }
            else
            {
                IsValid = false;
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                ValidationMessage = $"Validation failed: {errorMsg}";
            }
        }
        catch (Exception ex)
        {
            IsValid = false;
            ValidationMessage = $"Validation error: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }

    /// <summary>
    /// Applies the current policy to the service.
    /// </summary>
    [RelayCommand]
    private async Task ApplyPolicyAsync()
    {
        if (CurrentPolicy == null)
        {
            _dialogService.ShowWarning("No policy to apply. Create or load a policy first.", "No Policy");
            return;
        }

        // Save first if there are unsaved changes or no file path
        if (HasUnsavedChanges || string.IsNullOrEmpty(CurrentFilePath))
        {
            await SavePolicyAsync();
            if (HasUnsavedChanges || string.IsNullOrEmpty(CurrentFilePath))
            {
                // User cancelled save
                return;
            }
        }

        if (!_dialogService.Confirm(
                $"Apply policy from:\n{CurrentFilePath}\n\nThis will update the active firewall rules.",
                "Confirm Apply"))
        {
            return;
        }

        IsApplying = true;

        try
        {
            var result = await _serviceClient.ApplyAsync(CurrentFilePath);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                _dialogService.ShowSuccess(
                    $"Policy applied successfully!\n\n" +
                    $"Filters created: {response.FiltersCreated}\n" +
                    $"Filters removed: {response.FiltersRemoved}\n" +
                    $"Rules skipped: {response.RulesSkipped}\n" +
                    $"Total rules: {response.TotalRules}",
                    "Apply Successful");
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";

                if (result.IsSuccess && result.Value.CompilationErrors.Count > 0)
                {
                    var errors = string.Join("\n",
                        result.Value.CompilationErrors.Select(e => $"  - [{e.RuleId}] {e.Message}"));
                    errorMsg = $"Compilation errors:\n{errors}";
                }

                _dialogService.ShowError($"Failed to apply policy:\n\n{errorMsg}", "Apply Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error applying policy:\n\n{ex.Message}", "Error");
        }
        finally
        {
            IsApplying = false;
        }
    }

    /// <summary>
    /// Adds a new rule to the policy.
    /// </summary>
    [RelayCommand]
    private void AddRule()
    {
        var newRule = new RuleViewModel
        {
            Id = $"rule-{Guid.NewGuid():N}".Substring(0, 20),
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true,
            Priority = 100
        };

        newRule.PropertyChanged += (s, e) => MarkAsChanged();

        Rules.Add(newRule);
        SelectedRule = newRule;
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Deletes the selected rule.
    /// </summary>
    [RelayCommand]
    private void DeleteRule()
    {
        if (SelectedRule == null)
            return;

        if (!_dialogService.Confirm(
                $"Delete rule '{SelectedRule.Id}'?",
                "Confirm Delete"))
        {
            return;
        }

        var index = Rules.IndexOf(SelectedRule);
        Rules.Remove(SelectedRule);

        if (Rules.Count > 0)
        {
            SelectedRule = Rules[Math.Min(index, Rules.Count - 1)];
        }
        else
        {
            SelectedRule = null;
        }

        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Moves the selected rule up in the list.
    /// </summary>
    [RelayCommand]
    private void MoveRuleUp()
    {
        if (SelectedRule == null)
            return;

        var index = Rules.IndexOf(SelectedRule);
        if (index > 0)
        {
            Rules.Move(index, index - 1);
            HasUnsavedChanges = true;
        }
    }

    /// <summary>
    /// Moves the selected rule down in the list.
    /// </summary>
    [RelayCommand]
    private void MoveRuleDown()
    {
        if (SelectedRule == null)
            return;

        var index = Rules.IndexOf(SelectedRule);
        if (index < Rules.Count - 1)
        {
            Rules.Move(index, index + 1);
            HasUnsavedChanges = true;
        }
    }

    private void LoadPolicyToUI(Policy policy)
    {
        PolicyVersion = policy.Version;
        DefaultAction = policy.DefaultAction;
        UpdatedAt = policy.UpdatedAt;

        Rules.Clear();
        foreach (var rule in policy.Rules)
        {
            var ruleVm = new RuleViewModel
            {
                Id = rule.Id,
                Action = rule.Action,
                Direction = rule.Direction,
                Protocol = rule.Protocol,
                Process = rule.Process ?? "",
                RemoteIp = rule.Remote?.Ip ?? "",
                RemotePorts = rule.Remote?.Ports ?? "",
                LocalIp = rule.Local?.Ip ?? "",
                LocalPorts = rule.Local?.Ports ?? "",
                Priority = rule.Priority,
                Enabled = rule.Enabled,
                Comment = rule.Comment ?? ""
            };

            ruleVm.PropertyChanged += (s, e) => MarkAsChanged();
            Rules.Add(ruleVm);
        }

        if (Rules.Count > 0)
        {
            SelectedRule = Rules[0];
        }

        ValidationErrors.Clear();
        ValidationMessage = "";
    }

    private void SaveUIToPolicy()
    {
        if (CurrentPolicy == null)
            return;

        CurrentPolicy.Version = PolicyVersion;
        CurrentPolicy.DefaultAction = DefaultAction;
        CurrentPolicy.UpdatedAt = DateTime.UtcNow;

        CurrentPolicy.Rules.Clear();
        foreach (var ruleVm in Rules)
        {
            var rule = new Rule
            {
                Id = ruleVm.Id,
                Action = ruleVm.Action,
                Direction = ruleVm.Direction,
                Protocol = ruleVm.Protocol,
                Process = string.IsNullOrWhiteSpace(ruleVm.Process) ? null : ruleVm.Process,
                Priority = ruleVm.Priority,
                Enabled = ruleVm.Enabled,
                Comment = string.IsNullOrWhiteSpace(ruleVm.Comment) ? null : ruleVm.Comment
            };

            // Remote endpoint
            if (!string.IsNullOrWhiteSpace(ruleVm.RemoteIp) || !string.IsNullOrWhiteSpace(ruleVm.RemotePorts))
            {
                rule.Remote = new EndpointFilter
                {
                    Ip = string.IsNullOrWhiteSpace(ruleVm.RemoteIp) ? null : ruleVm.RemoteIp,
                    Ports = string.IsNullOrWhiteSpace(ruleVm.RemotePorts) ? null : ruleVm.RemotePorts
                };
            }

            // Local endpoint
            if (!string.IsNullOrWhiteSpace(ruleVm.LocalIp) || !string.IsNullOrWhiteSpace(ruleVm.LocalPorts))
            {
                rule.Local = new EndpointFilter
                {
                    Ip = string.IsNullOrWhiteSpace(ruleVm.LocalIp) ? null : ruleVm.LocalIp,
                    Ports = string.IsNullOrWhiteSpace(ruleVm.LocalPorts) ? null : ruleVm.LocalPorts
                };
            }

            CurrentPolicy.Rules.Add(rule);
        }
    }

    private void MarkAsChanged()
    {
        HasUnsavedChanges = true;
    }

    partial void OnPolicyVersionChanged(string value) => MarkAsChanged();
    partial void OnDefaultActionChanged(string value) => MarkAsChanged();
}

/// <summary>
/// ViewModel for a single rule in the policy editor.
/// </summary>
public partial class RuleViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = "";

    [ObservableProperty]
    private string _action = "block";

    [ObservableProperty]
    private string _direction = "outbound";

    [ObservableProperty]
    private string _protocol = "tcp";

    [ObservableProperty]
    private string _process = "";

    [ObservableProperty]
    private string _remoteIp = "";

    [ObservableProperty]
    private string _remotePorts = "";

    [ObservableProperty]
    private string _localIp = "";

    [ObservableProperty]
    private string _localPorts = "";

    [ObservableProperty]
    private int _priority = 100;

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    private string _comment = "";

    /// <summary>
    /// Gets a summary string for display in the rule list.
    /// </summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(RemoteIp))
                parts.Add(RemoteIp);

            if (!string.IsNullOrWhiteSpace(RemotePorts))
                parts.Add($":{RemotePorts}");

            if (!string.IsNullOrWhiteSpace(Process))
                parts.Add($"({Path.GetFileName(Process)})");

            return parts.Count > 0 ? string.Join(" ", parts) : "(any)";
        }
    }

    /// <summary>
    /// Available actions for the dropdown.
    /// </summary>
    public static string[] AvailableActions => new[] { "allow", "block" };

    /// <summary>
    /// Available directions for the dropdown.
    /// </summary>
    public static string[] AvailableDirections => new[] { "inbound", "outbound", "both" };

    /// <summary>
    /// Available protocols for the dropdown.
    /// </summary>
    public static string[] AvailableProtocols => new[] { "tcp", "udp", "any" };
}
