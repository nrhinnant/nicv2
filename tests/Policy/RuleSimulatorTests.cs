using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Policy;
using Xunit;
using PolicyModel = WfpTrafficControl.Shared.Policy.Policy;

namespace WfpTrafficControl.Tests.Policy;

/// <summary>
/// Unit tests for RuleSimulator logic.
/// </summary>
public sealed class RuleSimulatorTests
{
    private static PolicyModel CreatePolicy(params Rule[] rules)
    {
        return new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = DefaultAction.Allow,
            Rules = rules.ToList()
        };
    }

    // Basic Simulation Tests

    [Fact]
    public void Simulate_NoRules_UsesDefaultAction()
    {
        // Arrange
        var policy = CreatePolicy();
        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.Ok);
        Assert.True(result.WouldAllow);
        Assert.True(result.UsedDefaultAction);
        Assert.Equal("allow", result.DefaultAction);
        Assert.Null(result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_BlockRule_ReturnsBlocked()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-all",
            Action = "block",
            Direction = "both",
            Protocol = "any",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.Ok);
        Assert.False(result.WouldAllow);
        Assert.Equal("block-all", result.MatchedRuleId);
        Assert.Equal("block", result.MatchedAction);
        Assert.False(result.UsedDefaultAction);
    }

    [Fact]
    public void Simulate_AllowRule_ReturnsAllowed()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "allow-https",
            Action = "allow",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ports = "443" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            RemotePort = 443
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.Ok);
        Assert.True(result.WouldAllow);
        Assert.Equal("allow-https", result.MatchedRuleId);
        Assert.Equal("allow", result.MatchedAction);
    }

    // Direction Matching Tests

    [Fact]
    public void Simulate_DirectionMismatch_DoesNotMatch()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "outbound-only",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "inbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow); // Uses default action
        Assert.True(result.UsedDefaultAction);
        Assert.Single(result.EvaluationTrace);
        Assert.False(result.EvaluationTrace[0].Matched);
        Assert.Contains("Direction mismatch", result.EvaluationTrace[0].Reason);
    }

    [Fact]
    public void Simulate_DirectionBoth_MatchesAny()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "both-directions",
            Action = "block",
            Direction = "both",
            Protocol = "tcp",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "inbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("both-directions", result.MatchedRuleId);
    }

    // Protocol Matching Tests

    [Fact]
    public void Simulate_ProtocolMismatch_DoesNotMatch()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "tcp-only",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "udp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow);
        Assert.True(result.UsedDefaultAction);
        Assert.Contains("Protocol mismatch", result.EvaluationTrace[0].Reason);
    }

    [Fact]
    public void Simulate_ProtocolAny_MatchesAny()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "any-protocol",
            Action = "block",
            Direction = "outbound",
            Protocol = "any",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "udp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("any-protocol", result.MatchedRuleId);
    }

    // IP Matching Tests

    [Fact]
    public void Simulate_ExactIpMatch_Matches()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-ip",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ip = "192.168.1.100" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.100"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("block-ip", result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_IpMismatch_DoesNotMatch()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-ip",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ip = "192.168.1.100" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.200"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow);
        Assert.True(result.UsedDefaultAction);
        Assert.Contains("Remote IP mismatch", result.EvaluationTrace[0].Reason);
    }

    [Fact]
    public void Simulate_CidrMatch_Matches()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-subnet",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ip = "192.168.1.0/24" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.50"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("block-subnet", result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_CidrOutsideRange_DoesNotMatch()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-subnet",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ip = "192.168.1.0/24" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.2.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow);
        Assert.True(result.UsedDefaultAction);
    }

    // Port Matching Tests

    [Fact]
    public void Simulate_SinglePortMatch_Matches()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-https",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ports = "443" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            RemotePort = 443
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("block-https", result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_PortRangeMatch_Matches()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-range",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ports = "80-443" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            RemotePort = 200
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("block-range", result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_PortMismatch_DoesNotMatch()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-https",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new Endpoint { Ports = "443" },
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            RemotePort = 80
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow);
        Assert.Contains("Remote port mismatch", result.EvaluationTrace[0].Reason);
    }

    // Process Matching Tests

    [Fact]
    public void Simulate_ProcessPathMatch_Matches()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-chrome",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Process = @"C:\Program Files\Google\Chrome\chrome.exe",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            ProcessPath = @"C:\Program Files\Google\Chrome\chrome.exe"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("block-chrome", result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_ProcessNameOnly_MatchesFullPath()
    {
        // Arrange - rule specifies just the executable name
        var policy = CreatePolicy(new Rule
        {
            Id = "block-chrome",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Process = "chrome.exe",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            ProcessPath = @"C:\Program Files\Google\Chrome\chrome.exe"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("block-chrome", result.MatchedRuleId);
    }

    [Fact]
    public void Simulate_ProcessMismatch_DoesNotMatch()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "block-chrome",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Process = @"C:\Program Files\Google\Chrome\chrome.exe",
            Enabled = true
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1",
            ProcessPath = @"C:\Program Files\Mozilla Firefox\firefox.exe"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow);
        Assert.Contains("Process mismatch", result.EvaluationTrace[0].Reason);
    }

    // Priority Tests

    [Fact]
    public void Simulate_HigherPriorityWins()
    {
        // Arrange - two rules, higher priority should win
        var policy = CreatePolicy(
            new Rule
            {
                Id = "low-priority-allow",
                Action = "allow",
                Direction = "outbound",
                Protocol = "tcp",
                Priority = 50,
                Enabled = true
            },
            new Rule
            {
                Id = "high-priority-block",
                Action = "block",
                Direction = "outbound",
                Protocol = "tcp",
                Priority = 100,
                Enabled = true
            }
        );

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.Equal("high-priority-block", result.MatchedRuleId);
    }

    // Disabled Rule Tests

    [Fact]
    public void Simulate_DisabledRule_Ignored()
    {
        // Arrange
        var policy = CreatePolicy(new Rule
        {
            Id = "disabled-block",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = false
        });

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.True(result.WouldAllow);
        Assert.True(result.UsedDefaultAction);
        Assert.Empty(result.EvaluationTrace); // Disabled rules are not evaluated
    }

    // Evaluation Trace Tests

    [Fact]
    public void Simulate_ReturnsCompleteTrace()
    {
        // Arrange
        var policy = CreatePolicy(
            new Rule
            {
                Id = "rule-1",
                Action = "allow",
                Direction = "outbound",
                Protocol = "udp", // Won't match TCP
                Priority = 100,
                Enabled = true
            },
            new Rule
            {
                Id = "rule-2",
                Action = "block",
                Direction = "outbound",
                Protocol = "tcp", // Will match
                Priority = 50,
                Enabled = true
            }
        );

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.Equal(2, result.EvaluationTrace.Count);
        Assert.False(result.EvaluationTrace[0].Matched); // rule-1 (higher priority but TCP)
        Assert.True(result.EvaluationTrace[1].Matched);  // rule-2
        Assert.Equal("rule-2", result.MatchedRuleId);
    }

    // Default Deny Policy Tests

    [Fact]
    public void Simulate_DefaultDenyPolicy_BlocksWhenNoMatch()
    {
        // Arrange
        var policy = new PolicyModel
        {
            Version = "1.0.0",
            DefaultAction = DefaultAction.Block,
            Rules = new List<Rule>()
        };

        var request = new SimulateRequest
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.1"
        };

        // Act
        var result = RuleSimulator.Simulate(policy, request);

        // Assert
        Assert.False(result.WouldAllow);
        Assert.True(result.UsedDefaultAction);
        Assert.Equal("block", result.DefaultAction);
    }
}
