using WfpTrafficControl.Tests.UI;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for application discovery functionality.
/// </summary>
public class ApplicationDiscoveryTests
{
    private readonly MockServiceClient _mockServiceClient;
    private readonly MockDialogService _mockDialogService;
    private readonly TestApplicationDiscoveryService _discoveryService;

    public ApplicationDiscoveryTests()
    {
        _mockServiceClient = new MockServiceClient();
        _mockDialogService = new MockDialogService();
        _discoveryService = new TestApplicationDiscoveryService();
    }

    #region DiscoveredApplication Tests

    [Fact]
    public void DiscoveredApplication_DefaultValues_AreCorrect()
    {
        var app = new DiscoveredApplication();

        Assert.Equal(string.Empty, app.Id);
        Assert.Equal(string.Empty, app.Name);
        Assert.Equal(string.Empty, app.Publisher);
        Assert.Null(app.InstallPath);
        Assert.Null(app.ExecutablePath);
        Assert.Null(app.Version);
        Assert.Null(app.InstallDate);
        Assert.Equal(ApplicationCoverageStatus.Unknown, app.CoverageStatus);
        Assert.False(app.IsKnownApplication);
        Assert.Null(app.KnownApplicationId);
        Assert.Empty(app.MatchingRules);
        Assert.Empty(app.SuggestedRules);
    }

    [Fact]
    public void DiscoveredApplication_CanSetProperties()
    {
        var app = new DiscoveredApplication
        {
            Id = "test-app",
            Name = "Test Application",
            Publisher = "Test Publisher",
            InstallPath = @"C:\Program Files\Test",
            ExecutablePath = @"C:\Program Files\Test\test.exe",
            Version = "1.0.0",
            InstallDate = DateTime.Now,
            CoverageStatus = ApplicationCoverageStatus.FullyCovered,
            IsKnownApplication = true,
            KnownApplicationId = "known-test"
        };

        Assert.Equal("test-app", app.Id);
        Assert.Equal("Test Application", app.Name);
        Assert.Equal("Test Publisher", app.Publisher);
        Assert.Equal(@"C:\Program Files\Test", app.InstallPath);
        Assert.Equal(@"C:\Program Files\Test\test.exe", app.ExecutablePath);
        Assert.Equal("1.0.0", app.Version);
        Assert.NotNull(app.InstallDate);
        Assert.Equal(ApplicationCoverageStatus.FullyCovered, app.CoverageStatus);
        Assert.True(app.IsKnownApplication);
        Assert.Equal("known-test", app.KnownApplicationId);
    }

    #endregion

    #region SuggestedRule Tests

    [Fact]
    public void SuggestedRule_DefaultValues_AreCorrect()
    {
        var rule = new SuggestedRule();

        Assert.Equal(string.Empty, rule.Id);
        Assert.Equal(string.Empty, rule.Description);
        Assert.Equal("allow", rule.Action);
        Assert.Equal("outbound", rule.Direction);
        Assert.Equal("tcp", rule.Protocol);
        Assert.Null(rule.RemoteIp);
        Assert.Null(rule.RemotePorts);
        Assert.Null(rule.Comment);
        Assert.Equal(SuggestionPriority.Normal, rule.Priority);
    }

    [Fact]
    public void SuggestedRule_CanSetProperties()
    {
        var rule = new SuggestedRule
        {
            Id = "test-rule",
            Description = "Test Rule Description",
            Action = "block",
            Direction = "inbound",
            Protocol = "udp",
            RemoteIp = "192.168.1.0/24",
            RemotePorts = new List<int> { 80, 443 },
            Comment = "Test comment",
            Priority = SuggestionPriority.High
        };

        Assert.Equal("test-rule", rule.Id);
        Assert.Equal("Test Rule Description", rule.Description);
        Assert.Equal("block", rule.Action);
        Assert.Equal("inbound", rule.Direction);
        Assert.Equal("udp", rule.Protocol);
        Assert.Equal("192.168.1.0/24", rule.RemoteIp);
        Assert.Contains(80, rule.RemotePorts);
        Assert.Contains(443, rule.RemotePorts);
        Assert.Equal("Test comment", rule.Comment);
        Assert.Equal(SuggestionPriority.High, rule.Priority);
    }

