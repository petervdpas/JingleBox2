using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// One keystroke taken back from a window a plugin is drawing in, on X11.
/// </summary>
/// <remarks>
/// The X half of <see cref="Interfaces.IWindowShortcut"/>, and not an implementation of it: what
/// this holds is P/Invoke, a connection and a thread, which is the same family
/// <see cref="XEmbed"/> and <see cref="XErrors"/> are in and there is nothing in it to stand in
/// front of. Which platform is under us is decided one layer up, in
/// <see cref="WindowShortcuts"/>, so this is only ever called when the answer is already X.
///
/// A passive key grab, and the reason it works is in the X protocol's own words: a grab on a
/// window activates when the key is pressed and that window is an ancestor of the window holding
/// the focus. The plugin's interface is a child of ours, so ours qualifies while the plugin has
/// the keyboard, and the press is reported to us instead of to the plugin.
///
/// On a connection and a thread of its own, the same shape <see cref="XEmbed.WatchFocus"/> has
/// and for the same reason: an active grab reports to the client that made it, and the toolkit's
/// own connection is not that client. It also means the drawing thread is never waiting on X.
///
/// This is reached under Wayland as well as under a plain X server, and it has to be. A compositor
/// runs X clients through XWayland, which is an X server like any other, and both windows are
/// its clients; the grab is settled inside it and never reaches the compositor. That is not a
/// happy accident either: embedding somebody else's window at all is XEmbed, which is X11, so a
/// plugin drawing its own interface here is already an X client whatever is running the desktop.
/// </remarks>
internal sealed class XWindowShortcut
{
    /// <summary>Where the calls go.</summary>
    private const string Library = "libX11.so.6";

    /// <summary>Opens a connection of this thread's own.</summary>
    [DllImport(Library)] private static extern nint XOpenDisplay(nint name);

    /// <summary>Lets it go.</summary>
    [DllImport(Library)] private static extern int XCloseDisplay(nint display);

    /// <summary>Which physical key carries a symbol on this keyboard.</summary>
    [DllImport(Library)] private static extern byte XKeysymToKeycode(nint display, nuint keysym);

    /// <summary>Asks for the key while this window holds, or contains, the focus.</summary>
    [DllImport(Library)] private static extern int XGrabKey(
        nint display, int keycode, uint modifiers, nint window,
        int ownerEvents, int pointerMode, int keyboardMode);

    /// <summary>Gives it back.</summary>
    [DllImport(Library)] private static extern int XUngrabKey(
        nint display, int keycode, uint modifiers, nint window);

    /// <summary>How many events are waiting on this connection.</summary>
    [DllImport(Library)] private static extern int XPending(nint display);

    /// <summary>Takes the next one. Blocking, which is why it is only called with one waiting.</summary>
    [DllImport(Library)] private static extern int XNextEvent(nint display, byte[] into);

    /// <summary>The letter, as X names it.</summary>
    private const nuint KeyM = 0x006d;

    /// <summary>Shift held.</summary>
    private const uint Shift = 1 << 0;

    /// <summary>Caps lock on, which is a modifier like any other and has to be allowed for.</summary>
    private const uint Caps = 1 << 1;

    /// <summary>Control held.</summary>
    private const uint Control = 1 << 2;

    /// <summary>Num lock on, usually, which has to be allowed for the same way.</summary>
    private const uint Num = 1 << 4;

    /// <summary>Neither the pointer nor the keyboard is frozen while the grab is active.</summary>
    private const int Async = 1;

    /// <summary>A key going down, which is the only kind that arrives here.</summary>
    private const int KeyPressed = 2;

    /// <summary>Big enough for the largest event X will hand back on any platform.</summary>
    private const int EventBytes = 192;

    /// <summary>Catches Ctrl+Shift+M on an X window until the answer is let go of.</summary>
    /// <remarks>
    /// The lock keys are the reason there are four grabs rather than one. A grab names the exact
    /// modifier state, so one asking for Control and Shift alone is never activated with caps
    /// lock or num lock on, and the shortcut would work for most people and silently not for
    /// whoever left a lock key down.
    /// </remarks>
    /// <param name="window">The X window to grab on, which is the host's rather than the plugin's.</param>
    /// <param name="pressed">Told on the grabbing thread each time the key goes down.</param>
    public IDisposable? On(nint window, Action pressed)
    {
        if (window == 0 || pressed is null) return null;

        var grab = new Grab(window, pressed);

        grab.Start();

        return grab;
    }

    /// <summary>The thread, the connection and the grab behind <see cref="On"/>.</summary>
    private sealed class Grab : IDisposable
    {
        /// <summary>The window grabbed on, which is the host's rather than the plugin's.</summary>
        private readonly nint _window;

        /// <summary>Told each time the key goes down, on the grabbing thread.</summary>
        private readonly Action _pressed;

        /// <summary>
        /// Set to stop, and what the loop waits on between looks, so letting go is immediate
        /// rather than a tick late.
        /// </summary>
        private readonly ManualResetEventSlim _stop = new(false);

        /// <summary>The thread, or nothing before it starts and after it is let go.</summary>
        private Thread? _thread;

        /// <summary>Holds what is to be grabbed. Nothing runs until <see cref="Start"/>.</summary>
        /// <param name="window">The X window to grab on.</param>
        /// <param name="pressed">Told each time the key goes down.</param>
        public Grab(nint window, Action pressed)
        {
            _window = window;
            _pressed = pressed;
        }

        /// <summary>
        /// Starts the grabbing thread. A background thread, so a grab nobody let go of cannot
        /// keep the process alive.
        /// </summary>
        public void Start()
        {
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "plugin window key"
            };

            _thread.Start();
        }

        /// <summary>
        /// The loop: open one connection, ask for the key four ways, and read what arrives.
        /// </summary>
        /// <remarks>
        /// Polled rather than blocked in <c>XNextEvent</c>, so stopping needs no trick to wake
        /// the thread with. Sixty times a second on an idle connection costs a syscall that
        /// answers nought.
        ///
        /// A connection that will not open ends the grab quietly, which leaves the keystroke
        /// doing what it did before this existed: nothing, in a window that cannot hear it.
        /// </remarks>
        private void Run()
        {
            nint display = 0;

            try
            {
                display = XOpenDisplay(0);
                if (display == 0) return;

                int keycode = XKeysymToKeycode(display, KeyM);
                if (keycode == 0) return;

                foreach (uint locks in Locks) XGrabKey(display, keycode, Control | Shift | locks, _window, 0, Async, Async);

                var arrived = new byte[EventBytes];

                while (!_stop.Wait(16))
                {
                    while (XPending(display) > 0)
                    {
                        XNextEvent(display, arrived);

                        if (BitConverter.ToInt32(arrived, 0) != KeyPressed) continue;

                        try { _pressed(); } catch (Exception) { }
                    }
                }

                foreach (uint locks in Locks) XUngrabKey(display, keycode, Control | Shift | locks, _window);
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

        /// <summary>Every state the two lock keys can be in, since a grab names the exact one.</summary>
        private static readonly uint[] Locks = { 0, Caps, Num, Caps | Num };

        /// <summary>Stops the thread and gives the key back.</summary>
        public void Dispose()
        {
            _stop.Set();

            _thread = null;
        }
    }
}
