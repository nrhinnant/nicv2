using System.Windows;
using Microsoft.Win32;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// WPF implementation of dialog service.
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public void ShowSuccess(string message, string title = "Success")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <inheritdoc />
    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <inheritdoc />
    public void ShowWarning(string message, string title = "Warning")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <inheritdoc />
    public void ShowInfo(string message, string title = "Information")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <inheritdoc />
    public bool Confirm(string message, string title = "Confirm")
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public bool ConfirmWarning(string message, string title = "Warning")
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public string? ShowOpenFileDialog(string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", string title = "Open File")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? ShowSaveFileDialog(string filter = "JSON files (*.json)|*.json|All files (*.*)|*.*", string? defaultFileName = null, string title = "Save File")
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = title,
            FileName = defaultFileName ?? string.Empty
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
