using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for syslog/SIEM export settings.
/// </summary>
public partial class SyslogSettingsViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Available protocols.
    /// </summary>
    public static string[] AvailableProtocols => new[] { "UDP", "TCP", "TLS" };

    /// <summary>
    /// Available formats.
    /// </summary>
    public static string[] AvailableFormats => new[] { "RFC 5424 (Syslog)", "CEF (SIEM)", "JSON" };

    /// <summary>
    /// Available facility codes.
    /// </summary>
    public static ObservableCollection<KeyValuePair<int, string>> AvailableFacilities => new()
    {
        new(0, "kernel"),
        new(1, "user"),
        new(2, "mail"),
        new(3, "daemon"),
        new(4, "auth"),
        new(5, "syslog"),
        new(6, "lpr"),
        new(7, "news"),
        new(8, "uucp"),
        new(9, "cron"),
        new(10, "authpriv"),
        new(11, "ftp"),
        new(16, "local0"),
        new(17, "local1"),
        new(18, "local2"),
        new(19, "local3"),
        new(20, "local4"),
        new(21, "local5"),
        new(22, "local6"),
        new(23, "local7")
    };

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _host = "localhost";

    [ObservableProperty]
    private int _port = 514;

    [ObservableProperty]
    private string _selectedProtocol = "UDP";

    [ObservableProperty]
    private string _selectedFormat = "RFC 5424 (Syslog)";

    [ObservableProperty]
    private int _facility = 16;

    [ObservableProperty]
    private string _appName = "WfpTrafficControl";

    [ObservableProperty]
    private bool _verifyCertificate = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string? _testResult;

    [ObservableProperty]
    private bool _testSucceeded;

    /// <summary>
    /// Whether TLS options should be shown (only when TLS protocol selected).
    /// </summary>
    public bool ShowTlsOptions => SelectedProtocol == "TLS";

    public SyslogSettingsViewModel(IServiceClient serviceClient, IDialogService dialogService)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    partial void OnEnabledChanged(bool value) => HasChanges = true;
    partial void OnHostChanged(string value) => HasChanges = true;
    partial void OnPortChanged(int value) => HasChanges = true;
    partial void OnSelectedProtocolChanged(string value)
    {
        HasChanges = true;
        OnPropertyChanged(nameof(ShowTlsOptions));
    }
    partial void OnSelectedFormatChanged(string value) => HasChanges = true;
    partial void OnFacilityChanged(int value) => HasChanges = true;
    partial void OnAppNameChanged(string value) => HasChanges = true;
    partial void OnVerifyCertificateChanged(bool value) => HasChanges = true;

    /// <summary>
    /// Loads the current syslog configuration from the service.
    /// </summary>
    [RelayCommand]
    public async Task LoadConfigAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Loading configuration...";

        try
        {
            var result = await _serviceClient.GetSyslogConfigAsync();

            if (result.IsSuccess && result.Value.Ok)
            {
                var config = result.Value.Config;
                Enabled = config.Enabled;
                Host = config.Host;
                Port = config.Port;
                SelectedProtocol = config.Protocol.ToString().ToUpperInvariant();
                SelectedFormat = MapFormatToDisplay(config.Format);
                Facility = config.Facility;
                AppName = config.AppName;
                VerifyCertificate = config.VerifyCertificate;

                HasChanges = false;
                StatusMessage = "Configuration loaded";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Error: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the syslog configuration to the service.
    /// </summary>
    [RelayCommand]
    public async Task SaveConfigAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        StatusMessage = "Saving configuration...";

        try
        {
            var config = BuildConfig();
            var result = await _serviceClient.SetSyslogConfigAsync(config);

            if (result.IsSuccess && result.Value.Ok)
            {
                HasChanges = false;
                StatusMessage = "Configuration saved";
                _dialogService.ShowInfo("Syslog configuration saved successfully.", "Configuration Saved");
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Error: {error}";
                _dialogService.ShowError($"Failed to save configuration: {error}", "Save Failed");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _dialogService.ShowError($"Failed to save configuration: {ex.Message}", "Save Failed");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Tests the syslog connection.
    /// </summary>
    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        if (IsTesting || !Enabled)
            return;

        IsTesting = true;
        TestResult = null;
        StatusMessage = "Testing connection...";

        try
        {
            // First save the current config if there are changes
            if (HasChanges)
            {
                var saveResult = await _serviceClient.SetSyslogConfigAsync(BuildConfig());
                if (saveResult.IsFailure || !saveResult.Value.Ok)
                {
                    TestResult = "Failed to save configuration before testing";
                    TestSucceeded = false;
                    StatusMessage = "Test failed";
                    return;
                }
                HasChanges = false;
            }

            var result = await _serviceClient.TestSyslogAsync();

            if (result.IsSuccess && result.Value.Ok && result.Value.Sent)
            {
                TestResult = result.Value.RttMs.HasValue
                    ? $"Test message sent successfully ({result.Value.RttMs}ms)"
                    : "Test message sent successfully";
                TestSucceeded = true;
                StatusMessage = "Test successful";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Test failed";
                TestResult = error;
                TestSucceeded = false;
                StatusMessage = "Test failed";
            }
        }
        catch (Exception ex)
        {
            TestResult = $"Error: {ex.Message}";
            TestSucceeded = false;
            StatusMessage = "Test failed";
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>
    /// Resets the form to default values.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        Enabled = false;
        Host = "localhost";
        Port = 514;
        SelectedProtocol = "UDP";
        SelectedFormat = "RFC 5424 (Syslog)";
        Facility = 16;
        AppName = "WfpTrafficControl";
        VerifyCertificate = true;
        HasChanges = true;
        TestResult = null;
    }

    private SyslogConfig BuildConfig()
    {
        return new SyslogConfig
        {
            Enabled = Enabled,
            Host = Host,
            Port = Port,
            Protocol = ParseProtocol(SelectedProtocol),
            Format = ParseFormat(SelectedFormat),
            Facility = Facility,
            AppName = AppName,
            VerifyCertificate = VerifyCertificate
        };
    }

    private static SyslogProtocol ParseProtocol(string protocol)
    {
        return protocol.ToUpperInvariant() switch
        {
            "UDP" => SyslogProtocol.Udp,
            "TCP" => SyslogProtocol.Tcp,
            "TLS" => SyslogProtocol.Tls,
            _ => SyslogProtocol.Udp
        };
    }

    private static SyslogFormat ParseFormat(string format)
    {
        return format switch
        {
            "RFC 5424 (Syslog)" => SyslogFormat.Rfc5424,
            "CEF (SIEM)" => SyslogFormat.Cef,
            "JSON" => SyslogFormat.Json,
            _ => SyslogFormat.Rfc5424
        };
    }

    private static string MapFormatToDisplay(SyslogFormat format)
    {
        return format switch
        {
            SyslogFormat.Rfc5424 => "RFC 5424 (Syslog)",
            SyslogFormat.Cef => "CEF (SIEM)",
            SyslogFormat.Json => "JSON",
            _ => "RFC 5424 (Syslog)"
        };
    }
}
