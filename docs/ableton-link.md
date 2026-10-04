# Ableton Link

Built on 2026-10-04 from the plan written the day before, and **not yet heard against another Link
program**. Everything up to that point is built and tested; what is left is the measurement in a
real room, at the end of this page.

## Why Link and not network MIDI

Network MIDI needs no code. RTP-MIDI (RFC 6295) is a driver: `rtpmidid` on Linux shows each
session as an ALSA sequencer port, and Tobias Erichsen's `rtpMIDI` does the same on Windows. To
this application those are ordinary ports, so links, jobs and following a clock all work over
them today. It only removes a cable, and the controllers here are USB devices next to the
computer anyway.

Link gives something that could not be had before: tempo, bar phase and start/stop shared with
Live, Bitwig, Reason, Traktor, a phone app or another computer on the same network, with no master
and no setup. For an application that plays pads and a tracker in a room where something else is
running, that is the feature.

MIDI clock stays, and the two do not overlap. Hardware (the KeyStep Pro, most synths) speaks
clock and not Link; software speaks Link. On Link the outputs ticked for clock are sent the
session's tempo, so this machine is a bridge from one to the other without that being a feature of
its own.

## What Link is

An open source library from Ableton, GPL 2 or later (a proprietary licence is also offered).
This application is GPL 2, so the licence fits. The library is C++ and header only, with a
plain C wrapper, `abl_link`, in `extensions/abl_link/`, which is what P/Invoke can reach.

What it shares, and nothing else:

- **Tempo.** Any peer may change it and every peer follows.
- **Beat and phase.** A timeline mapping the host clock to beats. Peers agree on phase against a
  *quantum*, the number of beats that have to line up (usually 4, a bar), so bar one lands
  together, not just the beat.
- **Start and stop**, opt in per peer (`enable_start_stop_sync`).

It carries no notes, no controllers, no audio and no song position.

**It syncs clocks, not ticks, and that decides the whole design.** MIDI clock is 24 pulses a
beat, and each pulse is as late as the wire made it. Link instead keeps a mapping between each
machine's own monotonic clock and beat time, agreed over UDP multicast. An application asks
"which beat is it at host time T" and schedules its audio ahead so that beat leaves the speaker
at T. Network jitter only affects how fast peers agree, not when a beat sounds, which is why it
holds up over wifi where MIDI clock wobbles.

The release pinned is **Link 4.1** (2026-09-23), with asio at
`8806a6803cde7054c3049d3666d3ec36786568c5`, the commit that release's `modules/asio-standalone`
submodule points at. 4.1 also has a Link Audio half (`abl_link_audio_*`), which is not used.

## What is built

### The native library

`native/abletonlink/` is the recipe and `native/abletonlink/build.sh` is the one command. It
fetches the pinned Link release and its asio commit, builds `abl_link` as a shared library in one
docker image for all three runtimes, strips them, and puts them beside BASS:
`native/linux-x64/libabl_link.so`, `native/linux-arm64/libabl_link.so` and
`native/win-x64/abl_link.dll`. The binaries are committed, like BASS's, so a release depends on
nothing being reachable on the day it is made and ships the bytes that were tested.

What it took, each found by building rather than by reading:

- **A shared library, not Link's static one.** `abl_link.cmake` builds `STATIC` for C programs to
  link against; a managed program can only reach a shared one, so the recipe is the same source
  file with `SHARED` and the C++ runtime linked in statically.
- **Ubuntu 22.04 for its C library.** Built on this machine (Debian trixie, glibc 2.41) the library
  asked for glibc 2.38, which Raspberry Pi OS bookworm (2.36) and Ubuntu 22.04 (2.35) do not have.
  Built in the image it asks for 2.34 at most, on both Linux runtimes. Debian bullseye was tried
  first and its archive is gone.
- **arm64 and Windows are cross compiled**, since this machine has no binfmt emulation for arm64.
- **Three things about mingw**, since Link is written for MSVC on Windows. Link includes
  `<Windows.h>`, `<WinSock2.h>` and `<WS2tcpip.h>` with capitals and mingw's headers are lowercase,
  so `windows/` holds three aliases. mingw's headers define `interface` as a macro for COM, and
  `rpc.h` redefines it on every inclusion with no guard, while Link 4.1 uses `interface` as a
  variable name in `Channels.hpp`: `windows/first.h` is included ahead of everything, pulls in
  every Windows header Link and asio reach, and takes the macro away. And Ubuntu 22.04's mingw never
  declares `SetThreadDescription`, which Link uses to name its threads for a debugger, so it is
  defined to do nothing. MSVC finds the socket libraries through `#pragma comment(lib)`, which
  mingw ignores, so they are named in the CMake. The `-posix` compilers, since the win32 thread
  model has no `std::thread`.

