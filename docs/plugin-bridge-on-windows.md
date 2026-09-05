# The plugin bridge, measured on Windows

Everything here was measured on Linux, in Debug, on a laptop with no GPU and no sound card.
**None of the numbers carry over. The shape does.** This file is written so that somebody sitting
at a Windows machine can produce the matching column without reading any of the code, and it has
a table at the bottom with the Linux side already filled in and the Windows side empty.

Write the answers into this file. That is what it is for.

## Why anybody cares

A plugin runs in a process of its own here, so every block of audio is a round trip: the parent
puts the block in shared memory, sends eight bytes, and waits. **What that costs is not carrying
the audio, it is waking a process that has been asleep for the length of a block.** On Linux that
is 0.177 ms when the machine is quiet and 1.8 ms when four plugin processes are busy, against
0.008 ms for the socket itself.

It is paid once per plugin per block whatever the plugin is doing, so it grows with the number of
plugins rather than with the music, and it is the reason a buffer here might have to be larger
than another host's. A host that runs plugins in its own process pays none of it.

## Before you start

1. Build and run **Debug**. Release hides exactly the thing being looked at, and Debug is what
   `dotnet run` gives.
2. SETTINGS, System: turn the log on, with the **Audio** and **Plugins** areas ticked.
3. SETTINGS, Plugins: scan, if this installation has not.
4. The log is `%APPDATA%\JingleBox2\jinglebox.log`. Clear it from SETTINGS before each run rather
   than trying to find where one run ended and the next began.

## The two lines everything below is read out of

The parent says what a round trip cost, once every five seconds, per plugin:

```
bridge: ZamAutoSat 500 crossings, worst 5% of the time they had, mean 2%, 0.231 ms each
```

The plugin's own process says what it spent on its own side, once every two seconds. That is the
plugin's work and the two buffer copies either side of it and nothing else:

```
run loop: ... 201 blocks of audio in the last two seconds, ... ; 0.050 ms on this side of the
block each, worst 0.101
```

**Subtract them. What the parent saw and the child did not is the crossing**, and the crossing is
the part this application owns. Everything else is somebody's synthesis.

Two more lines say which arrangement is running, written when the setting is read at startup and
again whenever the tick moves:

```
plugin blocks: one track at a time
plugin blocks: begun together
drive curve: the system's own
```

## Measurement one: what a crossing costs when nothing else is going on

Put **one cheap plugin** on one track's chain and let the transport run for half a minute. Cheap
matters: the point is that nothing in the answer is somebody's synthesis. ZamAutoSat was used on
Linux because it reports no parameters at all and does almost nothing; any small utility plugin
will do, and the `run loop` line tells you whether you picked a cheap one.

Read one `bridge:` line and one `run loop:` line from the same stretch and subtract.

## Measurement two: the floor, so the crossing can be told from the mechanism

This is the one that decides whether there is anything to fix. If a round trip between two threads
is already most of what a crossing costs, the bridge is doing nothing wrong and the answer is the
machine. On Linux the floor is 8 microseconds and a crossing is 177, which is what proved the cost
is the wakeup rather than the socket.

**`AF_UNIX` on Windows is `afunix.sys` rather than the kernel-native thing Linux has, so this has
to be measured there rather than assumed.** Drop this into `Tests/` as `ZzTrip.cs`, run it, and
delete it afterwards. It is a probe and does not belong in the suite.

```csharp
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace JingleBox2.Tests;

public class ZzTrip(ITestOutputHelper output)
{
    private const int N = 20000;

    [Fact]
    public void Floor()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jb-trip-" + Guid.NewGuid().ToString("N"));

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);

        var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        client.Connect(new UnixDomainSocketEndPoint(path));
        var server = listener.Accept();

        var stop = new ManualResetEventSlim();

        var child = new Thread(() =>
        {
            var got = new byte[8];
            var back = new byte[8];
            while (!stop.IsSet)
            {
                int read = 0;
                try { while (read < 8) { int n = server.Receive(got, read, 8 - read, SocketFlags.None); if (n <= 0) return; read += n; } }
                catch { return; }
                try { int sent = 0; while (sent < 8) sent += server.Send(back, sent, 8 - sent, SocketFlags.None); }
                catch { return; }
            }
        }) { IsBackground = true };

        child.Start();

        var message = new byte[8];
        var reply = new byte[8];

        for (int at = 0; at < 2000; at++) { client.Send(message); int r = 0; while (r < 8) r += client.Receive(reply, r, 8 - r, SocketFlags.None); }

        double best = double.MaxValue;

        for (int round = 0; round < 5; round++)
        {
            var clock = Stopwatch.StartNew();
            for (int at = 0; at < N; at++)
            {
                client.Send(message);
                int r = 0;
                while (r < 8) r += client.Receive(reply, r, 8 - r, SocketFlags.None);
            }
            clock.Stop();
            double us = clock.Elapsed.TotalMilliseconds * 1000 / N;
            if (us < best) best = us;
        }

        output.WriteLine($"back to back: {best:F1} us");

        double idle = double.MaxValue;

        for (int round = 0; round < 5; round++)
        {
            double total = 0;

            for (int at = 0; at < 300; at++)
            {
                Thread.Sleep(10);

                long began = Stopwatch.GetTimestamp();
                client.Send(message);
                int r = 0;
                while (r < 8) r += client.Receive(reply, r, 8 - r, SocketFlags.None);
                total += Stopwatch.GetElapsedTime(began).TotalMilliseconds * 1000;
            }

            if (total / 300 < idle) idle = total / 300;
        }

        output.WriteLine($"once every 10 ms: {idle:F1} us");

        stop.Set();
        client.Dispose(); server.Dispose(); listener.Dispose();
        try { System.IO.File.Delete(path); } catch { }
    }
}
```

