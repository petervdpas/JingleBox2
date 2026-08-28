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
    /// <summary>
    /// The X11 client library by its versioned name, since that is what is actually installed;
    /// the unversioned link belongs to a development package plenty of machines do not have.
    /// </summary>
    private const string Library = "libX11.so.6";

    /// <summary>
    /// Opens a connection of our own. Every method here opens one and closes it: the toolkit's
    /// connection belongs to the toolkit, and using it from another thread is how a program ends
    /// up with two things writing down one socket.
    /// </summary>
    [DllImport(Library)] private static extern nint XOpenDisplay(nint name);

    /// <summary>Closes one.</summary>
    [DllImport(Library)] private static extern int XCloseDisplay(nint display);

    /// <summary>
    /// Sends everything queued and waits for the server to have done it. Needed before asking
    /// what is inside a window, or the plugin's own window may not have arrived yet.
    /// </summary>
    [DllImport(Library)] private static extern int XSync(nint display, int discard);

    /// <summary>Sends everything queued without waiting.</summary>
    [DllImport(Library)] private static extern int XFlush(nint display);

    /// <summary>
    /// A name as the server's own number for it. The last argument asks for one that already
    /// exists rather than making it, which is how a property nobody has ever set is told apart
    /// from one set to nothing.
    /// </summary>
    [DllImport(Library)] private static extern nint XInternAtom(nint display, string name, int onlyIfExists);

    /// <summary>Puts a window on screen. Mapping one that is already mapped does nothing.</summary>
    [DllImport(Library)] private static extern int XMapWindow(nint display, nint window);

    /// <summary>
    /// A window's parent and its children. The list of children is allocated by the library and
    /// has to be freed, on every path including the ones that fail.
    /// </summary>
    [DllImport(Library)] private static extern int XQueryTree(nint display, nint window, out nint root, out nint parent, out nint* children, out uint count);

    /// <summary>Gives back memory the library allocated.</summary>
    [DllImport(Library)] private static extern int XFree(void* data);

    /// <summary>Posts an event to a window as though the server had generated it.</summary>
    [DllImport(Library)] private static extern int XSendEvent(nint display, nint window, int propagate, nint mask, XEvent* send);

    /// <summary>Where a window is, how big, and whether it is on screen.</summary>
    [DllImport(Library)] private static extern int XGetWindowAttributes(nint display, nint window, out XWindowAttributes attributes);

    /// <summary>Which window has the keyboard, for the whole display.</summary>
    [DllImport(Library)] private static extern int XGetInputFocus(nint display, out nint window, out int revertTo);

    /// <summary>
    /// Reads a property off a window. The data comes back allocated by the library and has to be
    /// freed.
    /// </summary>
    [DllImport(Library)]
    private static extern int XGetWindowProperty(
        nint display, nint window, nint property, nint offset, nint length, int delete, nint type,
        out nint actualType, out int actualFormat, out nuint items, out nuint after, out byte* data);

    /// <summary>
    /// Everything X will say about a window. Written out in full because the layout has to match
    /// the C header: only the size and the map state are read, but a field left out would move
    /// every field after it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct XWindowAttributes
    {
        /// <summary>Where the window is, in its parent.</summary>
        public int X;

        /// <inheritdoc cref="X"/>
        public int Y;

        /// <summary>
        /// How big it is. The pair this class exists to ask about: a plugin handed a window that
        /// is one pixel by one pixel lays itself out against that and never recovers.
        /// </summary>
        public int Width;

        /// <inheritdoc cref="Width"/>
        public int Height;

        /// <summary>How wide its border is.</summary>
        public int BorderWidth;

        /// <summary>
        /// How many bits a pixel. Reported in the account of a window that will not draw, since
        /// a plugin whose visual does not match its parent's is one of the ways that happens.
        /// </summary>
        public int Depth;

        /// <summary>How the window's colours are laid out. Reported for the same reason.</summary>
        public nint Visual;

        /// <summary>The root window of the screen it is on.</summary>
        public nint Root;

        /// <summary>Whether the window can be drawn into or is only a container.</summary>
        public int Class;

        /// <summary>What happens to the contents when the window is resized.</summary>
        public int BitGravity;

        /// <summary>What happens to the window when its parent is resized.</summary>
        public int WinGravity;

        /// <summary>Whether the server keeps what is under the window.</summary>
        public int BackingStore;

        /// <summary>Which planes of the backing store are kept.</summary>
        public nuint BackingPlanes;

        /// <summary>What the backing store is filled with.</summary>
        public nuint BackingPixel;

        /// <summary>Whether what is under this window is kept while it is up.</summary>
        public int SaveUnder;

        /// <summary>The window's colour map.</summary>
        public nint Colormap;

        /// <summary>Whether that map is the one currently in use.</summary>
        public int MapInstalled;

        /// <summary>0 unmapped, 1 unviewable, 2 viewable.</summary>
        public int MapState;

        /// <summary>What every client on this window has asked to hear about.</summary>
        public nint AllEventMasks;

        /// <summary>What this connection has asked to hear about.</summary>
        public nint YourEventMask;

        /// <summary>What is not passed up to the parent.</summary>
        public nint DoNotPropagateMask;

        /// <summary>Whether the window manager is to leave this window alone.</summary>
        public int OverrideRedirect;

        /// <summary>Which screen it is on.</summary>
        public nint Screen;
    }

    /// <summary>A client message, in the shape X expects to send one.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct XEvent
    {
        /// <summary>Which kind of event. Always <see cref="ClientMessage"/> here.</summary>
        public int Type;

        /// <summary>The request number it came from. Filled in by the server on the way out.</summary>
        public nuint Serial;

        /// <summary>Whether it was posted rather than generated. Filled in by the server.</summary>
        public int SendEvent;

        /// <summary>Which connection. Filled in by the server.</summary>
        public nint Display;

        /// <summary>Which window it is for.</summary>
        public nint Window;

        /// <summary>Which message, as an atom. The _XEMBED atom here.</summary>
        public nint MessageType;

        /// <summary>
        /// How wide the data words are: 8, 16 or 32. Always 32 here, which is what XEMBED asks
        /// for and what makes the five words below native integers rather than bytes.
        /// </summary>
        public int Format;

        /// <summary>When it happened. Nought, which X reads as "now".</summary>
        public nint Data0;

        /// <summary>Which XEMBED message. See <see cref="EmbeddedNotify"/> and the ones beside it.</summary>
        public nint Data1;

        /// <summary>The message's detail word, whose meaning depends on the message.</summary>
        public nint Data2;

        /// <summary>The first of the two data words, likewise.</summary>
        public nint Data3;

        /// <summary>The second.</summary>
        public nint Data4;

        /// <summary>An XEvent is a union as wide as its widest member. This is the rest of it.</summary>
        public fixed long Padding[8];
    }

    /// <summary>X's own number for a client message, which is the only kind sent here.</summary>
    private const int ClientMessage = 33;

    /// <summary>
    /// XEMBED_EMBEDDED_NOTIFY: you are inside my window. Carries the embedder's window in the
    /// first data word and the protocol version in the second.
    /// </summary>
    private const long EmbeddedNotify = 0;

    /// <summary>XEMBED_WINDOW_ACTIVATE: the window you are in is the one being used.</summary>
    private const long WindowActivate = 1;

    /// <summary>XEMBED_WINDOW_DEACTIVATE: it is not, any more.</summary>
    private const long WindowDeactivate = 2;

    /// <summary>XEMBED_FOCUS_IN: you have the keyboard. The detail word says where in you.</summary>
    private const long FocusIn = 4;

    /// <summary>XEMBED_FOCUS_OUT: you have not.</summary>
    private const long FocusOut = 5;

    /// <summary>
    /// XEMBED_FOCUS_CURRENT: keep whatever had the focus inside you last time, rather than
    /// moving it to the first or the last thing.
    /// </summary>
    private const int FocusCurrent = 0;

    /// <summary>The version of the protocol being spoken. There has only ever been one.</summary>
    private const int Version = 1;

    /// <summary>
    /// The map state of a window that is not on screen at all. The other two are unviewable,
    /// which is on screen but with an ancestor that is not, and viewable.
    /// </summary>
    private const int Unmapped = 0;

    /// <summary>
    /// True when a window is actually on screen at a real size.
    /// </summary>
    /// <remarks>
    /// Worth asking before a plugin is given the window. A toolkit builds itself against
    /// whatever it is handed, and what it is handed a moment too early is one pixel by one
    /// pixel and not on screen: some plugins cope with that and some crash on the first thing
    /// they try to draw, which is not a bug anybody can fix from this side.
    ///
    /// Answers true where there is no X to ask, which is every platform that is not this one and
    /// every run with no display: a question that cannot be asked must not stop a window opening.
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

    /// <summary>The two answers XGetInputFocus gives that are not a window.</summary>
    private const nint NoFocus = 0;

    /// <inheritdoc cref="NoFocus"/>
    private const nint PointerRoot = 1;

    /// <summary>
    /// Watches whether the keyboard is inside a window, and says so when that changes.
    /// </summary>
    /// <remarks>
    /// On a thread of its own, with one connection to X that it opens once and keeps. Asking
    /// this from the thread that draws would be a round trip to the X server four times a
    /// second, on the same thread a plugin repainting itself is already keeping busy.
    ///
    /// Only changes are reported, and the first answer counts as a change, so whoever is told
    /// hears where things stand as soon as the watch starts.
    /// </remarks>
    public static IDisposable WatchFocus(nint window, Action<bool> changed)
    {
        var watch = new FocusWatch(window, changed);

        watch.Start();

        return watch;
    }

    /// <summary>The thread and the connection behind <see cref="WatchFocus"/>.</summary>
    private sealed class FocusWatch : IDisposable
    {
        /// <summary>The window being watched, which is the host's rather than the plugin's.</summary>
        private readonly nint _window;

        /// <summary>Told each time the answer changes, on the watching thread.</summary>
        private readonly Action<bool> _changed;

        /// <summary>
        /// Set to stop. Also what the loop waits on between asks, so stopping is immediate
        /// rather than up to a quarter of a second late.
        /// </summary>
        private readonly System.Threading.ManualResetEventSlim _stop = new(false);

        /// <summary>The thread, or null before it starts and after it is let go.</summary>
        private System.Threading.Thread? _thread;

        /// <summary>Holds what is to be watched. Nothing runs until <see cref="Start"/>.</summary>
        public FocusWatch(nint window, Action<bool> changed)
        {
            _window = window;
            _changed = changed;
        }

        /// <summary>
        /// Starts the watching thread, or does nothing where there is no X to ask. A background
        /// thread, so a watch nobody disposed cannot keep the process alive.
        /// </summary>
        public void Start()
        {
            if (_window == 0 || !OperatingSystem.IsLinux()) return;

            _thread = new System.Threading.Thread(Run)
            {
                IsBackground = true,
                Name = "plugin window focus"
            };

            _thread.Start();
        }

        /// <summary>
        /// The loop: open one connection, ask four times a second, and say so when the answer
        /// changes. A connection that has gone ends the watch, and the plugin keeps whatever it
        /// was last told, which is what it would have had without this at all.
        /// </summary>
        private void Run()
        {
            nint display = 0;

            try
            {
                display = XOpenDisplay(0);
                if (display == 0) return;

                bool? had = null;

                while (!_stop.Wait(250))
                {
                    bool inside = Inside(display, _window);

                    if (had == inside) continue;

                    had = inside;

                    try { _changed(inside); } catch (Exception) { }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (display != 0)
                {
                    try { XCloseDisplay(display); } catch (Exception) { }
                }
            }
        }

        /// <summary>Whoever has the keyboard, walked up its parents to see whether it is us.</summary>
        /// <remarks>
        /// Bounded at a handful of steps, since a plugin's interface is a window or two inside
        /// ours and a walk that went further would be following a tree that has changed under it.
        /// </remarks>
        private static bool Inside(nint display, nint window)
        {
            if (XGetInputFocus(display, out nint focused, out _) == 0) return false;
            if (focused == NoFocus || focused == PointerRoot) return false;

            for (int step = 0; step < 32 && focused != 0; step++)
            {
                if (focused == window) return true;

                if (XQueryTree(display, focused, out nint root, out nint parent, out nint* children, out _) == 0)
                    return false;

                if (children != null) XFree(children);

                if (parent == 0 || parent == root) return false;

                focused = parent;
            }

            return false;
        }

        /// <summary>
        /// Asks the thread to stop and lets go of it. Not joined: the thread's last act is to
        /// close its own connection, and waiting for it would put a round trip to X on whoever
        /// is closing the window.
        /// </summary>
        public void Dispose()
        {
            _stop.Set();
            _thread = null;
        }
    }

    /// <summary>
    /// Finds whatever the plugin put inside our window and completes the handshake with it.
    /// </summary>
    /// <remarks>
    /// Harmless for a plugin that was not waiting for it: a window that does not understand
    /// XEMBED gets a client message it ignores, and one that is already mapped is mapped again,
    /// which X treats as nothing at all.
    ///
    /// The order and the slots both matter. XEMBED_EMBEDDED_NOTIFY carries the embedder's window
    /// in the first data word and the protocol version in the second, and its detail word is
    /// nought. A client told this in the wrong slot is a client that was never told.
    /// </remarks>
    /// <returns>
    /// An account of what was found, for the log. Written whether or not anything went wrong,
    /// because a plugin that draws nothing looks the same from here as one that drew nothing on
    /// purpose, and the depths and visuals in it are what tell the two apart.
    /// </returns>
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

    /// <summary>
    /// Tells whatever is embedded in a window that the window has become active, or has
    /// stopped being active.
    /// </summary>
    /// <remarks>
    /// The handshake at attach says this once, and once is not enough. XEMBED puts the
    /// embedder in charge of saying when the client is active and when it has the focus, and
    /// expects it to keep saying so: told it is active, told when it is not, told again the
    /// next time it is. A client that hears it once and never again believes what it heard
    /// last, which after the first click on anything else is that it is not active. It goes on
    /// drawing, because its own timers keep running, and it ignores what is clicked on it,
    /// because as far as it knows the window it sits in is not the one being used. That is a
    /// plugin whose interface has become a picture.
    ///
    /// The order is the protocol's. Going in, the window is active before anything inside it
    /// has the focus; coming out, the focus goes before the window does.
    ///
    /// No X to talk to is not a reason to take a window down, so a failure here is silent.
    /// </remarks>
    public static void Activated(nint parent, bool active)
    {
        if (parent == 0 || !OperatingSystem.IsLinux()) return;

        nint display = 0;

        try
        {
            display = XOpenDisplay(0);
            if (display == 0) return;

            if (XQueryTree(display, parent, out _, out _, out nint* children, out uint count) == 0 || children == null)
                return;

            nint embed = XInternAtom(display, "_XEMBED", 0);

            for (uint index = 0; index < count; index++)
            {
                nint child = children[index];

                if (active)
                {
                    Tell(display, child, embed, WindowActivate, 0, 0, 0);
                    Tell(display, child, embed, FocusIn, FocusCurrent, 0, 0);
                }
                else
                {
                    Tell(display, child, embed, FocusOut, 0, 0, 0);
                    Tell(display, child, embed, WindowDeactivate, 0, 0, 0);
                }
            }

            XFree(children);
            XSync(display, 0);
        }
        catch (Exception)
        {
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
    /// <remarks>
    /// The _XEMBED_INFO property is two words: the protocol version the client speaks, and its
    /// flags. The one flag that exists says whether it would like to be mapped. Only read for
    /// the account written into the log: the messages are sent either way, since a window that
    /// does not understand them ignores them.
    /// </remarks>
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
