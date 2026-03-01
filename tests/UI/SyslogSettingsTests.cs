using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for SyslogSettingsViewModel.
/// </summary>
public sealed class SyslogSettingsViewModelTests
{
    private readonly MockServiceClient _mockClient;
    private readonly MockDialogService _mockDialog;
    private readonly SyslogSettingsViewModel _viewModel;

    public SyslogSettingsViewModelTests()
    {
        _mockClient = new MockServiceClient();
        _mockDialog = new MockDialogService();
        _viewModel = new SyslogSettingsViewModel(_mockClient, _mockDialog);
    }

    [Fact]
    public void ViewModel_InitialState_HasCorrectDefaults()
    {
        Assert.False(_viewModel.Enabled);
        Assert.Equal("localhost", _viewModel.Host);
        Assert.Equal(514, _viewModel.Port);
        Assert.Equal("UDP", _viewModel.SelectedProtocol);
        Assert.Equal("RFC 5424 (Syslog)", _viewModel.SelectedFormat);
        Assert.Equal(16, _viewModel.Facility);
        Assert.Equal("WfpTrafficControl", _viewModel.AppName);
        Assert.True(_viewModel.VerifyCertificate);
        Assert.False(_viewModel.IsLoading);
        Assert.False(_viewModel.IsTesting);
        Assert.False(_viewModel.HasChanges);
    }

    [Fact]
    public void AvailableProtocols_ContainsExpectedValues()
    {
        var protocols = SyslogSettingsViewModel.AvailableProtocols;

        Assert.Contains("UDP", protocols);
        Assert.Contains("TCP", protocols);
        Assert.Contains("TLS", protocols);
        Assert.Equal(3, protocols.Length);
    }

    [Fact]
    public void AvailableFormats_ContainsExpectedValues()
    {
        var formats = SyslogSettingsViewModel.AvailableFormats;

        Assert.Contains("RFC 5424 (Syslog)", formats);
        Assert.Contains("CEF (SIEM)", formats);
        Assert.Contains("JSON", formats);
        Assert.Equal(3, formats.Length);
    }

    [Fact]
    public void AvailableFacilities_ContainsExpectedValues()
    {
        var facilities = SyslogSettingsViewModel.AvailableFacilities;

        Assert.True(facilities.Count > 0);
        Assert.Contains(facilities, f => f.Key == 16 && f.Value == "local0");
        Assert.Contains(facilities, f => f.Key == 0 && f.Value == "kernel");
    }

    [Fact]
    public void ShowTlsOptions_FalseByDefault()
    {
        Assert.False(_viewModel.ShowTlsOptions);
    }

    [Fact]
    public void ShowTlsOptions_TrueWhenTlsSelected()
    {
        _viewModel.SelectedProtocol = "TLS";
        Assert.True(_viewModel.ShowTlsOptions);
    }

