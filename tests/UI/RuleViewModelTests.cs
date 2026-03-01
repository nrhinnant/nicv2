using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for RuleViewModel validation and JSON preview functionality.
/// </summary>
public sealed class RuleViewModelTests
{
    [Fact]
    public void InitialState_HasCorrectDefaults()
    {
        // Arrange & Act
        var vm = new RuleViewModel();

        // Assert
        Assert.Equal("", vm.Id);
        Assert.Equal("block", vm.Action);
        Assert.Equal("outbound", vm.Direction);
        Assert.Equal("tcp", vm.Protocol);
        Assert.Equal("", vm.Process);
        Assert.Equal("", vm.RemoteIp);
        Assert.Equal("", vm.RemotePorts);
        Assert.Equal("", vm.LocalIp);
        Assert.Equal("", vm.LocalPorts);
        Assert.Equal(100, vm.Priority);
        Assert.True(vm.Enabled);
        Assert.Equal("", vm.Comment);
    }

    [Fact]
    public void Summary_WithNoFields_ReturnsAny()
    {
        // Arrange
        var vm = new RuleViewModel();

        // Assert
        Assert.Equal("(any)", vm.Summary);
    }

    [Fact]
    public void Summary_WithRemoteIp_IncludesIp()
    {
        // Arrange
        var vm = new RuleViewModel { RemoteIp = "192.168.1.0/24" };

        // Assert
        Assert.Contains("192.168.1.0/24", vm.Summary);
    }

    [Fact]
    public void Summary_WithRemotePorts_IncludesPorts()
    {
        // Arrange
        var vm = new RuleViewModel { RemotePorts = "443" };

        // Assert
        Assert.Contains(":443", vm.Summary);
    }

    [Fact]
    public void Summary_WithProcess_IncludesProcessName()
    {
        // Arrange
        var vm = new RuleViewModel { Process = @"C:\Windows\System32\notepad.exe" };

        // Assert
        Assert.Contains("notepad.exe", vm.Summary);
    }

    // JSON Preview Tests

    [Fact]
    public void JsonPreview_ContainsRuleId()
    {
        // Arrange
        var vm = new RuleViewModel { Id = "test-rule-123" };

        // Assert
        Assert.Contains("test-rule-123", vm.JsonPreview);
    }

    [Fact]
    public void JsonPreview_ContainsAction()
    {
        // Arrange
        var vm = new RuleViewModel { Action = "allow" };

        // Assert
        Assert.Contains("\"action\": \"allow\"", vm.JsonPreview);
    }

    [Fact]
    public void JsonPreview_OmitsNullFields()
    {
        // Arrange
        var vm = new RuleViewModel
        {
            Id = "test",
            Action = "block",
            Direction = "outbound",
            Protocol = "tcp"
        };

        // Assert - should not contain process, local, remote when empty
        Assert.DoesNotContain("\"process\":", vm.JsonPreview);
        Assert.DoesNotContain("\"local\":", vm.JsonPreview);
        Assert.DoesNotContain("\"remote\":", vm.JsonPreview);
    }

    [Fact]
    public void JsonPreview_IncludesRemoteEndpoint()
    {
        // Arrange
        var vm = new RuleViewModel
        {
            Id = "test",
            RemoteIp = "10.0.0.0/8",
            RemotePorts = "443"
        };

        // Assert
        Assert.Contains("\"remote\":", vm.JsonPreview);
        Assert.Contains("10.0.0.0/8", vm.JsonPreview);
        Assert.Contains("443", vm.JsonPreview);
    }

    // IP/CIDR Validation Tests

