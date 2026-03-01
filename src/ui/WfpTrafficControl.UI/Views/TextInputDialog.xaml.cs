using System.Windows;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Dialog for multiline text input.
/// </summary>
public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the prompt text displayed above the input area.
    /// </summary>
    public string Prompt
    {
        get => PromptText.Text;
        set => PromptText.Text = value;
    }

    /// <summary>
    /// Gets or sets the input text.
    /// </summary>
    public string InputText
    {
        get => InputTextBox.Text;
        set => InputTextBox.Text = value;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
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