The Windows DLL depends on `KERNEL32`, `WS2_32`, `IPHLPAPI` and `msvcrt` and nothing else, and
exports all 51 `abl_link_` functions. **It has not been loaded on Windows yet**: the Windows CI run
is the first thing that will, through `AbletonLinkLibraryTests.The_library_is_here`.

It is carried like BASS through every place a native has to be named: the three csproj targets,
the `<None>` items for a publish, the test project's own copy step, `.github/scripts/linux-natives.sh`
and the Windows payload check in `release.yml`. **Optional** in all of them, a warning rather than a
failure: without it the Link choice is greyed with a sentence saying why, and nothing else is lost.
A linux-arm64 publish was checked to carry it, and a win-x64 one.

The Flatpak is the one place it is not carried but built: `packaging/flatpak/io.github.petervdpas.JingleBox2.yml`
gets a `cmake` module ahead of the application's, compiling `abl_link` from the pinned Link commit
(with its asio submodule) into `/app/lib`, since a Flatpak build has no network and Flathub
builds from source anyway. The manifest already has `--share=network`, which is all Link's
discovery needs.

`check-natives.sh` does not apply: that compares against what un4seen publishes. Moving the Link
pin is a decision, made in `build.sh`.

### `Sync/`

- `AbletonLinkAbi` is the P/Invoke declarations and nothing else, static like `Vst3Abi`. Both
  handles are a struct of one pointer passed by value, declared as that pointer, which is the same
  register on System V x86-64, aarch64 and Windows x64.
- `IAbletonLink` and `AbletonLink` are the session. The native instance is made the first time
  somebody joins, since Link starts threads of its own when it is made. One session state, captured
  into under one lock. The tempo is cached from Link's own callback, so the clock thread reads it
  without going to Link. The three callbacks are static, handed a `GCHandle`, and catch everything.
- `IAbletonLinkLines` is the arithmetic: alone a pass starts at once, with peers on the next
  quantum, and a quantum is held to one to sixteen with nonsense reading as four.

`AbletonLink` named in full everywhere, never `Link`, because `ControlLink`, `LinkKey`,
`ILinkTargets` and the rest already mean a controller pointed at a knob.

### The clock thread

`TrackerPlayer.RunClock` asks `OnLink` at every line, beside `ClockFollow`:

1. A pass starting on Link works out the beat the first line would sound on, which is now plus the
   lead, moves it to the next quantum where anybody else is there, waits without sending MIDI clock,
   and starts its stopwatch there, so every tick after it is placed from the moment the music began.
2. Each line after is due at `TimeAt(beat) - lead`, converted to the pass's stopwatch at the moment
   of asking, so the two clocks drifting over a long set never accumulates.
3. It waits through `WaitUntil`, so MIDI clock out keeps going, at the session's tempo.

The **lead** is `ITrackerOutput.LatencyMilliseconds`, the buffer plus the render-ahead cushion as
configured, plus `IAbletonLink.OffsetMilliseconds`, the hand adjustment in SETTINGS.

`Playing(song)` and `PlayingBpm` answer the session's tempo on Link. `PlayAt`, which is a lane or a
hand on a tempo knob, **puts its tempo to the session**, which is what Live does with its own tempo
automation. Play, pause and stop tell the session where start and stop are shared.

**Link's tempo callback fires for this peer's own commits.** That was found by taking the guard
out: without it, a tempo this peer proposed came straight back as a peer's and would have been
written into the song's tempo on every line a lane moved. `AbletonLink` remembers what it last
proposed and does not announce that one.

### The settings and the wiring

SETTINGS, MIDI, the Clock card: three radio buttons, Keep its own time, Follow another machine's
clock, Ableton Link. Under Link: **Beats that line up** (1, 2, 3, 4, 8 or 16, four by default),
**Share play and stop** (on by default), **Output offset** in milliseconds (-200 to 200, nought by
default), and the line at the foot of the card saying how many other programs are in the session.
`MidiClockSource.AbletonLink` is 2, `MidiConfig.LinkQuantum`, `LinkStartStop` and `LinkOffsetMs`
are stored beside `ClockPort`.

`MainViewModel` holds the one session and hands it to the page and the player. `DriveTheClock`
joins or leaves when the choice moves. `WhenTheSessionSays` is the shape of `WhenTheMasterSays`: a
peer's tempo goes into `Tracker.Bpm`, a peer's start and stop go through the page's own play and
stop, each only where it would change something, and the song's tempo moved by a hand is put to the
session. The session is taken down in `Finished`.

