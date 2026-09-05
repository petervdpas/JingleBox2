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

The Windows column was taken on an i3-13100, 4 physical cores and 8 logical, 16 GB, Windows 11
Pro 10.0.26200, on the **Balanced** power plan, with the audio on the default shared path rather
than an ASIO driver. The power plan is worth more than the processor here: everything below is a
wakeup, and Balanced is the setting that decides how deeply a core is allowed to go to sleep
between two blocks. The floor was taken six times rather than once, because unlike Linux's it
does not come back the same twice.

| | Linux, Debug, laptop, no GPU | Windows |
|---|---|---|
| socket round trip, back to back | 8.0 us | 11.4 us |
| socket round trip, once every 10 ms | 145.1 us | 60 to 136 us |
| round trip, one cheap plugin, quiet machine | 0.227 ms | not taken |
| that plugin's own side | 0.050 ms | not taken |
| **the crossing, quiet** | **0.177 ms** | **not taken** |
| the crossing, three busy plugin processes | 1.8 ms (four) | 0.06 to 0.10 ms |
| Gruber, mean, one track at a time | 69.0% | 33.6% |
| Gruber, mean, begun together | 64.3% | 30.4% |
| Gruber, blocks over budget per 5 s, in turn | 28.6 | 0.31 |
| Gruber, blocks over budget per 5 s, together | 19.3 | 0.03 |
| Gruber, worst block, in turn | 255% | 133% |
| Gruber, worst block, together | 259% | 104% |

Measurement one was not taken, and the two rows are left empty rather than filled with something
of the wrong shape: it wants one cheap plugin on a quiet machine and what was to hand was Gruber.
The crossing under load was had from the same run and is in the row under them.

## What the Windows column says

**The floor is the same finding.** Back to back and once every ten milliseconds are 11.4 and 60 to
136 microseconds, which is a factor of five to twelve apart. So the cost is the machine coming out
of idle rather than the socket, exactly as on Linux, and the diagnosis does not have to be redone
here. `afunix.sys` is about three microseconds dearer than the kernel-native socket back to back,
which is nothing. What is new is the **spread**: Linux answered 145.1 and meant it, and six runs
here gave anything from 60 to 136, each already a best-of-five over three hundred samples. That is
a coarser scheduler quantum and the Balanced power plan, and it sets how large a difference in
everything below is worth believing.

**The crossing is twenty times cheaper here, and load is why rather than the platform.** Under
three busy Serum processes it is 0.06 to 0.10 ms against Linux's 1.8. The Linux note already says
what to make of that: with four plugins each wanting two or three milliseconds of every eleven,
the mixing thread is not merely waking an idle core, it is waiting to be given one back. This
machine has the headroom that one had not, mean 33% of the block against 69%, so nothing is
queueing for a core. **The crossing grows with how loaded the machine is, and the platform is the
smaller term.**

**Overlapping pays here too, and it pays differently.** The mean came down 33.6% to 30.4%, three
points, which is close to the four points Linux saw and is the same shape: overlapping cannot make
the longest chain shorter, and Serum 2 is 2.2 ms of round trip against Serum 2 FX's 0.6, so one
plugin is the critical path and what overlapping removes is the other two queueing behind it.

Where it is not the same shape is the blocks over budget, and the difference is worth being exact
about rather than reading as a bigger win. Eleven blocks went over in three minutes serial against
one overlapped, which is 0.31 against 0.03 in every five seconds. As a ratio that is far better
than Linux's third; as a count it is eleven events against one, on a machine where almost nothing
goes over at all, and eleven is not many. **The honest reading is that this box had no problem for
overlapping to fix**, and that the ratio is flattering because the denominator is nearly nought.
The two mean ranges, 28 to 38 against 26 to 36, overlap almost entirely, where on Linux they
barely touched. That is the measurement to trust the least of the four here.

**The worst block came down, which it did not on Linux, and that is probably not overlapping.**
133% to 104%. The Linux column saw the worst block sit still at 255 and 259 and said so, rightly,
since a block at two and a half times its own length is a pause rather than the mixing and nothing
about when a plugin is asked for its block reaches it. Two windows of three minutes is not enough
to say a pause got rarer.