    #endregion

    #region ApplicationSignature Tests

    [Fact]
    public void ApplicationSignature_DefaultValues_AreCorrect()
    {
        var sig = new ApplicationSignature();

        Assert.Equal(string.Empty, sig.Id);
        Assert.Equal(string.Empty, sig.Name);
        Assert.Equal(string.Empty, sig.Publisher);
        Assert.Empty(sig.ExecutablePatterns);
        Assert.Empty(sig.DefaultRules);
        Assert.Null(sig.Description);
        Assert.Null(sig.Category);
    }

    [Fact]
    public void ApplicationSignature_CanSetProperties()
    {
        var sig = new ApplicationSignature
        {
            Id = "chrome",
            Name = "Google Chrome",
            Publisher = "Google",
            ExecutablePatterns = new List<string> { "chrome.exe" },
            Description = "Web browser",
            Category = "Web Browser"
        };

        Assert.Equal("chrome", sig.Id);
        Assert.Equal("Google Chrome", sig.Name);
        Assert.Equal("Google", sig.Publisher);
        Assert.Contains("chrome.exe", sig.ExecutablePatterns);
        Assert.Equal("Web browser", sig.Description);
        Assert.Equal("Web Browser", sig.Category);
    }

    #endregion

    #region ApplicationCoverageStatus Tests

