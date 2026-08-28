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
    /// <summary>
    /// The window a modal opens over, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Whichever window is in front, and not always the application's own. A dialog opened from
    /// inside a dialog is the case: the song list is already modal over the main window, and a
    /// confirmation owned by the main window is one the desktop is entitled to put behind the
    /// list that asked for it. What that looks like is a button that does nothing, because the
    /// question is on screen somewhere underneath and nobody can answer it.
    ///
    /// The last active one, since the windows are in the order they were made and the newest
    /// active one is the one that asked. The main window when none of them says it is active,
    /// which is the application not being focused at all.
    /// </remarks>
    public static Window? Owner
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            Window? front = null;

            foreach (var window in desktop.Windows)
                if (window.IsActive) front = window;

            return front ?? desktop.MainWindow;
        }
    }

    /// <summary>
    /// Shows a window modally over the app's own and gives back what it closed with.
    /// </summary>
    /// <param name="dialog">The window to show, already built with whatever it is asking about.</param>
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
