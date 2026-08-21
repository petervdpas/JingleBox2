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
/// The pump runs on a thread of its own. That is honest for what is hosted today, which is
/// effects with no window of their own. The day a plugin draws its own interface this has to
/// move onto the UI thread, because that is where a toolkit expects to be called, and
/// <see cref="DriveWith"/> is the seam for it.
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
            lock (Gate) post = _post;

            if (post == null) Pump();
            else post(Pump);
        }
    }

    /// <summary>One round: whatever is due, and whatever has something to read.</summary>
    private static void Pump()
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
    /// Remembers a file a plugin wants watching. Nothing watches it yet: this is how a plugin
    /// hears about X11 events, and there are no plugin windows to have events. Taken rather
    /// than refused, because a plugin told no is a plugin that may decide the host is broken.
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
