using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace JingleBox2.Views;

/// <summary>
/// Asks for a name and gives it back, or gives back nothing when it is cancelled.
/// </summary>
/// <remarks>
/// The same shape as the confirm dialog, with a box in it. Renaming a thing in a list wants a
/// dialog rather than an editable row: the row has to stay readable while you are picking
/// through the list, and a box that is only sometimes a box is a row you cannot trust.
/// </remarks>
public partial class NameDialog : Window
{
    public NameDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Shows the prompt over a window. Returns the trimmed name, or null for cancelled and for
    /// a name with nothing in it.
    /// </summary>
    public static async Task<string?> ShowAsync(Window owner, string title, string prompt, string current)
    {
        var dialog = new NameDialog { Title = title };

        var promptText = dialog.FindControl<TextBlock>("PromptText");
        if (promptText != null) promptText.Text = prompt;

        var box = dialog.FindControl<TextBox>("NameBox");

        if (box != null)
        {
            box.Text = current;

            // Opened with the old name selected, so typing replaces it and the arrow keys keep it.
            dialog.Opened += (_, _) =>
            {
                box.Focus();
                box.SelectAll();
            };
        }

        return await dialog.ShowDialog<string?>(owner);
    }

    /// <summary>
    /// The same prompt over the app's main window. Answers null when there is no window at all,
    /// so a headless run renames nothing rather than throwing.
    /// </summary>
    public static Task<string?> AskAsync(string title, string prompt, string current)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return Task.FromResult<string?>(null);

        return ShowAsync(desktop.MainWindow, title, prompt, current);
    }

    private void Name_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        Confirm_Click(sender, e);
        e.Handled = true;
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        string wanted = (this.FindControl<TextBox>("NameBox")?.Text ?? "").Trim();

        Close(wanted.Length == 0 ? null : wanted);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
