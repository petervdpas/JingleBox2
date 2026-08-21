using System;
using System.Runtime.InteropServices;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The handshake a window embedded from another program waits for before it will draw.
/// </summary>
/// <remarks>
/// X11 lets one program put its window inside another program's window, and that much happens
/// on its own. What does not happen on its own is the conversation that goes with it: XEMBED.
/// A toolkit that thinks it is embedded sits there unrealised until the window it was put into
/// tells it so, and a plugin whose interface is a toolkit window looks exactly like a plugin
/// that draws nothing at all.
///
/// This is the embedder's half, which is four things: notice the window the plugin made inside
/// ours, tell it it is embedded, tell it it is active, and map it.
/// </remarks>
internal static unsafe class XEmbed
{
    private const string Library = "libX11.so.6";

    [DllImport(Library)] private static extern nint XOpenDisplay(nint name);
    [DllImport(Library)] private static extern int XCloseDisplay(nint display);
    [DllImport(Library)] private static extern int XSync(nint display, int discard);
    [DllImport(Library)] private static extern int XFlush(nint display);
    [DllImport(Library)] private static extern nint XInternAtom(nint display, string name, int onlyIfExists);
    [DllImport(Library)] private static extern int XMapWindow(nint display, nint window);
    [DllImport(Library)] private static extern int XQueryTree(nint display, nint window, out nint root, out nint parent, out nint* children, out uint count);
    [DllImport(Library)] private static extern int XFree(void* data);
    [DllImport(Library)] private static extern int XSendEvent(nint display, nint window, int propagate, nint mask, XEvent* send);
    [DllImport(Library)] private static extern int XGetWindowAttributes(nint display, nint window, out XWindowAttributes attributes);

    [DllImport(Library)]
    private static extern int XGetWindowProperty(
        nint display, nint window, nint property, nint offset, nint length, int delete, nint type,
        out nint actualType, out int actualFormat, out nuint items, out nuint after, out byte* data);

    [StructLayout(LayoutKind.Sequential)]
    private struct XWindowAttributes
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public int BorderWidth;
        public int Depth;
        public nint Visual;
        public nint Root;
        public int Class;
        public int BitGravity;
        public int WinGravity;
        public int BackingStore;
        public nuint BackingPlanes;
        public nuint BackingPixel;
        public int SaveUnder;
        public nint Colormap;
        public int MapInstalled;

        /// <summary>0 unmapped, 1 unviewable, 2 viewable.</summary>
        public int MapState;