**A caveat that applies to every row of this column.** Debug, on the default shared audio path
rather than ASIO, so the block is 10.7 ms at 48 kHz and the percentages are a share of that. A
crappy default path means a longer block, which makes every percentage look better without
anything having got faster, so the two columns compare shapes rather than numbers.

## The thirty second cliff, which had to be fixed before any of this could be measured

The first attempt at measurement three could not be run, and it took a while to see why, because
what it looks like from a chair is every plugin crashing at once. **Every plugin process on Windows
was declared dead thirty seconds after its last control message, while alive and rendering.**

`PluginProcess.Start` gives the listening socket `StartTimeoutMilliseconds`, thirty seconds, so a
plugin that never connects cannot hold the caller for ever. That is right. **On Windows a socket
handed back by `Accept` carries a copy of the listening socket's options, timeout included; on
Linux it does not.** The audio socket has its own patience written over it on the next line. The
control socket did not, so a number meaning "how long to wait for a plugin to turn up" silently
became "how long a running plugin may go without speaking" — and a control socket is quiet by
design, since it carries knob moves and window resizes rather than audio.

Measured rather than reasoned about, twice. A bare probe: a listener set to 12345 ms hands back an
accepted socket reporting 12345 ms here and nought on Linux. And in a real session, four plugin
processes buried at **30.001, 30.001, 30.000 and 30.001 seconds** after each one's last control
message, every one of them still alive to be closed on purpose a quarter of a minute later. The
epitaph carried no exit code, because the child had not exited to have one.

The fix is one line, `controlSocket.ReceiveTimeout = PluginBridge.WaitForEver`, and
`Tests/BridgeSocketTests.cs` pins three things: that waiting for ever is nought, that the
inheritance really is what each platform does, and that a link saying nothing for longer than the
listener's patience is still there when it finally speaks. Checked by taking the fix out, which
fails the third at 315 ms with the same `SocketException` the reader was reading as a death.

**Worth keeping for the shape rather than the fault.** A socket option set in the right place for
the right reason, inherited somewhere nobody looked, on one platform only. And the run that caught
it was the long one: twenty seconds would have passed cleanly, produced a plausible column for the
table above, and left the cliff in place under every number in it.

## What would change a decision

If the Windows crossing is much larger than 0.177 ms, or the overlapped half saves much more than
the third of the blocks over budget it saved here, then **Overlap plugin blocks should stop being
off by default**, at least on that platform. It changes no sample, so the only argument for it
shipping off is caution about the audio path, and a large enough measurement outweighs that.

If the crossing is much smaller there, the opposite: the note in `CLAUDE.md` saying the buffer has
to be larger here than another host's needs a second cause found for it.

## What the decision actually came to

The second branch is the one that fired, and it fired on a measurement the first branch cannot be
read off at all.

**The crossing is smaller on Windows, not larger**, so by this document's own rule the note in
`CLAUDE.md` about the buffer having to be larger here than another host's **needs a second cause**.
It is not the wakeup. Three plugin processes cost 0.06 to 0.10 ms of crossing apiece on a box with
headroom, which at a 512 frame block is under three per cent of it, and no buffer was ever doubled
for that. Whatever is forcing the bigger buffer is still unfound, and the honest state of it is
that this exercise ruled the bridge out rather than explaining anything.

**Overlap plugin blocks should stay off by default, and this run is not the argument for changing
it.** The ratio looks decisive, eleven blocks over budget against one, and it is not: eleven events
in three minutes on a machine running at a third of its block is nearly nought against nearly
nought, and the two mean ranges overlap almost completely. What would settle it is the case the
switch exists for, which is a machine that is actually struggling — the Linux column, where the
serial half never came under 66% and the overlapped half never went over 68%. Turning a default
over on the strength of a box with nothing wrong with it is the wrong way round.

**And the reason to be careful about all four rows is the cliff.** Every number in the Windows
column above was taken after the thirty second fault was fixed, and every number anybody might have
taken before it would have been wrong without saying so. It is worth asking of the next platform
this is carried to what else is inherited, defaulted or assumed there that was set once on Linux
and never looked at again.
