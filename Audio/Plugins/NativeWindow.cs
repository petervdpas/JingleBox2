using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// What Windows has to be told, and what it will say back, about a window somebody else drew.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="XEmbed"/>, and it exists for the same reason and covers the
/// same ground: what one program owes another whose window it is holding. X11 asks for a
/// conversation and Windows asks for almost nothing, which is why this is the shorter of the two.
///
/// Three things. <see cref="Account"/> reads a window and what is inside it, for the log: a
/// plugin that draws nothing looks from here exactly like a plugin that was never given a
/// window, and the only way to tell those apart is to ask the window server what is actually
/// there. <see cref="ReadScalingProperly"/> and <see cref="ShareInput"/> are the two things a
/// plugin's own process has to say out loud, and both are only true of a process that has no
/// toolkit of its own: nothing else in this application would ever need either.
///
/// Every method answers empty off Windows, before anything is called, so nothing here ever looks
/// for user32 on a machine that has not got one.
/// </remarks>
internal static class NativeWindow
{
    /// <summary>The Win32 window library, by the name the loader knows it under.</summary>
    private const string Library = "user32.dll";

    /// <summary>Whether a handle really is a window. Everything else here is meaningless if not.</summary>
    [DllImport(Library)] private static extern bool IsWindow(nint window);

    /// <summary>Whether it is on screen, as far as its own style is concerned.</summary>
    [DllImport(Library)] private static extern bool IsWindowVisible(nint window);

