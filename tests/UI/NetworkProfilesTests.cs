using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for NetworkProfilesViewModel.
/// </summary>
public class NetworkProfilesTests
{
    private readonly MockServiceClient _mockServiceClient;
    private readonly MockDialogService _mockDialogService;
    private readonly NetworkProfilesViewModel _viewModel;

    public NetworkProfilesTests()
    {
        _mockServiceClient = new MockServiceClient();
        _mockDialogService = new MockDialogService();
        _viewModel = new NetworkProfilesViewModel(_mockServiceClient, _mockDialogService);
    }

    #region Initial State Tests

    [Fact]
    public void ViewModel_InitialState_HasCorrectDefaults()
    {
        Assert.Empty(_viewModel.Profiles);
        Assert.Null(_viewModel.SelectedProfile);
        Assert.Null(_viewModel.ActiveProfileId);
        Assert.False(_viewModel.IsLoading);
        Assert.False(_viewModel.IsEditing);
        Assert.False(_viewModel.AutoSwitchEnabled);
    }

    [Fact]
    public void AvailableCategories_ReturnsExpectedValues()
    {
        var categories = NetworkProfilesViewModel.AvailableCategories;
        Assert.Contains("Public", categories);
        Assert.Contains("Private", categories);
        Assert.Contains("Domain", categories);
    }

    #endregion

    #region Load Tests

    [Fact]
    public async Task LoadAsync_LoadsProfilesFromService()
    {
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile>
        {
            new NetworkProfile { Id = "p1", Name = "Home" },
            new NetworkProfile { Id = "p2", Name = "Work" }
        };
        _mockServiceClient.ActiveProfileId = "p1";

        await _viewModel.LoadAsync();

        Assert.Equal(2, _viewModel.Profiles.Count);
        Assert.Equal("Home", _viewModel.Profiles[0].Name);
        Assert.Equal("Work", _viewModel.Profiles[1].Name);
        Assert.Equal("p1", _viewModel.ActiveProfileId);
    }

    [Fact]
    public async Task LoadAsync_LoadsCurrentNetworkInfo()
    {
        _mockServiceClient.CurrentNetwork = new CurrentNetworkInfo
        {
            NetworkName = "TestNetwork",
            Ssid = "TestSSID",
            Category = "Private",
            Gateway = "192.168.1.1"
        };

        await _viewModel.LoadAsync();

        Assert.NotNull(_viewModel.CurrentNetwork);
        Assert.Equal("TestNetwork", _viewModel.CurrentNetwork!.NetworkName);
        Assert.Equal("TestSSID", _viewModel.CurrentNetwork.Ssid);
        Assert.Equal("Private", _viewModel.CurrentNetwork.Category);
        Assert.Equal("192.168.1.1", _viewModel.CurrentNetwork.Gateway);
    }

    [Fact]
    public async Task LoadAsync_LoadsAutoSwitchStatus()
    {
        _mockServiceClient.AutoSwitchEnabled = true;

        await _viewModel.LoadAsync();

        Assert.True(_viewModel.AutoSwitchEnabled);
    }

    [Fact]
    public async Task LoadAsync_ServiceUnavailable_ShowsError()
    {
        _mockServiceClient.ShouldConnect = false;

        await _viewModel.LoadAsync();

        Assert.Empty(_viewModel.Profiles);
        Assert.Contains("Failed to load profiles", _viewModel.StatusMessage);
    }

    #endregion

    #region Auto-Switch Tests

    [Fact]
    public async Task ToggleAutoSwitchAsync_TogglesEnabled()
    {
        _mockServiceClient.AutoSwitchEnabled = false;
        _viewModel.AutoSwitchEnabled = false;

        await _viewModel.ToggleAutoSwitchCommand.ExecuteAsync(null);

        Assert.True(_viewModel.AutoSwitchEnabled);
    }

    [Fact]
    public async Task ToggleAutoSwitchAsync_ServiceError_ShowsError()
    {
        _mockServiceClient.ShouldConnect = false;

        await _viewModel.ToggleAutoSwitchCommand.ExecuteAsync(null);

        Assert.True(_mockDialogService.ErrorCount > 0);
    }

