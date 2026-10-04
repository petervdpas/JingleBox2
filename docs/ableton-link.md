# Ableton Link

Not built. A plan, written on 2026-10-03, after comparing Link against MIDI over a network.

## Why Link and not network MIDI

Network MIDI needs no code. RTP-MIDI (RFC 6295) is a driver: `rtpmidid` on Linux shows each
session as an ALSA sequencer port, and Tobias Erichsen's `rtpMIDI` does the same on Windows. To
this application those are ordinary ports, so links, jobs and following a clock all work over
them today. It only removes a cable, and the controllers here are USB devices next to the
computer anyway.

Link gives something that cannot be had today: tempo, bar phase and start/stop shared with Live,
Bitwig, Reason, Traktor, a phone app or another computer on the same network, with no master and
no setup. For an application that plays pads and a tracker in a room where something else is
running, that is the feature.

MIDI clock stays, and the two do not overlap. Hardware (the KeyStep Pro, most synths) speaks
clock and not Link; software speaks Link. A machine following Link and sending MIDI clock out is
a bridge from one to the other, and that falls out of the design below rather than being a
feature of its own.

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

It carries no notes, no controllers and no audio.

**It syncs clocks, not ticks, and that decides the whole design.** MIDI clock is 24 pulses a
beat, and each pulse is as late as the wire made it. Link instead keeps a mapping between each
machine's own monotonic clock and beat time, agreed over UDP multicast. An application asks
"which beat is it at host time T" and schedules its audio ahead so that beat leaves the speaker
at T. Network jitter only affects how fast peers agree, not when a beat sounds, which is why it
holds up over wifi where MIDI clock wobbles.

The C wrapper's surface, read from `abl_link.h` (check it against whichever commit gets pinned):
`abl_link_create(bpm)`, `abl_link_enable`, `abl_link_enable_start_stop_sync`,
`abl_link_num_peers`, `abl_link_clock_micros`, the three callbacks (peers, tempo, start/stop),
and a session state that is *captured*, read or edited, and *committed*:
`abl_link_capture_app_session_state` / `abl_link_commit_app_session_state`, with
`abl_link_tempo`, `abl_link_set_tempo`, `abl_link_beat_at_time`, `abl_link_phase_at_time`,
`abl_link_time_at_beat`, `abl_link_request_beat_at_time`, `abl_link_is_playing`,
`abl_link_set_is_playing`, `abl_link_time_for_is_playing`. The `audio` variants of capture and
commit are the realtime-safe pair, for a sound card's thread only.

## What is already in place

More than it looks, because following MIDI clock already made the clock thread able to answer to
something outside.

- **`MidiClockSource`** is the setting saying whose clock the transport runs on: `Own` or
  `Followed`. Link is a third answer. That enum's remarks currently say "there is no third
  answer", and that sentence has to change along with the enum.
- **`TrackerPlayer.RunClock`** has two branches for when the next line is due: `WaitUntil` on
  the stopwatch, or `IMidiClockFollow.WaitFor` on arriving ticks. Link is a third. It is much
  closer to the first than the second, since it is a time to wait for and not a count.
- **`WaitUntil` sends MIDI clock out** while it waits, placed against the same stopwatch the
  lines are placed on. So if the Link branch waits through `WaitUntil` rather than beside it,
  the bridge to hardware is free.
- **The tempo already flows in from outside.** `IMidiClockFollow.TempoHeard` is written into
  `Tracker.Bpm` by `MainViewModel`, as an ordinary edit. Link's tempo callback goes the same way.