Run it with `dotnet test Tests\JingleBox2.Tests.csproj -c Debug --filter "FullyQualifiedName~ZzTrip" --logger "console;verbosity=detailed"`.

**The second number is the one that matters.** If it is far larger than the first, the cost is the
machine coming out of idle and the finding here holds. If the two are close, Windows is doing
something different and the whole diagnosis has to be redone there.

## Measurement three: does overlapping the crossings pay

`Audio.OverlapSwitch` is a tick in SETTINGS, Engine, called **Overlap plugin blocks**, off by
default. With it off, each track's chain is waited for before the next is started. With it on,
every track's chain is begun before any is waited for, so the processes wake at the same time. It
changes no sample: `Tests/OverlappedMixerTests.cs` renders the same three tracks both ways and
compares the block sample for sample.

**The song decides whether there is anything to see.** The width of the overlap is the number of
independent tracks, never the number of plugins, because a chain is audio flowing through boxes in
order. A song with five plugins stacked on one track will show nothing at all.

Gruber is the song this was measured on: three tracks with plugin chains, one plugin instrument,
five plugin processes. Anything of that shape will do.

Then, exactly:

1. Untick **Overlap plugin blocks**.
2. Play the song for a minute.
3. Tick it. Do not stop the transport for the tick if you can help it, and do not restart.
4. Play the song for another minute.

That gives about twelve `render:` lines each way. Take the **mean** and the **blocks over budget**
from each line, average them per half, and ignore the first and last line of each half, which
catch the transport starting and stopping.

**Do not read the `bridge:` numbers as the answer here.** They go up when overlapping is on and
that is expected rather than a contradiction: a plugin begun early and collected late shows a
longer round trip while doing its work alongside the others. The mean and the blocks over budget
are the answer.

## What was ruled out on Linux, so nobody spends the afternoon again

- **Real-time scheduling.** The same plugin with the tick on and its audio thread confirmed at
  real time, priority 5, measured 0.237 ms against 0.231. A priority says who runs when the
  machine is awake; a core that has gone to sleep has to be woken first either way.
- **The socket.** Eight microseconds when the other side is already awake.
- **This application's own code between the two timestamps.** The parent stamps immediately before
  the send and the child immediately after its read, so what lies between them is the system.

## What holds whatever the numbers are

Both follow from it being a wakeup rather than work, so neither needs measuring:

- Real-time priority will not fix it.
- A longer block shrinks it as a share, since the wakeup is paid once per plugin per block. On
  Linux, five plugins are 7.6% of a 512 frame block and 1.9% of a 2048 frame one.

## What differs there, and why each has to be measured

- **`AF_UNIX` is a different implementation**, so the floor does not carry.
- **The scheduler quantum and the timer resolution are not the same**, and a process that has not
  asked for a finer timer gets a coarser one.
- **Idle states are a power plan rather than a kernel parameter**, so the machine's own settings
  move the answer and should be written down beside it.

## The table to fill in

Say which machine, which power plan and how many cores, because a comparison without them is a
fact thrown away.

| | Linux, Debug, laptop, no GPU | Windows |
|---|---|---|
| socket round trip, back to back | 8.0 us | |
| socket round trip, once every 10 ms | 145.1 us | |
| round trip, one cheap plugin, quiet machine | 0.227 ms | |
| that plugin's own side | 0.050 ms | |
| **the crossing, quiet** | **0.177 ms** | |
| the crossing, four busy plugin processes | 1.8 ms | |
| Gruber, mean, one track at a time | 69.0% | |
| Gruber, mean, begun together | 64.3% | |
| Gruber, blocks over budget per 5 s, in turn | 28.6 | |
| Gruber, blocks over budget per 5 s, together | 19.3 | |

## What would change a decision

If the Windows crossing is much larger than 0.177 ms, or the overlapped half saves much more than
the third of the blocks over budget it saved here, then **Overlap plugin blocks should stop being
off by default**, at least on that platform. It changes no sample, so the only argument for it
shipping off is caution about the audio path, and a large enough measurement outweighs that.

If the crossing is much smaller there, the opposite: the note in `CLAUDE.md` saying the buffer has
to be larger here than another host's needs a second cause found for it.