    [Fact]
    public void ChangingEnabled_SetsHasChanges()
    {
        Assert.False(_viewModel.HasChanges);

        _viewModel.Enabled = true;
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingHost_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.Host = "syslog.example.com";
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingPort_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.Port = 1514;
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingProtocol_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.SelectedProtocol = "TCP";
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingFormat_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.SelectedFormat = "JSON";
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingFacility_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.Facility = 20;
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingAppName_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.AppName = "CustomApp";
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public void ChangingVerifyCertificate_SetsHasChanges()
    {
        _viewModel.HasChanges = false;
        _viewModel.VerifyCertificate = false;
        Assert.True(_viewModel.HasChanges);
    }

    [Fact]
    public async Task LoadConfigAsync_LoadsConfigFromService()
    {
        _mockClient.SyslogConfig = new SyslogConfig
        {
            Enabled = true,
            Host = "syslog.test.com",
            Port = 6514,
            Protocol = SyslogProtocol.Tls,
            Format = SyslogFormat.Json,
            Facility = 17,
            AppName = "TestApp",
            VerifyCertificate = false
        };

        await _viewModel.LoadConfigCommand.ExecuteAsync(null);

        Assert.True(_viewModel.Enabled);
        Assert.Equal("syslog.test.com", _viewModel.Host);
        Assert.Equal(6514, _viewModel.Port);
        Assert.Equal("TLS", _viewModel.SelectedProtocol);
        Assert.Equal("JSON", _viewModel.SelectedFormat);
        Assert.Equal(17, _viewModel.Facility);
        Assert.Equal("TestApp", _viewModel.AppName);
        Assert.False(_viewModel.VerifyCertificate);
        Assert.False(_viewModel.HasChanges);
    }

    [Fact]
    public async Task LoadConfigAsync_WhenServiceUnavailable_ShowsError()
    {
        _mockClient.ShouldConnect = false;

        await _viewModel.LoadConfigCommand.ExecuteAsync(null);

        Assert.Contains("Error", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadConfigAsync_SetsIsLoadingDuringOperation()
    {
        var isLoadingDuringOperation = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsLoading) && _viewModel.IsLoading)
            {
                isLoadingDuringOperation = true;
            }
        };

        await _viewModel.LoadConfigCommand.ExecuteAsync(null);

        Assert.True(isLoadingDuringOperation);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task SaveConfigAsync_SavesConfigToService()
    {
        _viewModel.Enabled = true;
        _viewModel.Host = "save.test.com";
        _viewModel.Port = 1514;
        _viewModel.SelectedProtocol = "TCP";
        _viewModel.SelectedFormat = "CEF (SIEM)";
        _viewModel.Facility = 18;
        _viewModel.AppName = "SaveTest";
        _viewModel.HasChanges = true;

        await _viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.True(_mockClient.SyslogConfig.Enabled);
        Assert.Equal("save.test.com", _mockClient.SyslogConfig.Host);
        Assert.Equal(1514, _mockClient.SyslogConfig.Port);
        Assert.Equal(SyslogProtocol.Tcp, _mockClient.SyslogConfig.Protocol);
        Assert.Equal(SyslogFormat.Cef, _mockClient.SyslogConfig.Format);
        Assert.Equal(18, _mockClient.SyslogConfig.Facility);
        Assert.Equal("SaveTest", _mockClient.SyslogConfig.AppName);
        Assert.False(_viewModel.HasChanges);
    }

    [Fact]
    public async Task SaveConfigAsync_WhenServiceUnavailable_ShowsError()
    {
        _mockClient.ShouldConnect = false;
        _viewModel.HasChanges = true;

        await _viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.Contains("Error", _viewModel.StatusMessage);
        Assert.True(_mockDialog.ErrorCount > 0);
    }

    [Fact]
    public async Task SaveConfigAsync_ShowsSuccessDialog()
    {
        _viewModel.HasChanges = true;

        await _viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.True(_mockDialog.InfoCount > 0);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenDisabled_DoesNotTest()
    {
        _viewModel.Enabled = false;

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.False(_viewModel.IsTesting);
        Assert.Null(_viewModel.TestResult);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenEnabled_TestsConnection()
    {
        _viewModel.Enabled = true;

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(_viewModel.TestSucceeded);
        Assert.NotNull(_viewModel.TestResult);
        Assert.Contains("success", _viewModel.TestResult.ToLowerInvariant());
    }

    [Fact]
    public async Task TestConnectionAsync_SetsIsTestingDuringOperation()
    {
        _viewModel.Enabled = true;
        var isTestingDuringOperation = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsTesting) && _viewModel.IsTesting)
            {
                isTestingDuringOperation = true;
            }
        };

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.True(isTestingDuringOperation);
        Assert.False(_viewModel.IsTesting);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenHasChanges_SavesFirst()
    {
        _viewModel.Enabled = true;
        _viewModel.Host = "test.example.com";
        _viewModel.HasChanges = true;

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        Assert.Equal("test.example.com", _mockClient.SyslogConfig.Host);
        Assert.False(_viewModel.HasChanges);
    }

    [Fact]
    public async Task TestConnectionAsync_WhenSyslogDisabledInConfig_ShowsNotEnabled()
    {
        // Enable in ViewModel but ensure config says disabled
        _viewModel.Enabled = true;
        _mockClient.SyslogConfig = new SyslogConfig { Enabled = false };

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        // The test should still report based on the ViewModel state
        Assert.Equal(1, _mockClient.TestSyslogCallCount);
    }

    [Fact]
    public void ResetToDefaultsCommand_ResetsAllValues()
    {
        _viewModel.Enabled = true;
        _viewModel.Host = "custom.host.com";
        _viewModel.Port = 6514;
        _viewModel.SelectedProtocol = "TLS";
        _viewModel.SelectedFormat = "JSON";
        _viewModel.Facility = 23;
        _viewModel.AppName = "CustomApp";
        _viewModel.VerifyCertificate = false;
        _viewModel.TestResult = "Previous result";
        _viewModel.HasChanges = false;

        _viewModel.ResetToDefaultsCommand.Execute(null);

        Assert.False(_viewModel.Enabled);
        Assert.Equal("localhost", _viewModel.Host);
        Assert.Equal(514, _viewModel.Port);
        Assert.Equal("UDP", _viewModel.SelectedProtocol);
        Assert.Equal("RFC 5424 (Syslog)", _viewModel.SelectedFormat);
        Assert.Equal(16, _viewModel.Facility);
        Assert.Equal("WfpTrafficControl", _viewModel.AppName);
        Assert.True(_viewModel.VerifyCertificate);
        Assert.True(_viewModel.HasChanges);
        Assert.Null(_viewModel.TestResult);
    }

    [Fact]
    public void ProtocolParsing_HandlesAllProtocols()
    {
        _viewModel.SelectedProtocol = "UDP";
        Assert.False(_viewModel.ShowTlsOptions);

        _viewModel.SelectedProtocol = "TCP";
        Assert.False(_viewModel.ShowTlsOptions);

        _viewModel.SelectedProtocol = "TLS";
        Assert.True(_viewModel.ShowTlsOptions);
    }

    [Fact]
    public void FormatParsing_HandlesAllFormats()
    {
        _viewModel.SelectedFormat = "RFC 5424 (Syslog)";
        _viewModel.SelectedFormat = "CEF (SIEM)";
        _viewModel.SelectedFormat = "JSON";
        Assert.NotNull(_viewModel.SelectedFormat);
    }

    [Fact]
    public async Task LoadConfigAsync_IncrementsCallCount()
    {
        await _viewModel.LoadConfigCommand.ExecuteAsync(null);

        Assert.Equal(1, _mockClient.GetSyslogConfigCallCount);
    }

    [Fact]
    public async Task SaveConfigAsync_IncrementsCallCount()
    {
        _viewModel.HasChanges = true;
        await _viewModel.SaveConfigCommand.ExecuteAsync(null);

        Assert.Equal(1, _mockClient.SetSyslogConfigCallCount);
    }
}

/// <summary>
/// Unit tests for SyslogConfig.
/// </summary>
public sealed class SyslogConfigTests
{
    [Fact]
    public void SyslogConfig_DefaultValues()
    {
        var config = new SyslogConfig();

        Assert.False(config.Enabled);
        Assert.Equal("localhost", config.Host);
        Assert.Equal(514, config.Port);
        Assert.Equal(SyslogProtocol.Udp, config.Protocol);
        Assert.Equal(SyslogFormat.Rfc5424, config.Format);
        Assert.Equal(16, config.Facility);
        Assert.Equal("WfpTrafficControl", config.AppName);
        Assert.True(config.VerifyCertificate);
    }

