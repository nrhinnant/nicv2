using WfpTrafficControl.Shared.Policy;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for PolicyEditorViewModel.
/// </summary>
public class PolicyEditorViewModelTests
{
    private readonly MockServiceClient _mockService;
    private readonly MockDialogService _mockDialog;
    private readonly IPolicyTemplateProvider _templateProvider;
    private readonly PolicyEditorViewModel _viewModel;

    public PolicyEditorViewModelTests()
    {
        _mockService = new MockServiceClient();
        _mockDialog = new MockDialogService();
        _templateProvider = new PolicyTemplateProvider();
        _viewModel = new PolicyEditorViewModel(_mockService, _mockDialog, _templateProvider);
    }

    [Fact]
    public void NewPolicyCommand_CreatesEmptyPolicy()
    {
        // Act
        _viewModel.NewPolicyCommand.Execute(null);

        // Assert
        Assert.True(_viewModel.HasPolicy);
        Assert.Equal("1.0.0", _viewModel.PolicyVersion);
        Assert.Equal("allow", _viewModel.DefaultAction);
        Assert.Empty(_viewModel.Rules);
        Assert.False(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void NewPolicyCommand_WithUnsavedChanges_AsksConfirmation()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        _mockDialog.ConfirmResult = false;

        // Act
        _viewModel.NewPolicyCommand.Execute(null);

        // Assert
        Assert.Equal(1, _mockDialog.ConfirmCount);
        Assert.Single(_viewModel.Rules); // Rules not cleared because user cancelled
    }

    [Fact]
    public void AddRuleCommand_AddsNewRule()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);

        // Act
        _viewModel.AddRuleCommand.Execute(null);

        // Assert
        Assert.Single(_viewModel.Rules);
        Assert.NotNull(_viewModel.SelectedRule);
        Assert.Equal("block", _viewModel.SelectedRule.Action);
        Assert.Equal("outbound", _viewModel.SelectedRule.Direction);
        Assert.Equal("tcp", _viewModel.SelectedRule.Protocol);
        Assert.True(_viewModel.SelectedRule.Enabled);
        Assert.True(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void DeleteRuleCommand_RemovesSelectedRule()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        var ruleToDelete = _viewModel.SelectedRule;
        _mockDialog.ConfirmResult = true;

        // Act
        _viewModel.DeleteRuleCommand.Execute(null);

        // Assert
        Assert.Single(_viewModel.Rules);
        Assert.DoesNotContain(ruleToDelete, _viewModel.Rules);
    }

    [Fact]
    public void DeleteRuleCommand_WhenUserCancels_DoesNotDelete()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        _mockDialog.ConfirmResult = false;

        // Act
        _viewModel.DeleteRuleCommand.Execute(null);

