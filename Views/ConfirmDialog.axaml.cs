using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    /// <summary>
    /// The same prompt over the app's main window, for callers with no window to hand. Answers
    /// no when there is no window at all, so a headless run never destroys anything.
    /// </summary>
    public static Task<bool> AskAsync(string title, string message, string confirmText)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return Task.FromResult(false);

        return ShowAsync(desktop.MainWindow, title, message, confirmText);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);
}