    /// <summary>The name of the class it was made from, which says whose window it is.</summary>
    [DllImport(Library, CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassNameW(nint window, [Out] char[] name, int length);

    /// <summary>Where it is and how big, in screen coordinates.</summary>
    [DllImport(Library)] private static extern bool GetWindowRect(nint window, out Rect rect);

    /// <summary>Its parent, which is what says whether the plugin really put its window in ours.</summary>
    [DllImport(Library)] private static extern nint GetParent(nint window);

    /// <summary>
    /// Which thread of which process owns a window. The process is wanted as much as the thread:
    /// a window belonging to this process is the parent not having arrived from anywhere else.
    /// </summary>
    [DllImport(Library)] private static extern uint GetWindowThreadProcessId(nint window, out uint process);

    /// <summary>
    /// Ties two threads' input together, or unties them. Told true, the two share one input
    /// state, which is what carries a key press across a process boundary.
    /// </summary>
    [DllImport(Library)] private static extern bool AttachThreadInput(uint from, uint to, bool attach);

    /// <summary>This thread, as the window server numbers threads.</summary>
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    /// <summary>
    /// Says how this process reads the screen's scaling. Refuses once anything has asked, which
    /// is why it is called before a plugin is loaded rather than when a window is wanted.
    /// </summary>
    [DllImport(Library)] private static extern bool SetProcessDpiAwarenessContext(nint context);

    /// <summary>
    /// Per-monitor aware, second version: real pixels, and told again when the window moves to a
    /// screen at another scaling. What the application's own toolkit asks for, which is the
    /// whole point of asking for it here.
    /// </summary>
    private static readonly nint PerMonitorV2 = -4;

    /// <summary>
    /// Walks the windows inside one. The first argument says where to start: nought is the first.
    /// </summary>
    [DllImport(Library)] private static extern nint GetWindow(nint window, uint relationship);

    /// <summary>Asks for the first window inside this one.</summary>
    private const uint FirstChild = 5;

    /// <summary>Asks for the next window beside this one.</summary>
    private const uint NextSibling = 2;

    /// <summary>How many children to name before giving up, so a broken list cannot spin.</summary>
    private const int MostChildren = 8;

    /// <summary>A rectangle, in the shape Win32 fills one in.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        /// <summary>The left edge, in screen coordinates.</summary>
        public int Left;

        /// <summary>The top edge.</summary>
        public int Top;

        /// <summary>The right edge.</summary>
        public int Right;

        /// <summary>The bottom edge.</summary>
        public int Bottom;
    }

    /// <summary>
    /// Everything worth saying about a window and what is inside it, in one line for the log.
    /// </summary>
    /// <remarks>
    /// Written whether or not anything went wrong. Asked before the plugin is given the window
    /// and again afterwards, so the two lines together say what the handover did: the same
    /// window with a child under it afterwards is a plugin that drew, and the same window empty
    /// is a plugin that took the handle and did nothing with it.
    /// </remarks>
    /// <param name="window">The handle to ask about, which is Avalonia's child window here.</param>
    /// <returns>The account, or an empty string where this is not the platform for it.</returns>
    public static string Account(nint window)
    {
        if (!OperatingSystem.IsWindows()) return "";

        if (window == 0) return "there is no window";

        try
        {
            if (!IsWindow(window)) return "handle " + window.ToString("X") + " is not a window";

            var report = new StringBuilder();

            report.Append("window ").Append(window.ToString("X"))
                  .Append(" class ").Append(ClassOf(window))
                  .Append(GetWindowRect(window, out var rect)
                      ? " at " + rect.Left + "," + rect.Top + " sized " +
                        (rect.Right - rect.Left) + " by " + (rect.Bottom - rect.Top)
                      : " of no known size")
                  .Append(IsWindowVisible(window) ? " visible" : " NOT visible")
                  .Append(" under ").Append(GetParent(window).ToString("X"))
                  .Append("; ");

            nint child = GetWindow(window, FirstChild);

            if (child == 0)
            {
                report.Append("nothing inside it");
                return report.ToString();
            }

            for (int step = 0; step < MostChildren && child != 0; step++)
            {
                report.Append("inside: ").Append(child.ToString("X"))
                      .Append(" class ").Append(ClassOf(child))
                      .Append(GetWindowRect(child, out var inner)
                          ? " sized " + (inner.Right - inner.Left) + " by " + (inner.Bottom - inner.Top)
                          : " of no known size")
                      .Append(IsWindowVisible(child) ? " visible" : " NOT visible")
                      .Append("; ");

                child = GetWindow(child, NextSibling);
            }

            return report.ToString();
        }
        catch (Exception error)
        {
            return "could not ask about the window: " + error.Message;
        }
    }

    /// <summary>
    /// Reads the screen's scaling the same way the application does, for a process that has no
    /// toolkit to do it.
    /// </summary>
    /// <remarks>
    /// The application's window is per-monitor aware because its toolkit says so on the way up.
    /// A plugin's process builds no toolkit, so nothing says it, and a process that has not said
    /// is one Windows quietly lies to: it is told a screen at 150% is a smaller screen at 100%,
    /// and the window it draws is stretched by the system to make up the difference. Embedded in
    /// an aware window that reads as a plugin whose interface is soft and a few pixels adrift,
    /// on exactly the machines least likely to be the developer's.
    ///
    /// Said before the plugin is loaded, because a plugin is entitled to ask about the screen
    /// while it loads and because Windows refuses to change its mind once anything has asked.
    ///
    /// Nothing is done about a refusal. It means something in the process asked first, and a
    /// plugin drawn at the wrong scale is worth more than a plugin not drawn at all.
    /// </remarks>
    /// <returns>What happened, for the log.</returns>
    public static string ReadScalingProperly()
    {
        if (!OperatingSystem.IsWindows()) return "";

        try
        {
            return SetProcessDpiAwarenessContext(PerMonitorV2)
                ? "reading the screen's scaling per monitor"
                : "could not say how to read the screen's scaling; something asked first";
        }
        catch (Exception error)
        {
            return "could not say how to read the screen's scaling: " + error.Message;
        }
    }

    /// <summary>
    /// Puts this thread's keyboard in with the thread that owns the window we are drawing into.
    /// </summary>
    /// <remarks>
    /// A plugin in a process of its own draws into a window belonging to the application, and
    /// Windows keeps focus, the active window and which keys are down per thread. Left alone,
    /// the plugin's interface takes the mouse perfectly and never sees a key: the presses go to
    /// whichever thread the system thinks is in the foreground, which is the application's. A
    /// name typed into a preset box goes nowhere and there is nothing on the screen to say why.
    ///
    /// So the two threads are told to share one input state. This is the one thing cross-process
    /// embedding needs that embedding inside one process does not, and it is the whole of it:
    /// the drawing, the mouse and the resizing all work from the parenting alone.
    ///
    /// Asked of the parent rather than told, since the parent's thread is the parent's business
    /// and it can be a different one for a window opened later. A parent that turns out to
    /// belong to this process needs nothing: the two threads are already one, or would be tied
    /// to themselves, which Windows refuses.
    ///
    /// A refusal is not worth failing over. The interface is up and works with the mouse, which
    /// is most of a plugin, so this is written down and passed over rather than taking the
    /// window down over a keyboard.
    /// </remarks>
    /// <param name="parent">The window this process has been given to draw into.</param>
    /// <returns>What happened, for the log.</returns>
    public static string ShareInput(nint parent)
    {
        if (!OperatingSystem.IsWindows() || parent == 0) return "";

        try
        {
            uint owner = GetWindowThreadProcessId(parent, out uint process);

            if (owner == 0) return "; the window says it has no thread, so the keyboard is on its own";

            if (process == (uint)Environment.ProcessId)
                return "; the window is this process's own, so the keyboard needs nothing";

            uint mine = GetCurrentThreadId();

            if (owner == mine) return "; already the same thread as the window";

            bool tied = AttachThreadInput(owner, mine, true);

            return tied
                ? "; the keyboard is shared with the window's thread"
                : "; the keyboard could not be shared, so keys may not reach the plugin";
        }
        catch (Exception error)
        {
            return "; sharing the keyboard went wrong: " + error.Message;
        }
    }

    /// <summary>The class a window was made from, or a word saying it would not say.</summary>
    /// <param name="window">The window to ask about.</param>
    private static string ClassOf(nint window)
    {
        var name = new char[256];

        int length = GetClassNameW(window, name, name.Length);

        return length > 0 ? new string(name, 0, length) : "unnamed";
    }
}
