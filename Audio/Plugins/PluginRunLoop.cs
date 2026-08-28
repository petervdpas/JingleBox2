using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The clock and the doorbell a Linux plugin expects the host to hold for it.
/// </summary>
/// <remarks>
/// Every other platform gives a plugin a run loop for free: Windows has a message pump and
/// macOS has a run loop, both already running before the plugin arrives. X11 has no such thing,
/// so the host is expected to hold one. A plugin hands over a timer, or a file it wants to be
/// told about when there is something to read on it, and the host is expected to come back.
///
/// Both standards ask for the same thing in different words. VST3 calls it IRunLoop and hands
/// over C++ objects; CLAP calls it timer support and posix fd support and hands over numbers.
/// The pump underneath does not care which, so there is one of it.
///
/// This is not only a window concern. Serum asks for one the moment it is created and says in
/// as many words that it cannot function without it, editor or no editor, which is what a host
/// that skips this gets wrong.
///
/// The pump runs on a thread of its own until somebody takes it over with
/// <see cref="DriveWith"/>. Once a plugin has a window, that somebody is the UI thread: a
/// toolkit drawing into an X11 window expects to be called where the window lives, and calling
/// it from anywhere else is a crash inside somebody else's code rather than a bug report.
/// </remarks>
internal static unsafe class PluginRunLoop
{
    /// <summary>How often the pump comes round when nothing is happening. Fine for a meter.</summary>
    private const int TickMilliseconds = 16;

    /// <summary>
    /// And how soon it comes back when something was happening.
    /// </summary>
    /// <remarks>
    /// A hand on a knob makes messages faster than sixty a second, and a round that always
    /// waits its full turn before looking again turns that into a window that lags behind the
    /// mouse. Busy means come straight back; quiet means take the sixteen milliseconds.
    /// </remarks>
    private const int BusyMilliseconds = 1;

    /// <summary>One clock a plugin has asked for.</summary>
    private sealed class Timer
    {
        /// <summary>
        /// The plugin's object for VST3, or a key the host made up for CLAP, which has no
        /// object to hand over. Either way it is what identifies the timer.
        /// </summary>
        public nint Handler;

        /// <summary>How often, in milliseconds, floored at one round of the pump.</summary>
        public long Interval;

        /// <summary>When it next comes round, on the same clock as <c>Environment.TickCount64</c>.</summary>
        public long Due;

        /// <summary>When it was asked for, so what a window brought with it can go with it.</summary>
        public long Asked;

        /// <summary>What to do when it is due, for a plugin that did not hand over an object.</summary>
        public Action? Fire;
    }

    /// <summary>One file a plugin has asked to be told about.</summary>
    private sealed class Watch
    {
        /// <inheritdoc cref="Timer.Handler"/>
        public nint Handler;

        /// <summary>The file descriptor. A plugin's X11 connection, in practice.</summary>
        public int File;

        /// <summary>When it was asked for. See <see cref="DropSince"/>.</summary>
        public long Asked;

        /// <summary>The same, for a file with something waiting on it.</summary>
        public Action<int>? Fire;
    }

    /// <summary>
    /// Held while a plugin's own timer or file handler is being called, and by nothing else
    /// except taking one back.
    /// </summary>
    /// <remarks>
    /// The handler is the plugin's object and the plugin frees it when it takes it back, so a
    /// call that is halfway through when that happens is a read of freed memory. Waiting for
    /// the call to finish is the fix, but it has to be this narrow: holding the registration
    /// lock for the length of a repaint stalls every other thread the plugin has, which comes
    /// out as a window that will not move and audio that stutters.
    /// </remarks>
    private static readonly object Calling = new();

    /// <summary>Every clock asked for, by every plugin. Only touched under <see cref="Gate"/>.</summary>
    private static readonly List<Timer> Timers = new();

    /// <summary>Every file being watched, likewise.</summary>
    private static readonly List<Watch> Watches = new();

