using JingleBox2.Diagnostics;
using System;
using System.Runtime.InteropServices;
using System.Text;

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
    private const string Library = "libX11.so.6";

    /// <summary>What Xlib hands the handler. Only the first few fields are read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int Type;
        public nint Display;
        public nint ResourceId;
        public nint Serial;
        public byte ErrorCode;
        public byte RequestCode;
        public byte MinorCode;
    }

    private delegate int Handler(nint display, ref XErrorEvent error);
    private delegate int FatalHandler(nint display);

    [DllImport(Library)] private static extern nint XSetErrorHandler(Handler handler);
    [DllImport(Library)] private static extern nint XSetIOErrorHandler(FatalHandler handler);

    [DllImport(Library)]
    private static extern int XGetErrorText(nint display, int code, StringBuilder buffer, int length);

    /// <summary>Kept alive for as long as the process is, or the collector takes the callback.</summary>
    private static Handler? _handler;
    private static FatalHandler? _fatal;

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
            // No X11 on this run. Nothing to take over, and nothing lost by not having.
        }
    }

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

        // What Xlib's own handler returns. The value is ignored; continuing is the point.
        return 0;
    }

    private static int Died(nint display)
    {
        Log.Write(LogArea.Plugins,
            "X11 has gone away from " + (_plugin.Length > 0 ? _plugin : "this plugin")
            + ". Nothing can be drawn or asked after this, so the process is ending.");

        // Xlib ends the process after this returns, whatever it returns.
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
