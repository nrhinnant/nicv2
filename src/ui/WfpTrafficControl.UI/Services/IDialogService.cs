namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Service for showing dialogs to the user.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a success message.
    /// </summary>
    void ShowSuccess(string message, string title = "Success");

    /// <summary>
    /// Shows an error message.
    /// </summary>
    void ShowError(string message, string title = "Error");

    /// <summary>
    /// Shows a warning message.
    /// </summary>
    void ShowWarning(string message, string title = "Warning");

    /// <summary>
    /// Shows an information message.
    /// </summary>
    void ShowInfo(string message, string title = "Information");

    /// <summary>
    /// Shows a confirmation dialog.
    /// </summary>
    /// <returns>True if the user confirmed, false otherwise.</returns>
    bool Confirm(string message, string title = "Confirm");

    /// <summary>
    /// Shows a confirmation dialog with a warning style.
    /// </summary>
    /// <returns>True if the user confirmed, false otherwise.</returns>
    bool ConfirmWarning(string message, string title = "Warning");

    /// <summary>
    /// Shows an open file dialog.
    /// </summary>
    /// <param name="filter">File filter (e.g., "JSON files (*.json)|*.json")</param>
    /// <param name="title">Dialog title</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowOpenFileDialog(string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", string title = "Open File");

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="filter">File filter</param>
    /// <param name="defaultFileName">Default file name</param>
    /// <param name="title">Dialog title</param>
    /// <returns>Selected file path, or null if cancelled.</returns>
    string? ShowSaveFileDialog(string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", string? defaultFileName = null, string title = "Save File");
}