    /// <summary>
    /// Held over every read and write of the two lists. Deliberately not held while a plugin's
    /// own handler is being called: see <see cref="Calling"/>.
    /// </summary>
    private static readonly object Gate = new();

    /// <summary>Counts registrations, so a moment in time can be named and gone back to.</summary>
    private static long _asked;

    /// <summary>How many timers have gone off, for <see cref="Census"/>.</summary>
    private static long _rings;

    /// <summary>How many files have been reported ready, likewise.</summary>
    private static long _deliveries;

    /// <summary>
    /// The run loop object a plugin is handed. Made once and never freed, since a plugin holds
    /// it for as long as it is loaded.
    /// </summary>
    private static void* _instance;

    /// <summary>The pump's own thread, started with the first registration and never stopped.</summary>
    private static Thread? _pump;

    /// <summary>
    /// Where a round is posted to, or null while the pump runs rounds itself. Set once a plugin
    /// has a window: a toolkit drawing into an X11 window expects to be called where the window
    /// lives.
    /// </summary>
    private static Action<Action>? _post;

    /// <summary>
    /// Set while a round is on its way to somebody else's thread. Without it a busy UI thread
    /// would collect a queue of rounds and then run them all at once.
    /// </summary>
    private static volatile bool _inFlight;

    /// <summary>
    /// Hands the pumping to somebody else, a UI dispatcher for instance. Each call is one
    /// round of the loop and has to come back.
    /// </summary>
    public static void DriveWith(Action<Action> post)
    {
        lock (Gate) _post = post;
    }

