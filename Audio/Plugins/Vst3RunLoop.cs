using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// The clock and the doorbell a Linux VST3 plugin expects the host to hold for it.
/// </summary>
/// <remarks>
/// Every other platform gives a plugin a run loop for free: Windows has a message pump and
/// macOS has a run loop, both already running before the plugin arrives. X11 has no such thing,
/// so VST3 makes it the host's job. A plugin hands over a timer, or a file the plugin wants to
/// be told about when there is something to read on it, and the host is expected to come back.
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
internal static unsafe class Vst3RunLoop
{
    /// <summary>How often the pump comes round. Fine enough for a blinking meter.</summary>
    private const int TickMilliseconds = 16;

    private sealed class Timer
    {
        public nint Handler;
        public long Interval;
        public long Due;
    }

    private sealed class Watch
    {
        public nint Handler;
        public int File;
    }

    private static readonly List<Timer> Timers = new();
    private static readonly List<Watch> Watches = new();
    private static readonly object Gate = new();

    private static void* _instance;
    private static Thread? _pump;
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

    private static void Run()
    {
        while (true)
        {
            Thread.Sleep(TickMilliseconds);

            Action<Action>? post;
            bool idle;

            lock (Gate)
            {
                post = _post;
                idle = Timers.Count == 0 && Watches.Count == 0;
            }

            // Nothing registered means nothing to do, and there is no reason to wake the UI
            // thread sixty times a second to find that out.
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
    private static void Pump()
    {
        Ring();
        Deliver();
    }

    private static void Ring()
    {
        long now = Environment.TickCount64;

        // Copied out under the lock and called outside it: a plugin is entitled to add or
        // remove a timer from inside its own timer, and doing that under our lock would be a
        // deadlock in somebody else's code.
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

        foreach (var timer in due)
        {
            try
            {
                Call(timer.Handler);
            }
            catch (Exception)
            {
                // A plugin's timer throwing is that plugin's problem for this round, not a
                // reason to stop ringing everybody else's.
            }
        }
    }

    /// <summary>
    /// Tells any plugin whose file has something waiting on it. This is how a plugin's own
    /// window hears about a mouse or a keystroke: its toolkit is sitting on an X11 connection
    /// and the host is the only thing that will ever look at it.
    /// </summary>
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

        if (Waiting(files, ready) <= 0) return;

        for (int index = 0; index < watching.Length; index++)
        {
            if (!ready[index]) continue;

            try
            {
                Ready(watching[index].Handler, watching[index].File);
            }
            catch (Exception)
            {
                // One plugin's event handling is that plugin's problem for this round.
            }
        }
    }

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
            // No poll to call means no windows getting events, which is a plugin that does
            // not respond rather than an application that stops.
            return 0;
        }

        for (int index = 0; index < files.Length && index < ready.Length; index++)
        {
            // A file that has gone wrong counts as something to hear about: a plugin told its
            // connection has hung up can tidy up, and one told nothing waits forever.
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
        public int File;
        public short Events;
        public short Returned;
    }

    /// <summary>There is something to read.</summary>
    private const short PollIn = 0x001;

    /// <summary>The file has gone wrong or the other end has gone away.</summary>
    private const short PollBroken = 0x008 | 0x010 | 0x020;

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int Poll(PollFile* files, nuint count, int timeoutMilliseconds);

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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static uint KeepAlive(void* self) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterTimer(void* self, void* handler, ulong milliseconds)
    {
        if (handler == null) return Vst3Abi.NoInterface;

        // Nought means as often as possible, which here means every round.
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

            Timers.Add(new Timer
            {
                Handler = (nint)handler,
                Interval = interval,
                Due = Environment.TickCount64 + interval
            });
        }

        return Vst3Abi.ResultOk;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int UnregisterTimer(void* self, void* handler)
    {
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

        lock (Gate) Watches.Add(new Watch { Handler = (nint)handler, File = file });

        return Vst3Abi.ResultOk;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int UnregisterEventHandler(void* self, void* handler)
    {
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
