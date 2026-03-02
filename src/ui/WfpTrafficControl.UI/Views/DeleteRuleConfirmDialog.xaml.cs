using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Custom confirmation dialog for deleting firewall rules.
/// Shows detailed rule information before deletion.
/// </summary>
public partial class DeleteRuleConfirmDialog : Window
{
    public string RuleId { get; set; } = "";
    public string RuleAction { get; set; } = "";
    public string RuleDirection { get; set; } = "";
    public string RuleProtocol { get; set; } = "";
    public string RuleProcess { get; set; } = "";
    public string RuleRemote { get; set; } = "";
    public string RuleLocal { get; set; } = "";
    public string RulePriority { get; set; } = "";
    public string RuleComment { get; set; } = "";

    public DeleteRuleConfirmDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>
    /// Factory method to create a delete confirmation dialog from a rule.
    /// </summary>
    /// <param name="rule">The rule to display for deletion confirmation.</param>
    /// <returns>Configured delete confirmation dialog.</returns>
    /// <exception cref="ArgumentNullException">Thrown when rule is null.</exception>
    public static DeleteRuleConfirmDialog CreateFromRule(RuleViewModel rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var dialog = new DeleteRuleConfirmDialog
        {
            RuleId = rule.Id,
            RuleAction = rule.Action,
            RuleDirection = rule.Direction,
            RuleProtocol = rule.Protocol,
            RuleProcess = string.IsNullOrWhiteSpace(rule.Process) ? "Any" : rule.Process,
            RulePriority = rule.Priority.ToString()
        };

        // Format remote endpoint
        if (!string.IsNullOrWhiteSpace(rule.RemoteIp) || !string.IsNullOrWhiteSpace(rule.RemotePorts))
        {
            var ip = string.IsNullOrWhiteSpace(rule.RemoteIp) ? "*" : rule.RemoteIp;
            var ports = string.IsNullOrWhiteSpace(rule.RemotePorts) ? "*" : rule.RemotePorts;
            dialog.RuleRemote = $"{ip}:{ports}";
        }
        else
        {
            dialog.RuleRemote = "Any";
        }

        // Format local endpoint
        if (!string.IsNullOrWhiteSpace(rule.LocalIp) || !string.IsNullOrWhiteSpace(rule.LocalPorts))
        {
            var ip = string.IsNullOrWhiteSpace(rule.LocalIp) ? "*" : rule.LocalIp;
            var ports = string.IsNullOrWhiteSpace(rule.LocalPorts) ? "*" : rule.LocalPorts;
            dialog.RuleLocal = $"{ip}:{ports}";
        }
        else
        {
            dialog.RuleLocal = "Any";
        }

        // Comment (empty if not set)
        dialog.RuleComment = string.IsNullOrWhiteSpace(rule.Comment) ? "(none)" : rule.Comment;

        return dialog;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