Joining is the only moment the network is touched. A machine with Link chosen joins again when
the application starts, the way a followed port is opened again.

The log writes to the Tracker area: joining and leaving, each peer count, each tempo and start or
stop from a peer, each tempo put to the session, and for every pass the beat it began on, how many
peers there were, how long it waited, and how far ahead it ran.

The help is `Help/Topics/settings.clock.md`, which has a section of its own for Link.

## Tests

- `AbletonLinkLinesTests`: where a pass begins, alone and with peers, on the boundary, behind
  nought, on a beat that is not a number, and every quantum a hand-edited file could hold.
- `AbletonLinkLibraryTests`: the real library, **never on the network**. `AbletonLink.Ready` makes
  the native instance without joining, since a test that joined would become a peer in whatever
  session is on the network and could move somebody's Live. The library loads, every read before
  it is made answers nothing, a beat and its moment go round within two microseconds, the clock is
  in microseconds, a proposed tempo is the timeline's tempo, its own tempo is not heard as a peer's,
  nonsense tempos are refused, start and stop reach the state only while shared, and disposing
  twice is harmless.
- `AbletonLinkClockTests`: a real `TrackerPlayer` over a session double whose timeline is a straight
  line. The first line lands on the next bar with peers and at once alone, every line falls on its
  own beat, the offset runs the lines ahead, a timeline that moves under a running pass is followed,
  the playing tempo is the session's, a tempo set while playing is proposed, play and stop are told
  once each, and a session that is off is left alone.

Each guard was checked by taking it out. **One test passed with its fault in**: at a steady tempo a
stopwatch started on the first beat lands on every later beat too, so "each line falls on its own
beat" could not tell the timeline's path from the stopwatch's. `A_timeline_that_moves_is_followed`
moves the timeline a tenth of a beat later between two lines, and with the per-line placement taken
out it fails at 24.9 ms early, which is that tenth of a beat at 240.

## Not done

- **Heard against another Link program.** The one thing no test here can say. Live, or the
  `link_hut` example from Link's own source, on another machine on the same network: are the
  bars together by ear, and what offset does this machine need. The log line for each pass says
  what lead it used.
- **Run on a Raspberry Pi.** The arm64 library is built and asks for glibc 2.34; nothing has loaded
  it on one.
- **The beat handed to plugins** is still this pass's own count from nought. A pass started with
  peers begins on a quantum, so a plugin's tempo synced LFO is in phase with the session within the
  quantum; alone, or after the session's phase moves, it is not.
- **Link's clock jumping backwards** has no test.

## Decided

- Link before network MIDI, because network MIDI already works through the operating system.
- One more choice of clock source, exclusive with following a MIDI port. Sending MIDI clock out stays
  independent of it.
- Off until chosen; nothing touches the network until somebody chooses it.
- The native is optional; without it, only Link is missing.
- Host time is Link's clock, not the stopwatch.
- The enum stays `MidiClockSource` in `Midi/Enums`, with a third value. It is one question asked on
  the MIDI page, and its remarks say why it lives there. Renaming it changes a stored type for no
  reader's benefit.
- `Sync/` is the folder, and every type says `AbletonLink` in full.
- **A tempo lane on Link moves the session**, the way Live's does. A lane that moved this transport
  and nobody else's cannot exist on a shared timeline: the lines are placed where the session's
  beats fall.
- **Joining with peers waits for the next quantum** whether or not start and stop are shared, which
  is Link's own rule for a quantised launch and what Live does.
- The log area is Tracker.
- The binaries are committed and rebuilt by `build.sh`, rather than built by every release run.

## Still open

- **How much output latency matters in practice.** Is the configured cushion plus buffer close
  enough, or does it need measuring from the output's position? On ASIO the buffer counted is
  BASS's shared one and not the driver's block, so the figure is wrong there by however much the
  two differ, and the offset is the only correction. Only answered by listening.
- **Sample accurate note starts.** Lines start on block boundaries, up to 11.6 ms at 512 frames.
  Fixing that is a mixer change (a frame offset on a voice start) that would help MIDI clock
  following and pad launching too.
- **Song position.** Link has no song position, only beats and phase. Starting together from
  somewhere other than the top of the song is not something Link can say.
- **The pads.** FIRE has no beat; a pad starts when it is hit. Launching a pad on the next bar, the
  way Live quantises clip launches, is the obvious next thing Link makes possible, and it needs a
  beat whether Link is on or not.