- **The beat count.** `RunClock` sums `beat` per line and hands it to plugins (the "same count
  the plugins are told"). Under Link that number should be Link's beat, so a plugin's tempo
  synced LFO lines up with the other applications as well as with this one.
- **`ITrackerPlayer.NearestLine`** and the `LineMark` it reads are timestamp based already, so a
  note from outside still lands on the right line.
- **`IMidiClockFollow`'s remarks** settle what silence means (hold, do not stop). Link has no
  silence: the timeline keeps running without peers. Nothing to decide there.

## The pieces

### The native library

Nobody ships `abl_link` as a binary, so it is built here: a small CMake project that compiles
`abl_link` as a shared library (`abl_link.dll`, `libabl_link.so`) against a pinned Link commit,
for win-x64, linux-x64 and linux-arm64. Link pulls in standalone asio as a submodule.

It is then carried exactly as BASS is, and everything CLAUDE.md says about carrying a native
applies: the csproj targets that put it beside the executable (`CopyBassToOutput`,
`CopyBassToLinuxOutput`, `EnsureBassDllInPublish`), `.github/scripts/linux-natives.sh`, the
Windows payload check, `build-deb.sh`, and **the test project's own copy step**, which is the one
that was missed for `bassasio.dll`.

It is **optional**, and missing it must cost Link and nothing else. Same shape as ASIO: a
missing library throws on the first call, so the seam asks once, remembers, and SETTINGS says
why the choice is grey. The release check treats it as a warning, like `libbass_aac.so`, not
like `libbassmix.so`.

`check-natives.sh` does not apply: that compares against what un4seen publishes. A Link we build
ourselves is pinned by commit, and moving the pin is a decision.

### `ILinkSession`, the seam

In `Audio/` or a new `Sync/` folder (open, see below), behind an interface like everything else,
so the clock thread can be tested against a double that hands out a scripted timeline:

- enable and disable, peers, start/stop sync on or off
- the current tempo, and set it
- the beat at a host time, the host time of a beat, against a quantum
- playing, and when it started or stopped
- three events: peers, tempo, playing

**Host time is Link's own clock, `abl_link_clock_micros`, and nothing else.** On Linux it is
`CLOCK_MONOTONIC_RAW`, while `Stopwatch` is `CLOCK_MONOTONIC`. The two drift by however much NTP
is slewing, a few parts per million. That is small, but two clocks that nearly agree is the fault
this codebase keeps naming, so under Link the clock thread waits on Link's time rather than
converting from the stopwatch.

### The clock thread

A third branch in `RunClock`, chosen once a line like the other two:

1. When a pass starts, ask Link for the host time of the next quantum boundary (or of now, if
   the music is already playing elsewhere and quantised start is off) and anchor line 0 there.
2. Line `n` is due at `time_at_beat(anchor + n * beatsPerLine) - outputLatency`.
3. Wait for it through `WaitUntil`, so MIDI clock out keeps working.

The tempo is never read from the song under Link: it is whatever the timeline says, read per line
like it is today. A tempo change elsewhere then lengthens the lines from there on, and the line
times stay absolute, so nothing drifts.

**Output latency is the part that decides whether this is in time or just at the same tempo.**
The clock thread starts a note; that note is mixed into the render-ahead cushion and then leaves
through the device buffer. So the speaker is behind the clock thread by the cushion plus the
buffer, and the clock thread has to run that far *ahead* of Link's timeline. Live does exactly
this with its own output latency.

Two things make it harder here than in a DAW:

- **The cushion is not a constant.** `TrackerOutput`'s own remarks describe it wandering between
  nothing and its full size when something steals from it. Its target size is known
  (`RenderAheadMs`), the fill level at any instant is not reported.
- **A line starts on a block boundary, not on its sample.** At 512 frames that is up to 11.6 ms
  of quantisation, which MIDI clock following has today and nobody has noticed. Against another
  application on the same speakers, 11.6 ms is a flam.

So the first version uses the configured cushion plus the device buffer as a constant, offers a
manual offset in SETTINGS the way Live does, and the log says what it used. Measuring the real
latency, and starting voices on their sample within a block, are their own pieces of work (see
Still open).

### Tempo and start/stop, both ways

- **In:** the tempo callback writes `Tracker.Bpm` through the drawing thread, as `TempoHeard`
  does. Start/stop from a peer starts and stops the transport, as `Began`/`Ended` do.
- **Out:** the tempo field moved by hand calls `set_tempo` and commits. Play and stop here call
  `set_is_playing` when start/stop sync is on.
- **Echo.** A tempo arriving from Link and written into the song must not be sent straight back
  as a change. The MIDI clock path has the same problem in the other direction and solved it by
  not sending to the port it follows (`ClockDriven`). Here it is a flag or a comparison: do not
  commit a tempo that equals what Link just said.

### Settings

SETTINGS, where the clock source is chosen today: Own, Followed (a MIDI port), or Link. Under
Link: the quantum (default 4), start/stop sync (default on), the latency offset, and a live line
saying how many peers there are, since "is it connected" is the first question anybody asks.

Off by default, and that is not optional: enabling Link opens a UDP socket and joins a multicast
group on the network, and Windows will show its firewall prompt the first time. That happens when
somebody chooses it, never on start.

### The log

`LogArea.Midi` or a new area (open). One line when Link is enabled or disabled and why, one per
peer count change, one per tempo or start/stop arriving, the latency used for each pass, and
the beat and line each pass anchored on. When it is out of time, the question is always "which
moment did it think line 0 was", and that has to be readable afterwards.

### The pads

FIRE has no beat today; a pad starts when it is hit. Launching a pad on the next bar, the way Live
quantises clip launches, is the obvious next thing Link makes possible, and it needs a beat
whether Link is on or not. Not part of this plan; noted so it is not forgotten.

## Tests

The seam is what makes these possible without a network:

- With a scripted timeline, the clock thread fires line `n` at `time_at_beat(...) - latency`,
  across a tempo change mid pass and with lines per beat changed mid song.
- Quantised start: a pass started halfway through a bar waits for the next boundary.
- The beat handed to plugins is Link's beat, not this pass's own count.
- An incoming tempo is written once and not sent back.
- MIDI clock out is still sent while following Link, at Link's tempo.
- No library at all: the choice is refused, the transport stays on its own clock and plays, and
  there is a reason to show. This is the case CI on a machine without the native runs.
- Unhappy paths: a peer setting a tempo of nought, NaN, or past what a song can have (clamped,
  as `ClampedBpm` already does); Link's clock jumping backwards; the session disabled while a pass
  runs (falls back to its own clock and keeps playing, the rule `MidiClockSource.Followed`
  already keeps for an unplugged port).

What cannot be tested here is whether it is in time with Live in a real room. That is a hand on
two machines and a measurement, done once the rest is green.

## Order

1. Build `abl_link` for one platform, P/Invoke the handful of calls, and log peers and tempo with
   Live open on another machine. Proves the native and the network before anything else.
2. `ILinkSession` and its double; the clock source value; SETTINGS.
3. Tempo in and out, with the echo guard.
4. The third clock branch, with the configured latency.
5. Start/stop sync.
6. Carry the native through every build target and both release checks, on all three runtimes.
7. Measure it against Live and decide whether latency needs measuring rather than configuring.

## Decided already

- Link before network MIDI, because network MIDI already works through the operating system.
- Link is one more choice of clock source, exclusive with following a MIDI port. Sending MIDI
  clock out stays independent of it, as it is today.
- Off by default; nothing touches the network until somebody chooses it.
- The native is optional; without it, only Link is missing.
- Host time is Link's clock, not the stopwatch.

## Still open

- **Where the enum lives.** `MidiClockSource` sits in `Midi/Enums`, and Link is not MIDI. Moving
  and renaming it (`ClockSource`) changes a stored setting's type name but not its numbers. Same
  question for the folder the seam goes in.
- **How much output latency matters in practice.** Is the configured cushion plus buffer close
  enough, or does it need measuring from the output's position? Only answered by step 7.
- **Sample accurate note starts.** Lines start on block boundaries. Fixing that is a mixer
  change (a frame offset on a voice start) that would help MIDI clock following and pad
  launching too. Worth doing for its own sake, maybe before Link.
- **Tempo automation.** Tempo is an automation lane now (`9e2a58b`, `84026d4`). Under Link, a lane
  moving the tempo either sets Link's tempo (everyone follows the song) or is ignored (the session
  wins). Both are defensible; it is a decision about who leads.
- **Joining mid bar with start/stop sync off.** Start at once out of phase, or wait for the
  quantum. Live waits; that is probably the answer, but it should be chosen.
- **Song position.** Link has no song position, only beats and phase. Starting together from
  somewhere other than the top of the song is not something Link can say, which is a real
  difference from MIDI clock's position pointer.
- **Which log area.**
- **Building the native in CI or committing the binaries.** BASS binaries are committed because
  they are somebody else's. A library we compile could be built by the release workflow instead,
  which costs a C++ toolchain on three runners.
