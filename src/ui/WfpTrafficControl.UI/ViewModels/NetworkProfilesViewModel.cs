using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for network profiles management and automatic policy switching.
/// </summary>
public partial class NetworkProfilesViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Available network categories for matching.
    /// </summary>
    public static string[] AvailableCategories => new[] { "Public", "Private", "Domain" };

    [ObservableProperty]
    private ObservableCollection<NetworkProfile> _profiles = new();

    [ObservableProperty]
    private NetworkProfile? _selectedProfile;

    [ObservableProperty]
    private string? _activeProfileId;

    [ObservableProperty]
    private string? _activeProfileName;

    [ObservableProperty]
    private bool _autoSwitchEnabled;

    [ObservableProperty]
    private CurrentNetworkInfo? _currentNetwork;

    [ObservableProperty]
    private string? _matchingProfileId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Profile editor fields
    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editProfileId = string.Empty;

    [ObservableProperty]
    private string _editProfileName = string.Empty;

    [ObservableProperty]
    private string _editProfileDescription = string.Empty;

    [ObservableProperty]
    private string _editProfilePolicyPath = string.Empty;

    [ObservableProperty]
    private int _editProfilePriority = 100;

    [ObservableProperty]
    private bool _editProfileEnabled = true;

    [ObservableProperty]
    private bool _editProfileIsDefault;

    [ObservableProperty]
    private string _editConditionsSsids = string.Empty;

    [ObservableProperty]
    private string _editConditionsDnsSuffixes = string.Empty;

    [ObservableProperty]
    private string _editConditionsNetworkNames = string.Empty;

    [ObservableProperty]
    private string _editConditionsGateways = string.Empty;

    [ObservableProperty]
    private string? _editConditionsNetworkCategory;

    [ObservableProperty]
    private bool _editConditionsMatchAll;

    [ObservableProperty]
    private bool _isNewProfile;

    public NetworkProfilesViewModel(IServiceClient serviceClient, IDialogService dialogService)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    /// <summary>
    /// Whether the selected profile can be edited.
    /// </summary>
    public bool CanEditSelectedProfile => SelectedProfile != null && !IsEditing;

    /// <summary>
    /// Whether the selected profile can be deleted.
    /// </summary>
    public bool CanDeleteSelectedProfile => SelectedProfile != null && !IsEditing && !SelectedProfile.IsDefault;

    /// <summary>
    /// Whether a profile can be activated.
    /// </summary>
    public bool CanActivateSelectedProfile => SelectedProfile != null && !IsEditing && ActiveProfileId != SelectedProfile.Id;

    partial void OnSelectedProfileChanged(NetworkProfile? value)
    {
        OnPropertyChanged(nameof(CanEditSelectedProfile));
        OnPropertyChanged(nameof(CanDeleteSelectedProfile));
        OnPropertyChanged(nameof(CanActivateSelectedProfile));
    }

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditSelectedProfile));
        OnPropertyChanged(nameof(CanDeleteSelectedProfile));
        OnPropertyChanged(nameof(CanActivateSelectedProfile));
    }

    partial void OnActiveProfileIdChanged(string? value)
    {
        OnPropertyChanged(nameof(CanActivateSelectedProfile));
    }

    /// <summary>
    /// Loads all profiles and current network information.
    /// </summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Loading profiles...";

        try
        {
            // Load profiles, current network, and auto-switch status in parallel
            var profilesTask = _serviceClient.GetNetworkProfilesAsync();
            var networkTask = _serviceClient.GetCurrentNetworkAsync();
            var statusTask = _serviceClient.GetAutoSwitchStatusAsync();

            await Task.WhenAll(profilesTask, networkTask, statusTask);

            var profilesResult = profilesTask.Result;
            var networkResult = networkTask.Result;
            var statusResult = statusTask.Result;

            if (profilesResult.IsSuccess && profilesResult.Value.Ok)
            {
                Profiles = new ObservableCollection<NetworkProfile>(profilesResult.Value.Profiles);
                ActiveProfileId = profilesResult.Value.ActiveProfileId;
            }
            else
            {
                var error = profilesResult.IsFailure
                    ? profilesResult.Error.Message
                    : profilesResult.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to load profiles: {error}";
                return;
            }

            if (networkResult.IsSuccess && networkResult.Value.Ok)
            {
                CurrentNetwork = networkResult.Value.Network;
                MatchingProfileId = networkResult.Value.MatchingProfileId;
            }

            if (statusResult.IsSuccess && statusResult.Value.Ok)
            {
                AutoSwitchEnabled = statusResult.Value.Enabled;
                ActiveProfileName = statusResult.Value.ActiveProfileName;
            }

            StatusMessage = $"Loaded {Profiles.Count} profiles";
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
    /// Refreshes the current network information.
    /// </summary>
    [RelayCommand]
    public async Task RefreshNetworkAsync()
    {
        try
        {
            var result = await _serviceClient.GetCurrentNetworkAsync();
            if (result.IsSuccess && result.Value.Ok)
            {
                CurrentNetwork = result.Value.Network;
                MatchingProfileId = result.Value.MatchingProfileId;
                StatusMessage = "Network information refreshed";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Toggles automatic profile switching.
    /// </summary>
    [RelayCommand]
    public async Task ToggleAutoSwitchAsync()
    {
        try
        {
            var newValue = !AutoSwitchEnabled;
            var result = await _serviceClient.SetAutoSwitchAsync(newValue);

            if (result.IsSuccess && result.Value.Ok)
            {
                AutoSwitchEnabled = newValue;
                StatusMessage = newValue ? "Automatic switching enabled" : "Automatic switching disabled";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                _dialogService.ShowError($"Failed to change auto-switch setting: {error}", "Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to change auto-switch setting: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Activates the selected profile manually.
    /// </summary>
    [RelayCommand]
    public async Task ActivateProfileAsync()
    {
        if (SelectedProfile == null)
            return;

        try
        {
            StatusMessage = $"Activating profile '{SelectedProfile.Name}'...";
            var result = await _serviceClient.ActivateProfileAsync(SelectedProfile.Id);

            if (result.IsSuccess && result.Value.Ok)
            {
                ActiveProfileId = result.Value.ActivatedProfileId;
                ActiveProfileName = SelectedProfile.Name;

                if (result.Value.PolicyApplied)
                {
                    StatusMessage = $"Profile '{SelectedProfile.Name}' activated and policy applied";
                    _dialogService.ShowInfo($"Profile '{SelectedProfile.Name}' activated successfully.", "Profile Activated");
                }
                else
                {
                    StatusMessage = $"Profile '{SelectedProfile.Name}' activated (no policy path)";
                }
                OnPropertyChanged(nameof(CanActivateSelectedProfile));
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to activate profile: {error}";
                _dialogService.ShowError($"Failed to activate profile: {error}", "Error");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Failed to activate profile: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Starts creating a new profile.
    /// </summary>
    [RelayCommand]
    public void AddProfile()
    {
        IsNewProfile = true;
        EditProfileId = Guid.NewGuid().ToString("N")[..8];
        EditProfileName = "New Profile";
        EditProfileDescription = string.Empty;
        EditProfilePolicyPath = string.Empty;
        EditProfilePriority = 100;
        EditProfileEnabled = true;
        EditProfileIsDefault = false;
        EditConditionsSsids = string.Empty;
        EditConditionsDnsSuffixes = string.Empty;
        EditConditionsNetworkNames = string.Empty;
        EditConditionsGateways = string.Empty;
        EditConditionsNetworkCategory = null;
        EditConditionsMatchAll = false;
        IsEditing = true;
    }

    /// <summary>
    /// Starts editing the selected profile.
    /// </summary>
    [RelayCommand]
    public void EditProfile()
    {
        if (SelectedProfile == null)
            return;

        IsNewProfile = false;
        EditProfileId = SelectedProfile.Id;
        EditProfileName = SelectedProfile.Name;
        EditProfileDescription = SelectedProfile.Description ?? string.Empty;
        EditProfilePolicyPath = SelectedProfile.PolicyPath ?? string.Empty;
        EditProfilePriority = SelectedProfile.Priority;
        EditProfileEnabled = SelectedProfile.Enabled;
        EditProfileIsDefault = SelectedProfile.IsDefault;
        EditConditionsSsids = string.Join(", ", SelectedProfile.Conditions.Ssids);
        EditConditionsDnsSuffixes = string.Join(", ", SelectedProfile.Conditions.DnsSuffixes);
        EditConditionsNetworkNames = string.Join(", ", SelectedProfile.Conditions.NetworkNames);
        EditConditionsGateways = string.Join(", ", SelectedProfile.Conditions.Gateways);
        EditConditionsNetworkCategory = SelectedProfile.Conditions.NetworkCategory;
        EditConditionsMatchAll = SelectedProfile.Conditions.MatchAll;
        IsEditing = true;
    }

    /// <summary>
    /// Saves the profile being edited.
    /// </summary>
    [RelayCommand]
    public async Task SaveProfileAsync()
    {
        if (!IsEditing)
            return;

        if (string.IsNullOrWhiteSpace(EditProfileName))
        {
            _dialogService.ShowError("Profile name is required.", "Validation Error");
            return;
        }

        try
        {
            StatusMessage = "Saving profile...";

            var profile = new NetworkProfile
            {
                Id = EditProfileId,
                Name = EditProfileName.Trim(),
                Description = string.IsNullOrWhiteSpace(EditProfileDescription) ? null : EditProfileDescription.Trim(),
                PolicyPath = string.IsNullOrWhiteSpace(EditProfilePolicyPath) ? null : EditProfilePolicyPath.Trim(),
                Priority = EditProfilePriority,
                Enabled = EditProfileEnabled,
                IsDefault = EditProfileIsDefault,
                Conditions = new ProfileConditions
                {
                    Ssids = ParseCommaSeparated(EditConditionsSsids),
                    DnsSuffixes = ParseCommaSeparated(EditConditionsDnsSuffixes),
                    NetworkNames = ParseCommaSeparated(EditConditionsNetworkNames),
                    Gateways = ParseCommaSeparated(EditConditionsGateways),
                    NetworkCategory = EditConditionsNetworkCategory,
                    MatchAll = EditConditionsMatchAll
                }
            };

            var result = await _serviceClient.SaveNetworkProfileAsync(profile);

            if (result.IsSuccess && result.Value.Ok)
            {
                IsEditing = false;
                StatusMessage = $"Profile '{profile.Name}' saved";
                _dialogService.ShowInfo($"Profile '{profile.Name}' saved successfully.", "Profile Saved");
                await LoadAsync();
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to save profile: {error}";
                _dialogService.ShowError($"Failed to save profile: {error}", "Error");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Failed to save profile: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Cancels editing.
    /// </summary>
    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        StatusMessage = "Edit cancelled";
    }

    /// <summary>
    /// Deletes the selected profile.
    /// </summary>
    [RelayCommand]
    public async Task DeleteProfileAsync()
    {
        if (SelectedProfile == null)
            return;

        if (SelectedProfile.IsDefault)
        {
            _dialogService.ShowError("Cannot delete the default profile.", "Error");
            return;
        }

        var confirm = _dialogService.Confirm(
            $"Are you sure you want to delete profile '{SelectedProfile.Name}'?",
            "Delete Profile");

        if (!confirm)
            return;

        try
        {
            StatusMessage = $"Deleting profile '{SelectedProfile.Name}'...";
            var result = await _serviceClient.DeleteNetworkProfileAsync(SelectedProfile.Id);

            if (result.IsSuccess && result.Value.Ok)
            {
                StatusMessage = $"Profile deleted";
                await LoadAsync();
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to delete profile: {error}";
                _dialogService.ShowError($"Failed to delete profile: {error}", "Error");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Failed to delete profile: {ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Copies the current network's SSID to the edit conditions.
    /// </summary>
    [RelayCommand]
    public void CopyCurrentSsid()
    {
        if (CurrentNetwork?.Ssid != null && IsEditing)
        {
            if (string.IsNullOrWhiteSpace(EditConditionsSsids))
            {
                EditConditionsSsids = CurrentNetwork.Ssid;
            }
            else
            {
                EditConditionsSsids = EditConditionsSsids + ", " + CurrentNetwork.Ssid;
            }
        }
    }

    /// <summary>
    /// Copies the current network's gateway to the edit conditions.
    /// </summary>
    [RelayCommand]
    public void CopyCurrentGateway()
    {
        if (CurrentNetwork?.Gateway != null && IsEditing)
        {
            if (string.IsNullOrWhiteSpace(EditConditionsGateways))
            {
                EditConditionsGateways = CurrentNetwork.Gateway;
            }
            else
            {
                EditConditionsGateways = EditConditionsGateways + ", " + CurrentNetwork.Gateway;
            }
        }
    }

    /// <summary>
    /// Copies the current network's DNS suffix to the edit conditions.
    /// </summary>
    [RelayCommand]
    public void CopyCurrentDnsSuffix()
    {
        if (CurrentNetwork?.DnsSuffix != null && IsEditing)
        {
            if (string.IsNullOrWhiteSpace(EditConditionsDnsSuffixes))
            {
                EditConditionsDnsSuffixes = CurrentNetwork.DnsSuffix;
            }
            else
            {
                EditConditionsDnsSuffixes = EditConditionsDnsSuffixes + ", " + CurrentNetwork.DnsSuffix;
            }
        }
    }

    /// <summary>
    /// Browses for a policy file.
    /// </summary>
    [RelayCommand]
    public void BrowsePolicyFile()
    {
        var path = _dialogService.ShowOpenFileDialog(
            "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            "Select Policy File");

        if (!string.IsNullOrEmpty(path))
        {
            EditProfilePolicyPath = path;
        }
    }

    private static List<string> ParseCommaSeparated(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
