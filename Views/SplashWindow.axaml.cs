using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;

namespace JingleBox2.Views;

/// <summary>
/// What is on screen while the application is being built.
/// </summary>
/// <remarks>
/// The window everything hangs off does real work before it can be shown: it reads the
/// settings, opens the log, walks the machines folder, brings up the audio engine and builds
/// every view model behind it. All of that is on the drawing thread and none of it can be
/// hurried, so without this the application is a taskbar entry and nothing else for as long as
/// it takes.
///
/// It says what it is doing rather than only that it is doing something, because the two slow
/// steps are the two worth naming: a machines folder read off a cold disc, and BASS opening a
/// device. When somebody reports that the application takes a while to start, the last line
/// this showed is the answer.
///
/// Deliberately not themed. It is up before the settings have been read, so there is no chosen
/// theme to wear yet, and it is the machine's own purple in every one of them.
/// </remarks>
public partial class SplashWindow : Window
{
    /// <summary>Builds the window and writes the build number into it.</summary>
    public SplashWindow()
    {
        InitializeComponent();

        VersionText.Text = Version();
    }

    /// <summary>
    /// Squares the corners off where the machine will not give us a transparent window.
    /// </summary>
    /// <remarks>
    /// The rounded corners are a hole in the window, and a hole needs the compositor's
    /// permission. Where that is refused, and it is refused on a Windows box with composition
    /// off, in a remote session, and on a Linux desktop running without a compositor, what is
    /// outside the rounding is not nothing: it is the window's own background, which is
    /// transparent, which draws as black. The splash would arrive with four black corners on
    /// exactly the machines nobody tests on.
    ///
    /// So it is asked rather than assumed. Granted, the corners stay round and the window keeps
    /// its hole; refused, the frame is squared off and the window is filled with the same purple,
    /// which is a plain rectangle and correct. Asked here because the answer is not known until
    /// there is a window: it is what the platform actually gave, not what was hinted for.
    /// </remarks>
    /// <param name="e">Unused: the question is about the window rather than about the opening.</param>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
        {
            Background = Frame.Background;
            Frame.CornerRadius = new CornerRadius(0);
        }
    }

    /// <summary>
    /// Says what the application is busy with, for the line under the version.
    /// </summary>
    /// <remarks>
    /// Told rather than watched, because the steps it names are plain statements in a
    /// constructor and not something that announces itself. A splash that has to be subscribed
    /// to would be a startup with a notification system in front of it.
    /// </remarks>
    /// <param name="what">The step now under way, in the words somebody reading a log would want.</param>
    public void Doing(string what) => DoingText.Text = what;

    /// <summary>
    /// The build, without the commit that the informational version carries after a plus.
    /// </summary>
    /// <remarks>
    /// Empty when the attribute is missing, which is a run from somewhere that did not stamp
    /// one. The line simply stays blank rather than showing a word standing in for a number.
    /// </remarks>
    private static string Version()
    {
        string? said = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(said)) return "";

        int plus = said.IndexOf('+');

        return "v" + (plus > 0 ? said[..plus] : said);
    }
}