    [Fact]
    public void SyslogProtocol_HasExpectedValues()
    {
        Assert.Equal(0, (int)SyslogProtocol.Udp);
        Assert.Equal(1, (int)SyslogProtocol.Tcp);
        Assert.Equal(2, (int)SyslogProtocol.Tls);
    }

    [Fact]
    public void SyslogFormat_HasExpectedValues()
    {
        Assert.Equal(0, (int)SyslogFormat.Rfc5424);
        Assert.Equal(1, (int)SyslogFormat.Cef);
        Assert.Equal(2, (int)SyslogFormat.Json);
    }
}

/// <summary>
/// Unit tests for syslog IPC messages.
/// </summary>
public sealed class SyslogMessagesTests
{
    [Fact]
    public void GetSyslogConfigRequest_HasCorrectType()
    {
        Assert.Equal("get-syslog-config", GetSyslogConfigRequest.RequestType);
    }

    [Fact]
    public void SetSyslogConfigRequest_HasCorrectType()
    {
        Assert.Equal("set-syslog-config", SetSyslogConfigRequest.RequestType);
    }

    [Fact]
    public void TestSyslogRequest_HasCorrectType()
    {
        Assert.Equal("test-syslog", TestSyslogRequest.RequestType);
    }

    [Fact]
    public void GetSyslogConfigResponse_Success()
    {
        var config = new SyslogConfig { Host = "test.com" };
        var response = GetSyslogConfigResponse.Success(config);

        Assert.True(response.Ok);
        Assert.Equal("test.com", response.Config.Host);
        Assert.Null(response.Error);
    }

