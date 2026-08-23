using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;

namespace JingleBox2.Views;

/// <summary>
/// The part every modal has in common: find the window to sit over, and come back with an answer.
/// </summary>
/// <remarks>
/// Each dialog was carrying its own copy of this, which is two things to get right per dialog
/// and two places to forget the headless case. A run with no window has to answer rather than
/// throw, because "no" and "cancelled" are the safe answers and a test should get them without
/// a screen.
/// </remarks>
public static class Dialog
{
    /// <summary>The window a modal opens over, or null when there is none.</summary>
    public static Window? Owner =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    /// <summary>
    /// Shows a window modally over the app's own and gives back what it closed with.
    /// </summary>
    /// <param name="whenNone">
    /// What to answer when there is no window to sit over. The one that changes nothing: false
    /// for a confirm, null for a name.
    /// </param>
    public static Task<T> ShowAsync<T>(Window dialog, T whenNone)
    {
        var owner = Owner;

        return owner == null ? Task.FromResult(whenNone) : dialog.ShowDialog<T>(owner);
    }
}
