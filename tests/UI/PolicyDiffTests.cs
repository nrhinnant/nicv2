using WfpTrafficControl.Shared.Policy;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.ViewModels;
using Xunit;
using PolicyModel = WfpTrafficControl.Shared.Policy.Policy;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for PolicyDiffService and PolicyDiffViewModel.
/// </summary>
public class PolicyDiffTests
{
    #region PolicyDiffService Tests

    [Fact]
    public void Compare_BothNull_ReturnsEmptyResult()
    {
        // Arrange
        var service = new PolicyDiffService();

        // Act
        var result = service.Compare(null, null);

        // Assert
        Assert.Empty(result.AddedRules);
        Assert.Empty(result.RemovedRules);
        Assert.Empty(result.ModifiedRules);
        Assert.Empty(result.UnchangedRules);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void Compare_LeftNull_AllRulesAreAdded()
    {
        // Arrange
        var service = new PolicyDiffService();
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" },
                new Rule { Id = "rule-2", Action = "block", Direction = "inbound", Protocol = "udp" }
            }
        };

        // Act
        var result = service.Compare(null, right);

        // Assert
        Assert.Equal(2, result.AddedRules.Count);
        Assert.Empty(result.RemovedRules);
        Assert.Empty(result.ModifiedRules);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_RightNull_AllRulesAreRemoved()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" },
                new Rule { Id = "rule-2", Action = "block", Direction = "inbound", Protocol = "udp" }
            }
        };

        // Act
        var result = service.Compare(left, null);

        // Assert
        Assert.Empty(result.AddedRules);
        Assert.Equal(2, result.RemovedRules.Count);
        Assert.Empty(result.ModifiedRules);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_IdenticalPolicies_NoChanges()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" }
            }
        };
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Empty(result.AddedRules);
        Assert.Empty(result.RemovedRules);
        Assert.Empty(result.ModifiedRules);
        Assert.Single(result.UnchangedRules);
        Assert.False(result.HasChanges);
    }

    [Fact]
    public void Compare_AddedRule_DetectedCorrectly()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" }
            }
        };
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" },
                new Rule { Id = "rule-2", Action = "block", Direction = "inbound", Protocol = "udp" }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Single(result.AddedRules);
        Assert.Equal("rule-2", result.AddedRules[0].Rule.Id);
        Assert.Empty(result.RemovedRules);
        Assert.Single(result.UnchangedRules);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_RemovedRule_DetectedCorrectly()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" },
                new Rule { Id = "rule-2", Action = "block", Direction = "inbound", Protocol = "udp" }
            }
        };
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Empty(result.AddedRules);
        Assert.Single(result.RemovedRules);
        Assert.Equal("rule-2", result.RemovedRules[0].Rule.Id);
        Assert.Single(result.UnchangedRules);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_ModifiedRule_DetectedCorrectly()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" }
            }
        };
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "block", Direction = "outbound", Protocol = "tcp" }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Empty(result.AddedRules);
        Assert.Empty(result.RemovedRules);
        Assert.Single(result.ModifiedRules);
        Assert.Contains("action:", result.ModifiedRules[0].ChangedFields[0]);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_ModifiedRuleMultipleFields_AllChangesDetected()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule
                {
                    Id = "rule-1",
                    Action = "allow",
                    Direction = "outbound",
                    Protocol = "tcp",
                    Priority = 100,
                    Enabled = true
                }
            }
        };
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule
                {
                    Id = "rule-1",
                    Action = "block",
                    Direction = "inbound",
                    Protocol = "udp",
                    Priority = 200,
                    Enabled = false
                }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Single(result.ModifiedRules);
        var changes = result.ModifiedRules[0].ChangedFields;
        Assert.Equal(5, changes.Count);
        Assert.Contains(changes, c => c.Contains("action:"));
        Assert.Contains(changes, c => c.Contains("direction:"));
        Assert.Contains(changes, c => c.Contains("protocol:"));
        Assert.Contains(changes, c => c.Contains("priority:"));
        Assert.Contains(changes, c => c.Contains("enabled:"));
    }

    [Fact]
    public void Compare_DefaultActionChanged_Detected()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel { Version = "1.0.0", DefaultAction = "allow", Rules = new List<Rule>() };
        var right = new PolicyModel { Version = "1.0.0", DefaultAction = "block", Rules = new List<Rule>() };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.True(result.DefaultActionChanged);
        Assert.Equal("allow", result.OldDefaultAction);
        Assert.Equal("block", result.NewDefaultAction);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_VersionChanged_Detected()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel { Version = "1.0.0", DefaultAction = "allow", Rules = new List<Rule>() };
        var right = new PolicyModel { Version = "2.0.0", DefaultAction = "allow", Rules = new List<Rule>() };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.True(result.VersionChanged);
        Assert.Equal("1.0.0", result.OldVersion);
        Assert.Equal("2.0.0", result.NewVersion);
        Assert.True(result.HasChanges);
    }

    [Fact]
    public void Compare_RemoteEndpointChanged_Detected()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule
                {
                    Id = "rule-1",
                    Action = "allow",
                    Direction = "outbound",
                    Protocol = "tcp",
                    Remote = new EndpointFilter { Ip = "192.168.1.0/24", Ports = "80" }
                }
            }
        };
        var right = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule
                {
                    Id = "rule-1",
                    Action = "allow",
                    Direction = "outbound",
                    Protocol = "tcp",
                    Remote = new EndpointFilter { Ip = "10.0.0.0/8", Ports = "443" }
                }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Single(result.ModifiedRules);
        var changes = result.ModifiedRules[0].ChangedFields;
        Assert.Contains(changes, c => c.Contains("remote IP:"));
        Assert.Contains(changes, c => c.Contains("remote ports:"));
    }

    [Fact]
    public void Summary_ReturnsCorrectDescription()
    {
        // Arrange
        var service = new PolicyDiffService();
        var left = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "allow", Direction = "outbound", Protocol = "tcp" },
                new Rule { Id = "rule-2", Action = "block", Direction = "inbound", Protocol = "udp" }
            }
        };
        var right = new PolicyModel
        {
            Version = "2.0.0",
            DefaultAction = "allow",
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "block", Direction = "outbound", Protocol = "tcp" },
                new Rule { Id = "rule-3", Action = "allow", Direction = "both", Protocol = "any" }
            }
        };

        // Act
        var result = service.Compare(left, right);

        // Assert
        Assert.Contains("1 added", result.Summary);
        Assert.Contains("1 removed", result.Summary);
        Assert.Contains("1 modified", result.Summary);
        Assert.Contains("version changed", result.Summary);
    }

    #endregion

    #region PolicyDiffViewModel Tests

    [Fact]
    public void ViewModel_InitialState_HasCorrectDefaults()
    {
        // Arrange & Act
        var mockDialog = new MockDialogService();
        var vm = new PolicyDiffViewModel(mockDialog);

        // Assert
        Assert.Null(vm.LeftPolicy);
        Assert.Null(vm.RightPolicy);
        Assert.Equal("(No policy loaded)", vm.LeftPolicyName);
        Assert.Equal("(No policy loaded)", vm.RightPolicyName);
        Assert.Null(vm.DiffResult);
        Assert.False(vm.HasChanges);
        Assert.Empty(vm.DiffItems);
    }

    [Fact]
    public void SwapPolicies_SwapsLeftAndRight()
    {
        // Arrange
        var mockDialog = new MockDialogService();
        var vm = new PolicyDiffViewModel(mockDialog);

        var leftPolicy = new PolicyModel { Version = "1.0.0", DefaultAction = "allow", Rules = new List<Rule>() };
        var rightPolicy = new PolicyModel { Version = "2.0.0", DefaultAction = "block", Rules = new List<Rule>() };

        // Manually set properties (simulating loaded policies)
        typeof(PolicyDiffViewModel).GetProperty("LeftPolicy")!.SetValue(vm, leftPolicy);
        typeof(PolicyDiffViewModel).GetProperty("RightPolicy")!.SetValue(vm, rightPolicy);
        typeof(PolicyDiffViewModel).GetProperty("LeftPolicyName")!.SetValue(vm, "left.json");
        typeof(PolicyDiffViewModel).GetProperty("RightPolicyName")!.SetValue(vm, "right.json");

        // Act
        vm.SwapPoliciesCommand.Execute(null);

        // Assert
        Assert.Equal(rightPolicy, vm.LeftPolicy);
        Assert.Equal(leftPolicy, vm.RightPolicy);
        Assert.Equal("right.json", vm.LeftPolicyName);
        Assert.Equal("left.json", vm.RightPolicyName);
    }

    [Fact]
    public void ClearAll_ResetsAllState()
    {
        // Arrange
        var mockDialog = new MockDialogService();
        var vm = new PolicyDiffViewModel(mockDialog);

        // Manually set some state
        typeof(PolicyDiffViewModel).GetProperty("LeftPolicy")!.SetValue(vm,
            new PolicyModel { Version = "1.0.0", DefaultAction = "allow", Rules = new List<Rule>() });
        typeof(PolicyDiffViewModel).GetProperty("LeftPolicyName")!.SetValue(vm, "test.json");
        typeof(PolicyDiffViewModel).GetProperty("HasChanges")!.SetValue(vm, true);

        // Act
        vm.ClearAllCommand.Execute(null);

        // Assert
        Assert.Null(vm.LeftPolicy);
        Assert.Null(vm.RightPolicy);
        Assert.Equal("(No policy loaded)", vm.LeftPolicyName);
        Assert.Equal("(No policy loaded)", vm.RightPolicyName);
        Assert.False(vm.HasChanges);
        Assert.Empty(vm.DiffItems);
    }

    #endregion

    #region DiffItemViewModel Tests

    [Fact]
    public void DiffItemViewModel_ChangeIndicator_ReturnsCorrectSymbol()
    {
        Assert.Equal("+", new DiffItemViewModel { ChangeType = DiffChangeType.Added }.ChangeIndicator);
        Assert.Equal("-", new DiffItemViewModel { ChangeType = DiffChangeType.Removed }.ChangeIndicator);
        Assert.Equal("~", new DiffItemViewModel { ChangeType = DiffChangeType.Modified }.ChangeIndicator);
        Assert.Equal(" ", new DiffItemViewModel { ChangeType = DiffChangeType.Unchanged }.ChangeIndicator);
    }

    #endregion
}