    [Fact]
    public void ApplicationCoverageStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)ApplicationCoverageStatus.Unknown);
        Assert.Equal(1, (int)ApplicationCoverageStatus.Uncovered);
        Assert.Equal(2, (int)ApplicationCoverageStatus.PartiallyCovered);
        Assert.Equal(3, (int)ApplicationCoverageStatus.FullyCovered);
    }

    #endregion

    #region SuggestionPriority Tests

    [Fact]
    public void SuggestionPriority_HasExpectedValues()
    {
        Assert.Equal(0, (int)SuggestionPriority.Low);
        Assert.Equal(1, (int)SuggestionPriority.Normal);
        Assert.Equal(2, (int)SuggestionPriority.High);
        Assert.Equal(3, (int)SuggestionPriority.Critical);
    }

    #endregion

    #region IApplicationDiscoveryService Tests

    [Fact]
    public async Task DiscoverApplicationsAsync_ReturnsApplications()
    {
        var apps = await _discoveryService.DiscoverApplicationsAsync();

        Assert.NotNull(apps);
        Assert.NotEmpty(apps);
    }

    [Fact]
    public void GetSuggestedRules_ValidId_ReturnsRules()
    {
        var rules = _discoveryService.GetSuggestedRules("chrome");

        Assert.NotNull(rules);
        Assert.NotEmpty(rules);
    }

    [Fact]
    public void GetSuggestedRules_InvalidId_ReturnsEmptyList()
    {
        var rules = _discoveryService.GetSuggestedRules("nonexistent");

        Assert.NotNull(rules);
        Assert.Empty(rules);
    }

    [Fact]
    public void GetApplicationSignatures_ReturnsSignatures()
    {
        var signatures = _discoveryService.GetApplicationSignatures();

        Assert.NotNull(signatures);
        Assert.NotEmpty(signatures);
    }

    [Fact]
    public void GetApplicationSignatures_ContainsKnownApps()
    {
        var signatures = _discoveryService.GetApplicationSignatures();

        var chrome = signatures.FirstOrDefault(s => s.Id == "chrome");
        Assert.NotNull(chrome);
        Assert.Equal("Google Chrome", chrome.Name);
        Assert.Equal("Web Browser", chrome.Category);
    }

    #endregion

    #region ApplicationDiscoveryViewModel Tests

    [Fact]
    public void ViewModel_InitialState_IsCorrect()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);

        Assert.Empty(vm.Applications);
        Assert.Empty(vm.FilteredApplications);
        Assert.Null(vm.SelectedApplication);
        Assert.False(vm.IsScanning);
        Assert.Equal(string.Empty, vm.StatusMessage);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal("All Categories", vm.SelectedCategoryFilter);
        Assert.Equal("All", vm.SelectedCoverageFilter);
        Assert.Equal(0, vm.TotalApplications);
        Assert.Equal(0, vm.CoveredApplications);
        Assert.Equal(0, vm.UncoveredApplications);
        Assert.Equal(0, vm.KnownApplications);
    }

    [Fact]
    public void ViewModel_AvailableCategoryFilters_HasExpectedOptions()
    {
        var filters = ApplicationDiscoveryViewModel.AvailableCategoryFilters;

        Assert.Contains("All Categories", filters);
        Assert.Contains("Web Browser", filters);
        Assert.Contains("Communication", filters);
        Assert.Contains("Development", filters);
        Assert.Contains("Cloud Storage", filters);
        Assert.Contains("Gaming", filters);
        Assert.Contains("Media", filters);
    }

    [Fact]
    public void ViewModel_AvailableCoverageFilters_HasExpectedOptions()
    {
        var filters = ApplicationDiscoveryViewModel.AvailableCoverageFilters;

        Assert.Contains("All", filters);
        Assert.Contains("Uncovered", filters);
        Assert.Contains("Partially Covered", filters);
        Assert.Contains("Fully Covered", filters);
        Assert.Contains("Known Apps Only", filters);
    }

    [Fact]
    public async Task ScanAsync_DiscoversApplications()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);

        await vm.ScanAsync();

        Assert.NotEmpty(vm.Applications);
        Assert.NotEmpty(vm.FilteredApplications);
    }

    [Fact]
    public async Task ScanAsync_UpdatesStatistics()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);

        await vm.ScanAsync();

        Assert.True(vm.TotalApplications > 0);
    }

    [Fact]
    public async Task ScanAsync_SetsStatusMessage()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);

        await vm.ScanAsync();

        Assert.Contains("Found", vm.StatusMessage);
    }

    [Fact]
    public async Task SearchText_FiltersApplications()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        await vm.ScanAsync();

        var originalCount = vm.FilteredApplications.Count;
        vm.SearchText = "Chrome";

        Assert.True(vm.FilteredApplications.Count <= originalCount);
    }

    [Fact]
    public async Task CategoryFilter_FiltersApplications()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        await vm.ScanAsync();

        var originalCount = vm.FilteredApplications.Count;
        vm.SelectedCategoryFilter = "Web Browser";

        Assert.True(vm.FilteredApplications.Count <= originalCount);
    }

    [Fact]
    public async Task CoverageFilter_FiltersApplications()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        await vm.ScanAsync();

        var originalCount = vm.FilteredApplications.Count;
        vm.SelectedCoverageFilter = "Known Apps Only";

        Assert.True(vm.FilteredApplications.Count <= originalCount);
    }

    [Fact]
    public async Task SelectApplication_SetsSelectedProperty()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        await vm.ScanAsync();

        var firstApp = vm.Applications.First();
        vm.SelectedApplication = firstApp;

        Assert.Equal(firstApp, vm.SelectedApplication);
    }

    [Fact]
    public void CopyPath_WithNoSelection_DoesNothing()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        vm.SelectedApplication = null;

        // Should not throw
        vm.CopyPathCommand.Execute(null);
    }

    [Fact]
    public void ShowSignatures_ShowsDialog()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);

        vm.ShowSignaturesCommand.Execute(null);

        Assert.Equal(1, _mockDialogService.InfoCount);
    }

    [Fact]
    public async Task ApplySuggestedRules_NoSelection_ShowsWarning()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        vm.SelectedApplication = null;

        await vm.ApplySuggestedRulesCommand.ExecuteAsync(null);

        Assert.Equal(1, _mockDialogService.WarningCount);
    }

    [Fact]
    public async Task ApplySuggestedRules_NoSuggestedRules_ShowsWarning()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        vm.SelectedApplication = new DiscoveredApplication
        {
            Id = "test",
            Name = "Test App",
            SuggestedRules = new List<SuggestedRule>()
        };

        await vm.ApplySuggestedRulesCommand.ExecuteAsync(null);

        Assert.Equal(1, _mockDialogService.WarningCount);
    }

    [Fact]
    public async Task ApplySuggestedRules_UserCancels_DoesNothing()
    {
        var vm = new ApplicationDiscoveryViewModel(_discoveryService, _mockServiceClient, _mockDialogService);
        vm.SelectedApplication = new DiscoveredApplication
        {
            Id = "chrome",
            Name = "Google Chrome",
            ExecutablePath = @"C:\Program Files\Google\Chrome\chrome.exe",
            SuggestedRules = new List<SuggestedRule>
            {
                new SuggestedRule
                {
                    Id = "chrome-https",
                    Description = "Allow HTTPS",
                    Action = "allow",
                    Direction = "outbound",
                    Protocol = "tcp",
                    RemotePorts = new List<int> { 443 }
                }
            }
        };
        _mockDialogService.ConfirmResult = false;

        await vm.ApplySuggestedRulesCommand.ExecuteAsync(null);

        // No save dialog should be shown
        Assert.Equal(0, _mockDialogService.SaveFileCount);
    }

    #endregion

    #region ApplicationDiscoveryService Integration Tests

    [Fact]
    public void RealService_GetSignatures_ReturnsExpectedApps()
    {
        var service = new ApplicationDiscoveryService();
        var signatures = service.GetApplicationSignatures();

        // Check for expected known applications
        Assert.Contains(signatures, s => s.Id == "chrome");
        Assert.Contains(signatures, s => s.Id == "firefox");
        Assert.Contains(signatures, s => s.Id == "edge");
        Assert.Contains(signatures, s => s.Id == "discord");
        Assert.Contains(signatures, s => s.Id == "slack");
        Assert.Contains(signatures, s => s.Id == "teams");
        Assert.Contains(signatures, s => s.Id == "vscode");
        Assert.Contains(signatures, s => s.Id == "git");
        Assert.Contains(signatures, s => s.Id == "steam");
        Assert.Contains(signatures, s => s.Id == "spotify");
    }

    [Fact]
    public void RealService_GetSignatures_HasCategories()
    {
        var service = new ApplicationDiscoveryService();
        var signatures = service.GetApplicationSignatures();

        var categories = signatures.Select(s => s.Category).Distinct().ToList();

        Assert.Contains("Web Browser", categories);
        Assert.Contains("Communication", categories);
        Assert.Contains("Development", categories);
        Assert.Contains("Cloud Storage", categories);
        Assert.Contains("Gaming", categories);
        Assert.Contains("Media", categories);
    }

    [Fact]
    public void RealService_GetSuggestedRules_ChromeHasRules()
    {
        var service = new ApplicationDiscoveryService();
        var rules = service.GetSuggestedRules("chrome");

        Assert.NotEmpty(rules);
        Assert.Contains(rules, r => r.Id == "chrome-https");
        Assert.Contains(rules, r => r.Id == "chrome-http");
    }

    [Fact]
    public void RealService_GetSuggestedRules_DiscordHasVoiceRule()
    {
        var service = new ApplicationDiscoveryService();
        var rules = service.GetSuggestedRules("discord");

        Assert.NotEmpty(rules);
        Assert.Contains(rules, r => r.Id == "discord-voice" && r.Protocol == "udp");
    }

    [Fact]
    public void RealService_GetSuggestedRules_GitHasSshRule()
    {
        var service = new ApplicationDiscoveryService();
        var rules = service.GetSuggestedRules("git");

        Assert.Contains(rules, r => r.RemotePorts != null && r.RemotePorts.Contains(22));
    }

    [Fact]
    public async Task RealService_DiscoverApplications_CompletesWithoutError()
    {
        var service = new ApplicationDiscoveryService();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Should not throw
        var apps = await service.DiscoverApplicationsAsync(cts.Token);

        Assert.NotNull(apps);
        // May or may not find applications depending on the test environment
    }

    [Fact]
    public async Task RealService_DiscoverApplications_CanBeCancelled()
    {
        var service = new ApplicationDiscoveryService();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // TaskCanceledException derives from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DiscoverApplicationsAsync(cts.Token));
    }

    #endregion
}

