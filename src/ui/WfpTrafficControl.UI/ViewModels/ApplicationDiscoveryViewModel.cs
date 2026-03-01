using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for application discovery and rule suggestion.
/// </summary>
public partial class ApplicationDiscoveryViewModel : ObservableObject
{
    private readonly IApplicationDiscoveryService _discoveryService;
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;
    private List<string> _currentPolicyPaths = new();

    /// <summary>
    /// Available category filters.
    /// </summary>
    public static string[] AvailableCategoryFilters => new[]
    {
        "All Categories",
        "Web Browser",
        "Communication",
        "Development",
        "Cloud Storage",
        "Gaming",
        "Media"
    };

    /// <summary>
    /// Available coverage filters.
    /// </summary>
    public static string[] AvailableCoverageFilters => new[]
    {
        "All",
        "Uncovered",
        "Partially Covered",
        "Fully Covered",
        "Known Apps Only"
    };

    [ObservableProperty]
    private ObservableCollection<DiscoveredApplication> _applications = new();

    [ObservableProperty]
    private ObservableCollection<DiscoveredApplication> _filteredApplications = new();

    [ObservableProperty]
    private DiscoveredApplication? _selectedApplication;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryFilter = "All Categories";

    [ObservableProperty]
    private string _selectedCoverageFilter = "All";

    [ObservableProperty]
    private int _totalApplications;

    [ObservableProperty]
    private int _coveredApplications;

    [ObservableProperty]
    private int _uncoveredApplications;

    [ObservableProperty]
    private int _knownApplications;

    public ApplicationDiscoveryViewModel(
        IApplicationDiscoveryService discoveryService,
        IServiceClient serviceClient,
        IDialogService dialogService)
    {
        _discoveryService = discoveryService;
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCoverageFilterChanged(string value)
    {
        ApplyFilters();
    }

    /// <summary>
    /// Scans for installed applications.
    /// </summary>
    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsScanning)
            return;

        IsScanning = true;
        StatusMessage = "Scanning installed applications...";