    [Theory]
    [InlineData("")] // Empty is valid (optional)
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("192.168.1.0/24")]
    [InlineData("10.0.0.0/8")]
    [InlineData("0.0.0.0/0")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("::1/128")]
    public void RemoteIpError_ValidIps_ReturnsNull(string ip)
    {
        // Arrange
        var vm = new RuleViewModel { RemoteIp = ip };

        // Assert
        Assert.Null(vm.RemoteIpError);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("999.999.999.999")]
    [InlineData("192.168.1.1.1")]
    [InlineData("abc.def.ghi.jkl")]
    public void RemoteIpError_InvalidIps_ReturnsError(string ip)
    {
        // Arrange
        var vm = new RuleViewModel { RemoteIp = ip };

        // Assert
        Assert.NotNull(vm.RemoteIpError);
        Assert.Contains("Invalid", vm.RemoteIpError);
    }

    [Theory]
    [InlineData("192.168.1.1/-1")] // Negative prefix
    [InlineData("192.168.1.1/abc")] // Non-numeric prefix
    [InlineData("192.168.1.1/24/8")] // Multiple slashes
    [InlineData("192.168.1.1/999")] // Very large prefix
    public void RemoteIpError_InvalidCidr_ReturnsError(string cidr)
    {
        // Arrange
        var vm = new RuleViewModel { RemoteIp = cidr };

        // Assert
        Assert.NotNull(vm.RemoteIpError);
    }

    [Fact]
    public void LocalIpError_ValidIp_ReturnsNull()
    {
        // Arrange
        var vm = new RuleViewModel { LocalIp = "192.168.1.1" };

        // Assert
        Assert.Null(vm.LocalIpError);
    }

    [Fact]
    public void LocalIpError_InvalidIp_ReturnsError()
    {
        // Arrange
        var vm = new RuleViewModel { LocalIp = "invalid" };

        // Assert
        Assert.NotNull(vm.LocalIpError);
    }

    // Port Validation Tests

    [Theory]
    [InlineData("")] // Empty is valid (optional)
    [InlineData("80")]
    [InlineData("443")]
    [InlineData("0")]
    [InlineData("65535")]
    [InlineData("80-443")]
    [InlineData("1024-65535")]
    [InlineData("80,443")]
    [InlineData("80,443,8080")]
    [InlineData("80,443,8080-9000")]
    [InlineData("22, 80, 443")] // With spaces
    public void RemotePortsError_ValidPorts_ReturnsNull(string ports)
    {
        // Arrange
        var vm = new RuleViewModel { RemotePorts = ports };

        // Assert
        Assert.Null(vm.RemotePortsError);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("65536")] // Too high
    [InlineData("-1")] // Negative
    [InlineData("80-")] // Incomplete range
    [InlineData("-443")] // Incomplete range
    [InlineData("443-80")] // Start > end
    [InlineData("80-443-1024")] // Multiple dashes
    public void RemotePortsError_InvalidPorts_ReturnsError(string ports)
    {
        // Arrange
        var vm = new RuleViewModel { RemotePorts = ports };

        // Assert
        Assert.NotNull(vm.RemotePortsError);
    }

    [Fact]
    public void LocalPortsError_ValidPorts_ReturnsNull()
    {
        // Arrange
        var vm = new RuleViewModel { LocalPorts = "80,443" };

        // Assert
        Assert.Null(vm.LocalPortsError);
    }

    [Fact]
    public void LocalPortsError_InvalidPorts_ReturnsError()
    {
        // Arrange
        var vm = new RuleViewModel { LocalPorts = "invalid" };

        // Assert
        Assert.NotNull(vm.LocalPortsError);
    }

    // HasValidationErrors Tests

    [Fact]
    public void HasValidationErrors_NoErrors_ReturnsFalse()
    {
        // Arrange
        var vm = new RuleViewModel
        {
            RemoteIp = "192.168.1.1",
            RemotePorts = "443",
            LocalIp = "10.0.0.1",
            LocalPorts = "80"
        };

        // Assert
        Assert.False(vm.HasValidationErrors);
    }

    [Fact]
    public void HasValidationErrors_WithRemoteIpError_ReturnsTrue()
    {
        // Arrange
        var vm = new RuleViewModel { RemoteIp = "invalid" };

        // Assert
        Assert.True(vm.HasValidationErrors);
    }

    [Fact]
    public void HasValidationErrors_WithRemotePortsError_ReturnsTrue()
    {
        // Arrange
        var vm = new RuleViewModel { RemotePorts = "invalid" };

        // Assert
        Assert.True(vm.HasValidationErrors);
    }

    [Fact]
    public void HasValidationErrors_WithLocalIpError_ReturnsTrue()
    {
        // Arrange
        var vm = new RuleViewModel { LocalIp = "invalid" };

        // Assert
        Assert.True(vm.HasValidationErrors);
    }

    [Fact]
    public void HasValidationErrors_WithLocalPortsError_ReturnsTrue()
    {
        // Arrange
        var vm = new RuleViewModel { LocalPorts = "invalid" };

        // Assert
        Assert.True(vm.HasValidationErrors);
    }

    // Property Change Notification Tests

    [Fact]
    public void PropertyChanged_UpdatesJsonPreview()
    {
        // Arrange
        var vm = new RuleViewModel { Id = "initial" };
        var initialJson = vm.JsonPreview;

        // Act
        vm.Id = "updated";

        // Assert
        Assert.NotEqual(initialJson, vm.JsonPreview);
        Assert.Contains("updated", vm.JsonPreview);
    }

    [Fact]
    public void PropertyChanged_UpdatesValidation()
    {
        // Arrange
        var vm = new RuleViewModel { RemoteIp = "192.168.1.1" };
        Assert.Null(vm.RemoteIpError);

        // Act
        vm.RemoteIp = "invalid";

        // Assert
        Assert.NotNull(vm.RemoteIpError);
    }

    // Static Properties Tests

    [Fact]
    public void AvailableActions_ContainsAllowAndBlock()
    {
        // Assert
        Assert.Contains("allow", RuleViewModel.AvailableActions);
        Assert.Contains("block", RuleViewModel.AvailableActions);
        Assert.Equal(2, RuleViewModel.AvailableActions.Length);
    }

    [Fact]
    public void AvailableDirections_ContainsAllValues()
    {
        // Assert
        Assert.Contains("inbound", RuleViewModel.AvailableDirections);
        Assert.Contains("outbound", RuleViewModel.AvailableDirections);
        Assert.Contains("both", RuleViewModel.AvailableDirections);
        Assert.Equal(3, RuleViewModel.AvailableDirections.Length);
    }

    [Fact]
    public void AvailableProtocols_ContainsAllValues()
    {
        // Assert
        Assert.Contains("tcp", RuleViewModel.AvailableProtocols);
        Assert.Contains("udp", RuleViewModel.AvailableProtocols);
        Assert.Contains("any", RuleViewModel.AvailableProtocols);
        Assert.Equal(3, RuleViewModel.AvailableProtocols.Length);
    }
}
