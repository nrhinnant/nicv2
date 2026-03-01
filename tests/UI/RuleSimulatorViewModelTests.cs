using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for RuleSimulatorViewModel.
/// </summary>
public sealed class RuleSimulatorViewModelTests
{
    [Fact]
    public void InitialState_HasCorrectDefaults()
    {
        // Arrange & Act
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client);

        // Assert
        Assert.Equal("outbound", vm.Direction);
        Assert.Equal("tcp", vm.Protocol);
        Assert.Equal("", vm.RemoteIp);
        Assert.Equal("", vm.RemotePort);
        Assert.Equal("", vm.ProcessPath);
        Assert.Equal("", vm.LocalIp);
        Assert.Equal("", vm.LocalPort);
        Assert.False(vm.IsSimulating);
        Assert.False(vm.HasResult);
        Assert.False(vm.ShowEvaluationTrace);
    }

    [Fact]
    public void CanSimulate_FalseWhenRemoteIpEmpty()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = ""
        };

        // Assert
        Assert.False(vm.CanSimulate);
    }

    [Fact]
    public void CanSimulate_TrueWhenRemoteIpProvided()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Assert
        Assert.True(vm.CanSimulate);
    }

    [Fact]
    public void CanSimulate_FalseWhileSimulating()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act - Use reflection to set IsSimulating since it's private set
        var prop = typeof(RuleSimulatorViewModel).GetProperty("IsSimulating");
        prop?.SetValue(vm, true);

        // Assert
        Assert.False(vm.CanSimulate);
    }

    [Fact]
    public async Task SimulateAsync_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            Direction = "outbound",
            Protocol = "tcp",
            RemoteIp = "192.168.1.100",
            RemotePort = "443",
            ProcessPath = @"C:\test\app.exe",
            LocalIp = "10.0.0.1",
            LocalPort = "12345"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, client.SimulateCallCount);
        Assert.NotNull(client.LastSimulateRequest);
        Assert.Equal("outbound", client.LastSimulateRequest.Direction);
        Assert.Equal("tcp", client.LastSimulateRequest.Protocol);
        Assert.Equal("192.168.1.100", client.LastSimulateRequest.RemoteIp);
        Assert.Equal(443, client.LastSimulateRequest.RemotePort);
        Assert.Equal(@"C:\test\app.exe", client.LastSimulateRequest.ProcessPath);
        Assert.Equal("10.0.0.1", client.LastSimulateRequest.LocalIp);
        Assert.Equal(12345, client.LastSimulateRequest.LocalPort);
    }

    [Fact]
    public async Task SimulateAsync_SetsResultProperties()
    {
        // Arrange
        var client = new MockServiceClient
        {
            SimulateWouldAllow = false,
            SimulateMatchedRuleId = "test-block",
            SimulateMatchedAction = "block",
            SimulateUsedDefaultAction = false
        };
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.True(vm.HasResult);
        Assert.False(vm.ResultWouldAllow);
        Assert.Equal("test-block", vm.MatchedRuleId);
        Assert.Equal("block", vm.MatchedRuleAction);
        Assert.False(vm.UsedDefaultAction);
    }

    [Fact]
    public async Task SimulateAsync_AllowedResult_SetsCorrectSummary()
    {
        // Arrange
        var client = new MockServiceClient
        {
            SimulateWouldAllow = true,
            SimulateMatchedRuleId = "allow-all",
            SimulateMatchedAction = "allow",
            SimulateUsedDefaultAction = false
        };
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.True(vm.ResultWouldAllow);
        Assert.Contains("ALLOWED", vm.ResultSummary);
        Assert.Contains("allow-all", vm.ResultSummary);
    }

    [Fact]
    public async Task SimulateAsync_DefaultAction_SetsCorrectSummary()
    {
        // Arrange
        var client = new MockServiceClient
        {
            SimulateWouldAllow = true,
            SimulateUsedDefaultAction = true
        };
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.True(vm.UsedDefaultAction);
        Assert.Contains("No rule matched", vm.ResultSummary);
    }

    [Fact]
    public async Task SimulateAsync_NoPolicyLoaded_SetsCorrectSummary()
    {
        // Arrange
        var client = new MockServiceClient
        {
            SimulatePolicyLoaded = false
        };
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.False(vm.PolicyLoaded);
        Assert.Contains("No policy is loaded", vm.ResultSummary);
    }

    [Fact]
    public async Task SimulateAsync_ServiceError_SetsErrorMessage()
    {
        // Arrange
        var client = new MockServiceClient
        {
            ShouldConnect = false
        };
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.False(vm.HasResult);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Service not running", vm.ErrorMessage);
    }

    [Fact]
    public async Task SimulateAsync_PopulatesEvaluationTrace()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1"
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.NotEmpty(vm.EvaluationTrace);
    }

    [Fact]
    public void ClearForm_ResetsAllFields()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            Direction = "inbound",
            Protocol = "udp",
            RemoteIp = "10.0.0.1",
            RemotePort = "80",
            ProcessPath = @"C:\test.exe",
            LocalIp = "192.168.1.1",
            LocalPort = "12345"
        };

        // Act
        vm.ClearFormCommand.Execute(null);

        // Assert
        Assert.Equal("outbound", vm.Direction);
        Assert.Equal("tcp", vm.Protocol);
        Assert.Equal("", vm.RemoteIp);
        Assert.Equal("", vm.RemotePort);
        Assert.Equal("", vm.ProcessPath);
        Assert.Equal("", vm.LocalIp);
        Assert.Equal("", vm.LocalPort);
        Assert.False(vm.HasResult);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void ToggleEvaluationTrace_TogglesVisibility()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client);
        Assert.False(vm.ShowEvaluationTrace);

        // Act & Assert
        vm.ToggleEvaluationTraceCommand.Execute(null);
        Assert.True(vm.ShowEvaluationTrace);

        vm.ToggleEvaluationTraceCommand.Execute(null);
        Assert.False(vm.ShowEvaluationTrace);
    }

    [Fact]
    public void SetQuickTest_SetsConnectionParameters()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client);

        // Act
        vm.SetQuickTest("inbound", "udp", "10.0.0.1", 53);

        // Assert
        Assert.Equal("inbound", vm.Direction);
        Assert.Equal("udp", vm.Protocol);
        Assert.Equal("10.0.0.1", vm.RemoteIp);
        Assert.Equal("53", vm.RemotePort);
    }

    [Fact]
    public void AvailableDirections_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains("outbound", RuleSimulatorViewModel.AvailableDirections);
        Assert.Contains("inbound", RuleSimulatorViewModel.AvailableDirections);
        Assert.Equal(2, RuleSimulatorViewModel.AvailableDirections.Length);
    }

    [Fact]
    public void AvailableProtocols_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains("tcp", RuleSimulatorViewModel.AvailableProtocols);
        Assert.Contains("udp", RuleSimulatorViewModel.AvailableProtocols);
        Assert.Equal(2, RuleSimulatorViewModel.AvailableProtocols.Length);
    }

    [Fact]
    public async Task SimulateAsync_WithOptionalFieldsEmpty_PassesNulls()
    {
        // Arrange
        var client = new MockServiceClient();
        var vm = new RuleSimulatorViewModel(client)
        {
            RemoteIp = "192.168.1.1",
            RemotePort = "",  // Empty
            ProcessPath = "", // Empty
            LocalIp = "",     // Empty
            LocalPort = ""    // Empty
        };

        // Act
        await vm.SimulateCommand.ExecuteAsync(null);

        // Assert
        Assert.NotNull(client.LastSimulateRequest);
        Assert.Null(client.LastSimulateRequest.RemotePort);
        Assert.Null(client.LastSimulateRequest.ProcessPath);
        Assert.Null(client.LastSimulateRequest.LocalIp);
        Assert.Null(client.LastSimulateRequest.LocalPort);
    }
}
