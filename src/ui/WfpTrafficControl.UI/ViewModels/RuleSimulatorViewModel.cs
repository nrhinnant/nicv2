using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.Views;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Rule Simulator ("What If" testing) view.
/// </summary>
public partial class RuleSimulatorViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSimulate))]
    private string _direction = "outbound";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSimulate))]
    private string _protocol = "tcp";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSimulate))]
    private string _remoteIp = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSimulate))]
    private string _remotePort = "";

    [ObservableProperty]
    private string _processPath = "";

    [ObservableProperty]
    private string _localIp = "";

    [ObservableProperty]
    private string _localPort = "";

    [ObservableProperty]
    private bool _isSimulating;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private bool _resultWouldAllow;

    [ObservableProperty]
    private string _resultSummary = "";

    [ObservableProperty]
    private string? _matchedRuleId;

    [ObservableProperty]
    private string? _matchedRuleAction;

    [ObservableProperty]
    private string? _matchedRuleComment;

    [ObservableProperty]
    private bool _usedDefaultAction;

    [ObservableProperty]
    private string? _policyVersion;

    [ObservableProperty]
    private bool _policyLoaded;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _showEvaluationTrace;

    public ObservableCollection<SimulateEvaluationStep> EvaluationTrace { get; } = new();

    public static string[] AvailableDirections { get; } = { "outbound", "inbound" };
    public static string[] AvailableProtocols { get; } = { "tcp", "udp" };

    public bool CanSimulate => !IsSimulating && !string.IsNullOrWhiteSpace(RemoteIp);

    public RuleSimulatorViewModel(IServiceClient serviceClient)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
    }

    [RelayCommand]
    private async Task SimulateAsync()
    {
        if (!CanSimulate) return;

        IsSimulating = true;
        HasResult = false;
        ErrorMessage = null;
        EvaluationTrace.Clear();

        try
        {
            int? remotePortInt = null;
            if (!string.IsNullOrWhiteSpace(RemotePort) && int.TryParse(RemotePort, out var port))
            {
                remotePortInt = port;
            }

            int? localPortInt = null;
            if (!string.IsNullOrWhiteSpace(LocalPort) && int.TryParse(LocalPort, out var lport))
            {
                localPortInt = lport;
            }

            var result = await _serviceClient.SimulateAsync(
                Direction,
                Protocol,
                RemoteIp,
                remotePortInt,
                string.IsNullOrWhiteSpace(ProcessPath) ? null : ProcessPath,
                string.IsNullOrWhiteSpace(LocalIp) ? null : LocalIp,
                localPortInt);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.Message;
                return;
            }

            var response = result.Value;

            if (!response.Ok)
            {
                ErrorMessage = response.Error ?? "Unknown error";
                return;
            }

            // Update result properties
            HasResult = true;
            ResultWouldAllow = response.WouldAllow;
            MatchedRuleId = response.MatchedRuleId;
            MatchedRuleAction = response.MatchedAction;
            MatchedRuleComment = response.MatchedRuleComment;
            UsedDefaultAction = response.UsedDefaultAction;
            PolicyVersion = response.PolicyVersion;
            PolicyLoaded = response.PolicyLoaded;

            // Build summary
            if (!response.PolicyLoaded)
            {
                ResultSummary = "No policy is loaded. All connections are allowed by default.";
            }
            else if (response.UsedDefaultAction)
            {
                ResultSummary = $"No rule matched. Default action '{response.DefaultAction}' would be applied.";
            }
            else
            {
                var action = response.WouldAllow ? "ALLOWED" : "BLOCKED";
                ResultSummary = $"Connection would be {action} by rule '{response.MatchedRuleId}'.";
            }

            // Update evaluation trace
            foreach (var step in response.EvaluationTrace)
            {
                EvaluationTrace.Add(step);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Simulation failed: {ex.Message}";
        }
        finally
        {
            IsSimulating = false;
        }
    }

    [RelayCommand]
    private void BrowseProcess()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Process Executable",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            ProcessPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void PickProcess()
    {
        var dialog = new ProcessPickerDialog();
        if (dialog.ShowDialog() == true && dialog.SelectedPath != null)
        {
            ProcessPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        Direction = "outbound";
        Protocol = "tcp";
        RemoteIp = "";
        RemotePort = "";
        ProcessPath = "";
        LocalIp = "";
        LocalPort = "";
        HasResult = false;
        ErrorMessage = null;
        EvaluationTrace.Clear();
    }

    [RelayCommand]
    private void ToggleEvaluationTrace()
    {
        ShowEvaluationTrace = !ShowEvaluationTrace;
    }

    [RelayCommand]
    private void CopyResult()
    {
        if (!HasResult) return;

        var text = new System.Text.StringBuilder();
        text.AppendLine($"Simulation Result: {(ResultWouldAllow ? "ALLOWED" : "BLOCKED")}");
        text.AppendLine($"Direction: {Direction}");
        text.AppendLine($"Protocol: {Protocol}");
        text.AppendLine($"Remote: {RemoteIp}:{RemotePort}");

        if (!string.IsNullOrEmpty(ProcessPath))
        {
            text.AppendLine($"Process: {ProcessPath}");
        }

        if (!string.IsNullOrEmpty(LocalIp) || !string.IsNullOrEmpty(LocalPort))
        {
            text.AppendLine($"Local: {LocalIp}:{LocalPort}");
        }

        text.AppendLine();
        text.AppendLine(ResultSummary);

        if (!UsedDefaultAction && !string.IsNullOrEmpty(MatchedRuleId))
        {
            text.AppendLine($"Matched Rule: {MatchedRuleId}");
            text.AppendLine($"Rule Action: {MatchedRuleAction}");
            if (!string.IsNullOrEmpty(MatchedRuleComment))
            {
                text.AppendLine($"Rule Comment: {MatchedRuleComment}");
            }
        }

        System.Windows.Clipboard.SetText(text.ToString());
    }

    /// <summary>
    /// Sets up a quick test with common values.
    /// </summary>
    public void SetQuickTest(string direction, string protocol, string remoteIp, int remotePort)
    {
        Direction = direction;
        Protocol = protocol;
        RemoteIp = remoteIp;
        RemotePort = remotePort.ToString();
    }
}
