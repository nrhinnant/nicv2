using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Policy;
using WfpTrafficControl.UI.Models;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Policy Editor screen.
/// </summary>
public partial class PolicyEditorViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;
    private readonly IPolicyTemplateProvider _templateProvider;

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

    // Search/Filter
    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _actionFilter = "all";

    private ICollectionView? _rulesView;

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

    // Templates
    [ObservableProperty]
    private ObservableCollection<PolicyTemplate> _templates = new();

    public PolicyEditorViewModel(IServiceClient serviceClient, IDialogService dialogService, IPolicyTemplateProvider templateProvider)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
        _templateProvider = templateProvider;

        // Load available templates
        foreach (var template in _templateProvider.GetTemplates())
        {
            Templates.Add(template);
        }

        // Setup collection view for filtering
        SetupRulesView();
    }

    /// <summary>
    /// Gets the filtered view of rules for data binding.
    /// </summary>
    public ICollectionView RulesView => _rulesView ??= SetupRulesView();

    /// <summary>
    /// Gets the count of visible (filtered) rules.
    /// </summary>
    public int FilteredRuleCount => _rulesView?.Cast<object>().Count() ?? Rules.Count;

    /// <summary>
    /// Available action filters.
    /// </summary>
    public static string[] AvailableActionFilters => new[] { "all", "allow", "block" };

    private ICollectionView SetupRulesView()
    {
        _rulesView = CollectionViewSource.GetDefaultView(Rules);
        _rulesView.Filter = FilterRules;
        return _rulesView;
    }

    private bool FilterRules(object obj)
    {
        if (obj is not RuleViewModel rule)
            return false;

        // Apply action filter
        if (ActionFilter != "all" && !rule.Action.Equals(ActionFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Apply search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            return rule.Id.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   rule.Action.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   rule.Direction.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   rule.Protocol.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                   (rule.Process?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.RemoteIp?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.RemotePorts?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.LocalIp?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.LocalPorts?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (rule.Comment?.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        return true;
    }

    private void RefreshRulesFilter()
    {
        _rulesView?.Refresh();
        OnPropertyChanged(nameof(FilteredRuleCount));
    }

    partial void OnSearchTextChanged(string value) => RefreshRulesFilter();
    partial void OnActionFilterChanged(string value) => RefreshRulesFilter();

    /// <summary>
    /// Clears the search filter.
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        ActionFilter = "all";
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
    /// Loads a policy from a template.
    /// </summary>
    [RelayCommand]
    private void LoadFromTemplate(PolicyTemplate? template)
    {
        if (template == null)
            return;

        if (HasUnsavedChanges)
        {
            if (!_dialogService.Confirm(
                    "You have unsaved changes. Load a template anyway?",
                    "Unsaved Changes"))
            {
                return;
            }
        }

        // Show warning if template has one
        if (!string.IsNullOrEmpty(template.Warning))
        {
            if (!_dialogService.ConfirmWarning(
                    $"{template.Description}\n\nWarning: {template.Warning}\n\nDo you want to continue?",
                    $"Load Template: {template.Name}"))
            {
                return;
            }
        }

        // Create the policy from the template
        CurrentPolicy = template.CreatePolicy();

        LoadPolicyToUI(CurrentPolicy);
        CurrentFilePath = "";
        HasUnsavedChanges = true; // Mark as unsaved since it's from a template
        HasPolicy = true;

        _dialogService.ShowSuccess(
            $"Template '{template.Name}' loaded with {CurrentPolicy.Rules.Count} rules.\n\nReview the rules and click 'Save' to save the policy to a file, or 'Apply to Service' to activate it.",
            "Template Loaded");
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

    /// <summary>
    /// Copies the selected rule.
    /// </summary>
    [RelayCommand]
    private void CopyRule()
    {
        if (SelectedRule == null)
            return;

        var copy = new RuleViewModel
        {
            Id = $"{SelectedRule.Id}-copy",
            Action = SelectedRule.Action,
            Direction = SelectedRule.Direction,
            Protocol = SelectedRule.Protocol,
            Process = SelectedRule.Process,
            RemoteIp = SelectedRule.RemoteIp,
            RemotePorts = SelectedRule.RemotePorts,
            LocalIp = SelectedRule.LocalIp,
            LocalPorts = SelectedRule.LocalPorts,
            Priority = SelectedRule.Priority,
            Enabled = SelectedRule.Enabled,
            Comment = SelectedRule.Comment
        };

        copy.PropertyChanged += (s, e) => MarkAsChanged();

        // Insert after the selected rule
        var index = Rules.IndexOf(SelectedRule);
        Rules.Insert(index + 1, copy);
        SelectedRule = copy;
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Opens a file dialog to browse for a process executable.
    /// </summary>
    [RelayCommand]
    private void BrowseProcess()
    {
        if (SelectedRule == null)
            return;

        var filePath = _dialogService.ShowOpenFileDialog(
            "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            "Select Process");

        if (!string.IsNullOrEmpty(filePath))
        {
            SelectedRule.Process = filePath;
        }
    }

    /// <summary>
    /// Opens the process picker dialog to select from running processes.
    /// </summary>
    [RelayCommand]
    private void PickProcess()
    {
        if (SelectedRule == null)
            return;

        var dialog = new Views.ProcessPickerDialog
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedPath))
        {
            SelectedRule.Process = dialog.SelectedPath;
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
    /// Gets the JSON preview of this rule.
    /// </summary>
    public string JsonPreview
    {
        get
        {
            var rule = new Rule
            {
                Id = Id,
                Action = Action,
                Direction = Direction,
                Protocol = Protocol,
                Process = string.IsNullOrWhiteSpace(Process) ? null : Process,
                Priority = Priority,
                Enabled = Enabled,
                Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment
            };

            // Remote endpoint
            if (!string.IsNullOrWhiteSpace(RemoteIp) || !string.IsNullOrWhiteSpace(RemotePorts))
            {
                rule.Remote = new EndpointFilter
                {
                    Ip = string.IsNullOrWhiteSpace(RemoteIp) ? null : RemoteIp,
                    Ports = string.IsNullOrWhiteSpace(RemotePorts) ? null : RemotePorts
                };
            }

            // Local endpoint
            if (!string.IsNullOrWhiteSpace(LocalIp) || !string.IsNullOrWhiteSpace(LocalPorts))
            {
                rule.Local = new EndpointFilter
                {
                    Ip = string.IsNullOrWhiteSpace(LocalIp) ? null : LocalIp,
                    Ports = string.IsNullOrWhiteSpace(LocalPorts) ? null : LocalPorts
                };
            }

            return System.Text.Json.JsonSerializer.Serialize(rule, JsonPreviewOptions);
        }
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonPreviewOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Gets validation errors for the remote IP field.
    /// </summary>
    public string? RemoteIpError => ValidateIpCidr(RemoteIp);

    /// <summary>
    /// Gets validation errors for the local IP field.
    /// </summary>
    public string? LocalIpError => ValidateIpCidr(LocalIp);

    /// <summary>
    /// Gets validation errors for the remote ports field.
    /// </summary>
    public string? RemotePortsError => ValidatePorts(RemotePorts);

    /// <summary>
    /// Gets validation errors for the local ports field.
    /// </summary>
    public string? LocalPortsError => ValidatePorts(LocalPorts);

    /// <summary>
    /// Gets whether this rule has any validation errors.
    /// </summary>
    public bool HasValidationErrors =>
        !string.IsNullOrEmpty(RemoteIpError) ||
        !string.IsNullOrEmpty(LocalIpError) ||
        !string.IsNullOrEmpty(RemotePortsError) ||
        !string.IsNullOrEmpty(LocalPortsError);

    private static string? ValidateIpCidr(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Check for CIDR notation
        var parts = value.Split('/');
        if (parts.Length > 2)
            return "Invalid CIDR notation";

        var ip = parts[0];
        if (!System.Net.IPAddress.TryParse(ip, out _))
            return "Invalid IP address";

        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > 128)
                return "Invalid CIDR prefix (0-128)";
        }

        return null;
    }

    private static string? ValidatePorts(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Ports can be: "80", "80-443", "80,443,8080-9000"
        var segments = value.Split(',');
        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (trimmed.Contains('-'))
            {
                var range = trimmed.Split('-');
                if (range.Length != 2)
                    return "Invalid port range format";

                if (!ushort.TryParse(range[0].Trim(), out var start) ||
                    !ushort.TryParse(range[1].Trim(), out var end))
                    return "Port must be a number 0-65535";

                if (start > end)
                    return "Port range start must be <= end";
            }
            else
            {
                if (!ushort.TryParse(trimmed, out _))
                    return "Port must be a number 0-65535";
            }
        }

        return null;
    }

    // Notify property changed for derived properties
    partial void OnIdChanged(string value) => NotifyDerivedProperties();
    partial void OnActionChanged(string value) => NotifyDerivedProperties();
    partial void OnDirectionChanged(string value) => NotifyDerivedProperties();
    partial void OnProtocolChanged(string value) => NotifyDerivedProperties();
    partial void OnProcessChanged(string value) => NotifyDerivedProperties();
    partial void OnRemoteIpChanged(string value) => NotifyDerivedProperties();
    partial void OnRemotePortsChanged(string value) => NotifyDerivedProperties();
    partial void OnLocalIpChanged(string value) => NotifyDerivedProperties();
    partial void OnLocalPortsChanged(string value) => NotifyDerivedProperties();
    partial void OnPriorityChanged(int value) => NotifyDerivedProperties();
    partial void OnEnabledChanged(bool value) => NotifyDerivedProperties();
    partial void OnCommentChanged(string value) => NotifyDerivedProperties();

    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(JsonPreview));
        OnPropertyChanged(nameof(RemoteIpError));
        OnPropertyChanged(nameof(LocalIpError));
        OnPropertyChanged(nameof(RemotePortsError));
        OnPropertyChanged(nameof(LocalPortsError));
        OnPropertyChanged(nameof(HasValidationErrors));
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
