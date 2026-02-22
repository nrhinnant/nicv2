using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for the RuleCompiler that converts policy rules to WFP filter definitions.
/// Phase 12: Compile Outbound TCP Rules
/// </summary>
public class RuleCompilerTests
{
    // ========================================
    // Basic Compilation Tests
    // ========================================

    [Fact]
    public void Compile_NullPolicy_ReturnsError()
    {
        var result = RuleCompiler.Compile(null!);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("null", result.Errors[0].Message.ToLower());
    }

    [Fact]
    public void Compile_EmptyPolicy_ReturnsNoFilters()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>()
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Single(result.Warnings);
        Assert.Contains("no rules", result.Warnings[0].ToLower());
    }

    [Fact]
    public void Compile_ValidOutboundTcpRule_ReturnsOneFilter()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "test-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "1.1.1.1", Ports = "443" },
            Enabled = true,
            Priority = 100
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal("test-rule", filter.RuleId);
        Assert.Equal(FilterAction.Block, filter.Action);
        Assert.Equal((byte)6, filter.Protocol); // TCP
    }

    // ========================================
    // Direction Support Tests
    // ========================================

    [Fact]
    public void Compile_InboundDirection_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-rule",
            Action = "block",
            Direction = "inbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal(RuleDirection.Inbound, result.Filters[0].Direction);
    }

    [Fact]
    public void Compile_BothDirection_ReturnsError()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "both-rule",
            Action = "block",
            Direction = "both",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("Unsupported direction", result.Errors[0].Message);
    }

    [Fact]
    public void Compile_OutboundDirection_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "outbound-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal(RuleDirection.Outbound, result.Filters[0].Direction);
    }

    // ========================================
    // Inbound TCP Support Tests (Phase 15)
    // ========================================

    [Fact]
    public void Compile_InboundWithRemoteIpAndPort_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-block",
            Action = "block",
            Direction = "inbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "192.168.1.100", Ports = "8080" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal(RuleDirection.Inbound, filter.Direction);
        Assert.Equal(0xC0A80164u, filter.RemoteIpAddress); // 192.168.1.100
        Assert.Equal((ushort)8080, filter.RemotePort);
    }

    [Fact]
    public void Compile_InboundWithCidr_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-cidr",
            Action = "block",
            Direction = "inbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "10.0.0.0/8" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal(RuleDirection.Inbound, filter.Direction);
        Assert.Equal(0x0A000000u, filter.RemoteIpAddress); // 10.0.0.0
        Assert.Equal(0xFF000000u, filter.RemoteIpMask); // /8
    }

    [Fact]
    public void Compile_InboundWithProcess_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-process",
            Action = "allow",
            Direction = "inbound",
            Protocol = "tcp",
            Process = @"C:\Program Files\MyApp\server.exe",
            Remote = new EndpointFilter { Ports = "443" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal(RuleDirection.Inbound, filter.Direction);
        Assert.Equal(@"C:\Program Files\MyApp\server.exe", filter.ProcessPath);
        Assert.Equal(FilterAction.Allow, filter.Action);
    }

    [Fact]
    public void Compile_InboundDescription_ContainsInbound()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-desc",
            Action = "block",
            Direction = "inbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "1.1.1.1", Ports = "443" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Contains("inbound", result.Filters[0].Description.ToLower());
        Assert.Contains("1.1.1.1", result.Filters[0].Description);
    }

    [Fact]
    public void Compile_MixedInboundOutbound_CreatesFiltersWithCorrectDirection()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "outbound-1", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "inbound-1", Action = "block", Direction = "inbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "outbound-2", Action = "allow", Direction = "outbound", Protocol = "tcp", Enabled = true }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Filters.Count);

        var outbound1 = result.Filters.First(f => f.RuleId == "outbound-1");
        var inbound1 = result.Filters.First(f => f.RuleId == "inbound-1");
        var outbound2 = result.Filters.First(f => f.RuleId == "outbound-2");

        Assert.Equal(RuleDirection.Outbound, outbound1.Direction);
        Assert.Equal(RuleDirection.Inbound, inbound1.Direction);
        Assert.Equal(RuleDirection.Outbound, outbound2.Direction);
    }

    [Fact]
    public void Compile_InboundWithPortRange_CreatesOneFilter()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-range",
            Action = "block",
            Direction = "inbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "8000-9000" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal(RuleDirection.Inbound, filter.Direction);
        Assert.Null(filter.RemotePort);
        Assert.Equal((ushort)8000, filter.RemotePortRangeStart);
        Assert.Equal((ushort)9000, filter.RemotePortRangeEnd);
    }

    // ========================================
    // Protocol Support Tests
    // ========================================

    [Fact]
    public void Compile_InboundUdpProtocol_ReturnsError()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "inbound-udp-rule",
            Action = "block",
            Direction = "inbound",
            Protocol = "udp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("Inbound UDP rules are not supported", result.Errors[0].Message);
    }

    [Fact]
    public void Compile_OutboundUdpProtocol_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "outbound-udp-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "udp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal((byte)17, result.Filters[0].Protocol); // UDP
    }

    [Fact]
    public void Compile_OutboundUdpWithRemoteIpAndPort_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "udp-block-dns",
            Action = "block",
            Direction = "outbound",
            Protocol = "udp",
            Remote = new EndpointFilter { Ip = "8.8.8.8", Ports = "53" },
            Enabled = true,
            Priority = 100
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal("udp-block-dns", filter.RuleId);
        Assert.Equal(FilterAction.Block, filter.Action);
        Assert.Equal((byte)17, filter.Protocol); // UDP
        Assert.Equal(0x08080808u, filter.RemoteIpAddress); // 8.8.8.8
        Assert.Equal((ushort)53, filter.RemotePort);
    }

    [Fact]
    public void Compile_OutboundUdpDescription_ContainsUdp()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "udp-desc",
            Action = "block",
            Direction = "outbound",
            Protocol = "udp",
            Remote = new EndpointFilter { Ip = "1.1.1.1", Ports = "53" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Contains("udp", result.Filters[0].Description.ToLower());
        Assert.Contains("outbound", result.Filters[0].Description.ToLower());
    }

    [Fact]
    public void Compile_OutboundUdpWithCidr_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "udp-cidr",
            Action = "block",
            Direction = "outbound",
            Protocol = "udp",
            Remote = new EndpointFilter { Ip = "192.168.0.0/16" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal((byte)17, filter.Protocol); // UDP
        Assert.Equal(0xC0A80000u, filter.RemoteIpAddress); // 192.168.0.0
        Assert.Equal(0xFFFF0000u, filter.RemoteIpMask); // /16
    }

    [Fact]
    public void Compile_OutboundUdpWithProcess_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "udp-process",
            Action = "allow",
            Direction = "outbound",
            Protocol = "udp",
            Process = @"C:\Windows\System32\svchost.exe",
            Remote = new EndpointFilter { Ports = "53" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal((byte)17, filter.Protocol); // UDP
        Assert.Equal(@"C:\Windows\System32\svchost.exe", filter.ProcessPath);
        Assert.Equal(FilterAction.Allow, filter.Action);
    }

    [Fact]
    public void Compile_MixedTcpUdpOutbound_CreatesBothFilters()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "tcp-rule", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "udp-rule", Action = "block", Direction = "outbound", Protocol = "udp", Enabled = true }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);

        var tcpFilter = result.Filters.First(f => f.RuleId == "tcp-rule");
        var udpFilter = result.Filters.First(f => f.RuleId == "udp-rule");

        Assert.Equal((byte)6, tcpFilter.Protocol);  // TCP
        Assert.Equal((byte)17, udpFilter.Protocol); // UDP
    }

    [Fact]
    public void Compile_AnyProtocol_ReturnsError()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "any-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "any",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("Unsupported protocol", result.Errors[0].Message);
    }

    [Fact]
    public void Compile_TcpProtocol_Succeeds()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "tcp-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal((byte)6, result.Filters[0].Protocol);
    }

    // ========================================
    // Local Endpoint Tests
    // ========================================

    [Fact]
    public void Compile_LocalIp_ReturnsError()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "local-ip-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Local = new EndpointFilter { Ip = "192.168.1.1" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("Local endpoint filters", result.Errors[0].Message);
    }

    [Fact]
    public void Compile_LocalPorts_ReturnsError()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "local-port-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Local = new EndpointFilter { Ports = "8080" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("Local endpoint filters", result.Errors[0].Message);
    }

    // ========================================
    // Remote IP Tests
    // ========================================

    [Fact]
    public void Compile_RemoteIpv4_ParsesCorrectly()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "ipv4-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "1.2.3.4" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.NotNull(filter.RemoteIpAddress);
        // 1.2.3.4 in host byte order: 0x01020304
        Assert.Equal(0x01020304u, filter.RemoteIpAddress.Value);
        Assert.Equal(0xFFFFFFFFu, filter.RemoteIpMask); // Exact match
    }

    [Fact]
    public void Compile_RemoteCidr_ParsesMaskCorrectly()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "cidr-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "10.0.0.0/8" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.NotNull(filter.RemoteIpAddress);
        // 10.0.0.0 in host byte order: 0x0A000000
        Assert.Equal(0x0A000000u, filter.RemoteIpAddress.Value);
        // /8 mask in host byte order: 0xFF000000
        Assert.Equal(0xFF000000u, filter.RemoteIpMask);
    }

    [Fact]
    public void Compile_RemoteIpv6_ReturnsError()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "ipv6-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "::1" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Contains("IPv6", result.Errors[0].Message);
    }

    // ========================================
    // Remote Port Tests
    // ========================================

    [Fact]
    public void Compile_SinglePort_CreatesOneFilter()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "single-port",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "443" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Equal((ushort)443, filter.RemotePort);
        Assert.Null(filter.RemotePortRangeStart);
        Assert.Null(filter.RemotePortRangeEnd);
    }

    [Fact]
    public void Compile_PortRange_CreatesOneFilter()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "port-range",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "80-443" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Null(filter.RemotePort);
        Assert.Equal((ushort)80, filter.RemotePortRangeStart);
        Assert.Equal((ushort)443, filter.RemotePortRangeEnd);
    }

    [Fact]
    public void Compile_CommaSeparatedPorts_CreatesMultipleFilters()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "multi-port",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "80,443,8080" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Filters.Count);
        Assert.Equal((ushort)80, result.Filters[0].RemotePort);
        Assert.Equal((ushort)443, result.Filters[1].RemotePort);
        Assert.Equal((ushort)8080, result.Filters[2].RemotePort);
    }

    [Fact]
    public void Compile_MixedPortSpec_CreatesCorrectFilters()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "mixed-ports",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "80,8080-8090" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);
        // First filter: single port 80
        Assert.Equal((ushort)80, result.Filters[0].RemotePort);
        // Second filter: range 8080-8090
        Assert.Null(result.Filters[1].RemotePort);
        Assert.Equal((ushort)8080, result.Filters[1].RemotePortRangeStart);
        Assert.Equal((ushort)8090, result.Filters[1].RemotePortRangeEnd);
    }

    [Fact]
    public void Compile_NoPort_CreatesFilterWithoutPortCondition()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "no-port",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "1.1.1.1" }, // IP only, no port
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        var filter = result.Filters[0];
        Assert.Null(filter.RemotePort);
        Assert.Null(filter.RemotePortRangeStart);
        Assert.Null(filter.RemotePortRangeEnd);
    }

    // ========================================
    // Enabled/Disabled Tests
    // ========================================

    [Fact]
    public void Compile_DisabledRule_IsSkipped()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "disabled-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = false
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Filters);
        Assert.Equal(1, result.SkippedRules);
        Assert.Single(result.Warnings);
        Assert.Contains("disabled", result.Warnings[0].ToLower());
    }

    [Fact]
    public void Compile_MixedEnabledDisabled_CompilesOnlyEnabled()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "enabled-1", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "disabled-1", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = false },
                new Rule { Id = "enabled-2", Action = "allow", Direction = "outbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "disabled-2", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = false }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);
        Assert.Equal(2, result.SkippedRules);
        Assert.Contains(result.Filters, f => f.RuleId == "enabled-1");
        Assert.Contains(result.Filters, f => f.RuleId == "enabled-2");
    }

    // ========================================
    // Action Tests
    // ========================================

    [Fact]
    public void Compile_BlockAction_SetsBlockAction()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "block-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal(FilterAction.Block, result.Filters[0].Action);
    }

    [Fact]
    public void Compile_AllowAction_SetsAllowAction()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "allow-rule",
            Action = "allow",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal(FilterAction.Allow, result.Filters[0].Action);
    }

    // ========================================
    // Priority/Weight Tests
    // ========================================

    [Fact]
    public void Compile_PriorityAffectsWeight()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "low-priority", Action = "block", Direction = "outbound", Protocol = "tcp", Priority = 10, Enabled = true },
                new Rule { Id = "high-priority", Action = "block", Direction = "outbound", Protocol = "tcp", Priority = 100, Enabled = true }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);

        var lowPriFilter = result.Filters.First(f => f.RuleId == "low-priority");
        var highPriFilter = result.Filters.First(f => f.RuleId == "high-priority");

        Assert.True(highPriFilter.Weight > lowPriFilter.Weight);
    }

    // ========================================
    // Process Path Tests
    // ========================================

    [Fact]
    public void Compile_ProcessPath_StoresPath()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "process-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Process = @"C:\Windows\System32\notepad.exe",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Equal(@"C:\Windows\System32\notepad.exe", result.Filters[0].ProcessPath);
    }

    [Fact]
    public void Compile_NoProcess_HasNullProcessPath()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "no-process-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Null(result.Filters[0].ProcessPath);
    }

    // ========================================
    // Filter GUID Tests
    // ========================================

    [Fact]
    public void Compile_SameRuleId_GeneratesSameGuid()
    {
        var policy1 = CreatePolicy(new Rule
        {
            Id = "deterministic-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "443" },
            Enabled = true
        });

        var policy2 = CreatePolicy(new Rule
        {
            Id = "deterministic-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "443" },
            Enabled = true
        });

        var result1 = RuleCompiler.Compile(policy1);
        var result2 = RuleCompiler.Compile(policy2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Filters[0].FilterKey, result2.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_DifferentRuleId_GeneratesDifferentGuid()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "rule-2", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = true }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);
        Assert.NotEqual(result.Filters[0].FilterKey, result.Filters[1].FilterKey);
    }

    [Fact]
    public void Compile_MultiplePortsFromSameRule_GenerateDifferentGuids()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "multi-port-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ports = "80,443" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Filters.Count);
        Assert.NotEqual(result.Filters[0].FilterKey, result.Filters[1].FilterKey);
    }

    [Fact]
    public void Compile_DifferentRemoteIp_GeneratesDifferentGuid()
    {
        // Content-based GUID: different IP should produce different GUID
        var policy1 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "1.1.1.1" },
            Enabled = true
        });

        var policy2 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "8.8.8.8" },
            Enabled = true
        });

        var result1 = RuleCompiler.Compile(policy1);
        var result2 = RuleCompiler.Compile(policy2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Filters[0].FilterKey, result2.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_DifferentAction_GeneratesDifferentGuid()
    {
        // Content-based GUID: different action should produce different GUID
        var policy1 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var policy2 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "allow",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result1 = RuleCompiler.Compile(policy1);
        var result2 = RuleCompiler.Compile(policy2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Filters[0].FilterKey, result2.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_DifferentProtocol_GeneratesDifferentGuid()
    {
        // Content-based GUID: different protocol should produce different GUID
        var policy1 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var policy2 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "udp",
            Enabled = true
        });

        var result1 = RuleCompiler.Compile(policy1);
        var result2 = RuleCompiler.Compile(policy2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Filters[0].FilterKey, result2.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_DifferentDirection_GeneratesDifferentGuid()
    {
        // Content-based GUID: different direction should produce different GUID
        var policy1 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var policy2 = CreatePolicy(new Rule
        {
            Id = "same-rule",
            Action = "block",
            Direction = "inbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result1 = RuleCompiler.Compile(policy1);
        var result2 = RuleCompiler.Compile(policy2);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.NotEqual(result1.Filters[0].FilterKey, result2.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_GuidStability_AcrossCompilations()
    {
        // Verify deterministic GUID generation across multiple compilation calls
        var guids = new HashSet<Guid>();

        for (int i = 0; i < 10; i++)
        {
            var policy = CreatePolicy(new Rule
            {
                Id = "stable-rule",
                Action = "block",
                Direction = "outbound",
                Protocol = "tcp",
                Remote = new EndpointFilter { Ip = "1.2.3.4", Ports = "443" },
                Enabled = true
            });

            var result = RuleCompiler.Compile(policy);
            Assert.True(result.IsSuccess);
            guids.Add(result.Filters[0].FilterKey);
        }

        // All compilations should produce the same GUID
        Assert.Single(guids);
    }

    [Fact]
    public void Compile_UnicodeRuleId_GeneratesValidGuid()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "规则-αβγ-日本語",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.NotEqual(Guid.Empty, result.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_LongRuleId_GeneratesValidGuid()
    {
        var longId = new string('a', 1000);
        var policy = CreatePolicy(new Rule
        {
            Id = longId,
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.NotEqual(Guid.Empty, result.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_SpecialCharRuleId_GeneratesValidGuid()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "rule!@#$%^&*()_+-=[]{}|;':\",./<>?",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.NotEqual(Guid.Empty, result.Filters[0].FilterKey);
    }

    [Fact]
    public void Compile_SimilarRules_GenerateUniqueGuids()
    {
        // Test collision resistance for similar but not identical rules
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "rule-1", Action = "block", Direction = "outbound", Protocol = "tcp", Remote = new EndpointFilter { Ports = "443" }, Enabled = true },
                new Rule { Id = "rule-2", Action = "block", Direction = "outbound", Protocol = "tcp", Remote = new EndpointFilter { Ports = "443" }, Enabled = true },
                new Rule { Id = "rule-3", Action = "block", Direction = "outbound", Protocol = "tcp", Remote = new EndpointFilter { Ports = "443" }, Enabled = true }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Filters.Count);

        var guids = result.Filters.Select(f => f.FilterKey).ToHashSet();
        Assert.Equal(3, guids.Count); // All GUIDs should be unique
    }

    // ========================================
    // Display Name/Description Tests
    // ========================================

    [Fact]
    public void Compile_GeneratesDisplayName()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "my-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Contains("WfpTrafficControl", result.Filters[0].DisplayName);
        Assert.Contains("my-rule", result.Filters[0].DisplayName);
    }

    [Fact]
    public void Compile_GeneratesDescription()
    {
        var policy = CreatePolicy(new Rule
        {
            Id = "my-rule",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp",
            Remote = new EndpointFilter { Ip = "1.1.1.1", Ports = "443" },
            Enabled = true
        });

        var result = RuleCompiler.Compile(policy);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Filters);
        Assert.Contains("block", result.Filters[0].Description.ToLower());
        Assert.Contains("tcp", result.Filters[0].Description.ToLower());
        Assert.Contains("1.1.1.1", result.Filters[0].Description);
    }

    // ========================================
    // Multiple Error Tests
    // ========================================

    [Fact]
    public void Compile_MultipleUnsupportedRules_ReturnsAllErrors()
    {
        var policy = new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule>
            {
                new Rule { Id = "both-rule", Action = "block", Direction = "both", Protocol = "tcp", Enabled = true },
                new Rule { Id = "inbound-udp-rule", Action = "block", Direction = "inbound", Protocol = "udp", Enabled = true },
                new Rule { Id = "valid-outbound-tcp", Action = "block", Direction = "outbound", Protocol = "tcp", Enabled = true },
                new Rule { Id = "valid-outbound-udp", Action = "block", Direction = "outbound", Protocol = "udp", Enabled = true },
                new Rule { Id = "valid-inbound", Action = "block", Direction = "inbound", Protocol = "tcp", Enabled = true }
            }
        };

        var result = RuleCompiler.Compile(policy);

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Count); // "both" direction and inbound udp errors
        Assert.Equal(3, result.Filters.Count); // valid outbound tcp, outbound udp, and inbound tcp rules compiled
    }

    // ========================================
    // Helper Methods
    // ========================================

    private static Policy CreatePolicy(Rule rule)
    {
        return new Policy
        {
            Version = "1.0.0",
            DefaultAction = "allow",
            UpdatedAt = DateTime.UtcNow,
            Rules = new List<Rule> { rule }
        };
    }
}

/// <summary>
/// Tests for ApplyRequest/ApplyResponse IPC messages.
/// </summary>
public class ApplyIpcMessageTests
{
    [Fact]
    public void ApplyRequest_HasCorrectType()
    {
        var request = new ApplyRequest { PolicyPath = @"C:\test.json" };
        Assert.Equal("apply", request.Type);
    }

    [Fact]
    public void ParseRequest_ApplyRequest_ParsesCorrectly()
    {
        var json = "{\"type\":\"apply\",\"policyPath\":\"C:\\\\test.json\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsSuccess);
        Assert.IsType<ApplyRequest>(result.Value);
        var request = (ApplyRequest)result.Value;
        Assert.Equal(@"C:\test.json", request.PolicyPath);
    }

    [Fact]
    public void ParseRequest_ApplyRequest_MissingPath_ReturnsError()
    {
        var json = "{\"type\":\"apply\"}";
        var result = IpcMessageParser.ParseRequest(json);

        Assert.True(result.IsFailure);
        Assert.Contains("policyPath", result.Error.Message);
    }

    [Fact]
    public void ApplyResponse_Success_SetsAllFields()
    {
        var response = ApplyResponse.Success(
            filtersCreated: 5,
            filtersRemoved: 3,
            rulesSkipped: 2,
            policyVersion: "1.0.0",
            totalRules: 10,
            warnings: new List<string> { "Warning 1" });

        Assert.True(response.Ok);
        Assert.Null(response.Error);
        Assert.Equal(5, response.FiltersCreated);
        Assert.Equal(3, response.FiltersRemoved);
        Assert.Equal(2, response.RulesSkipped);
        Assert.Equal("1.0.0", response.PolicyVersion);
        Assert.Equal(10, response.TotalRules);
        Assert.Single(response.Warnings);
    }

    [Fact]
    public void ApplyResponse_Failure_SetsError()
    {
        var response = ApplyResponse.Failure("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void ApplyResponse_CompilationFailed_IncludesErrors()
    {
        var compilationResult = new CompilationResult();
        compilationResult.AddError("rule-1", "Error 1");
        compilationResult.AddError("rule-2", "Error 2");
        compilationResult.AddWarning("Warning 1");

        var response = ApplyResponse.CompilationFailed(compilationResult);

        Assert.False(response.Ok);
        Assert.Contains("compilation failed", response.Error!.ToLower());
        Assert.Equal(2, response.CompilationErrors.Count);
        Assert.Equal("rule-1", response.CompilationErrors[0].RuleId);
        Assert.Single(response.Warnings);
    }

    [Fact]
    public void SerializeResponse_ApplyResponse_ProducesValidJson()
    {
        var response = ApplyResponse.Success(5, 3, 2, "1.0.0", 10, new List<string>());
        var json = IpcMessageParser.SerializeResponse(response);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"filtersCreated\":5", json);
        Assert.Contains("\"filtersRemoved\":3", json);
        Assert.Contains("\"policyVersion\":\"1.0.0\"", json);
    }
}