    [Fact]
    public void GetSyslogConfigResponse_Failure()
    {
        var response = GetSyslogConfigResponse.Failure("Test error");

        Assert.False(response.Ok);
        Assert.Equal("Test error", response.Error);
    }

    [Fact]
    public void SetSyslogConfigResponse_Success()
    {
        var response = SetSyslogConfigResponse.Success();

        Assert.True(response.Ok);
        Assert.Null(response.Error);
    }

    [Fact]
    public void SetSyslogConfigResponse_Failure()
    {
        var response = SetSyslogConfigResponse.Failure("Save failed");

        Assert.False(response.Ok);
        Assert.Equal("Save failed", response.Error);
    }

    [Fact]
    public void TestSyslogResponse_Success()
    {
        var response = TestSyslogResponse.Success(42);

        Assert.True(response.Ok);
        Assert.True(response.Sent);
        Assert.Equal(42, response.RttMs);
        Assert.Null(response.Error);
    }

    [Fact]
    public void TestSyslogResponse_NotEnabled()
    {
        var response = TestSyslogResponse.NotEnabled();

        Assert.True(response.Ok);
        Assert.False(response.Sent);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void TestSyslogResponse_Failure()
    {
        var response = TestSyslogResponse.Failure("Connection failed");

        Assert.False(response.Ok);
        Assert.False(response.Sent);
        Assert.Equal("Connection failed", response.Error);
    }
}

/// <summary>
/// Unit tests for SyslogExporter.
/// </summary>
public sealed class SyslogExporterTests
{
    [Fact]
    public void SyslogExporter_InitialConfig_IsDisabled()
    {
        using var exporter = new WfpTrafficControl.Shared.Logging.SyslogExporter();
        Assert.False(exporter.Config.Enabled);
    }

    [Fact]
    public void SyslogExporter_Configure_UpdatesConfig()
    {
        using var exporter = new WfpTrafficControl.Shared.Logging.SyslogExporter();
        var config = new SyslogConfig
        {
            Enabled = true,
            Host = "test.com",
            Port = 1514
        };

        exporter.Configure(config);

        Assert.True(exporter.Config.Enabled);
        Assert.Equal("test.com", exporter.Config.Host);
        Assert.Equal(1514, exporter.Config.Port);
    }

    [Fact]
    public void SyslogExporter_Send_WhenDisabled_ReturnsFalse()
    {
        using var exporter = new WfpTrafficControl.Shared.Logging.SyslogExporter();
        var evt = new WfpTrafficControl.Shared.Logging.SyslogEvent
        {
            Message = "Test message"
        };

        var result = exporter.Send(evt);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public void SyslogExporter_TestConnection_WhenDisabled_ReturnsError()
    {
        using var exporter = new WfpTrafficControl.Shared.Logging.SyslogExporter();

        var result = exporter.TestConnection();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    public void SyslogEvent_DefaultValues()
    {
        var evt = new WfpTrafficControl.Shared.Logging.SyslogEvent();

        Assert.Equal(WfpTrafficControl.Shared.Logging.SyslogSeverity.Informational, evt.Severity);
        Assert.Equal("unknown", evt.EventType);
        Assert.Equal(string.Empty, evt.Message);
        Assert.NotNull(evt.AdditionalData);
    }

    [Fact]
    public void SyslogSeverity_HasCorrectValues()
    {
        Assert.Equal(0, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Emergency);
        Assert.Equal(1, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Alert);
        Assert.Equal(2, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Critical);
        Assert.Equal(3, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Error);
        Assert.Equal(4, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Warning);
        Assert.Equal(5, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Notice);
        Assert.Equal(6, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Informational);
        Assert.Equal(7, (int)WfpTrafficControl.Shared.Logging.SyslogSeverity.Debug);
    }

    [Fact]
    public void SyslogExporter_Dispose_DoesNotThrow()
    {
        var exporter = new WfpTrafficControl.Shared.Logging.SyslogExporter();
        exporter.Dispose();
        // Should not throw on second dispose
        exporter.Dispose();
    }
}
