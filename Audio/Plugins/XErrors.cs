using JingleBox2.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// Catches what X11 complains about and writes it to the log instead of the terminal.
/// </summary>
/// <remarks>
/// Xlib answers a bad request by printing four lines to standard error and carrying on. In a
/// plugin's own process that goes round the log entirely: the process inherits whatever
/// terminal the application was started from, so the account of a plugin misbehaving with its
/// window ends up somewhere the log knows nothing about, and nowhere at all for anybody who
/// started the application from a menu.
///
/// So the handler is taken over. What it does is what Xlib's own does, which is to write the
/// complaint down and let the program continue: these are asynchronous errors about requests
/// already sent, and there is nothing to be done about them by the time they arrive. Nearly all
/// of them are a plugin taking its own window down while something underneath is still tidying
/// up after it, which is untidy and harmless.
///
/// Worth having anyway, because the interesting ones look exactly the same from here: a plugin
/// drawing into a window that has been destroyed is what comes just before one of them takes
/// the host down, and that is a line worth having next to the moment it happened.
///
/// The fatal handler is separate and is not the same thing. That one is the display going away,
/// after which nothing can be done and the process is over; all this does is say so before it
/// goes, since the alternative is a plugin process that vanishes without a word.
/// </remarks>
internal static class XErrors
{
    /// <summary>
    /// The X11 client library by its versioned name, since that is what is actually installed;
    /// the unversioned link belongs to a development package plenty of machines do not have.
    /// </summary>
    private const string Library = "libX11.so.6";

    /// <summary>What Xlib hands the handler. Only the first few fields are read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        /// <summary>Always the error event's own number. Not read.</summary>
        public int Type;

        /// <summary>Which display complained, and what the error text is asked of.</summary>
        public nint Display;

        /// <summary>The window or other resource the failed request was about.</summary>
        public nint ResourceId;

        /// <summary>Which request it was, counted from the start of the connection.</summary>
        public nint Serial;

        /// <summary>What went wrong, as one of X's own numbers. Turned into words by Xlib.</summary>
        public byte ErrorCode;

        /// <summary>Which kind of request failed.</summary>
        public byte RequestCode;

        /// <summary>Which call within an extension, for a request that came from one.</summary>
        public byte MinorCode;
    }

    /// <summary>
    /// What Xlib calls for a request that failed. The return value is ignored by Xlib; what
    /// matters is that returning at all is what lets the program carry on.
    /// </summary>
    private delegate int Handler(nint display, ref XErrorEvent error);

    /// <summary>
    /// What Xlib calls when the connection itself has gone. Xlib ends the process after this
    /// returns, whatever it returns.
    /// </summary>
    private delegate int FatalHandler(nint display);

    /// <summary>Puts a handler in place and hands back whatever was there before.</summary>
    [DllImport(Library)] private static extern nint XSetErrorHandler(Handler handler);

    /// <summary>The same for the fatal one.</summary>
    [DllImport(Library)] private static extern nint XSetIOErrorHandler(FatalHandler handler);

    /// <summary>Xlib's own wording for an error code, which is better than any guess here.</summary>
    [DllImport(Library)]
    private static extern int XGetErrorText(nint display, int code, StringBuilder buffer, int length);

    /// <summary>Kept alive for as long as the process is, or the collector takes the callback.</summary>
    private static Handler? _handler;

    /// <inheritdoc cref="_handler"/>
    private static FatalHandler? _fatal;

    /// <summary>
    /// Which plugin this process is, so a line in the shared log says whose window misbehaved.
    /// Empty until <see cref="Catch"/> is called.
    /// </summary>
    private static string _plugin = "";

    /// <summary>
    /// Has X11's complaints written to the log, with the plugin's name on them.
    /// </summary>
    /// <remarks>
    /// Does nothing where there is no X11 to take over from, which is a scan, a headless run,
    /// and every platform that is not this one. Failing to install a debugging aid is not a
    /// reason to fail.
    /// </remarks>
    public static void Catch(string plugin)
    {
        if (!OperatingSystem.IsLinux()) return;

        _plugin = plugin ?? "";

        try
        {
            _handler = Complained;
            _fatal = Died;

            XSetErrorHandler(_handler);
            XSetIOErrorHandler(_fatal);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Writes one complaint down and carries on, which is what Xlib's own handler does. The
    /// fields are copied out before the log closure is built, since the event belongs to Xlib
    /// and is gone the moment this returns.
    /// </summary>
    private static int Complained(nint display, ref XErrorEvent error)
    {
        byte code = error.ErrorCode;
        byte request = error.RequestCode;
        nint resource = error.ResourceId;

        Log.Write(LogArea.Plugins, () =>
            "X11 complained about " + (_plugin.Length > 0 ? _plugin : "this plugin")
            + ": " + Text(display, code)
            + " on request " + request + ", window 0x" + resource.ToString("x")
            + ". Carrying on, as Xlib would.");

        return 0;
    }

    /// <summary>
    /// Says the display has gone before Xlib ends the process. The alternative is a plugin
    /// process that vanishes without a word.
    /// </summary>
    private static int Died(nint display)
    {
        Log.Write(LogArea.Plugins,
            "X11 has gone away from " + (_plugin.Length > 0 ? _plugin : "this plugin")
            + ". Nothing can be drawn or asked after this, so the process is ending.");

        return 0;
    }

    /// <summary>What X calls that error, in its own words.</summary>
    private static string Text(nint display, int code)
    {
        try
        {
            var said = new StringBuilder(256);

            XGetErrorText(display, code, said, said.Capacity);

            return said.Length > 0 ? said.ToString() : "error " + code;
        }
        catch (Exception)
        {
            return "error " + code;
        }
    }
}
