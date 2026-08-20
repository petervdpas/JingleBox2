using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace JingleBox2.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>Shows a modal yes/no prompt. Returns false if the user cancels or closes the window.</summary>
    public static Task<bool> ShowAsync(Window owner, string title, string message, string confirmText)
    {
        var dialog = new ConfirmDialog { Title = title };

        var messageText = dialog.FindControl<TextBlock>("MessageText");
        if (messageText != null) messageText.Text = message;

        var confirmButton = dialog.FindControl<Button>("ConfirmButton");
        if (confirmButton != null) confirmButton.Content = confirmText;

        return dialog.ShowDialog<bool>(owner);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);
}