/// <summary>
/// Test implementation of IApplicationDiscoveryService.
/// </summary>
public class TestApplicationDiscoveryService : IApplicationDiscoveryService
{
    private readonly List<DiscoveredApplication> _testApps = new()
    {
        new DiscoveredApplication
        {
            Id = "test-chrome",
            Name = "Google Chrome",
            Publisher = "Google",
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Version = "120.0.0.0",
            IsKnownApplication = true,
            KnownApplicationId = "chrome",
            CoverageStatus = ApplicationCoverageStatus.Uncovered,
            SuggestedRules = new List<SuggestedRule>
            {
                new SuggestedRule
                {
                    Id = "chrome-https",
                    Description = "Allow Chrome HTTPS",
                    Action = "allow",
                    Direction = "outbound",
                    Protocol = "tcp",
                    RemotePorts = new List<int> { 443 }
                }
            }
        },
        new DiscoveredApplication
        {
            Id = "test-notepad",
            Name = "Notepad++",
            Publisher = "Notepad++",
            ExecutablePath = @"C:\Program Files\Notepad++\notepad++.exe",
            Version = "8.0.0",
            IsKnownApplication = false,
            CoverageStatus = ApplicationCoverageStatus.Unknown
        },
        new DiscoveredApplication
        {
            Id = "test-vscode",
            Name = "Visual Studio Code",
            Publisher = "Microsoft",
            ExecutablePath = @"C:\Users\User\AppData\Local\Programs\Microsoft VS Code\Code.exe",
            Version = "1.85.0",
            IsKnownApplication = true,
            KnownApplicationId = "vscode",
            CoverageStatus = ApplicationCoverageStatus.Uncovered,
            SuggestedRules = new List<SuggestedRule>
            {
                new SuggestedRule
                {
                    Id = "vscode-https",
                    Description = "Allow VS Code HTTPS",
                    Action = "allow",
                    Direction = "outbound",
                    Protocol = "tcp",
                    RemotePorts = new List<int> { 443 }
                }
            }
        }
    };