        // Assert
        Assert.Single(_viewModel.Rules);
    }

    [Fact]
    public void MoveRuleUpCommand_MovesRule()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        var firstRule = _viewModel.SelectedRule;
        _viewModel.AddRuleCommand.Execute(null);
        var secondRule = _viewModel.SelectedRule;

        // Act
        _viewModel.MoveRuleUpCommand.Execute(null);

        // Assert
        Assert.Equal(secondRule, _viewModel.Rules[0]);
        Assert.Equal(firstRule, _viewModel.Rules[1]);
    }

    [Fact]
    public void MoveRuleDownCommand_MovesRule()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        var firstRule = _viewModel.SelectedRule;
        _viewModel.AddRuleCommand.Execute(null);
        var secondRule = _viewModel.SelectedRule;
        _viewModel.SelectedRule = firstRule;

        // Act
        _viewModel.MoveRuleDownCommand.Execute(null);

        // Assert
        Assert.Equal(secondRule, _viewModel.Rules[0]);
        Assert.Equal(firstRule, _viewModel.Rules[1]);
    }

    [Fact]
    public async Task ValidatePolicyCommand_WhenNoPolicy_ShowsWarning()
    {
        // Act
        await _viewModel.ValidatePolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockDialog.WarningCount);
        Assert.Equal(0, _mockService.ValidateCallCount);
    }

    [Fact]
    public async Task ValidatePolicyCommand_WhenValid_ShowsValidMessage()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _mockService.ValidationIsValid = true;

        // Act
        await _viewModel.ValidatePolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockService.ValidateCallCount);
        Assert.True(_viewModel.IsValid);
        Assert.Contains("valid", _viewModel.ValidationMessage.ToLower());
    }

    [Fact]
    public async Task ValidatePolicyCommand_WhenInvalid_ShowsErrors()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _mockService.ValidationIsValid = false;
        _mockService.ValidationErrors = new List<WfpTrafficControl.Shared.Ipc.ValidationErrorDto>
        {
            new() { Path = "rules[0].id", Message = "ID is required" }
        };

        // Act
        await _viewModel.ValidatePolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.False(_viewModel.IsValid);
        Assert.Single(_viewModel.ValidationErrors);
        Assert.Contains("error", _viewModel.ValidationMessage.ToLower());
    }

    [Fact]
    public async Task ApplyPolicyCommand_WhenNoPolicy_ShowsWarning()
    {
        // Act
        await _viewModel.ApplyPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockDialog.WarningCount);
        Assert.Equal(0, _mockService.ApplyCallCount);
    }

    [Fact]
    public void RuleViewModel_SummaryIncludesRelevantInfo()
    {
        // Arrange
        var rule = new RuleViewModel
        {
            RemoteIp = "192.168.1.0/24",
            RemotePorts = "443",
            Process = @"C:\Program Files\App\app.exe"
        };

        // Act
        var summary = rule.Summary;

        // Assert
        Assert.Contains("192.168.1.0/24", summary);
        Assert.Contains(":443", summary);
        Assert.Contains("app.exe", summary);
    }

    [Fact]
    public void RuleViewModel_SummaryHandlesEmptyFields()
    {
        // Arrange
        var rule = new RuleViewModel();

        // Act
        var summary = rule.Summary;

        // Assert
        Assert.Equal("(any)", summary);
    }

    [Fact]
    public void PolicyVersion_ChangeSetsUnsavedChanges()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.GetType().GetProperty("HasUnsavedChanges")?.SetValue(_viewModel, false);

        // Act
        _viewModel.PolicyVersion = "2.0.0";

        // Assert
        Assert.True(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void DefaultAction_ChangeSetsUnsavedChanges()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.GetType().GetProperty("HasUnsavedChanges")?.SetValue(_viewModel, false);

        // Act
        _viewModel.DefaultAction = "block";

        // Assert
        Assert.True(_viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void RuleViewModel_HasCorrectStaticOptions()
    {
        // Assert
        Assert.Contains("allow", RuleViewModel.AvailableActions);
        Assert.Contains("block", RuleViewModel.AvailableActions);
        Assert.Contains("inbound", RuleViewModel.AvailableDirections);
        Assert.Contains("outbound", RuleViewModel.AvailableDirections);
        Assert.Contains("both", RuleViewModel.AvailableDirections);
        Assert.Contains("tcp", RuleViewModel.AvailableProtocols);
        Assert.Contains("udp", RuleViewModel.AvailableProtocols);
        Assert.Contains("any", RuleViewModel.AvailableProtocols);
    }

    // ========================================
    // Template Tests
    // ========================================

    [Fact]
    public void Templates_AreLoadedOnConstruction()
    {
        // Assert
        Assert.NotEmpty(_viewModel.Templates);
        Assert.True(_viewModel.Templates.Count >= 8, "Expected at least 8 built-in templates");
    }

    [Fact]
    public void LoadFromTemplateCommand_WithNullTemplate_DoesNothing()
    {
        // Act
        _viewModel.LoadFromTemplateCommand.Execute(null);

        // Assert
        Assert.False(_viewModel.HasPolicy);
    }

    [Fact]
    public void LoadFromTemplateCommand_LoadsTemplate()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => t.Id == "block-cloudflare-dns");

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.True(_viewModel.HasPolicy);
        Assert.NotEmpty(_viewModel.Rules);
        Assert.True(_viewModel.HasUnsavedChanges);
        Assert.Equal(1, _mockDialog.SuccessCount);
    }

    [Fact]
    public void LoadFromTemplateCommand_WithWarningTemplate_ShowsWarning()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => !string.IsNullOrEmpty(t.Warning));

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.Equal(1, _mockDialog.ConfirmWarningCount);
    }

    [Fact]
    public void LoadFromTemplateCommand_UserCancelsWarning_DoesNotLoad()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => !string.IsNullOrEmpty(t.Warning));
        _mockDialog.ConfirmResult = false;

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.False(_viewModel.HasPolicy);
        Assert.Equal(1, _mockDialog.ConfirmWarningCount);
    }

    [Fact]
    public void LoadFromTemplateCommand_WithUnsavedChanges_AsksConfirmation()
    {
        // Arrange
        _viewModel.NewPolicyCommand.Execute(null);
        _viewModel.AddRuleCommand.Execute(null);
        var template = _viewModel.Templates.First(t => t.Id == "block-cloudflare-dns");
        _mockDialog.ConfirmResult = false;

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.Equal(1, _mockDialog.ConfirmCount);
        Assert.Single(_viewModel.Rules); // Original rules still there
    }

    [Fact]
    public void LoadFromTemplateCommand_ClearsFilePath()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => t.Id == "block-cloudflare-dns");

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.Equal("", _viewModel.CurrentFilePath);
    }

    [Fact]
    public void BlockCloudflareDnsTemplate_HasExpectedRules()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => t.Id == "block-cloudflare-dns");

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.True(_viewModel.Rules.Count >= 6, "Expected at least 6 rules for Cloudflare DNS blocking");
        Assert.All(_viewModel.Rules, r => Assert.Equal("block", r.Action));
        Assert.Contains(_viewModel.Rules, r => r.RemoteIp.Contains("1.1.1.1"));
        Assert.Contains(_viewModel.Rules, r => r.RemoteIp.Contains("1.0.0.1"));
    }

    [Fact]
    public void BlockAllTrafficTemplate_HasDefaultDeny()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => t.Id == "block-all-traffic");

        // Act
        _viewModel.LoadFromTemplateCommand.Execute(template);

        // Assert
        Assert.Equal("block", _viewModel.DefaultAction);
        Assert.Contains(_viewModel.Rules, r => r.Action == "allow" && r.RemoteIp.Contains("127."));
    }

    [Fact]
    public void Templates_HaveUniqueIds()
    {
        // Assert
        var ids = _viewModel.Templates.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Templates_HaveRequiredProperties()
    {
        // Assert
        foreach (var template in _viewModel.Templates)
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Id), $"Template has empty Id");
            Assert.False(string.IsNullOrWhiteSpace(template.Name), $"Template {template.Id} has empty Name");
            Assert.False(string.IsNullOrWhiteSpace(template.Description), $"Template {template.Id} has empty Description");
            Assert.False(string.IsNullOrWhiteSpace(template.Category), $"Template {template.Id} has empty Category");

            var policy = template.CreatePolicy();
            Assert.NotNull(policy);
            Assert.NotNull(policy.Rules);
        }
    }

    [Fact]
    public void Templates_CreateFreshPoliciesEachTime()
    {
        // Arrange
        var template = _viewModel.Templates.First(t => t.Id == "block-cloudflare-dns");

        // Act
        var policy1 = template.CreatePolicy();
        var policy2 = template.CreatePolicy();

        // Assert - different object references
        Assert.NotSame(policy1, policy2);
        Assert.NotSame(policy1.Rules, policy2.Rules);
    }
}