        try
        {
            // Load current policy to check coverage
            await LoadCurrentPolicyAsync();

            // Discover applications
            var apps = await _discoveryService.DiscoverApplicationsAsync();

            // Calculate coverage for each application
            foreach (var app in apps)
            {
                CalculateCoverage(app);
            }

            Applications = new ObservableCollection<DiscoveredApplication>(apps);
            UpdateStatistics();
            ApplyFilters();

            StatusMessage = $"Found {apps.Count} applications";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
            _dialogService.ShowError($"Failed to scan applications: {ex.Message}", "Error");
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Applies selected rules from the current application.
    /// </summary>
    [RelayCommand]
    public async Task ApplySuggestedRulesAsync()
    {
        if (SelectedApplication?.SuggestedRules == null || !SelectedApplication.SuggestedRules.Any())
        {
            _dialogService.ShowWarning("No suggested rules available for this application.", "No Rules");
            return;
        }

        var confirm = _dialogService.Confirm(
            $"Apply {SelectedApplication.SuggestedRules.Count} suggested rules for {SelectedApplication.Name}?\n\n" +
            "This will add the rules to a new policy file.",
            "Apply Suggested Rules");

        if (!confirm)
            return;

        try
        {
            // Create policy rules from suggestions
            var rules = SelectedApplication.SuggestedRules.Select((rule, index) => new
            {
                id = $"{SelectedApplication.KnownApplicationId ?? SelectedApplication.Id}-{rule.Id}",
                action = rule.Action,
                direction = rule.Direction,
                protocol = rule.Protocol,
                process = SelectedApplication.ExecutablePath,
                remote = rule.RemotePorts != null ? new
                {
                    ports = rule.RemotePorts
                } : null,
                priority = 1000 + index,
                enabled = true,
                comment = rule.Description
            }).ToArray();

            var policy = new
            {
                version = "1.0",
                defaultAction = "allow",
                updatedAt = DateTime.UtcNow.ToString("o"),
                rules
            };

            var json = JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true });

            // Show save dialog
            var fileName = $"{SelectedApplication.Name.Replace(" ", "_")}_rules.json";
            var path = _dialogService.ShowSaveFileDialog(
                "JSON files (*.json)|*.json|All files (*.*)|*.*",
                fileName,
                "Save Suggested Rules");

            if (string.IsNullOrEmpty(path))
                return;

            // Write policy file
            await File.WriteAllTextAsync(path, json);

            // Ask if user wants to apply
            var applyNow = _dialogService.Confirm(
                "Policy file saved. Would you like to apply it now?",
                "Apply Policy");

            if (applyNow)
            {
                var result = await _serviceClient.ApplyAsync(path);
                if (result.IsSuccess && result.Value.Ok)
                {
                    _dialogService.ShowSuccess("Policy applied successfully!", "Success");
                    // Reload policy paths and update coverage
                    await LoadCurrentPolicyAsync();
                    CalculateCoverage(SelectedApplication);
                    OnPropertyChanged(nameof(SelectedApplication));
                }
                else
                {
                    var error = result.IsFailure ? result.Error.Message : result.Value.Error ?? "Unknown error";
                    _dialogService.ShowError($"Failed to apply policy: {error}", "Error");
                }
            }
            else
            {
                _dialogService.ShowInfo($"Policy saved to:\n{path}", "Saved");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to apply rules: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Copies the selected application's path to clipboard.
    /// </summary>
    [RelayCommand]
    public void CopyPath()
    {
        if (SelectedApplication?.ExecutablePath == null)
            return;

        try
        {
            System.Windows.Clipboard.SetText(SelectedApplication.ExecutablePath);
            StatusMessage = "Path copied to clipboard";
        }
        catch
        {
            // Clipboard access can fail
        }
    }

    /// <summary>
    /// Shows details for all known application signatures.
    /// </summary>
    [RelayCommand]
    public void ShowSignatures()
    {
        var signatures = _discoveryService.GetApplicationSignatures();
        var summary = string.Join("\n", signatures.Select(s => $"- {s.Name} ({s.Category})"));

        _dialogService.ShowInfo(
            $"Known Applications ({signatures.Count}):\n\n{summary}",
            "Application Signatures");
    }

    private async Task LoadCurrentPolicyAsync()
    {
        _currentPolicyPaths.Clear();

        try
        {
            // Get block rules to extract process paths
            var result = await _serviceClient.GetBlockRulesAsync();
            if (result.IsSuccess && result.Value.Ok)
            {
                // Extract unique process paths from rules
                foreach (var rule in result.Value.Rules)
                {
                    if (!string.IsNullOrEmpty(rule.Process))
                    {
                        _currentPolicyPaths.Add(rule.Process.ToLowerInvariant());
                    }
                }
            }
        }
        catch
        {
            // Service may not be available
        }
    }

    private void CalculateCoverage(DiscoveredApplication app)
    {
        app.MatchingRules.Clear();

        if (string.IsNullOrEmpty(app.ExecutablePath))
        {
            app.CoverageStatus = ApplicationCoverageStatus.Unknown;
            return;
        }

        var appPath = app.ExecutablePath.ToLowerInvariant();

        // Check if any policy rules match this application
        var matchingRules = _currentPolicyPaths
            .Where(p => p.Contains(Path.GetFileName(appPath)))
            .ToList();

        app.MatchingRules = matchingRules;

        if (matchingRules.Count == 0)
        {
            app.CoverageStatus = ApplicationCoverageStatus.Uncovered;
        }
        else if (app.SuggestedRules.Count > 0 && matchingRules.Count < app.SuggestedRules.Count)
        {
            app.CoverageStatus = ApplicationCoverageStatus.PartiallyCovered;
        }
        else
        {
            app.CoverageStatus = ApplicationCoverageStatus.FullyCovered;
        }
    }

    private void UpdateStatistics()
    {
        TotalApplications = Applications.Count;
        CoveredApplications = Applications.Count(a =>
            a.CoverageStatus == ApplicationCoverageStatus.FullyCovered ||
            a.CoverageStatus == ApplicationCoverageStatus.PartiallyCovered);
        UncoveredApplications = Applications.Count(a =>
            a.CoverageStatus == ApplicationCoverageStatus.Uncovered);
        KnownApplications = Applications.Count(a => a.IsKnownApplication);
    }

    private void ApplyFilters()
    {
        var filtered = Applications.AsEnumerable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(a =>
                a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (a.Publisher?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.ExecutablePath?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Category filter
        if (SelectedCategoryFilter != "All Categories")
        {
            filtered = filtered.Where(a =>
            {
                if (!a.IsKnownApplication)
                    return false;

                var sig = _discoveryService.GetApplicationSignatures()
                    .FirstOrDefault(s => s.Id == a.KnownApplicationId);
                return sig?.Category == SelectedCategoryFilter;
            });
        }

        // Coverage filter
        switch (SelectedCoverageFilter)
        {
            case "Uncovered":
                filtered = filtered.Where(a => a.CoverageStatus == ApplicationCoverageStatus.Uncovered);
                break;
            case "Partially Covered":
                filtered = filtered.Where(a => a.CoverageStatus == ApplicationCoverageStatus.PartiallyCovered);
                break;
            case "Fully Covered":
                filtered = filtered.Where(a => a.CoverageStatus == ApplicationCoverageStatus.FullyCovered);
                break;
            case "Known Apps Only":
                filtered = filtered.Where(a => a.IsKnownApplication);
                break;
        }

        FilteredApplications = new ObservableCollection<DiscoveredApplication>(filtered);
        StatusMessage = $"Showing {FilteredApplications.Count} of {Applications.Count} applications";
    }
}