    private readonly List<ApplicationSignature> _signatures = new()
    {
        new ApplicationSignature
        {
            Id = "chrome",
            Name = "Google Chrome",
            Publisher = "Google",
            Category = "Web Browser",
            ExecutablePatterns = new List<string> { "chrome.exe" },
            DefaultRules = new List<SuggestedRule>
            {
                new SuggestedRule
                {
                    Id = "chrome-https",
                    Description = "Allow Chrome HTTPS",
                    RemotePorts = new List<int> { 443 }
                }
            }
        },
        new ApplicationSignature
        {
            Id = "vscode",
            Name = "Visual Studio Code",
            Publisher = "Microsoft",
            Category = "Development",
            ExecutablePatterns = new List<string> { "Code.exe" },
            DefaultRules = new List<SuggestedRule>
            {
                new SuggestedRule
                {
                    Id = "vscode-https",
                    Description = "Allow VS Code HTTPS",
                    RemotePorts = new List<int> { 443 }
                }
            }
        }
    };

    public Task<List<DiscoveredApplication>> DiscoverApplicationsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_testApps.ToList());
    }

    public List<SuggestedRule> GetSuggestedRules(string applicationId)
    {
        var signature = _signatures.FirstOrDefault(s => s.Id == applicationId);
        return signature?.DefaultRules ?? new List<SuggestedRule>();
    }

    public List<ApplicationSignature> GetApplicationSignatures()
    {
        return _signatures.ToList();
    }
}
