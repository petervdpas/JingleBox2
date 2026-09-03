# One output stream, and BASSmix

Not built. This is the design for the piece ASIO is missing, written after reading what is
actually there, and it replaces a first draft that described a codebase this is not: it named
`Audio/SynthOutput.cs`, `Audio/PadPlaybackEngine.cs` and `OutputKind`, none of which exist, and
called BASSmix functions that do not exist either. What is below is checked against
`ManagedBass.Mix` 4.0.2 and against the files it names.

## What is wrong

An ASIO driver is not a device BASS can be opened on. The driver owns the card, so
`BassAudioEngine.SetOutputDevice` opens BASS on its own silent device instead, `TrackerOutput`
makes its stream a decoding one, and `AsioDevices.Open` hands that stream to the driver to pull.
That is right and it works.

The pads did not come with it. They are ordinary playing streams, made in
`BassAudioEngine.PlaySample` and `PlayStream` and started with `Bass.ChannelPlay`, and the device
they play on is the one that plays nothing. So picking an ASIO driver silences FIRE. Nothing says
so, since from BASS's point of view every one of those calls succeeded.

## What is not wrong, and was reported as wrong

The resampling. `AsioDevices.RateLocked` asks the card for the mix's rate, reads back what the
card actually settled on, and `ChannelSetRate` resamples into the difference. That is deliberate
and the alternative is the whole song playing sharp with nothing anywhere saying why, which is
written on the method already.

BASSmix does not remove it. A mixer has one rate, and a source at another rate is resampled as it
is mixed in, by the same kind of arithmetic in a different place. What a mixer buys here is one
audio path rather than one per output kind. It is not an argument about quality, and a design
that sells it as one is selling something it cannot deliver.

## The shape of it

One mixer stream owned by the audio engine. Everything this application plays is a decoding
source plugged into it, and the mixer stream is the one thing that is either played or handed to
the driver:

```
tracker stream   (decode)  ─┐
pad 0..n streams (decode)  ─┼─→  mixer stream  ─→  ChannelPlay, or AsioDevices.Open
take preview     (decode)  ─┘
```

There are three sources and not two. The third is `Waveform/WaveformPlayer.cs`, which auditions a
take on RECORD and plays the region in the recording edit dialog, and there are two instances of
it because those are two places. It is an ordinary playing stream like a pad, so it is silent
under a driver like a pad, and it was missed on the first pass through this for the same reason
the pads were: it is not where anybody looks when they are thinking about a song.

The branch that is left is the last arrow, and it is the branch `TrackerOutput.EnsureStarted`
already has, moved up one level to the thing that owns the mixer. Everything below it stops
caring which kind of output is picked, which is the whole return: the pads work under ASIO
because there is no second path for them to be missing from.

### What the mixer is not, and no UI moves

Two different things are called the mixer and this is the other one. The song's mixer is
`TrackMixer`: the tracks, the busses, the master strip at -1 and its effect chain, drawn under
the pattern, and it is a musical object that belongs to the song. The BASSmix stream is plumbing
that belongs to the audio engine. It sums three things that were already finished and it has no
settings, no strips and nothing to draw.

So nothing in this design touches a view, a view model or a tab. A pad and a take preview must
not appear on the song's mixer: a pad is not on a track and never has been, and running one
through the song's master chain would change what a song sounds like when somebody hits FIRE.
Coupling the two would also be coupling the pads to the tracker, which is backwards, since the
pads work with no song open at all.

That separation is the whole lesson of the `mixer-work` branch. It forked at `aaa5d08` on
2026-08-30, ran to five commits and 13,842 lines across 149 files, and did all of this at once:
the pads summed into one path, the mixer moved to a top-level tab at position one with the pads
and the recorder as strips on it, a wiring graph, a resampler, pad voices, detachable windows,
and a buffer that stopped being a fixed 60 ms. The audio got bad, and because six things had
moved together there was no way to say which one did it, so master went back to `aaa5d08` at
`d4ba95b` and the lot was abandoned.

The idea was not what was wrong with it. That branch's own `docs/audio-backends.md` had already
reached this same conclusion in the same words, that the pads have to stop being played channels
and become decoding channels and something has to sum them. What was wrong was the size: the
culprit was one buffer constant, and it took two days to find because it was travelling with a
UI reorganisation. This is the audio half on its own, and it moves nothing anybody can see.

## The API, as it really is

Verified against `ManagedBass.Mix` 4.0.2:

```
BassMix.CreateMixerStream(rate, channels, flags)   the mixer, returns a handle or 0
BassMix.MixerAddChannel(mixer, source, flags)      plugs a source in
BassMix.MixerRemoveChannel(source)                 unplugs it, by the source's own handle
BassMix.ChannelGetLevel(source)                    a source's peak, needs MixerChanBuffer
BassFlags.MixerNonStop                             do not stall when there are no sources
BassFlags.MixerChanBuffer                          buffer a source so its level can be read
BassFlags.MixerChanPause                           stop processing a source without removing it
```

`MixerNonStop` is load bearing rather than a nicety. Without it a mixer with nothing plugged in
stalls, and a stalled mixer under an ASIO driver is the driver pulling from something that has
stopped producing. The ordinary state of this application is a stopped transport and no pad
playing, so that is the state it would spend most of its life in.

Two rules come with the add-on. A source must be a decoding channel, since the mixer pulls it
with `ChannelGetData`, and a channel can be plugged into one mixer only, which is what splitter
streams exist for and nothing here needs.

## The tracker side