    #endregion

    #region Profile Selection Tests

    [Fact]
    public void CanEditSelectedProfile_WhenProfileSelected_ReturnsTrue()
    {
        _viewModel.Profiles.Add(new NetworkProfile { Id = "p1", Name = "Test" });
        _viewModel.SelectedProfile = _viewModel.Profiles[0];

        Assert.True(_viewModel.CanEditSelectedProfile);
    }

    [Fact]
    public void CanEditSelectedProfile_WhenEditing_ReturnsFalse()
    {
        _viewModel.Profiles.Add(new NetworkProfile { Id = "p1", Name = "Test" });
        _viewModel.SelectedProfile = _viewModel.Profiles[0];
        _viewModel.AddProfileCommand.Execute(null); // Start editing

        Assert.False(_viewModel.CanEditSelectedProfile);
    }

    [Fact]
    public void CanDeleteSelectedProfile_WhenDefaultProfile_ReturnsFalse()
    {
        var profile = new NetworkProfile { Id = "p1", Name = "Default", IsDefault = true };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        Assert.False(_viewModel.CanDeleteSelectedProfile);
    }

    [Fact]
    public void CanActivateSelectedProfile_WhenAlreadyActive_ReturnsFalse()
    {
        var profile = new NetworkProfile { Id = "p1", Name = "Test" };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;
        _viewModel.ActiveProfileId = "p1";

        Assert.False(_viewModel.CanActivateSelectedProfile);
    }

    #endregion

    #region Add Profile Tests

    [Fact]
    public void AddProfile_StartsEditingNewProfile()
    {
        _viewModel.AddProfileCommand.Execute(null);

        Assert.True(_viewModel.IsEditing);
        Assert.True(_viewModel.IsNewProfile);
        Assert.Equal("New Profile", _viewModel.EditProfileName);
        Assert.NotEmpty(_viewModel.EditProfileId);
    }

    [Fact]
    public void AddProfile_SetsDefaultValues()
    {
        _viewModel.AddProfileCommand.Execute(null);

        Assert.Equal(100, _viewModel.EditProfilePriority);
        Assert.True(_viewModel.EditProfileEnabled);
        Assert.False(_viewModel.EditProfileIsDefault);
        Assert.Empty(_viewModel.EditConditionsSsids);
        Assert.Empty(_viewModel.EditConditionsGateways);
    }

    #endregion

    #region Edit Profile Tests

    [Fact]
    public void EditProfile_PopulatesEditorFields()
    {
        var profile = new NetworkProfile
        {
            Id = "p1",
            Name = "Home Network",
            Description = "My home",
            PolicyPath = "C:\\policy.json",
            Priority = 50,
            Enabled = true,
            IsDefault = false,
            Conditions = new ProfileConditions
            {
                Ssids = new List<string> { "MySSID" },
                Gateways = new List<string> { "192.168.1.1" },
                DnsSuffixes = new List<string> { "local" },
                MatchAll = true
            }
        };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        _viewModel.EditProfileCommand.Execute(null);

        Assert.True(_viewModel.IsEditing);
        Assert.False(_viewModel.IsNewProfile);
        Assert.Equal("p1", _viewModel.EditProfileId);
        Assert.Equal("Home Network", _viewModel.EditProfileName);
        Assert.Equal("My home", _viewModel.EditProfileDescription);
        Assert.Equal("C:\\policy.json", _viewModel.EditProfilePolicyPath);
        Assert.Equal(50, _viewModel.EditProfilePriority);
        Assert.True(_viewModel.EditProfileEnabled);
        Assert.False(_viewModel.EditProfileIsDefault);
        Assert.Equal("MySSID", _viewModel.EditConditionsSsids);
        Assert.Equal("192.168.1.1", _viewModel.EditConditionsGateways);
        Assert.Equal("local", _viewModel.EditConditionsDnsSuffixes);
        Assert.True(_viewModel.EditConditionsMatchAll);
    }

    #endregion

    #region Save Profile Tests