        public nint AllEventMasks;
        public nint YourEventMask;
        public nint DoNotPropagateMask;
        public int OverrideRedirect;
        public nint Screen;
    }

    /// <summary>A client message, in the shape X expects to send one.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct XEvent
    {
        public int Type;
        public nuint Serial;
        public int SendEvent;
        public nint Display;
        public nint Window;
        public nint MessageType;
        public int Format;
        public nint Data0;
        public nint Data1;
        public nint Data2;
        public nint Data3;
        public nint Data4;

        /// <summary>An XEvent is a union as wide as its widest member. This is the rest of it.</summary>
        public fixed long Padding[8];
    }

    private const int ClientMessage = 33;

    private const long EmbeddedNotify = 0;
    private const long WindowActivate = 1;
    private const long FocusIn = 4;

    private const int FocusCurrent = 0;

    /// <summary>The version of the protocol being spoken. There has only ever been one.</summary>
    private const int Version = 1;

    private const int Unmapped = 0;

    /// <summary>
    /// True when a window is actually on screen at a real size.
    /// </summary>
    /// <remarks>
    /// Worth asking before a plugin is given the window. A toolkit builds itself against
    /// whatever it is handed, and what it is handed a moment too early is one pixel by one
    /// pixel and not on screen: some plugins cope with that and some crash on the first thing
    /// they try to draw, which is not a bug anybody can fix from this side.
    /// </remarks>
    public static bool OnScreen(nint window, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (window == 0 || !OperatingSystem.IsLinux()) return true;

        nint display = 0;

        try
        {
            display = XOpenDisplay(0);
            if (display == 0) return true;

            if (XGetWindowAttributes(display, window, out var attributes) == 0) return false;

            width = attributes.Width;
            height = attributes.Height;

            return attributes.MapState != Unmapped && width > 1 && height > 1;
        }
        catch (Exception)
        {
            // No X to ask means this is not a machine where the question makes sense.
            return true;
        }
        finally
        {
            if (display != 0)
            {
                try { XCloseDisplay(display); } catch (Exception) { }
            }
        }
    }

    /// <summary>
    /// Finds whatever the plugin put inside our window and completes the handshake with it.
    /// </summary>
    /// <remarks>
    /// Harmless for a plugin that was not waiting for it: a window that does not understand
    /// XEMBED gets a client message it ignores, and one that is already mapped is mapped again,
    /// which X treats as nothing at all.
    /// </remarks>
    public static string Complete(nint parent)
    {
        if (parent == 0 || !OperatingSystem.IsLinux()) return "";

        nint display = 0;

        try
        {
            display = XOpenDisplay(0);
            if (display == 0) return "no display";

            XSync(display, 0);

            if (XQueryTree(display, parent, out _, out _, out nint* children, out uint count) == 0 || children == null)
            {
                return "nothing inside the window";
            }

            var report = new System.Text.StringBuilder();

            bool ours = XGetWindowAttributes(display, parent, out var mine) != 0 && mine.MapState != Unmapped;

            report.Append("our own window is ").Append(ours ? "on screen" : "NOT on screen")
                  .Append(" (").Append(mine.Width).Append(" by ").Append(mine.Height)
                  .Append(", depth ").Append(mine.Depth)
                  .Append(", visual ").Append(mine.Visual.ToString("X")).Append("); ");

            nint embed = XInternAtom(display, "_XEMBED", 0);
            nint info = XInternAtom(display, "_XEMBED_INFO", 1);

            for (uint index = 0; index < count; index++)
            {
                nint child = children[index];

                bool mapped = XGetWindowAttributes(display, child, out var attributes) != 0 && attributes.MapState != Unmapped;
                bool wants = Wants(display, child, info, out int flags);

                report.Append("window ").Append(child.ToString("X"))
                      .Append(" depth ").Append(attributes.Depth)
                      .Append(" visual ").Append(attributes.Visual.ToString("X"))
                      .Append(mapped ? " mapped" : " not mapped")
                      .Append(wants ? ", wants embedding, flags " + flags : ", plain")
                      .Append("; ");

                // The order and the slots both matter. XEMBED_EMBEDDED_NOTIFY carries the
                // embedder's window in the first data word and the protocol version in the
                // second; the detail word is nought. A client told this in the wrong slot is a
                // client that was never told.
                Tell(display, child, embed, EmbeddedNotify, 0, parent, Version);
                Tell(display, child, embed, WindowActivate, 0, 0, 0);
                Tell(display, child, embed, FocusIn, FocusCurrent, 0, 0);

                XMapWindow(display, child);
            }

            XFree(children);
            XSync(display, 0);

            return report.Length == 0 ? "nothing inside the window" : report.ToString();
        }
        catch (Exception error)
        {
            return "could not reach the display: " + error.Message;
        }
        finally
        {
            if (display != 0)
            {
                try { XFlush(display); XCloseDisplay(display); } catch (Exception) { }
            }
        }
    }

    /// <summary>True when the window says, in the way XEMBED says it, that it is a plug.</summary>
    private static bool Wants(nint display, nint window, nint info, out int flags)
    {
        flags = 0;

        if (info == 0) return false;

        byte* data = null;

        try
        {
            int answer = XGetWindowProperty(
                display, window, info, 0, 2, 0, 0,
                out _, out _, out nuint items, out _, out data);

            if (answer != 0 || items < 2 || data == null) return false;

            // Two words: the protocol version the client speaks, and its flags. The one flag
            // that exists says whether it would like to be mapped.
            flags = (int)*((nint*)data + 1);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (data != null) XFree(data);
        }
    }

    /// <summary>
    /// Sends one XEMBED message to a window.
    /// </summary>
    /// <remarks>
    /// The five words of a client message are, in order: when, which message, a detail, and two
    /// pieces of data whose meaning depends on the message.
    /// </remarks>
    private static void Tell(nint display, nint window, nint embed, long message, nint detail, nint first, nint second)
    {
        if (embed == 0) return;

        var send = new XEvent
        {
            Type = ClientMessage,
            Window = window,
            MessageType = embed,
            Format = 32,
            Data0 = 0,
            Data1 = (nint)message,
            Data2 = detail,
            Data3 = first,
            Data4 = second
        };

        XSendEvent(display, window, 0, 0, &send);
    }
}