Almost nothing. `TrackerOutput` already makes its stream with `BassFlags.Decode` when the output
is ASIO, and the change is to make it always, then plug it in rather than either playing it or
handing it to the driver. `Feed` and the fallback branch under it go away with the decision they
carried.

## The pad side

This is where the work is, and it is four consequences rather than one edit.

A pad's stream is made with `BassFlags.Decode` added, and playing it becomes
`BassMix.MixerAddChannel` where it is now `Bass.ChannelPlay`. Stopping becomes
`MixerRemoveChannel`. `Bass.ChannelIsActive` no longer says whether a pad is playing, because a
decoding channel is not playing anything; whether a pad is plugged in is what the engine has to
keep, and it keeps a handle per pad already.

The end sync fires from somewhere else. `Bass.ChannelSetSync(handle, SyncFlags.End, ...)` on a
decoding channel behind a mixer needs `SyncFlags.Mixtime`, and it then runs on whichever thread
pulled the data, which under a driver is the ASIO thread. `OnChannelEnd` raises
`PadPlaybackChanged` from there, so what that event reaches has to be looked at rather than
assumed: it is a UI event today raised on a thread that is allowed to be slow.

The fades and the volume are unaffected. `ChannelSlideAttribute` and `ChannelSetAttribute` are
channel level and work on a decoding channel, timed by data decoded rather than by wall clock,
which is the same thing while audio is being asked for.

A pad's effect is unaffected. `Bass.ChannelSetDSP` hangs on the source channel and stays there.

`BassFlags.AutoFree` on the stream pads is the one to check rather than believe: a source that
frees itself while a mixer holds it is the shape of fault that shows up as an occasional crash
on somebody else's machine, and the answer is probably to drop the flag and free it from the end
sync, which is where the engine already learns that a pad has finished.

## The take preview

The same edit as a pad and a smaller one, since `WaveformPlayer` has no fades, no effect and no
loop. `BassFlags.Decode` on the stream, `MixerAddChannel` where `ChannelPlay` is, and
`MixerRemoveChannel` in `Stop`. `ChannelSetPosition` still works, which is what `SeekTo` and the
region start need, and the progress timer reads `ChannelGetPosition`, which is a decoding
channel's own position and is what it was already reading.

Its `IsPlaying` is its own flag rather than `Bass.ChannelIsActive`, so nothing there has to
change. The end of the region is polled rather than synced, so it does not meet the mixtime rule
the pads do.

## Recording, and the honest limit

A DSP on the mixer stream is a tap on everything this application is playing, on every platform,
which is the second thing worth having here. `WasapiLoopback` is Windows only and captures what
an output device is playing, which under ASIO is the silent device, so recording the mix while
using a driver records nothing.

The limit is worth writing down because the first draft got it the wrong way round. A mixer tap
captures what this application plays and cannot capture anything else. WASAPI loopback captures
whatever the device is playing, this application included, which is how somebody records a
browser or another program with no virtual cable. Those are two features, and the tap replaces
loopback for one of them. On Linux the second is a monitor source among the capture devices and
is not affected either way.

So `ILoopbackCapture` gets a second implementation over the mixer rather than being replaced, and
what RECORD offers is one more source in a list it already builds.

## What has to ship

`ManagedBass.Mix` 4.0.2 beside the four packages already referenced, and the native library for
each platform:

```
native/win-x64/bassmix.dll        bassmix24.zip, x64/bassmix.dll
native/linux-x64/libbassmix.so    bassmix24-linux.zip, libs/x86_64/libbassmix.so
native/linux-arm64/libbassmix.so  bassmix24-linux.zip, libs/aarch64/libbassmix.so
```

Both archives answer 200 and hold those paths, so the three rows go into
`.github/scripts/check-natives.sh` in the shape the eight rows there already have.

Three copy targets in `JingleBox2.csproj` name each file one by one and all three have to learn
this one: `CopyBassToOutput`, `CopyBassToLinuxOutput` and `EnsureBassDllInPublish`. The `<None>`
item alone lands the file under `native/` in the output, which is not where a program looks. The
release workflow checks the payload by name as well, beside its `bassasio.dll` line. That is the
lesson `bassasio.dll` cost the first time: adding a native and forgetting the targets is a
publish that carries it in a folder nothing reads, with nothing anywhere saying so.

Unlike `bassasio`, this one is not optional and not Windows only, so it is not guarded by
`Exists(...)` in the way ASIO is: a build without it is a build with no audio path at all.

## Not decided

Whether the mixer is created once for the process or per output. Per output is the simpler life,
since the rate follows the device and every source is remade on a device change anyway.

Whether a pad meter comes off `BassMix.ChannelGetLevel` while it is there for free. It would be
the first level FIRE has ever shown, which makes it a feature rather than part of this.

Whether the pads eventually belong on the song's mixer as a strip. They do not today, and the
argument for a strip is not the argument for this: this is about one output path, and that is
about what a pad is.

The mixer as a window of its own, dockable from the menu, which is a separate piece of work and
should stay separate. There is prior art for it on `mixer-work`: `Views/DetachedWindow.axaml` and
its 76 lines of code behind, with the `MainViewModel` half beside them. That is worth lifting on
its own, where it can be looked at and judged as a window, rather than arriving inside an audio
change again.

## What to measure, since nothing here has been

The current claim is one silence. Everything else in the old draft that read as a measurement
(29 ms against 7.5 ms, hours per task) was written down without anything being run. What is worth
taking, on the Windows machine, before and after:

- the driver's own reported latency, which `AsioDevices.Latency` already reads
- `RenderCost` over the same stretch, which already times each block against its own length
- whether a pad and a tracker note started together arrive together