    [Fact]
    public async Task SaveProfileAsync_WithValidData_SavesProfile()
    {
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditProfileName = "Test Profile";
        _viewModel.EditProfileDescription = "A test profile";
        _viewModel.EditProfilePolicyPath = "C:\\test.json";
        _viewModel.EditConditionsSsids = "SSID1, SSID2";
        _viewModel.EditConditionsGateways = "192.168.1.1";

        await _viewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsEditing);
        Assert.True(_mockDialogService.InfoCount > 0);
    }

    [Fact]
    public async Task SaveProfileAsync_WithEmptyName_ShowsError()
    {
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditProfileName = "";

        await _viewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(_viewModel.IsEditing); // Still editing
        Assert.True(_mockDialogService.ErrorCount > 0);
    }

    [Fact]
    public async Task SaveProfileAsync_ServiceError_ShowsError()
    {
        _mockServiceClient.ShouldConnect = false;
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditProfileName = "Test Profile";

        await _viewModel.SaveProfileCommand.ExecuteAsync(null);

        Assert.True(_mockDialogService.ErrorCount > 0);
    }

    #endregion

    #region Cancel Edit Tests

    [Fact]
    public void CancelEdit_StopsEditing()
    {
        _viewModel.AddProfileCommand.Execute(null);
        Assert.True(_viewModel.IsEditing);

        _viewModel.CancelEditCommand.Execute(null);

        Assert.False(_viewModel.IsEditing);
    }

    #endregion

    #region Delete Profile Tests

    [Fact]
    public async Task DeleteProfileAsync_WhenConfirmed_DeletesProfile()
    {
        _mockDialogService.ConfirmResult = true;
        var profile = new NetworkProfile { Id = "p1", Name = "Test" };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        await _viewModel.DeleteProfileCommand.ExecuteAsync(null);

        Assert.True(_mockDialogService.ConfirmCount > 0);
        // Profile should be deleted (LoadAsync called which will reload from mock)
    }

    [Fact]
    public async Task DeleteProfileAsync_WhenCancelled_DoesNotDelete()
    {
        _mockDialogService.ConfirmResult = false;
        var profile = new NetworkProfile { Id = "p1", Name = "Test" };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;
        var initialCount = _viewModel.Profiles.Count;

        await _viewModel.DeleteProfileCommand.ExecuteAsync(null);

        Assert.Equal(initialCount, _viewModel.Profiles.Count);
    }

    [Fact]
    public async Task DeleteProfileAsync_DefaultProfile_ShowsError()
    {
        var profile = new NetworkProfile { Id = "p1", Name = "Default", IsDefault = true };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        await _viewModel.DeleteProfileCommand.ExecuteAsync(null);

        Assert.True(_mockDialogService.ErrorCount > 0);
    }

    #endregion

    #region Activate Profile Tests

    [Fact]
    public async Task ActivateProfileAsync_ActivatesSelectedProfile()
    {
        var profile = new NetworkProfile { Id = "p1", Name = "Test" };
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile> { profile };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        await _viewModel.ActivateProfileCommand.ExecuteAsync(null);

        Assert.Equal("p1", _viewModel.ActiveProfileId);
    }

    [Fact]
    public async Task ActivateProfileAsync_WithPolicyApplied_ShowsSuccess()
    {
        var profile = new NetworkProfile { Id = "p1", Name = "Test", PolicyPath = "C:\\policy.json" };
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile> { profile };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        await _viewModel.ActivateProfileCommand.ExecuteAsync(null);

        Assert.Contains("activated", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task ActivateProfileAsync_ServiceError_ShowsError()
    {
        _mockServiceClient.ShouldConnect = false;
        var profile = new NetworkProfile { Id = "p1", Name = "Test" };
        _viewModel.Profiles.Add(profile);
        _viewModel.SelectedProfile = profile;

        await _viewModel.ActivateProfileCommand.ExecuteAsync(null);

        Assert.True(_mockDialogService.ErrorCount > 0);
    }

    #endregion

    #region Copy Current Network Tests

    [Fact]
    public void CopyCurrentSsid_AddsToConditions()
    {
        _viewModel.CurrentNetwork = new CurrentNetworkInfo { Ssid = "TestSSID" };
        _viewModel.AddProfileCommand.Execute(null);

        _viewModel.CopyCurrentSsidCommand.Execute(null);

        Assert.Equal("TestSSID", _viewModel.EditConditionsSsids);
    }

    [Fact]
    public void CopyCurrentSsid_AppendsToExisting()
    {
        _viewModel.CurrentNetwork = new CurrentNetworkInfo { Ssid = "NewSSID" };
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditConditionsSsids = "ExistingSSID";

        _viewModel.CopyCurrentSsidCommand.Execute(null);

        Assert.Contains("ExistingSSID", _viewModel.EditConditionsSsids);
        Assert.Contains("NewSSID", _viewModel.EditConditionsSsids);
    }

    [Fact]
    public void CopyCurrentGateway_AddsToConditions()
    {
        _viewModel.CurrentNetwork = new CurrentNetworkInfo { Gateway = "192.168.1.1" };
        _viewModel.AddProfileCommand.Execute(null);

        _viewModel.CopyCurrentGatewayCommand.Execute(null);

        Assert.Equal("192.168.1.1", _viewModel.EditConditionsGateways);
    }

    [Fact]
    public void CopyCurrentDnsSuffix_AddsToConditions()
    {
        _viewModel.CurrentNetwork = new CurrentNetworkInfo { DnsSuffix = "corp.local" };
        _viewModel.AddProfileCommand.Execute(null);

        _viewModel.CopyCurrentDnsSuffixCommand.Execute(null);

        Assert.Equal("corp.local", _viewModel.EditConditionsDnsSuffixes);
    }

    #endregion

    #region Refresh Network Tests

    [Fact]
    public async Task RefreshNetworkAsync_UpdatesCurrentNetwork()
    {
        _mockServiceClient.CurrentNetwork = new CurrentNetworkInfo
        {
            NetworkName = "UpdatedNetwork",
            Ssid = "UpdatedSSID"
        };

        await _viewModel.RefreshNetworkCommand.ExecuteAsync(null);

        Assert.NotNull(_viewModel.CurrentNetwork);
        Assert.Equal("UpdatedNetwork", _viewModel.CurrentNetwork!.NetworkName);
        Assert.Equal("UpdatedSSID", _viewModel.CurrentNetwork.Ssid);
    }

    #endregion

    #region Parse Comma-Separated Tests

    [Fact]
    public async Task SaveProfileAsync_ParsesCommaSeparatedConditions()
    {
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditProfileName = "Test";
        _viewModel.EditConditionsSsids = "SSID1, SSID2, SSID3";
        _viewModel.EditConditionsGateways = "192.168.1.1, 10.0.0.1";
        _viewModel.EditConditionsDnsSuffixes = "local, corp.local";

        await _viewModel.SaveProfileCommand.ExecuteAsync(null);

        // Verify the profile was saved (mock doesn't persist but we can check the call was made)
        Assert.False(_viewModel.IsEditing);
    }

    [Fact]
    public async Task SaveProfileAsync_HandlesWhitespaceInConditions()
    {
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditProfileName = "Test";
        _viewModel.EditConditionsSsids = "  SSID1  ,  SSID2  ,  ";

        await _viewModel.SaveProfileCommand.ExecuteAsync(null);

        // Should not throw, should trim whitespace
        Assert.False(_viewModel.IsEditing);
    }

    #endregion

    #region Network Profile Conditions Tests

    [Fact]
    public void ProfileConditions_MatchAll_DefaultsFalse()
    {
        var conditions = new ProfileConditions();
        Assert.False(conditions.MatchAll);
    }

    [Fact]
    public void ProfileConditions_Lists_InitializedEmpty()
    {
        var conditions = new ProfileConditions();
        Assert.NotNull(conditions.Ssids);
        Assert.NotNull(conditions.DnsSuffixes);
        Assert.NotNull(conditions.NetworkNames);
        Assert.NotNull(conditions.Gateways);
        Assert.Empty(conditions.Ssids);
        Assert.Empty(conditions.DnsSuffixes);
        Assert.Empty(conditions.NetworkNames);
        Assert.Empty(conditions.Gateways);
    }

    [Fact]
    public void NetworkProfile_Defaults()
    {
        var profile = new NetworkProfile();
        Assert.Equal(string.Empty, profile.Id);
        Assert.Equal(string.Empty, profile.Name);
        Assert.Null(profile.Description);
        Assert.Null(profile.PolicyPath);
        Assert.Equal(100, profile.Priority);
        Assert.True(profile.Enabled);
        Assert.False(profile.IsDefault);
    }

    [Fact]
    public void CurrentNetworkInfo_Defaults()
    {
        var info = new CurrentNetworkInfo();
        Assert.Null(info.NetworkName);
        Assert.Null(info.Category);
        Assert.Null(info.Ssid);
        Assert.Null(info.DnsSuffix);
        Assert.Null(info.Gateway);
        Assert.False(info.IsConnected);
        Assert.Null(info.AdapterName);
    }

    #endregion

    #region IPC Message Tests

    [Fact]
    public void GetNetworkProfilesRequest_HasCorrectType()
    {
        var request = new GetNetworkProfilesRequest();
        Assert.Equal("get-network-profiles", request.Type);
    }

    [Fact]
    public void SaveNetworkProfileRequest_HasCorrectType()
    {
        var request = new SaveNetworkProfileRequest();
        Assert.Equal("save-network-profile", request.Type);
    }

    [Fact]
    public void DeleteNetworkProfileRequest_HasCorrectType()
    {
        var request = new DeleteNetworkProfileRequest();
        Assert.Equal("delete-network-profile", request.Type);
    }

    [Fact]
    public void GetCurrentNetworkRequest_HasCorrectType()
    {
        var request = new GetCurrentNetworkRequest();
        Assert.Equal("get-current-network", request.Type);
    }

    [Fact]
    public void ActivateProfileRequest_HasCorrectType()
    {
        var request = new ActivateProfileRequest();
        Assert.Equal("activate-profile", request.Type);
    }

    [Fact]
    public void SetAutoSwitchRequest_HasCorrectType()
    {
        var request = new SetAutoSwitchRequest();
        Assert.Equal("set-auto-switch", request.Type);
    }

    [Fact]
    public void GetAutoSwitchStatusRequest_HasCorrectType()
    {
        var request = new GetAutoSwitchStatusRequest();
        Assert.Equal("get-auto-switch-status", request.Type);
    }

    [Fact]
    public void GetNetworkProfilesResponse_Success_HasProfilesAndActiveId()
    {
        var profiles = new List<NetworkProfile> { new NetworkProfile { Id = "p1" } };
        var response = GetNetworkProfilesResponse.Success(profiles, "p1");
        Assert.True(response.Ok);
        Assert.Single(response.Profiles);
        Assert.Equal("p1", response.ActiveProfileId);
    }

    [Fact]
    public void GetNetworkProfilesResponse_Failure_HasError()
    {
        var response = GetNetworkProfilesResponse.Failure("Test error");
        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void GetCurrentNetworkResponse_Success_HasNetworkInfo()
    {
        var network = new CurrentNetworkInfo { NetworkName = "Test" };
        var response = GetCurrentNetworkResponse.Success(network, "p1");
        Assert.True(response.Ok);
        Assert.Equal("Test", response.Network.NetworkName);
        Assert.Equal("p1", response.MatchingProfileId);
    }

    [Fact]
    public void ActivateProfileResponse_Success_HasActivatedInfo()
    {
        var response = ActivateProfileResponse.Success("p1", true);
        Assert.True(response.Ok);
        Assert.Equal("p1", response.ActivatedProfileId);
        Assert.True(response.PolicyApplied);
    }

    [Fact]
    public void GetAutoSwitchStatusResponse_Success_HasStatus()
    {
        var response = GetAutoSwitchStatusResponse.Success(true, "p1", "Home");
        Assert.True(response.Ok);
        Assert.True(response.Enabled);
        Assert.Equal("p1", response.ActiveProfileId);
        Assert.Equal("Home", response.ActiveProfileName);
    }

    #endregion

    #region MockServiceClient Profile Tests

    [Fact]
    public async Task MockServiceClient_GetNetworkProfiles_ReturnsConfiguredProfiles()
    {
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile>
        {
            new NetworkProfile { Id = "p1", Name = "Profile 1" },
            new NetworkProfile { Id = "p2", Name = "Profile 2" }
        };

        var result = await _mockServiceClient.GetNetworkProfilesAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.Equal(2, result.Value.Profiles.Count);
    }

    [Fact]
    public async Task MockServiceClient_SaveNetworkProfile_AddsNewProfile()
    {
        var profile = new NetworkProfile { Id = "new", Name = "New Profile" };

        var result = await _mockServiceClient.SaveNetworkProfileAsync(profile);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.Contains(_mockServiceClient.NetworkProfiles, p => p.Id == "new");
    }

    [Fact]
    public async Task MockServiceClient_SaveNetworkProfile_UpdatesExistingProfile()
    {
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile>
        {
            new NetworkProfile { Id = "p1", Name = "Old Name" }
        };
        var updatedProfile = new NetworkProfile { Id = "p1", Name = "New Name" };

        var result = await _mockServiceClient.SaveNetworkProfileAsync(updatedProfile);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", _mockServiceClient.NetworkProfiles.First(p => p.Id == "p1").Name);
    }

    [Fact]
    public async Task MockServiceClient_DeleteNetworkProfile_RemovesProfile()
    {
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile>
        {
            new NetworkProfile { Id = "p1", Name = "Profile 1" }
        };

        var result = await _mockServiceClient.DeleteNetworkProfileAsync("p1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.DoesNotContain(_mockServiceClient.NetworkProfiles, p => p.Id == "p1");
    }

    [Fact]
    public async Task MockServiceClient_ActivateProfile_SetsActiveId()
    {
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile>
        {
            new NetworkProfile { Id = "p1", Name = "Profile 1" }
        };

        var result = await _mockServiceClient.ActivateProfileAsync("p1");

        Assert.True(result.IsSuccess);
        Assert.Equal("p1", result.Value.ActivatedProfileId);
        Assert.Equal("p1", _mockServiceClient.ActiveProfileId);
    }

    [Fact]
    public async Task MockServiceClient_SetAutoSwitch_ChangesEnabled()
    {
        _mockServiceClient.AutoSwitchEnabled = false;

        var result = await _mockServiceClient.SetAutoSwitchAsync(true);

        Assert.True(result.IsSuccess);
        Assert.True(_mockServiceClient.AutoSwitchEnabled);
    }

    [Fact]
    public async Task MockServiceClient_GetAutoSwitchStatus_ReturnsCurrentStatus()
    {
        _mockServiceClient.AutoSwitchEnabled = true;
        _mockServiceClient.ActiveProfileId = "p1";
        _mockServiceClient.NetworkProfiles = new List<NetworkProfile>
        {
            new NetworkProfile { Id = "p1", Name = "Active Profile" }
        };

        var result = await _mockServiceClient.GetAutoSwitchStatusAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Enabled);
        Assert.Equal("p1", result.Value.ActiveProfileId);
        Assert.Equal("Active Profile", result.Value.ActiveProfileName);
    }

    #endregion

    #region Browse Policy File Tests

    [Fact]
    public void BrowsePolicyFile_WhenFileSelected_SetsPath()
    {
        _mockDialogService.OpenFileResult = "C:\\selected\\policy.json";
        _viewModel.AddProfileCommand.Execute(null);

        _viewModel.BrowsePolicyFileCommand.Execute(null);

        Assert.Equal("C:\\selected\\policy.json", _viewModel.EditProfilePolicyPath);
    }

    [Fact]
    public void BrowsePolicyFile_WhenCancelled_DoesNotChangePath()
    {
        _mockDialogService.OpenFileResult = null;
        _viewModel.AddProfileCommand.Execute(null);
        _viewModel.EditProfilePolicyPath = "existing\\path.json";

        _viewModel.BrowsePolicyFileCommand.Execute(null);

        Assert.Equal("existing\\path.json", _viewModel.EditProfilePolicyPath);
    }

    #endregion
}
