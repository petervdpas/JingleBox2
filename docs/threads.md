# Threads

Which threads this application has, what each one is allowed to touch, and the rule for every
place two of them meet.

It is written down because the alternative has already cost real work. Every threading fault
this codebase has had was the same shape: a rule that was true when the code was written, held
in somebody's head, and quietly untrue once a second caller arrived. Never a lock forgotten.
Always a lock guarding the wrong thing.

## The threads

| Thread | Started by | What it does |
|---|---|---|
| **drawing** | Avalonia | Every view and view model. The document: the song, its patterns, the machines, the settings |
| **sound card** | BASS | `SynthOutput.Fill`, and one stream callback per pad in `BassAudioEngine`. Has a deadline of a few milliseconds and may never wait on anything |
| **mixing ahead** | `SynthOutput.StartMixingAhead` | Renders blocks in advance into a ring, so a plugin's round trip eats the cushion rather than the output. Exists only while render-ahead is on |
| **tracker clock** | `TrackerPlayer.StartClock` | Reads the song a line at a time and turns lines into notes. Above normal priority |
| **MIDI** | the port | Every message off every open device, through `MidiService` into the dispatcher and the routers |
| **log writer** | `Log` | Takes lines off a queue and writes them to the file, so nobody waits on a disc |
| **plugin run loop** | `PluginRunLoop` | The clock and the doorbell an X11 plugin window needs. One for the process |
| **plugin window focus** | `XEmbed` | Asks X four times a second who has the focus. One per watched window |
| **plugin bridge** | `PluginProcess` | Reads replies from one plugin's process. One per loaded plugin |
| **bridge control**, **bridge audio** | `PluginHostProcess` | Inside a plugin's own process, not this one |
| timers and `Task.Run` | various | Saving, metering, scanning. All of these post back to the drawing thread |

So: around eleven threads in this process on a busy session, and the ones that matter are the
first five.

## The rule

**A seam that more than one thread can enter says so, on the interface, naming them.** Not
"thread safe", which says nothing: which threads, and what each may do. Where the contract can
be broken from outside, it is guarded rather than trusted, because a contract nobody can break
is worth more than a contract everybody has read.

**And a guard on the audio path refuses rather than waits.** One quiet block is a click. A
blocked callback is every stream on the device stuttering, and on some drivers it is the device
gone until the application restarts. That rule is older than this document: it is why a queue
that has run dry hands back silence rather than waiting for the mixing thread to catch up.

## The seams

### `TrackMixer`

**Entered by:** the sound card, or the mixing-ahead thread, for `Render`. The drawing thread and
the MIDI thread for everything else: notes, levels, inserts, instruments, the track order.

**The rule:** one render at a time, whoever asks. A second caller is given a cleared buffer and
returns at once. Everything else may be called from any thread at any time, including during a
render.

**Why it is guarded and not merely documented:** the block size is not a value the two callers
share, it is the size of the arrays. `EnsureBusses` builds the bus, the loose bus and the
scratch again whenever the frame count changes, so two threads rendering at once with different
counts is one of them shortening the arrays the other is halfway through. It has taken the
application down: an index outside the bounds of the array, on the audio thread, after an
afternoon's work.

**Why there are two callers at all**, when the design says there is one: `SynthOutput` swaps
between rendering in step and rendering ahead, and `StopMixingAhead` waits two tenths of a
second for the ahead thread and then carries on regardless. That is right, since a plugin
holding it up must not hang the application, and it leaves that thread still inside the mixer
while the sound card's own thread starts. Changing the output device, or the render-ahead
setting, is exactly that moment.

**Its own lock**, not the state lock. The state lock is taken and released several times during
one render and by callers who are not rendering at all; sharing them would have a note played by
hand wait behind a block of audio.

**And the block is clamped to the buffer**, once, at the top. It used to be `frames * 2` on
trust with only the first clear asking whether the buffer was that long, so the bus mixing, the
loose bus and the master each wrote past the end of a buffer smaller than the caller claimed.
Half a fault guarded is worse than none: the guard that is there reads as the question having
been asked.

### `SynthOutput`

**Entered by:** the sound card for `Fill`, the mixing-ahead thread for `MixAhead`, the drawing
thread for everything else.

**The rule:** the ring is `_queueLock` and nothing else touches it. Which of the two ways is
running is `_cushion`, volatile, written only by the drawing thread while starting or stopping.
`_mixing` is volatile for the same reason.

**What is deliberately not guaranteed:** that the ahead thread has stopped when
`StopMixingAhead` returns. It is given two tenths of a second and then left to finish, and the
mixer's own guard is what makes that safe. A thread that would not stop is written to the log,
since it means a plugin took longer than a fifth of a second over one block, which is worth
knowing on its own.

### `TrackerPlayer`

**Entered by:** the clock thread while a pass runs, the drawing thread for the transport and the
mix, the MIDI thread for notes played by hand.

**The rule:** the song is read under `_lock` and the reference is taken once per pass, so a song
opened mid-pass does not tear the pass in half. `_generation` is the answer to a clock thread
that outlives its own stop: every pass carries its number and returns when it stops matching.

**The document it walks is edited from the drawing thread while it walks it**, and that is
allowed. What makes it safe is `Pattern`: its cells, the per-track column counts and the running
totals are one object, swapped whole, so a track widened mid-pass is either wholly there or
wholly not. They were three fields once, and the clock thread could read the new running total
against the old array and walk off the end of it.

### `MidiService` and the routers

**Entered by:** the MIDI thread for everything arriving, the drawing thread for opening and
closing ports and for reading what is open.

**The rule:** the port table, the per-device running status and the system-exclusive buffers are
all `_lock`. Everything above the router that touches a view model posts to the drawing thread
rather than doing it where it stands.

**One thing is deliberately not posted:** a lane being recorded reads the instant on the MIDI
thread and posts only the write. Posted whole, a fast hand would pile several values onto
whichever line the drawing thread happened to wake on.

**And one thing was got wrong here once**: a control target that read where a parameter *is*
rather than where it is *going*. Writes are coalesced onto the drawing thread, so twenty notches
arriving in the time it takes that thread to wake once each added a notch to the same stale
number, only the last survived, and the parameter moved one notch. A coalesced write is not a
race over an array; it is a race over what "the current value" means.

### `Log`

**Entered by:** every thread there is, including the one filling the audio buffer.

**The rule:** `Log.Write` may be called from anywhere. It appends to a queue and returns; the
writer thread does the file. Whether an area is on is one comparison, and a line that will not
be printed is never built, which is why the audio path may call it at all.

### The plugin bridge

**Entered by:** the audio path for blocks, the drawing thread for parameters and windows, the
bridge reader for replies.

**The rule:** the shared memory block is touched only between `Enter` and `Leave`. A plugin's
process falling over is not a fault to be guarded against on the caller's thread: it is the
whole reason the bridge exists, and the answer is that an effect passes its audio through and an
instrument goes quiet.

## Writing a new one

Three questions, in order:

1. **Which threads can reach this, today and after somebody adds a caller?** If the honest answer
   is "one, for now", that is the sentence to write down, because it is the one that stops being
   true.
2. **Is what they share a value, or the shape of something?** A value wants a lock or an
   interlocked read. A shape (an array and its length, a list and its count, a buffer and the
   size it was made for) wants to be one object, swapped whole. Every fault in this file's
   history was the second kind read as the first.
3. **What happens to the caller that loses?** On the audio path, silence and return. Anywhere
   else, wait. Never the other way round.
