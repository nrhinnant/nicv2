using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for BlockRulesViewModel.
/// </summary>
public sealed class BlockRulesViewModelTests
{
    private readonly MockServiceClient _mockClient;
    private readonly BlockRulesViewModel _viewModel;

    public BlockRulesViewModelTests()
    {
        _mockClient = new MockServiceClient();
        _viewModel = new BlockRulesViewModel(_mockClient);
    }

    [Fact]
    public void InitialState_HasCorrectDefaults()
    {
        // Assert
        Assert.Empty(_viewModel.BlockRules);
        Assert.Equal(0, _viewModel.TotalRuleCount);
        Assert.Null(_viewModel.PolicyVersion);
        Assert.False(_viewModel.PolicyLoaded);
        Assert.False(_viewModel.IsLoading);
        Assert.Equal("Click Refresh to load block rules", _viewModel.StatusMessage);
        Assert.Null(_viewModel.SelectedRule);
    }

    [Fact]
    public void SimplificationNotice_ReturnsExpectedText()
    {
        // Assert
        Assert.Contains("policy", _viewModel.SimplificationNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ETW", _viewModel.SimplificationNotice);
        Assert.Contains("blocked connection", _viewModel.SimplificationNotice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_WhenServiceReturnsRules_PopulatesCollection()
    {
        // Arrange
        _mockClient.BlockRulesPolicyLoaded = true;
        _mockClient.BlockRulesPolicyVersion = "2.0.0";
        _mockClient.BlockRules = new List<BlockRuleDto>
        {
            new() { Id = "rule-1", Direction = "outbound", Summary = "Test rule 1" },
            new() { Id = "rule-2", Direction = "inbound", Summary = "Test rule 2" },
            new() { Id = "rule-3", Direction = "both", Summary = "Test rule 3" }
        };

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert
        Assert.Equal(3, _viewModel.BlockRules.Count);
        Assert.True(_viewModel.PolicyLoaded);
        Assert.Equal("2.0.0", _viewModel.PolicyVersion);
        Assert.Equal(3, _viewModel.TotalRuleCount);
        Assert.Contains("3 block rule(s)", _viewModel.StatusMessage);
        Assert.Equal(1, _mockClient.GetBlockRulesCallCount);
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_WhenNoPolicyLoaded_ShowsAppropriateMessage()
    {
        // Arrange
        _mockClient.BlockRulesPolicyLoaded = false;

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert
        Assert.Empty(_viewModel.BlockRules);
        Assert.False(_viewModel.PolicyLoaded);
        Assert.Null(_viewModel.PolicyVersion);
        Assert.Contains("No policy is currently loaded", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_WhenPolicyHasNoBlockRules_ShowsAppropriateMessage()
    {
        // Arrange
        _mockClient.BlockRulesPolicyLoaded = true;
        _mockClient.BlockRulesPolicyVersion = "1.0.0";
        _mockClient.BlockRules = new List<BlockRuleDto>(); // Empty list

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert
        Assert.Empty(_viewModel.BlockRules);
        Assert.True(_viewModel.PolicyLoaded);
        Assert.Contains("no block rules", _viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_WhenServiceUnavailable_ShowsErrorMessage()
    {
        // Arrange
        _mockClient.ShouldConnect = false;

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert
        Assert.Empty(_viewModel.BlockRules);
        Assert.False(_viewModel.PolicyLoaded);
        Assert.Contains("Failed to load block rules", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_ClearsExistingRulesBeforeReload()
    {
        // Arrange - first load with rules
        _mockClient.BlockRulesPolicyLoaded = true;
        _mockClient.BlockRules = new List<BlockRuleDto>
        {
            new() { Id = "rule-1", Direction = "outbound", Summary = "Test rule" }
        };
        await _viewModel.RefreshBlockRulesAsync();
        Assert.Single(_viewModel.BlockRules);

        // Arrange - set up for empty reload
        _mockClient.BlockRules = new List<BlockRuleDto>();

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert
        Assert.Empty(_viewModel.BlockRules);
    }

    [Fact]
    public async Task InitializeAsync_CallsRefreshBlockRulesAsync()
    {
        // Arrange
        _mockClient.BlockRulesPolicyLoaded = true;
        _mockClient.BlockRules = new List<BlockRuleDto>
        {
            new() { Id = "rule-1", Direction = "outbound", Summary = "Test rule" }
        };

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        Assert.Single(_viewModel.BlockRules);
        Assert.Equal(1, _mockClient.GetBlockRulesCallCount);
    }

    [Fact]
    public void CopyRuleSummaryCommand_WhenNoRuleSelected_DoesNothing()
    {
        // Arrange
        _viewModel.SelectedRule = null;

        // Act & Assert - should not throw
        _viewModel.CopyRuleSummaryCommand.Execute(null);
    }

    [Fact]
    public void CopyRuleSummaryCommand_WhenRuleSelected_DoesNotThrow()
    {
        // Arrange
        _viewModel.SelectedRule = new BlockRuleDto
        {
            Id = "test-rule",
            Direction = "outbound",
            Protocol = "tcp",
            RemotePorts = "443",
            Summary = "Test summary",
            Comment = "Test comment"
        };

        // Act & Assert - should not throw even if clipboard access fails
        _viewModel.CopyRuleSummaryCommand.Execute(null);
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_SetsIsLoadingDuringOperation()
    {
        // Arrange
        var loadingStates = new List<bool>();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BlockRulesViewModel.IsLoading))
            {
                loadingStates.Add(_viewModel.IsLoading);
            }
        };

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert - should have set to true then false
        Assert.Contains(true, loadingStates);
        Assert.Contains(false, loadingStates);
        Assert.False(_viewModel.IsLoading); // Final state
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_UpdatesStatusMessageDuringLoad()
    {
        // Arrange
        var statusMessages = new List<string>();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BlockRulesViewModel.StatusMessage))
            {
                statusMessages.Add(_viewModel.StatusMessage);
            }
        };

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert - should have shown "Loading..." at some point
        Assert.Contains(statusMessages, m => m.Contains("Loading"));
    }

    [Fact]
    public async Task RefreshBlockRulesAsync_PreservesRuleDetails()
    {
        // Arrange
        _mockClient.BlockRulesPolicyLoaded = true;
        _mockClient.BlockRules = new List<BlockRuleDto>
        {
            new()
            {
                Id = "complex-rule",
                Direction = "both",
                Protocol = "tcp",
                Process = @"C:\Windows\System32\telnet.exe",
                RemoteIp = "192.168.1.0/24",
                RemotePorts = "23,22",
                LocalIp = "10.0.0.1",
                LocalPorts = "1024-65535",
                Comment = "Block telnet from specific process",
                Priority = 500,
                Enabled = true,
                Summary = "Both directions TCP from telnet.exe to 192.168.1.0/24 ports 23,22"
            }
        };

        // Act
        await _viewModel.RefreshBlockRulesAsync();

        // Assert
        var rule = _viewModel.BlockRules.Single();
        Assert.Equal("complex-rule", rule.Id);
        Assert.Equal("both", rule.Direction);
        Assert.Equal("tcp", rule.Protocol);
        Assert.Equal(@"C:\Windows\System32\telnet.exe", rule.Process);
        Assert.Equal("192.168.1.0/24", rule.RemoteIp);
        Assert.Equal("23,22", rule.RemotePorts);
        Assert.Equal("10.0.0.1", rule.LocalIp);
        Assert.Equal("1024-65535", rule.LocalPorts);
        Assert.Equal("Block telnet from specific process", rule.Comment);
        Assert.Equal(500, rule.Priority);
        Assert.True(rule.Enabled);
        Assert.NotEmpty(rule.Summary);
    }
}

/// <summary>
/// Tests for BlockRuleDto.
/// </summary>
public class BlockRuleDtoTests
{
    [Fact]
    public void BlockRuleDto_HasCorrectDefaultValues()
    {
        // Arrange & Act
        var dto = new BlockRuleDto();

        // Assert
        Assert.Equal(string.Empty, dto.Id);
        Assert.Equal(string.Empty, dto.Direction);
        Assert.Null(dto.Protocol);
        Assert.Null(dto.Process);
        Assert.Null(dto.RemoteIp);
        Assert.Null(dto.RemotePorts);
        Assert.Null(dto.LocalIp);
        Assert.Null(dto.LocalPorts);
        Assert.Null(dto.Comment);
        Assert.Equal(0, dto.Priority);
        Assert.True(dto.Enabled);
        Assert.Equal(string.Empty, dto.Summary);
    }
}

/// <summary>
/// Tests for BlockRulesResponse.
/// </summary>
public class BlockRulesResponseTests
{
    [Fact]
    public void Success_CreatesValidResponse()
    {
        // Arrange
        var rules = new List<BlockRuleDto>
        {
            new() { Id = "r1", Direction = "outbound", Summary = "Rule 1" }
        };

        // Act
        var response = BlockRulesResponse.Success(rules, "1.0.0");

        // Assert
        Assert.True(response.Ok);
        Assert.True(response.PolicyLoaded);
        Assert.Equal("1.0.0", response.PolicyVersion);
        Assert.Single(response.Rules);
        Assert.Equal(1, response.Count);
    }

    [Fact]
    public void NoPolicyLoaded_CreatesValidResponse()
    {
        // Act
        var response = BlockRulesResponse.NoPolicyLoaded();

        // Assert
        Assert.True(response.Ok);
        Assert.False(response.PolicyLoaded);
        Assert.Null(response.PolicyVersion);
        Assert.Empty(response.Rules);
        Assert.Equal(0, response.Count);
    }

    [Fact]
    public void Failure_CreatesValidResponse()
    {
        // Act
        var response = BlockRulesResponse.Failure("Test error");

        // Assert
        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }
}
