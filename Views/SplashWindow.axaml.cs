using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using JingleBox2.UI.Interfaces;

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
/// It also names what is on the rack, both worlds, as each folder is read. That is where a
/// first run does its one piece of writing: what ships beside the program is copied into the
/// application folder the first time it is offered, so the names arriving here are the names of
/// the boxes that were just taken. Every box on the rack rather than the shipped ones, since a
/// machine somebody built themselves is exactly the one they want to see arrive.
///
/// Deliberately not themed. It is up before the settings have been read, so there is no chosen
/// theme to wear yet, and it is the machine's own purple in every one of them.
/// </remarks>
public partial class SplashWindow : Window, IStartupLines
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

    /// <inheritdoc/>
    /// <remarks>
    /// The line under the version, and it takes whatever was under it with it: what was being
    /// read a moment ago has nothing to do with the step now under way, and a name left standing
    /// beneath a new heading is the one thing on here that could say something untrue.
    /// </remarks>
    public void Doing(string what)
    {
        UnderText.Text = "";

        Said(DoingText, what);
    }

    /// <inheritdoc/>
    /// <remarks>The indented line, which is the only one that moves while a heading stands.</remarks>
    public void Under(string one) => Said(UnderText, one);

    /// <inheritdoc/>
    /// <remarks>
    /// One name at a time, under the heading that says the devices are being read. The name
    /// alone, since the line above it is already the rest of the sentence.
    ///
    /// Sorted here rather than trusted, since what comes back is the order the disc handed the
    /// folders over in and that is not an order at all. Nothing at all is said about a world
    /// with nothing in it, because the other one may still have something: what is on the line
    /// then is the step, which is true.
    /// </remarks>
    public void Devices(IEnumerable<string> names)
    {
        var said = names.Where(name => !string.IsNullOrWhiteSpace(name))
                        .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase);

        foreach (string name in said) Under(name);
    }

    /// <summary>
    /// How long each thing the splash says stays up before the next thing is said.
    /// </summary>
    /// <remarks>
    /// A deliberate wait and not a measurement. The work being named is milliseconds, so a line
    /// said on the way past would never be painted at all: what is worth showing is worth
    /// showing for long enough to read.
    ///
    /// One number for every kind of line, steps and device names alike, because to somebody
    /// watching they are one list of things going past and a list that changes pace reads as a
    /// machine hesitating rather than as two kinds of line.
    ///
    /// It costs less than it looks, since the splash is already held for a shortest stay
    /// whatever happens, which is <c>App.SplashLeast</c> and is two and a half seconds: what
    /// this really does is spend that wait on something to look at. Past that it is paid by the
    /// line, and there is deliberately no ceiling on the whole run: a rack of thirty devices is
    /// thirty names, and racing them past to fit a budget would be showing them to nobody.
    /// </remarks>
    private static readonly TimeSpan LineHold = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Writes a line, draws it, and leaves it up long enough to be read.
    /// </summary>
    /// <remarks>
    /// Everything the splash says is said from the drawing thread, in the middle of the work it
    /// is describing, so nothing here would be painted until that work was finished and the line
    /// had already been replaced by the next one. On a machine slow enough to want a splash that
    /// is every line of it: the reader would see "Starting" and then the application.
    ///
    /// So the queue is run as far as the frame, which paints what was just set and nothing else:
    /// the work going on above is at a lower priority than this and cannot be re-entered by it.
    /// Then the thread is held, which is the same thread the work is on, deliberately: a line that is overtaken by the next one before the screen has been
    /// looked at was not shown, whatever the code says.
    /// </remarks>
    /// <param name="line">The line being written.</param>
    /// <param name="text">And what it now says.</param>
    private static void Said(TextBlock line, string text)
    {
        line.Text = text;

        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

        Thread.Sleep(LineHold);
    }

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
