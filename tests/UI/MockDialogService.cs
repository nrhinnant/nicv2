using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Mock dialog service for testing ViewModels.
/// </summary>
public class MockDialogService : IDialogService
{
    // Configuration
    public bool ConfirmResult { get; set; } = true;
    public string? OpenFileResult { get; set; } = @"C:\test\policy.json";
    public string? SaveFileResult { get; set; } = @"C:\test\saved-policy.json";
    public string? TextInputResult { get; set; } = null;

    // Queue-based results for testing multiple sequential confirmations
    public Queue<bool>? ConfirmWarningResults { get; set; }

    // Call tracking
    public int SuccessCount { get; private set; }
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }
    public int ConfirmCount { get; private set; }
    public int ConfirmWarningCount { get; private set; }
    public int OpenFileCount { get; private set; }
    public int SaveFileCount { get; private set; }
    public int TextInputCount { get; private set; }

    public string? LastSuccessMessage { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public string? LastWarningMessage { get; private set; }
    public string? LastConfirmMessage { get; private set; }

    public void ShowSuccess(string message, string title = "Success")
    {
        SuccessCount++;
        LastSuccessMessage = message;
    }

    public void ShowError(string message, string title = "Error")
    {
        ErrorCount++;
        LastErrorMessage = message;
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        WarningCount++;
        LastWarningMessage = message;
    }

    public void ShowInfo(string message, string title = "Information")
    {
        InfoCount++;
    }

    public bool Confirm(string message, string title = "Confirm")
    {
        ConfirmCount++;
        LastConfirmMessage = message;
        return ConfirmResult;
    }

    public bool ConfirmWarning(string message, string title = "Warning")
    {
        ConfirmWarningCount++;
        LastConfirmMessage = message;

        // Use queue if provided, otherwise fall back to default
        if (ConfirmWarningResults != null && ConfirmWarningResults.Count > 0)
        {
            return ConfirmWarningResults.Dequeue();
        }

        return ConfirmResult;
    }

    public string? ShowOpenFileDialog(string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", string title = "Open File")
    {
        OpenFileCount++;
        return OpenFileResult;
    }

    public string? ShowSaveFileDialog(string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", string? defaultFileName = null, string title = "Save File")
    {
        SaveFileCount++;
        return SaveFileResult;
    }

    public string? ShowTextInputDialog(string prompt, string title = "Input", string? initialText = null)
    {
        TextInputCount++;
        return TextInputResult;
    }

    public void Reset()
    {
        SuccessCount = 0;
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        ConfirmCount = 0;
        ConfirmWarningCount = 0;
        OpenFileCount = 0;
        SaveFileCount = 0;
        TextInputCount = 0;
        LastSuccessMessage = null;
        LastErrorMessage = null;
        LastWarningMessage = null;
        LastConfirmMessage = null;
        ConfirmWarningResults = null;
        TextInputResult = null;
    }
}