    /// <summary>The object a plugin is handed when it asks the host context for a run loop.</summary>
    public static void* Instance()
    {
        lock (Gate)
        {
            if (_instance != null) return _instance;

            var table = (nint*)NativeMemory.AllocZeroed(7, (nuint)sizeof(nint));

            table[0] = (nint)(delegate* unmanaged[Cdecl]<void*, byte*, void**, int>)&Query;
            table[1] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[2] = (nint)(delegate* unmanaged[Cdecl]<void*, uint>)&KeepAlive;
            table[3] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, int, int>)&RegisterEventHandler;
            table[4] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, int>)&UnregisterEventHandler;
            table[5] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, ulong, int>)&RegisterTimer;
            table[6] = (nint)(delegate* unmanaged[Cdecl]<void*, void*, int>)&UnregisterTimer;

            var loop = (nint*)NativeMemory.AllocZeroed(1, (nuint)sizeof(nint));
            loop[0] = (nint)table;

            _instance = loop;

            Start();
            return _instance;
        }
    }

    /// <summary>
    /// Starts the pump if it is not already running. A background thread, so the process is
    /// never held open by a plugin nobody unloaded.
    /// </summary>
    private static void Start()
    {
        if (_pump != null) return;

        _pump = new Thread(Run)
        {
            IsBackground = true,
            Name = "plugin run loop"
        };

        _pump.Start();
    }

    /// <summary>
    /// The pump: wait, then run a round or hand one to whoever is driving. Nothing registered
    /// means nothing to do, and there is no reason to wake the drawing thread sixty times a
    /// second to find that out.
    /// </summary>
    private static void Run()
    {
        while (true)
        {
            Thread.Sleep(_busy ? BusyMilliseconds : TickMilliseconds);

            _busy = false;

            Action<Action>? post;
            bool idle;

            lock (Gate)
            {
                post = _post;
                idle = Timers.Count == 0 && Watches.Count == 0;
            }

            if (idle) continue;

            if (post == null)
            {
                Pump();
                continue;
            }

            if (_inFlight) continue;

            _inFlight = true;

            post(() =>
            {
                try
                {
                    Pump();
                }
                finally
                {
                    _inFlight = false;
                }
            });
        }
    }

    /// <summary>One round: whatever is due, and whatever has something to read.</summary>
    /// <remarks>
    /// Files before timers, deliberately. A plugin's timer is where it draws, and what it draws
    /// depends on what it has been told; a plugin that repaints before it has read the message
    /// saying its window is on screen is a plugin drawing into nothing.
    /// </remarks>
    private static void Pump()
    {
        Deliver();
        Ring();
    }

    /// <summary>
    /// Rings whatever is due. The list of due timers is taken under the registration lock and
    /// they are called under the calling lock, so a plugin taking a timer back mid-round waits
    /// for the call rather than freeing memory underneath it.
    /// </summary>
    /// <remarks>
    /// Each timer is asked about again right before it rings. A plugin closing its window takes
    /// its timers away, and it can do that from inside another one of its own timers or from the
    /// call that closes it, so the list taken a moment ago may already name something freed.
    ///
    /// A timer that throws is that plugin's problem for this round rather than a reason to stop
    /// ringing everybody else's.
    /// </remarks>
    private static void Ring()
    {
        long now = Environment.TickCount64;

        Timer[] due;

        lock (Gate)
        {
            int count = 0;
            foreach (var timer in Timers)
            {
                if (now >= timer.Due) count++;
            }

            if (count == 0) return;

            due = new Timer[count];
            int index = 0;

            foreach (var timer in Timers)
            {
                if (now < timer.Due) continue;

                timer.Due = now + timer.Interval;
                due[index++] = timer;
            }
        }

        lock (Calling)
        {
            foreach (var timer in due)
            {
                if (!StillWanted(timer.Handler, Timers)) continue;

                try
                {
                    _rings++;

                    if (timer.Fire != null) timer.Fire();
                    else Call(timer.Handler);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    /// <summary>
    /// Tells any plugin whose file has something waiting on it. This is how a plugin's own
    /// window hears about a mouse or a keystroke: its toolkit is sitting on an X11 connection
    /// and the host is the only thing that will ever look at it.
    /// </summary>
    /// <remarks>
    /// Drained rather than one message per round. A toolkit handles one message per call, and a
    /// mouse being dragged makes hundreds a second; handing over one every sixteen milliseconds
    /// is a window that answers sixty times a second at best, which is what a plugin that will
    /// not keep up with a knob feels like. Bounded by <see cref="MaxDrain"/>, so a plugin that
    /// never empties its own queue cannot hold this thread for ever.
    ///
    /// The calling lock is held for the whole drain, because the handler belongs to the plugin
    /// and the plugin frees it when it takes it back. Each handler is asked about again just
    /// before it is told, for the same reason the timers are.
    /// </remarks>
    private static void Deliver()
    {
        Watch[] watching;

        lock (Gate)
        {
            if (Watches.Count == 0) return;

            watching = Watches.ToArray();
        }

        var files = new int[watching.Length];
        var ready = new bool[watching.Length];

        for (int index = 0; index < watching.Length; index++) files[index] = watching[index].File;

        lock (Calling)
        {
            for (int pass = 0; pass < MaxDrain; pass++)
            {
                if (Waiting(files, ready) <= 0) return;

                bool any = false;

                for (int index = 0; index < watching.Length; index++)
                {
                    if (!ready[index]) continue;

                    if (!StillWatched(watching[index].Handler)) continue;

                    any = true;
                    _busy = true;
                    _deliveries++;

                    try
                    {
                        if (watching[index].Fire != null) watching[index].Fire!(watching[index].File);
                        else Ready(watching[index].Handler, watching[index].File);
                    }
                    catch (Exception)
                    {
                    }
                }

                if (!any) return;
            }
        }
    }

    /// <summary>How many messages one round will hand over before it comes back for air.</summary>
    private const int MaxDrain = 64;

    /// <summary>Set by a round that had something to do, so the next one comes sooner.</summary>
    private static volatile bool _busy;

    /// <summary>
    /// Which of these files have something waiting. Asked and answered at once: this runs on
    /// the thread that draws, so it may not wait for anything.
    /// </summary>
    /// <remarks>
    /// The array is pinned rather than handed over as a managed array, and that is the whole
    /// point of this method existing on its own. poll writes its answer back into the same
    /// memory it was given; an array marshalled by copy goes in and comes back untouched, so
    /// every file reads as quiet and no plugin is ever told anything. What that looks like is
    /// a plugin window that opens at the right size and stays black forever, which is why it
    /// is worth a check of its own.
    ///
    /// A file that has gone wrong counts as something to hear about, not only one with data on
    /// it: a plugin told its connection has hung up can tidy up, and one told nothing waits for
    /// ever. No poll to call means no windows getting events, which is a plugin that does not
    /// respond rather than an application that stops, so a failure answers nought.
    /// </remarks>
    internal static unsafe int Waiting(int[] files, bool[] ready)
    {
        if (files == null || ready == null || files.Length == 0) return 0;

        var polled = new PollFile[files.Length];

        for (int index = 0; index < files.Length; index++)
        {
            polled[index].File = files[index];
            polled[index].Events = PollIn;
            polled[index].Returned = 0;
        }

        int answer;

        try
        {
            fixed (PollFile* first = polled)
            {
                answer = Poll(first, (nuint)polled.Length, 0);
            }
        }
        catch (Exception)
        {
            return 0;
        }

        for (int index = 0; index < files.Length && index < ready.Length; index++)
        {
            ready[index] = (polled[index].Returned & (PollIn | PollBroken)) != 0;
        }

        return answer;
    }

    /// <summary>There is something to read on this file. The third entry after the usual three.</summary>
    private static void Ready(nint handler, int file)
    {
        if (handler == 0) return;

        var table = *(nint**)handler;
        if (table == null) return;

        var onReady = (delegate* unmanaged[Cdecl]<void*, int, void>)table[3];
        if (onReady == null) return;

        onReady((void*)handler, file);
    }

    /// <summary>One file being waited on, in the shape poll expects.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PollFile
    {
        /// <summary>The descriptor being asked about.</summary>
        public int File;

        /// <summary>What to ask about. <see cref="PollIn"/> here, and nothing else.</summary>
        public short Events;

        /// <summary>
        /// What the kernel writes back. Written into the same memory that was handed over, which
        /// is why the array has to be pinned rather than marshalled by copy.
        /// </summary>
        public short Returned;
    }

    /// <summary>There is something to read.</summary>
    private const short PollIn = 0x001;

    /// <summary>The file has gone wrong or the other end has gone away.</summary>
    private const short PollBroken = 0x008 | 0x010 | 0x020;

    /// <summary>
    /// Asks the kernel which of a set of descriptors is ready. Called with a timeout of nought,
    /// so it answers at once: this runs on the thread that draws and may not wait for anything.
    /// </summary>
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int Poll(PollFile* files, nuint count, int timeoutMilliseconds);

    /// <summary>True when this timer is still one the plugin wants ringing.</summary>
    private static bool StillWanted(nint handler, List<Timer> timers)
    {
        lock (Gate)
        {
            foreach (var timer in timers)
            {
                if (timer.Handler == handler) return true;
            }
        }

        return false;
    }

    /// <summary>True when this file is still one the plugin wants watching.</summary>
    private static bool StillWatched(nint handler)
    {
        lock (Gate)
        {
            foreach (var watch in Watches)
            {
                if (watch.Handler == handler) return true;
            }
        }

        return false;
    }

    /// <summary>Rings one timer, through the fourth entry of its table.</summary>
    private static void Call(nint handler)
    {
        if (handler == 0) return;

        var table = *(nint**)handler;
        if (table == null) return;

        var onTimer = (delegate* unmanaged[Cdecl]<void*, void>)table[3];
        if (onTimer == null) return;

        onTimer((void*)handler);
    }

    /// <summary>
    /// What the loop is holding and how much of it has gone off, for a plugin that has been
    /// given a window and is not drawing in it. The first thing worth knowing then is whether
    /// the plugin ever asked for anything at all.
    /// </summary>
    public static string Census()
    {
        lock (Gate)
        {
            return Timers.Count + " timers, " + Watches.Count + " files, " +
                   _rings + " rings, " + _deliveries + " deliveries";
        }
    }

    /// <summary>
    /// A note of this moment, to be handed to <see cref="DropSince"/> later.
    /// </summary>
    public static long Mark()
    {
        lock (Gate) return _asked;
    }

    /// <summary>
    /// Forgets everything registered since a moment, without telling the plugin.
    /// </summary>
    /// <remarks>
    /// For a window being taken down. A plugin is supposed to give back what it asked for when
    /// its window goes, and Vital does not: it asks for two files to be watched when its window
    /// is attached and never mentions them again. What it does do is free the handler behind
    /// them, so the next time either file has something on it the host calls into memory that
    /// is not there any more, and the plugin dies with a jump into nothing.
    ///
    /// So whatever a window brought with it goes when the window goes. Anything registered
    /// before it opened is left alone: Serum asks for a timer the moment it loads, editor or
    /// no editor, and it needs that one for as long as it is loaded.
    /// </remarks>
    public static void DropSince(long mark)
    {
        int timers = 0;
        int watches = 0;

        lock (Calling)
        lock (Gate)
        {
            for (int index = Timers.Count - 1; index >= 0; index--)
            {
                if (Timers[index].Asked < mark) continue;

                Timers.RemoveAt(index);
                timers++;
            }

            for (int index = Watches.Count - 1; index >= 0; index--)
            {
                if (Watches[index].Asked < mark) continue;

                Watches.RemoveAt(index);
                watches++;
            }
        }

        if (timers == 0 && watches == 0) return;

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            "the window took " + timers + " timers and " + watches + " watched files with it");
    }

    /// <summary>
    /// Takes a timer from a plugin that speaks in numbers rather than in objects.
    /// </summary>
    /// <remarks>
    /// CLAP hands the host a period and takes back an id, and expects to be called on its own
    /// plugin pointer when the time is up. There is no object to call, so the caller says what
    /// to do instead, and the key is only there so it can be taken away again.
    /// </remarks>
    public static void Keep(nint key, long milliseconds, Action fire)
    {
        if (key == 0 || fire == null) return;

        long interval = Math.Max(TickMilliseconds, milliseconds);

        lock (Gate)
        {
            foreach (var timer in Timers)
            {
                if (timer.Handler != key) continue;

                timer.Interval = interval;
                timer.Due = Environment.TickCount64 + interval;
                timer.Fire = fire;
                return;
            }

            Timers.Add(new Timer
            {
                Handler = key,
                Interval = interval,
                Due = Environment.TickCount64 + interval,
                Fire = fire,
                Asked = _asked++
            });
        }

        Start();
    }

    /// <summary>Gives a timer back. Waits for a call in progress, so nothing is freed mid-call.</summary>
    public static void Drop(nint key)
    {
        lock (Calling)
        lock (Gate)
        {
            for (int index = Timers.Count - 1; index >= 0; index--)
            {
                if (Timers[index].Handler == key) Timers.RemoveAt(index);
            }
        }
    }

    /// <summary>Takes a file to watch from a plugin that speaks in numbers.</summary>
    /// <remarks>
    /// The CLAP half of <see cref="RegisterEventHandler"/>. The same key asked for twice moves
    /// the existing watch rather than adding a second.
    /// </remarks>
    public static void Watching(nint key, int file, Action<int> fire)
    {
        if (key == 0 || fire == null) return;

        lock (Gate)
        {
            foreach (var watch in Watches)
            {
                if (watch.Handler != key) continue;

                watch.File = file;
                watch.Fire = fire;
                return;
            }

            Watches.Add(new Watch { Handler = key, File = file, Fire = fire, Asked = _asked++ });
        }

        Start();
    }

    /// <summary>Gives a watched file back. Waits for a call in progress.</summary>
    public static void Unwatch(nint key)
    {
        lock (Calling)
        lock (Gate)
        {
            for (int index = Watches.Count - 1; index >= 0; index--)
            {
                if (Watches[index].Handler == key) Watches.RemoveAt(index);
            }
        }
    }

    /// <summary>The run loop is an IRunLoop and the root interface, and nothing else.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Query(void* self, byte* id, void** result)
    {
        if (result == null) return Vst3Abi.NoInterface;

        if (Vst3Abi.SameId(id, Vst3Abi.RunLoopId) || Vst3Abi.SameId(id, Vst3Abi.FUnknownId))
        {
            *result = self;
            return Vst3Abi.ResultOk;
        }

        *result = null;
        return Vst3Abi.NoInterface;
    }

    /// <summary>
    /// AddRef and Release both. There is one run loop for the process and it is never freed, so
    /// it always answers one: nought would tell a plugin the object had already gone.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint KeepAlive(void* self) => 1;

    /// <summary>
    /// A VST3 plugin asking for a clock, by handing over an object to call. The same handler
    /// asked for twice moves the existing timer rather than adding a second, since two timers on
    /// one object would ring it twice per round.
    /// </summary>
    /// <remarks>
    /// Nought means as often as possible, which here means every round of the pump. Floored at
    /// the round rather than honoured, since asking for a millisecond would only mean the round
    /// arrives late.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterTimer(void* self, void* handler, ulong milliseconds)
    {
        if (handler == null) return Vst3Abi.NoInterface;

        long interval = Math.Max(TickMilliseconds, (long)milliseconds);

        lock (Gate)
        {
            foreach (var timer in Timers)
            {
                if (timer.Handler != (nint)handler) continue;

                timer.Interval = interval;
                timer.Due = Environment.TickCount64 + interval;
                return Vst3Abi.ResultOk;
            }

            Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
                "the plugin asked for a timer every " + interval + "ms, handler " + ((nint)handler).ToString("X"));

            Timers.Add(new Timer
            {
                Handler = (nint)handler,
                Interval = interval,
                Due = Environment.TickCount64 + interval,
                Asked = _asked++
            });
        }

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// A plugin giving a clock back. Waits for a call in progress, so nothing is freed mid-call.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int UnregisterTimer(void* self, void* handler)
    {
        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            "the plugin gave back timer " + ((nint)handler).ToString("X"));

        lock (Calling)
        lock (Gate)
        {
            for (int index = Timers.Count - 1; index >= 0; index--)
            {
                if (Timers[index].Handler == (nint)handler) Timers.RemoveAt(index);
            }
        }

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// Takes a file a plugin wants watching. Its X11 connection, in practice: this is how a
    /// plugin's window hears about a click.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterEventHandler(void* self, void* handler, int file)
    {
        if (handler == null) return Vst3Abi.NoInterface;

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            "the plugin asked to have file " + file + " watched, handler " + ((nint)handler).ToString("X"));

        lock (Gate) Watches.Add(new Watch { Handler = (nint)handler, File = file, Asked = _asked++ });

        return Vst3Abi.ResultOk;
    }

    /// <summary>
    /// A plugin giving a watch back. Waits for a call in progress. A plugin that never does this
    /// is what <see cref="DropSince"/> exists for.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int UnregisterEventHandler(void* self, void* handler)
    {
        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            "the plugin gave back the watch on handler " + ((nint)handler).ToString("X"));

        lock (Calling)
        lock (Gate)
        {
            for (int index = Watches.Count - 1; index >= 0; index--)
            {
                if (Watches[index].Handler == (nint)handler) Watches.RemoveAt(index);
            }
        }

        return Vst3Abi.ResultOk;
    }
}
