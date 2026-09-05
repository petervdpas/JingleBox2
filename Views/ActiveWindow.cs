using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace JingleBox2.Views;

/// <summary>
/// Which of this application's windows somebody is working in.
/// </summary>
/// <remarks>
/// A door, and the same shape as the other doors here: there is one application with one set of
/// windows and handing that about would be handing the same thing about under another name.
///
/// It exists because a line on a device's Menu is pressed inside a panel, and a panel is a
/// control in the published library that knows nothing about this application's windows and
/// never should. The line has to open a window over something all the same, and what a person
/// means by "over something" is the window they are looking at, which is this.
///
/// Nothing where there is no desktop lifetime, which is a plugin's own process and a test: a
/// window opened over nothing is put wherever the desktop puts it rather than refused.
/// </remarks>
public static class ActiveWindow
{
    /// <summary>The window with the focus, or the main one, or nothing.</summary>
    /// <remarks>
    /// The main window is the fallback because a menu can be pressed while a panel's own window
    /// is closing, and a moment with nothing active is a moment where over the main window is
    /// still the right answer.
    /// </remarks>
    public static Window? Now
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            return desktop.Windows.FirstOrDefault(one => one.IsActive) ?? desktop.MainWindow;
        }
    }
}
