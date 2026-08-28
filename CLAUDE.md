# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

JingleBox2 is a cross-platform audio pad launcher built with .NET 9 and Avalonia UI for radio, streaming, and live audio workflows. It features a FIRE tab for performance (triggering audio pads) and a PADS tab for setup, with MIDI controller support.

## Build Commands

```bash
dotnet restore          # Restore packages
dotnet build            # Build project
dotnet run              # Run locally
dotnet publish -c Release -r win-x64    # Publish for Windows
dotnet publish -c Release -r linux-x64  # Publish for Linux
```

## Architecture

### Core Layers

1. **Entry Point**: `Program.cs` → `App.axaml.cs` → `MainWindow.axaml.cs`
2. **ViewModels**: MVVM pattern using CommunityToolkit.Mvvm
3. **Services**: Audio engine (BASS), MIDI service (managed-midi), Config store (JSON)

### Key Directories

- `Audio/` - Audio playback engine using ManagedBass (BASS library wrapper)
- `Audio/Plugins/` - CLAP and VST3 hosting: scanning, loading, parameters, chains, plugin windows
- `Audio/Plugins/Bridge/` - Runs each plugin in a process of its own and talks to it
- `Config/` - Configuration models and JSON persistence to `%APPDATA%/JingleBox2/config.json`
- `Diagnostics/` - The log: one file for the app and every plugin process, off by default
- `Midi/` - MIDI input handling and routing to pads and to the tracker
- `Tracker/` - Song model, sequencing, playback, `.jibx` song files, and the machine rack
- `Tracker/Synth/` - The synth voice: waves, ADSR, modulation, and the preset bank
- `ViewModels/` - MainViewModel (orchestrator), PadViewModel (per-pad), MidiViewModel
- `Views/` - Avalonia user controls (UseView, PadsView, TrackerView, RecordView, SettingsView) plus MidiView, hosted by the MidiMappingWindow dialog
- `Themes/` - XAML resource dictionaries (Dark, Light, Neon, Industrial)
- `native/` - BASS audio library binaries for win-x64, linux-x64, linux-arm64

### Data Flow

- **Playback**: PadViewModel → BassAudioEngine → BASS library → PadPlaybackChanged event → UI update
- **Config**: PadViewModel property change → MainViewModel → ConfigStore.Save() → JSON file
- **MIDI**: MidiService.MessageReceived → MidiDispatcher → (MidiRouter → PadTriggerAdapter → PadViewModel.TogglePlayCommand) or (MidiNoteRouter → TrackerNoteAdapter → TrackerViewModel)
- **Tracker**: TrackerPlayer clock → TrackerSequencer events → sample channels (TrackerSampleBank) or voices and plugins (TrackMixer → SynthOutput → one BASS stream)

### Key Classes

- `BassAudioEngine` (Audio/): Manages pad audio playback, device selection, file/stream sources, dynamic resize
- `Log` (Diagnostics/): What the app writes down about itself, switched on in SETTINGS or with `JB_LOG=1`. Off costs one comparison and does not even build the message. Areas (App, Audio, Plugins, Tracker, Midi) so a log can be read without reading all of it
- `AppFolder` (Config/): Where everything the app keeps lives. Knows nothing, so a plugin's own process can find the same folder without loading the settings
- `PluginHost` (Audio/Plugins/): The one place that knows both plugin standards. Everything above it deals in `PluginInfo` and `IPluginEffect`
- `BridgedPlugin` / `PluginProcess` (Audio/Plugins/Bridge/): A plugin running in another process, wearing the same face as one that is not. Socket for messages, shared memory for audio
- `PluginHostProcess` (Audio/Plugins/Bridge/): This same executable started again with `--plugin-host`, being one plugin and nothing else
- `PluginRunLoop` (Audio/Plugins/): The clock and the doorbell an X11 plugin needs. Both standards ask for the same thing in different words
- `Vst3Messages` (Audio/Plugins/): The envelope a VST3 plugin's two halves post to each other. A host that refuses to supply one crashes plugins that do not check
- `XEmbed` (Audio/Plugins/): The handshake a plugin window from another program waits for before it will draw
- `ConfigStore` (Config/): JSON persistence with profile migration support
- `MidiRouter` (Midi/): Maps MIDI messages to pad triggers with toggle/start modes
- `MidiDispatcher` (Midi/): Sends each message to the pads, the tracker, or both, by the device's role in SETTINGS
- `MidiNoteRouter` (Midi/): Turns keyboard notes into tracker note entry
- `TrackerPlayer` (Tracker/): Owns the clock and routes each event to a sample channel or a synth voice, through the track's mixer strip
- `MixLevels` (Tracker/): What the mix adds up to, mute and solo included
- The mixer has a master, and it is a strip without being a track: `Song.Master` is a `TrackMix`
  because it is the same handful of settings, but it is not in `Song.Mix`, so nothing that walks
  the tracks reaches it by counting and it does not move when they are reordered. It has a
  level, a place and one effect chain the whole song goes through, no ducking because everything
  is already summed by the time it is reached, and no solo because soloing everything is what it
  is already doing. Applied where the fixed `MasterGain` always was, in this order: sum, effect,
  level and pan, then the saturation, which has to stay last or the fader could put the mix back
  outside it. Its meter reads what is leaving, which makes it the one meter on the page measuring
  what you actually hear. It is strip -1 everywhere: `TrackerPlayer.MasterStrip`, the chain
  walks, and `state/mDD.bin` in the song file rather than a number, so nobody can collide with it
  by adding a thirty-third track. A song written before it existed opens at unity with nothing
  across it and sounds exactly as it did
- Its chain is folded away under the mixer, the same `FoldStrip` the pattern's two use, holding
  the same `PluginStrip`, because a chain is a chain and what differs is only which strip it is
  about. Its own `PluginChainViewModel` rather than the one under the pattern, which follows the
  cursor: pointing that at the master would lose it the moment somebody touched an arrow key
- The master's meter is a peak measured off the last buffer, so it is only true while buffers are
  being asked for, and it went on showing the last thing that played after the stream stopped.
  Clearing it where the rendering rests only covers the one path that goes through the mixer, and
  there are several ways for rendering to stop. So the reading is stamped when it is taken and
  says nothing once it is older than `MeterHoldMs`, which is gone whichever way the music
  stopped. A track's meter needs none of that: it is worked out from the voices that are
  sounding, so it falls on its own
- And that only made the reading truthful; it did not make anybody ask for it again. The meters
  are polled by a timer that ran while the transport did, so the last thing the timer did was
  read a level that was true and the first thing it did not do was read it again: the master sat
  lit at whatever had last played, for ever. The same rule meant a note played by hand with the
  transport stopped moved no meter at all, since nothing was reading them. `TrackMixer.Sounding`
  is the rule now, polling while anything is sounding and only then while a pass runs, since a
  pass between two notes is silent and is not over. Auditioning starts the timer and the timer
  stops itself when everything reads nought. The mixer was never wrong here: a preview carrying
  a track already moved that track's meter and the master's, which `Tests/MixerIsolationTests.cs`
  has said for a while. Both faults were in what was asking
- A track's meter read nought until somebody pressed play, and the reason was a bound written
  against the wrong array. `TrackerPlayer.LevelFor` asked whether the track number was inside
  `_noteGain`, which is the volume column's memory and is made when a pass starts, so before one
  there were no tracks to report on at all. Bounded by how many tracks a song can have now. The
  master went through its own branch above that line, which is why a note played by hand moved
  the master's meter and no track's, and why it looked like the tracks were not isolated when
  what was really happening is that nobody was asking them
- A fader was as wide as the number under it. `MeasureOverride` measured the current reading, so
  "-10.0 dB" came out a character wider than "0.0 dB", two of the mixer's strips were wider
  inside than the other two, and the meter beside the wide ones was pushed into the strip's own
  border and drawn through it. `NumericInput.Widest` is the rule: the longest a reading can be
  over the range, since a format widens with magnitude and with the minus sign and both are at
  their worst at an end. The value is still in it, because nothing stops a control being handed
  one from outside its own ends, and it is the longest string rather than the widest, which is
  the same thing in the monospaced font readings are drawn in. `Value` is deliberately not in
  `AffectsMeasure` and now does not need to be. With every strip honest about its width the
  cards had to grow from 120 to 134, which is what the contents always needed
- What a drawn keyboard lights is a monitor of the notes going past, whatever produced them.
  `Midi/MidiMonitor.cs` is that monitor: one for the application, standing in front of the half
  that plays the notes and passing every one on untouched, wired at startup and never taken off.
  `IMidiMonitor` is what a keyboard reads, so a keyboard can be put a question to without a port,
  a window or a hand. Keys, not sound: a key is an event with two halves and a sound is a thing
  with a length, which is the question a kit's pads answer, and a cymbal rings for four seconds
  after the key that started it came up
- It had been the other way round twice, and both were wrong for the same reason. A panel that
  kept a record of the presses it had heard showed nothing for a key on the hardware, since that
  key never touches a panel: it goes to whoever the notes are being played on. Patching that up
  panel by panel then meant a keyboard that listened only while its page was in front, or only
  while the cursor was on its track, and went on being wrong in quieter ways. One monitor and
  every keyboard reading it: a window opened mid-chord shows the chord, and two panels open at
  once agree, because a key is down or it is not
- The three producers reach it differently and that difference is the whole of what the type
  says. A key on the hardware arrives through `INoteTrigger` on its way past to being played. A
  mouse on a drawn key and a letter on the computer keyboard say so through `Pressed` and
  `Released`, because the panel they are on sounds them itself and putting those back into the
  stream would sound everything twice. `IMachineKeys` lost `Down` and `Up` again as a result:
  they were a door for "played somewhere else" and the monitor is that door
- A buffer off a MIDI port is not one message, and reading it as one is what hung keys. `Read`
  took the first message and the rest of the delivery was dropped. The traffic hid it perfectly:
  a hand does not put three fingers down at one instant, so a chord pressed arrives as three
  deliveries a millisecond or two apart and every press was read, while lifting a hand is one
  movement, so the three note offs arrive together and two of the three vanished. From every
  point in the program above it that reads as "the release is never sent". Two keys left lit out
  of a three note chord is the shape of it, and it is exactly what was reported. `Read` says how
  many bytes it took now, and the port's delivery is walked to its end. `Tests/MidiDeliveryTests.cs`
  is that walk: a chord released in one delivery, in running status and out, a press and its
  release together, a clock byte threaded between two messages, a message cut short, and a
  system exclusive message with a note behind it in the same breath
- The log had nothing to say about it, and that was the second fault. The two routers that read
  buttons and ignore notes wrote a line per note; the router that plays notes wrote nothing, so
  a log taken while a key hung showed every press and had no way of showing that the release
  never arrived. `MidiNoteRouter` says both halves now, and `TrackerNoteAdapter` says which half
  of the application each went to and whether the release went where the press did
- A key press is one thing with two halves, and the half that hears the second has to be the
  one that heard the first. `TrackerNoteAdapter` asked which half was in front twice, once per
  half of the press, and the answer can differ the second time: leave the rack, open a song or
  close one with a finger still on a key and the release went to the pattern while the rack was
  still holding the note and drawing its key lit, with nothing left to tell either of them the
  hand had gone. It remembers where it sent each key now, per note, and sends the release after
  it; a release nobody remembers still goes to the half in front, since that is what a device
  already holding a note when the program starts sends. The adapter takes `IPlaysNotes` rather
  than two view models, which is the whole reason the awkward cases can be put a question to
  without a window
- The path a key takes is tested end to end in three pieces, and each piece is somewhere a
  stuck light could have come from. `Tests/NotePathTests.cs` plays raw bytes through
  `MidiService.Read` and the router into an `INoteTrigger`: both spellings of a release, running
  status, a chord, and aftertouch, which shares a note's shape and must not be read as a key
  coming up. `Tests/NoteAdapterTests.cs` is the half-choosing above. `Tests/MachineKeysTests.cs`
  presses keys on a real `DesignerKeys` through `IMachineKeys` and reads what is lit. That last
  one found a fault the moment it existed: `Play` no longer refused a key that was already down,
  so a letter held on the computer keyboard retriggered the machine on every repeat
- `IMachineKeys.Down` and `Up` are that light on its own, for a note that was played elsewhere:
  a key on the hardware has already been sounded by whoever the notes are going to, and playing
  it again there would sound everything twice. `TrackerNoteAdapter` also stopped dropping the
  release for the rack: a note-off has nothing to be written into there, which was the reason,
  but it is also the moment a light goes out and a sound is let go, and dropped, the two halves
  of one key press went to different places. A one-shot is safe, because `LetAudition` already
  refuses to follow a key on one
- Nothing is added from code to a machine's face. Where there is a design, the design is the
  panel: what it does not draw, nobody draws. Three things were being filled in behind the
  machine's back, all with the same well meant reasoning, that a panel missing an obvious control
  should be given one. The keyboard at the foot, a preset picker in the header, and the line
  saying which recording is playing. A machine that had never asked for any of them grew all
  three, and the only way to be rid of one was to draw it yourself so the page's would stand
  down. `IsDescribed` is the whole test now, in the three places that used to ask a narrower
  question. A plugin is not a machine and is not edited in the designer: the panel there is this
  program's own drawing, so its keyboard, its picker and its source line are parts of that panel
  rather than additions to somebody's
- The keyboard at the foot of a panel is gone from every machine, and it was not a keyboard
  problem: the page was adding a part to somebody's design. A machine's face is what the machine
  says it is, keyboard included, and the old rule added one wherever the description did not, so
  a machine that had never asked for a keyboard grew one and the only way to be rid of it was to
  draw a second keyboard so the first would stand down. `ShowsSharedKeys` asks `IsDescribed` now
  rather than looking for a `Keys` element. The one exception is the panel this program draws
  itself, which is what a plugin instrument gets: a plugin is not edited in the designer, so
  there is no design there to decide anything and the keyboard is part of the panel rather than
  an addition to one. Nothing was added to any machine on the way past: a machine that wants a
  keyboard gets a Keys part dropped on it in the designer, by hand, like every other part
- The instrument window hears the same two halves through `MidiKeyDown` and `MidiKeyUp` on the
  tracker, which are beside `NotePlayed` rather than folded into it because they answer different
  questions: what is sounding is what a kit's pads show and what a playhead runs on, and where a
  hand is is what a keyboard shows. A note goes on sounding long after its key came up. Only the
  hardware raises them, deliberately: the drawn keyboard hears its own presses, and a letter
  typed into the pattern has no key coming up at all, so a light lit from one would stay lit for
  the rest of the session. The up carries no track, since the cursor can move between the press
  and the release and a light filtered by where the release landed would never go out
- The peak mark on a meter would not come down. The fall is in `MeterScale.DecayPeak` and always
  was, held for a moment and then twenty decibels a second, but it is worked out while the meter
  draws and a meter draws when a value changes. So the bar emptied when the last level arrived
  and the mark hung where the loudest moment had left it, for the rest of the session.
  `LevelMeter` asks for another frame while the mark is above the bar, through
  `TopLevel.RequestAnimationFrame` rather than a timer of its own, so it runs at the window's
  own rate and costs nothing once the mark is on the floor. It reaches the floor exactly, since
  `MeterScale.Decibels` clamps there, which is what ends the asking
- The master is automated the same way a track is, and can be, because a lane names a strip
  rather than a track: `ControlTargets` answers for strip minus one, `AutomationLane.For` allows
  it, and the file reads it back. Its own panel on the mixer rather than the one under the
  pattern, which follows the cursor, and the master is not somewhere a cursor can be. A master
  lane stays when tracks are removed and does not shift when they are moved, since no count of
  tracks reaches it
- `AutomationStrip` shows whichever `AutomationViewModel` it is given rather than reaching
  through to the tracker for one, which is what lets the same strip serve a track and the
  master. It used to take the pattern and the history off the tracker itself, so it could only
  ever be the pattern's; the panel answers for those now, and the strip knows only what it shows
- A knob pointed at the master's own fader is `ControlScope.Fixed` on strip minus one, not the
  tracks' `Focused`. There is only ever one master, so a knob pointed at its fader means that
  fader wherever you are; given the tracks' template it would have driven whichever track was
  selected, which is a knob doing something other than what you pointed it at
- `TrackMixer` (Tracker/Synth/): The song's tracks, summed. A bus, a level, a pan, an insert
  chain, a ducker and an instrument apiece, and one voice per track the tracker way: a new note
  cuts the one still ringing. Auditions carry no track at all and pile up, which is why a
  panel's keyboard cannot be heard on a strip or turned down by one. It was called `SynthMixer`,
  which was true when it summed synth voices and nothing else; it grew all of the above and went
  on wearing the old name, which said the wrong thing about the one class the whole mix goes
  through. `Tests/MixerIsolationTests.cs` plays a note on one track and asks what every other
  track is sounding, because everything in here is indexed by track number and indexed things go
  wrong quietly
- `MachineRack` (Tracker/): What you have, in `%APPDATA%/JingleBox2/instruments/`, one file
  each. The machines, one apiece under their own names, and the plugins you have added. Nothing
  else: anything that is neither is moved to `instruments/retired/` on the next open, since
  there is no longer any way to make one. A machine cannot be renamed, deleted or duplicated; a
  plugin can be deleted but takes its name from the VST3 or CLAP
- **Machines and instruments are not the same word.** A machine is a fixture on the rack: one of
  each, fixed name, always there. An instrument is what a machine becomes inside a song: your
  name, your settings, its own id, stored with the song, and two of them can come off one
  machine. `TrackerInstrument` is the data type for both, but the rack's types say machine
  (`MachineRack`, `MachineRackViewModel`, `RackMachine`, `MachinesView`) and the tracker's say
  instrument (`Song.Instruments`, `InstrumentSlot`, `AddInstrumentCommand`)
- `SampleSlicer` (Tracker/): Where a recording gets cut into pieces. Finds the attacks off the
  peak data by beating a decaying peak-hold, walks each cut back to where its sound began, and
  falls back to an even division when there is nothing to find. Knows nothing about what takes
  the pieces
- `Slices` / `KeyRegions` (Tracker/): The parts a kit and a map do identically. Reading the cuts
  back off the pieces, and sharing a stretch of keyboard out among them
- `SliceEditorViewModel` / `SliceEditor` (ViewModels/, Views/): One take, its picture and its
  boundaries, used by both machines that hold pieces
- A knob was measured the way it is not drawn, and the difference came out as a hole. `Knob`
  puts the name above the dial or below it, and its `MeasureOverride` only ever described the
  first: name room, then dial, then value. With the name underneath, which is every knob written
  in XAML rather than described by a machine, the room for a name at the top was reserved and
  never used, and the control ended up half a line taller than what it drew, with the slack
  under the value. It showed as a gap under the mixer's pan and as a squeeze on its ducking
  knobs. Measured per case now. The tick ring cost two more of the same kind: its reach was left
  below the dial and not above, and not at either side at all, so a name-below knob drew its top
  marks into whatever was over it and two side by side had their rings almost touching however
  much spacing the panel between them asked for. The marks are drawn outwards from the rim in
  every direction, so the room is left in every direction. Machine panels set `LabelAbove` and
  were unaffected by the height; the width moves every knob everywhere by nine pixels a side,
  which their grids absorb
- `Knob` / `Fader` / `NumberField` (Machines.Ui/): The app's own value controls; a pot knob, a vertical fader, and a compact stepper field. They live in the machine UI assembly because a machine bought from somebody else is built out of the same controls the app's own machines are
- `WaveformView` (Machines.Ui/): A recording's shape, with the window and the loop draggable on the picture
- `MachinePanelView` (Machines.Ui/): A machine's face, built from what the machine says it looks like. Designing, every element can be picked and none can be turned; off, it is an ordinary panel
- `MachinePartSample` (Machines.Ui/): One entry of the designer's library, drawn as the real control it adds
- `ThemePalette` (Machines.Ui/): Theme colours for custom-drawn controls, read as `Color.*` keys so a theme swap lands at once
- `MainViewModel`: Central orchestrator connecting audio, config, and MIDI subsystems
- `PadViewModel`: Single pad state (name, source, volume, playback state)

## Tests

```bash
dotnet test Tests/JingleBox2.Tests.csproj
```

330 of them, in about two seconds, with no window and no hardware. They run in CI on every push
and every pull request, on Linux **and** Windows, because two of them are genuinely platform
specific: a path is written with a separator that is not the same character on the two systems,
and those are exactly the tests that would pass on one machine for a year and fail on somebody
else's. The release workflow runs them first and every job that makes an artefact waits on them,
because a release is the one build nobody gets to take back. xunit, and they run one at a
time on purpose: several of them read and write the application folder, which
`Tests/Sandbox.cs` points at a temporary one before anything runs, and an environment variable
belongs to a process rather than to a test.

The suite exists because the code was already built for it. Five classes say in their own remarks
that they were kept free of Avalonia and of ports so they could be tested, and `MidiService.Read`
is public for exactly that. What was missing was somewhere to put the tests.

`Tests/` is inside the application's folder, so `JingleBox2.csproj` has to remove it from its own
globs the way it already does for the machine assemblies. Without that the app compiles the test
sources into itself and every generated assembly attribute is defined twice.

What is covered, and it is the parts that can be got wrong quietly: the wire (running status,
pitch bend, one-byte statuses), what kind of control is sending, pickup and endless knobs and
parking, device roles, controller profiles and codecs, shortcuts, all four histories, patterns
and their edits, a song being written down and poured back, the mix, envelopes, portable paths,
the screen's bytes, the transport's two dialects, and the Lua fence. Several of those tests exist
because that exact thing was wrong once.

## Technical Notes

- Configurable pad matrix size (rows x columns) via SETTINGS tab
  - Minimum: 4 pads total (e.g., 2x2, 1x4, 4x1)
  - Maximum: 16 pads total (e.g., 4x4, 2x8, 8x2)
  - Default: 4 rows x 2 columns = 8 pads (backward compatible)
- Two source types per pad: a recording off the shelf (picked from RECORD's takes, so the
  app owns every file a pad depends on) or an HTTP stream. A pad still plays a path
  underneath; what changed is where the path can come from
- Tracker instruments come in two kinds: a recording pitched by resampling, or a synth patch
  generated at playback time (the parameter set mirrors MappoGraph's chiptune synth, plus a
  drive control). The
  tracker only ever loads instruments; whether one is a sample or a synth is its own business
- Both Zampler and BongaBong can be filled from one recording rather than many: chopping. A
  chopped instrument is one whose pieces all point at the same file with different windows, so
  it needs no new storage and the take is decoded once for all of them. The cuts are not stored
  separately; they are read back off the pieces. Put a different sample on one piece and it
  stops being a chop, which is why `IsSliced` is asked rather than the `Sliced` flag
- A song is a `.jibx`, which is a zip: `song.json` for the patterns, the order, the mix and the
  instruments, `state/NN.bin` for each plugin instrument's patch and `state/tNN-DD.bin` for each
  effect on each track's chain, as the plugin handed it over. The patches came out of the
  document because they are almost all of it (one song here is 348 KB of which the music is 781
  bytes and one synth's patch is 331 KB) and because a document is all or nothing: a patch that
  came back damaged used to cost the song. Recordings are named `{app}/...` when they live in
  the application folder, so a song survives that folder moving or being on another machine.
  Songs written before this are converted on the way in and the original is kept as `.json.old`
- A preset is not a set of knob positions, and for a long time only instruments knew that. A
  plugin on a track's chain was written down as its parameters and nothing else, so Serum on a
  track came back sounding roughly right and calling itself "- Init -": the knobs were saved and
  the patch, which is the wavetables, the FX rack and the preset's own name, was not. `SaveState`
  and `LoadState` are on `IPluginParameters` now rather than on `IPluginInstrument`, because
  wanting a patch back has nothing to do with what a plugin is being used as, and both classes
  that host plugins implemented both interfaces already, so it moved no code. `PluginChainState`
  reads the lump as well as the knobs, and puts the lump back first: a patch moves every
  parameter at once, so the values after it are either agreement or the correction for a plugin
  whose state did not come back whole
- Reading a patch is a round trip to another process and a third of a megabyte, so it is asked
  for where a save is a save. `Capture(chain, patches)` is off by default and `MatchChains`
  leaves it off, and `Same` compares two chains as `Described()`, which is the chain without its
  patches: a plugin asked for its lump twice is under no obligation to answer the same bytes,
  and comparing them would report every chain as changed and rebuild all of them on every undo.
  A pad is the other rate problem, since it is written down on every property it has and a level
  dragged is a hundred of those: the pad reads its patches when its chain settles, which is the
  same 600ms tick that makes it save at all, and each save carries what was read
- CLAP effects had no state at all, because `clap.state` was never implemented: only the
  parameters had ever been asked for, and nothing noticed since VST3 is the only format that can
  be an instrument here. The extension is two calls over host-owned streams, which are structs
  on the stack with static functions in them and a `MemoryStream` on the thread; nought back from
  a read is the end of the lump rather than a failure, which is how a plugin knows to stop asking
- The cursor stays on the middle of the screen and the pattern runs under it, which is what
  every tracker does and what makes the line you are working on somewhere your eye can rest
  rather than a highlight to follow down the page. Always, with no exceptions: line 00 of a
  song's first pattern is on the middle exactly as any other row is, and what is above it there
  is blank. `PatternMetrics.TopPad` and `BottomPad` are half a screen each, whether or not there
  is anything to draw in them, so `RowY`, `LineAt` and `ContentHeight` all shift together and a
  click still lands where it looks. `ViewportScroller.CentreRow` is the offset. `HalfView` is
  set on the grid by `TrackerView`, since the grid is measured inside the scroll viewer with no
  height limit and never learns how tall the hole it is seen through is
- The space is the rule and a neighbouring pattern is only what fills it, drawn at 40% opacity
  so a note that is really there still reads as one. Only a pattern that is really coming: in
  song mode the neighbouring slot, by its place in the order rather than by the pattern, since
  the same pattern can be in a song twice and what follows it is a different answer each time;
  nothing at the two ends, because a song does not wrap; and nothing at all in pattern mode,
  where the only thing coming is this pattern again. Getting this wrong twice is what it took:
  first the space was left out at the ends, which moved the cursor off the middle, and then the
  pattern was ghosted against itself in pattern mode. Renoise leaves the room and leaves it
  empty, which is visible in its own screenshots by the cursor sitting at the same height on
  every one of them. Faded as a whole rather than by choosing paler colours, because picking the
  muted colour for every cell made a neighbouring pattern look exactly like an empty one
- `Views/DragGhost.cs` is the picture of whatever is in the hand, on a canvas laid over a page
  that takes no clicks. Both places here that drag things want one and neither can have the
  toolkit's: the machine designer follows the pointer itself and so was never offered one, and
  the tracker uses the toolkit's own drag and drop, which draws nothing at all on X11. Told what
  to draw rather than what is being carried, since a part of a machine and an instrument going
  onto a track have nothing in common except that somebody is holding one. The picture is the
  same as the thing that was picked up, which is what makes it read as the thing rather than as
  a label about it: an instrument keeps its machine's colour down the side and the sentence under
  its name, exactly as its row has them
- The tracker puts the ghost down in the `finally` of the await on `DoDragDropAsync` rather than
  on the drop, because a drag is just as often abandoned: let go over the order list or off the
  window and no drop is ever raised. It is drawn once and then only moved, and not taken away
  when the pointer leaves the grid for the header, or crossing the line between them would blink
  it. Followed in the page's own coordinates, since that is where the layer is
- Where it cannot land it turns red and gets more solid rather than fading, and the whole page
  takes a drop so that it keeps following the hand at all. The first version drew it only over
  the grid and the header, so leaving them made the picture vanish at the same moment the pointer
  became a barred circle, and two things changing at once read as the drag having failed rather
  than as the place being the wrong one. `OnPageDragOver` is reached only when neither of those
  two has already marked the event handled, which is exactly the places where letting go would do
  nothing. `DragGhost.Paint` clears the border and background rather than setting them to null,
  because the card is a style and a local null is a value like any other: it would win, and the
  picture would lose the background the theme gave it. The refused look is that one method if it
  wants changing
- The designer answers the same question the same way, and it already had the answer: its release
  refuses a drop that landed on neither the machine's picture nor the list of what is on it, and
  that test is now `Takes`, asked while the hand is still moving so the picture can say it rather
  than leaving it to be found out by letting go. One method for both, because two spellings of it
  would eventually disagree and the way that fails is a ghost promising a drop the release refuses
- A song can also be packed, which is Pack in the TRACKER bar: the same `.jibx` with the
  recordings inside it, written where you choose and never to the songs folder. Saving does not
  do this, because a song built on a long take is tens of megabytes and the open song is written
  out every twenty seconds. What travels is decided per recording by where it came from: a
  machine's presets ship with the program and are named, your own takes are carried. Reason's
  rule, and the same reason for it. Opening a packed song puts what it carried on the shelf
  through `RecordingImport` and repoints the instruments, skipping anything already there byte
  for byte, so opening one twice adds nothing
- RECORD asks the songs as well as the rack before deleting a take (`SampleUsers` over
  `MachineRack` and `SongStore`). A song owns its instruments, so a recording nothing on the
  rack plays can still be the sound of three songs, and deleting it used to empty them with
  nothing said. Only `song.json` is read for this, and the answer is cached per song by its
  write time: the shelf asks once per take, so the uncached version opened every song file
  once per recording
- A hardware knob is pointed at a software one by resting the pointer on it in the other mouse
  mode (Ctrl+Shift+M) and touching the control on the desk. The mapping names the machine and
  the parameter key, never a track or an instrument id, so it is Zampler's cutoff on every track
  and in every song; which track is a separate question answered by `ControlScope`. Only things
  that name a `MachineParameter` can be pointed at, and buttons separately as actions; a label
  or a take picker cannot, and does not glow. `MidiControlRouter` works out from three messages
  whether a control is a button, a knob or an encoder, since a CC says nothing about what sent
  it, and parks a control against an end until the stream turns round
- Pointing is a thing any control can do now, not a thing a machine panel does. `Views/Pointable.cs`
  is an attached property: hang a `ControlMapping` on a control with `Pointable.Offers` and
  resting the pointer on it offers that mapping, wherever the control lives. The panel keeps its
  own, rightly, since it is drawn rather than built and only it knows what element is under the
  pointer; everything else was copying the same handful of lines. What is hung is a template and
  it is copied before it is offered, because `ControlLink.Handle` fills the controller's half
  into the object it was given and then keeps it: one shared instance would have every link
  overwriting the last. Tunnel and bubble both, since a knob takes the move to be dragged
- Ctrl+Shift+M did nothing on the mixer because `LinkKey` counted machine panels rather than
  things that can be pointed at. A pointable control joins the same tally when it comes on
  screen and leaves it when it goes, so the mixer counts and so will anything else that opts in.
  The gesture is a plain toggle and works in the three places there is something to point at:
  a machine's preview, an instrument's dialog, and now the mixer
- The gesture used to be guarded by a flag saying the key was down, cleared by the key coming
  up, and a key can come up somewhere else: focus moves while it is held, the release goes to
  whatever took it, and the flag stays set for ever. Every press after that was swallowed and
  the mode stuck in whatever state it was left. It is a clock now, which cannot be stranded, and
  `LinkKey.Answers` is the rule on its own so it can be put a question to without a keyboard
- What can be pointed at on a machine's face is decided by two tests and not by what the control
  looks like: a button offers an action, and anything else offers a parameter if it names one
  that has a value. So `Knob`, `Fader`, `Switch`, `Number` and `Choice` are all linkable and
  always were, and `Label`, `Take`, `Meter`, `Image` and the rest are not, because they name a
  thing rather than a value and a link pointed at one would reach nothing
- A shelf of presets is a list and not a parameter, so pointing a knob at it and asking for
  "preset 0.62" means nothing. The picker offers two actions instead, the one before and the one
  after, and which of them depends on which side of it the pointer is on, because that is where
  its own two arrows are. `PresetStep` is the pair of decisions on their own so both can be
  asked without a window: which side, and where a step lands. It stops at the ends rather than
  coming round, since a button held down that wrapped would carry you past the one you were
  looking for
- `Midi/MixLinks.cs` is what a mixer strip offers: Level, Pan, Mute, Solo, Duck and the ducking
  release, which was missing until the strip was gone over control by control: every other value
  had a name for a link to use and that one had none. Not the Duck from picker, which names a
  track rather than a value, for the same reason a take picker cannot be pointed at. A mixer link
  is the song's rather than the desk's, because a track only exists in a song. Every one is
  `ControlScope.Focused`, so one knob pointed at Level is the level of whichever strip you last
  touched rather than a link per track. Touching a strip anywhere picks its track, tunnelled so
  grabbing a fader picks the strip on the way past rather than instead of moving it, and it goes
  through the pattern cursor rather than a second answer beside it: the mixer, the pattern, the
  chain and the automation then agree without being told about each other
- What a track plays is the track's own instrument and never the one picked out in the list
  beside the pattern. Those are two questions and the tracker answered the first with the second
  whenever a track had none of its own: the keyboard sounded an instrument the track had not
  got, playing or stopped, typing wrote that instrument's number into a cell on it, and the
  status bar named it. A track with nothing on it makes no sound and a note typed into it goes
  in with the instrument column blank, which the sequencer already reads as "whatever this track
  last played", and that is nothing either. Which one is picked in the list is about the list:
  what a new track would be given, and what the rack is showing
- What "the track you are on" means is the instrument window in front when there is one, and
  the pattern cursor otherwise. Two panels open in their own windows and the cursor is on
  neither of them, so a knob would drive whichever track the pattern last happened to be on.
  Nothing is applied when a window comes to the front: the mappings are walked per message, so
  the next thing you touch simply resolves against a different track
- One knob does one job, but a job can be spelled out once per machine: two links on one CC
  naming two machines are kept, because a link only answers while the track plays its machine
  and at most one can ever match. That makes an encoder "the filter, on whatever machine I am
  looking at". Pointing the same knob at another parameter of a machine it already has still
  replaces, since both of those would fire
- A link records the controller it was learned on, because a CC number means nothing on its
  own: two devices both have a CC 22. A link is displaced by exactly two things, the same
  physical control being pointed somewhere else, or something else being pointed at the same
  target; a controller that is simply not plugged in keeps its links untouched, since leaving
  one in the other room is not a decision to unwire it
- Linking lives in two layers, and which one a link lands in is decided by where you pointed.
  A machine on the rack is the machine: a knob pointed at its filter there is true of every song
  you will ever open, so it goes on the desk, which is kept in the settings and listed in
  SETTINGS. An instrument on a track is this song's, and so is anything pointed at it, plugins
  on a track's chain included: that goes in the song's own `.jibx` and is listed in the tracker
  under MIDI CC. The song's wins where both name the same control. One list per layer, because
  one list showing both with a word beside each row reads as a leak whatever the word says.
  Moving a link between the layers was built and taken out again: what anybody actually wants
  is a new song that starts with a layout already on it, which is a song template
- Automation is lanes rather than more effect commands, one per parameter per track per pattern,
  values normalised 0 to 1 and converted through `IControlTarget`, which is the same interface
  remote control writes through: the clock arriving at line 32 and a knob writing from CC 74 are
  one act, so `AutomationPlayer` reaches a parameter exactly as `MidiControlRouter` does and
  everything that made a link resolve makes a lane resolve. The shape is Renoise's, read off its
  own schema rather than inferred (`PatternTrack/Automations/Envelopes/Envelope`, each a device,
  a parameter and an envelope). Renoise keeps two ways of moving a parameter, effect commands and
  envelopes, and ships an icon warning you when a parameter has both: that is a conflict
  indicator rather than a design, and it is not copied. One storage here, and the two ways of
  editing it are views. `docs/automation.md` is what is built and what is next
- A point's time is a double although nothing produces a fraction. Renoise quantises to 256
  units per line and says what that unit is, "a time of 1.5 means line 1 with a note column
  delay of 128", so sub-line automation and a delay column are one grid and this codebase has
  neither. The file writes what it is given and reads back what it finds, so the day one appears
  the format does not have to move
- Recording a lane cost hours because everything under it was already there. The instant is read
  on the MIDI thread and only the write is handed to the drawing thread: posted whole, a fast
  hand would pile several values onto whichever line the drawing thread woke on. A pass leaves
  one undo step per lane and not one per point, which is the same rule the instrument knobs use
  and arrived at from the same direction. Lanes are part of a pattern's undo step, because left
  out, undo would put the notes back and leave the movement where it was
- The automation handle under the chain folds open a strip built to the chain's own shape: a
  block at the head saying which part you are working on, and the room after it given to that
  part. There the head is the instrument and what follows is its effects; here it is the
  parameter and its lane, and the room to the right is where the curve goes. Under the pattern
  because a lane is written against the pattern's lines, about the track the cursor is in, and
  following it through `FollowCursorTrack`, which is where the chain already followed. Folded
  away by default and one line tall either way, since every pixel under the pattern is a line of
  music nobody can see. `Views/FoldStrip.cs` is that line and nothing else: a `ContentControl`
  with a title and an open flag, holding whatever it is given, so the chain and the automation
  fold identically rather than in two spellings that would eventually disagree. The chain starts
  open because a track always has one, the automation shut because a track usually has none. Each
  carries its own grip along its top edge, a `Thumb` setting that strip's height, and that is why
  it is a control rather than two grid rows with a `GridSplitter` between them: a splitter shares
  one length between the rows it lies between, so the automation's handle took its room off the
  chain and moving one moved the other. A strip that owns its height answers only for itself, and
  the pattern, measured in what is left, gives up or takes back the difference. The grip is a
  short bar rather than a hairline, since along the bottom of a card a hairline is what that
  card's own edge looks like. The strip does not say which track it is about: the tab, the status
  line and the pattern all do already, which is why the chain's own
  badge came off. It was in two wrong places first, a page of its own and then a button per
  mixer strip. A page is somewhere you go instead of the music; the mixer is where a track's
  settings live but not where its lines are
- `IControlTargets.On` answers a `ControlChoice`, the device and the parameter's own name beside
  the mapping: the machine's parameters in panel order, then each insert's, then the strip. Named
  there rather than asked for later, because a target's name is written for a status line and
  ends in the track it is on, and forty rows ending in the same three words is a list nobody can
  scan. Adding a lane gives it one point holding where the parameter stands, since an empty lane
  would list as automated and not move
- `Views/AutomationCurve.cs` is the picture, in the room to the right of the head block, and an
  ordinary Avalonia control drawn in `Render` like the pattern grid and the knobs: the pattern's
  lines across with the beats picked out, nought to one up, click to add a point or take hold of
  one, drag to move it, right click to take it away. Time runs left to right although the
  pattern runs downwards, which is what Renoise does and for the reason that a shape a hand
  recognises rises and falls left to right. Time snaps to lines since there is no finer grid; a
  point dragged onto an occupied line keeps its old time and moves only its value, because a
  lane holds one per time and a drag that ate its neighbours would destroy work on the way past.
  One gesture is one undo step. The shape rests on the parameter's own nought, worked out from
  the target's range: the floor for a level, the middle for a pan or a pitch, since a pan drawn
  as a level reads as hard left the whole way with a bump in it. Not on it yet: a range
  selection, and the handle that bends a segment, which is what `LINES` mode's scaling is for
- The typed view is not built: a parameter column in the pattern, which shares its whole
  foundation with note columns. The split is by the nature of the data, deliberate changes typed
  and recorded gestures drawn, because no column can display a hundred values a second
- Polyphony is not built, and it is two features sharing a word. `docs/polyphony.md` is
  the plan. A new note action (what happens to the voice a new note lands on) is a setting and
  two methods `SynthVoice` already has, `Cut` being a 4ms fade and `NoteOff` the patch's own
  release, so it costs a day and the only real work in it is per-note offs for plugins, which
  `PluginNoteOn` does not do today. Note columns, a track playing chords, are a week and all of
  it is the pattern and its editor: the audio side has nothing to learn, since auditions are
  already polyphonic through the same mixer and a track already renders on its own bus. The
  numbers came off the Renoise 3.5.4 install rather than from memory
- A controller with a screen is written to: `ArturiaDisplay` puts the parameter's name, its
  reading and a value bar on a MiniLab 3 while a knob is turned. Arturia's own system exclusive,
  on the device's main port, and only while it is in a DAW mode. `MidiService.Send` opens an
  output on demand and answers false for a device with none, so nothing has to know which
  controllers have screens
- `docs/hardware-integration.md` is mostly a plan, and the rule in it governs everything here:
  plain MIDI is the floor, not a fallback. A controller nobody has written anything about works
  today, taught by hovering a knob and touching the hardware, so a profile may add names, shape
  and shortcuts and may never add capability. Three rungs: nothing, which is built; a file
  describing a controller the way `machine.json` describes a machine, so a link names `encoder3`
  rather than CC 89; and the vendor's own protocol, which is the screen and the lights. There is
  no handshake to sort them out. A device is known by its port name, since the universal identity
  request went unanswered by the one device here. Not built either: a default layout, which is
  what makes a device useful the moment it is plugged in. The encoders take the machine's
  parameters in panel order, so the third encoder is the third knob on whatever face is in front
  of you, and no profile is needed for it. The MiniLab 3's own manuals are in `docs/`, and two
  things in them are operational rather than background: its four ports each have a stated job,
  and Arturia asks a host to use either the DAW program or the MCU port and never both, which
  SETTINGS currently offers as two independent tick boxes
- A controller can have two files in `controllers/`, named after it, and needs neither. A
  `.json` saying what it *is* and a `.lua` saying what it *does*. The split is the design: what a
  MiniLab 3 is, is a fact about every MiniLab 3 and belongs in a file anybody can read; what one
  does is behaviour and needs a language. `Controllers/ControllerProfiles.cs` matches a profile
  to a port by pattern, names a control (`Encoder 3` rather than `CC 89 ch 1`) in both link
  lists, and says what each of a device's ports is for in SETTINGS, which is the answer to why a
  MiniLab shows up four times. It also works out which of the device's programs is running, from
  the numbers arriving: a MiniLab has seven and switching rearranges everything it sends, with
  nothing announced, but the programs do not overlap so one message is usually enough. That is
  not a workaround for a missing question. Arturia's own settings protocol was read with
  `sysex-controls` on 2026-08-27 and its Selected Preset Name field is **write-only**: the
  device will accept a name for the current preset and will not say which one is loaded. Even
  the vendor cannot ask, so inferring it from the numbers is the only method there is. A number
  in two programs says nothing and is ignored; a name from the wrong program would be worse than
  a number, since a number is merely unhelpful. Nothing requires a profile: without one a control
  is called by its number, which is what it always was
- The message path is measured, not guessed at. One MIDI message cost 2200 bytes and 3.2us and
  now costs 200 and 1.2; a control nothing is pointed at cost 1776 and now costs 88. Three things
  did it. `ControlLink.Mappings` merged the song's links and the desk's into a new list on every
  message and is now kept until one of them moves, invalidated in `Say`, which every change ends
  at. `ControllerProfiles` works out what a number implies about a device's program, and what a
  control's pickup should be, once per control rather than per message. And `Log.On(area)` exists
  so the two or three places that write a line per message can ask before building the closure:
  the guard inside `Log.Write` is checked after the caller has already allocated it. Everywhere
  else still writes without asking, because a line written when something is decided costs
  nothing worth counting
- `LogArea.Machines` is the sixth area, and everything under `Tracker/Machines/` writes to it
  rather than to the app's. It is a whole half of this program and it says almost nothing while
  nothing is wrong; the day a machine draws an empty panel or comes back from a zip missing a
  picture, the last thing anybody wants is to read that out of everything the application did at
  startup. The tick box in SETTINGS appeared on its own, since that page is built from
  `Log.Everywhere`
- The log switch is per area everywhere now. Ten places gated on `Log.IsOn`, which is any area at
  all, and then wrote to one: two of them on the audio thread, so switching MIDI logging on made
  the mixer do census work per block that nothing would ever print
- A panel hears about a value it did not write. `IMachineValues.Said` is raised alongside the
  owner's `Changed` callback, and `MachinePanelView` subscribes to it and reads itself again,
  coalesced to once a frame. Before this the only thing that made a panel redraw was the host
  bumping `Reread`, which happens when a kit or a zone is picked and never when a controller
  moves a knob: the number changed, the sound changed, and the picture sat there until something
  unrelated happened. Two names because there is one owner and any number of onlookers, and the
  owner's is set in an object initialiser, which an event cannot be
- And a control target reads where a parameter is *going*, not where it is. Writes are coalesced
  onto the drawing thread, so between a message arriving and the panel being drawn the machine
  still holds the old value. For a knob reporting a position that costs nothing, since the new
  value comes from the message. For one reporting movement it costs almost everything: twenty
  notches arrive in the time the drawing thread takes to wake once, each adds a notch to the same
  stale number, only the last write survives the coalescing, and the parameter moves one notch.
  Measured: identical movement whether you turned it once or forty times
- The machine designer has undo too, and by a different mechanism, because the document is a
  different shape. `ViewModels/DesignHistory.cs` keeps a step as the machine's own JSON: that
  reader and writer already exist and are trusted with people's work, so a step cannot disagree
  with what a save would produce, and 14 KB a machine means a hundred steps is under two
  megabytes. Put back **in place** rather than as a new instance, since panels and the rack hold
  the project they were opened on. The fields are found rather than listed, by walking the
  project's serialisable properties, so a field added later comes back without anybody naming it.
  The door is `MachineEditorViewModel.Redraw`, which every edit ends at: told more often than
  there are edits, and a redraw where nothing moved leaves no step
- An instrument's knobs have undo too, and that one had to turn a stream back into a gesture: a
  knob dragged across its range is one thing a person did and forty messages, and a controller
  sends a hundred a second. `ViewModels/InstrumentHistory.cs` gathers by the same control within
  half a second, deliberately not by "while the mouse is down", which is true of a mouse and
  false of a controller and of automation. A step is the instrument as its file holds it, minus
  `PluginState`, which a described panel cannot move anyway. Restoring pours into nested objects
  rather than replacing them, for the third time in this codebase and the same reason each time:
  the patch, the kit and the shape are held by reference by the panel's own view models
- SETTINGS aside, the tracker's song bar has Cancel changes: read the song back off disc as it
  was last saved, asked first, dead unless there is both a saved copy and something to lose. And
  the Save button glows warm instead of wearing a star, because a star is a character somebody
  has to know the meaning of and it moves the button's width as it comes and goes. `Color.Unsaved`
  is per theme
- Setting `currentPattern` rather than `CurrentPattern` in the tracker's constructor meant the
  song the application starts on never subscribed to its own pattern's changes: typing a note
  into it left the song looking saved. Every song opened afterwards went through the property and
  was fine, which is why it survived. Worth remembering as a shape: a backing field assignment
  skips exactly the part that was worth having
- A track's inserts are undoable, and they are the one edit that lives in two places: the song's
  description of the chain, which a song step carries, and the plugins actually loaded, each in a
  process of its own. Restoring the description alone leaves the picture and the sound
  disagreeing. `TrackerPlayer.MatchChains` makes them agree, and only for tracks where they
  differ, because rebuilding a chain is seconds a plugin: almost every undo changes no chain and
  pays one comparison, and only undoing a plugin change pays the reload. Compared as the two
  would be written down, so nothing can be forgotten. `Pour` clears `TrackEffect.Target` first,
  because a plugin drawing into a window whose plugin has been disposed is a crash inside its own
  toolkit, and an undo is the one moment that can happen with a plugin window on screen
- The pads have undo, on PADS and FIRE. A step is every pad at once, which costs nothing at that
  size and answers what a per-pad history could not: how many pads there are is an edit too, and
  it is about none of them. Hooked at `OnPadChanged`, the one place every pad edit already ended,
  and gathered by which pad and which setting so dragging a level is one step
- Deleting a recording no longer deletes it. It moves into `deleted/` beside the recordings, so
  undo on RECORD fetches the last one back, and the confirmation stopped having to say "this
  cannot be undone". A move rather than a copy, because a take is the one thing here that can be
  a hundred megabytes and paying for the undo up front would be paying whether or not anybody
  wanted it. Only this session's deletions are offered back: putting back a take from last week
  is a filing cabinet, not undo, and what is in the bin from before is emptied deliberately
- `MainViewModel` hands a shortcut to the page it is showing before answering it itself. The
  dispatcher walks outwards from whatever has the keyboard, and when nothing has it that walk
  only reaches the window, so a page with no focused control inside it was never asked. Pressing
  undo on RECORD straight after clicking a button in a dialog is exactly that, and it silently
  did nothing
- Song steps are gathered by their own description and time, the same rule the instrument knobs
  use: a fader dragged across its range says "the mix" a hundred times and is one thing a person
  did. Fourteen edits announce themselves now (the tempo, lines per beat, track count, the mix,
  pointing a track at an instrument, pointing its notes, moving a track, taking an instrument off,
  renaming one, the song's controller links, and the four that were already there). Not announced,
  and not a missing hook: a plugin added to or taken off a track's chain. The chain's settings are
  in `TrackMix.Plugins` and so a song step does carry them, but putting them back would leave the
  plugin that is actually loaded running, so undoing that needs the live chain rebuilt from the
  config rather than a line of announcement
- The tracker's undo is one history with two kinds of step, because Ctrl+Z means the last thing
  you did and not the last thing of a kind. A pattern edit is a memory copy of its cells; a song
  edit (an instrument added or taken out, the order, the track count) is the song as its own file
  would hold it, through `SongStore.Copy`/`Uncopy` and back in through `Song.TakeFrom`. They are
  apart because they cost 5 KB and 80 KB, and serialising the song per keystroke would be
  wasteful exactly where it must not be. `TakeFrom` keeps the patterns' identity as well as the
  song's: the cheap steps hold a pattern by reference, and replacing the list left them pointing
  at orphans, so undoing a note after undoing an instrument silently did nothing `Tracker/TrackerHistory.cs` keeps
  whole copies of a pattern rather than describing each change, which is right because a pattern
  is one array of value types: a step is 0.15ms for the largest pattern there can be, and
  describing edits instead would mean an inverse per operation and the certainty that one of them
  would be wrong. The unit is one call to `PatternEdit`, hooked inside that class rather than at
  its call sites so an edit added later is recorded without anybody remembering. An edit that
  changed nothing leaves no step; every step knows which pattern it is about, so undo after
  switching patterns goes back to the right one and takes the view with it; and the history is
  emptied when a song is opened
- Keyboard shortcuts are three pieces kept apart, in `Shortcuts/`. `ShortcutAction` is the closed
  list of what a key can ask for, `ShortcutMap` is which key asks for which and is the settings
  half (stored, edited, shown), and `ShortcutKeys` delivers and knows nothing about either. A page
  says what it can do through `IShortcutContext`: the dispatcher starts at whatever has the
  keyboard and walks outwards, the first thing that says yes does it, and a key nobody claims
  carries on untouched. So nothing has to be told when the context changes, a dialog answers
  because that is where the focus is, and Ctrl+S on the pads correctly does nothing. Only what
  somebody changed is stored, so a default can still be improved; one key does one job, and
  putting an action on a key takes it off whatever had it. Undo and redo are delivered correctly
  to pages that correctly say they cannot, because **there is no undo anywhere in this
  application**: not a stack, not a history, not a type with the word in it. `docs/shortcuts.md`
  is the plan, and the point in it is that undo belongs to each context rather than to the app
- The chain under the pattern is blocks rather than pills, and the point of the change is that
  a row of boxes with names on them tells you the order of the effects and nothing at all about
  the sound. A plugin block now prints its first four controls and what they read, which is what
  Renoise and Bitwig both do and the reason both put the controls in the chain rather than behind
  it. Asked of the plugin itself rather than of its panel, because the panel is built the first
  time somebody opens the window and a device nobody has opened is exactly the one you want to
  read; kept, since for a plugin in another process that list is a round trip and Serum answers
  with 2622. Not polled: the values are read again when the chain says something moved, because
  `ValueOf` on a bridged plugin is a synchronous round trip and four of them per device per tick
  is a cost nobody asked for
- What an instrument *is*, in one sentence, lives on `TrackerInstrument.Detail` and not on
  either of the two things that print it. The song's instrument list and the block at the head
  of a track's chain both say it, and it was briefly written out twice, which is how two lists
  come to disagree about one instrument
- An instrument's block prints its machine's first three controls, the same as the effects after
  it, and getting there meant fixing something that was wrong rather than adding a binding. Which
  values adapter reads an instrument was written out twice, in `InstrumentEditorViewModel` and in
  `MachinePresetFile`, and both wrote it while doing something else. It is now
  `Tracker/Machines/MachineValuesFor.cs` and the editor calls it. The view models are optional
  there because that is the only way the two callers differ: an editor owns the one the panel
  edits and must hand it over, or the panel and the values would be looking at two copies of one
  patch, and anything only reading wants a throwaway. Which three controls is `PanelOrder`, so
  they are the first three your eye lands on when you open the machine
- `ViewModels/DeviceReading.cs` is one control and its reading, and one row template draws it for
  both kinds of block, because to a track a machine and a plugin are the same thing. A plugin's
  wording goes through `PluginParameterViewModel` and is then thrown away: how a value is worded
  is real work, since a VST3 parameter is nought to one whatever it means and the many that hand
  back "50.000000" have to be cut down, and doing it again on the strip printed 0.5000 where the
  window printed 0.5
- The strip is one height whatever is in it, by `MinHeight` on its card rather than `Height`,
  which clipped. What it holds changes as the cursor moves between tracks, and a strip that grew
  and shrank with it would push the pattern above up and down while somebody was reading it. Two
  things also came off it: the TR-01 badge and the help badge. The track's number is on the tab
  above, on the status line below and in the pattern itself, and a fourth place saying it was
  three places too many. The help moved rather than went: the square above the line numbers in
  `PatternHeader` names no track, so it is the one place in that row where something that is not
  a thing you touch can sit. Laid over the header rather than inside it, since the header is
  drawn rather than built out of controls
- Three readings a block and not four, and the two buttons that are not settings live in the
  gutter down the right rather than on rows of their own: out at the top as a red cross, in at
  the bottom as a little window with a title bar. This sits under the pattern and every pixel it
  takes is a line of music nobody can see, so the ones it takes have to be earning it. The name
  opens it too, since that is where a hand goes first, but a name that merely happens to be
  clickable is not a thing anybody can see, and taking the button away on those grounds read as
  having taken the feature away. The window is a `DataTemplate` applied through `ContentTemplate`
  rather than a resource set as `Content`, because a template makes a fresh one per button and a
  control can only have one parent
- A controller does something before anybody has pointed it at anything. `Midi/DefaultLayout.cs`:
  faders are the first tracks' levels, pinned one per track, and encoders are the controls on the
  face in front of you, in the order the panel reads. It works on hardware nobody has written a
  file for, nothing is stored, and any link somebody made beats it, so the worst it can be is
  uninteresting. Expressed against the machine rather than the device, which is the only way it
  could be: a profile can know a MiniLab has eight encoders and can never know that encoder three
  should be a filter. The order is controller number ascending within a kind, which is right for
  any program written for a DAW nobody has heard of and wrong for one written for a particular
  instrument, and the second kind never points at this application. `PanelOrder` is the reading
  order, so "the third knob" is the third one your eye lands on. `ControlMapping.Ordinal` names a
  place where a link somebody made names a parameter
- There are two things a layout can point at, the mixer and the machine in front of you, and
  `DefaultLayout.Job` decides which from the kind: faders to the mixer, knobs and encoders to
  the machine, and everything else nowhere. A modulation strip is the one that looks like it
  belongs somewhere and does not: it is picked up exactly as a fader is, and it springs back, so
  a track whose level it drove would drop to nothing the moment a thumb came off. Knobs and encoders share one order rather than having one each, or a
  desk with both would have two first controls pointed at the same parameter. Faders to the
  mixer and knobs to the machine is a statement about desks and not about electronics: both
  report a position and are picked up identically, so `ControlSense` cannot tell them apart and
  does not try, and a device with no file keeps its knobs on the mixer exactly as before. Only a
  profile knows which is which, and that is the whole of what a profile adds here. It is also
  what makes an MPD218 useful on arrival, since six knobs and no faders would otherwise be a six
  channel mixer on a box built for hitting things
- Mackie Control is read, and it is the one protocol here that needs no file, no learning and
  no layout, because it says what every control on it is. `Midi/MidiMackieRouter.cs` is the
  fifth router and the same shape as the four before it: faders as pitch bend on channels 1 to
  8, knobs as relative controllers 0x10 to 0x17 with the direction in bit 6 and the ticks in
  the six below it, mute and solo as notes 0x10 and 0x08 plus the strip, banking on 0x2E and
  0x2F by eight and 0x30 and 0x31 by one. It reaches the mixer through `IControlTargets`, the
  same door a link written by hand goes through, and it holds none of the sensing machinery
  because there is nothing to sense
- A fader lands rather than picking up, which contradicts every other position-reporting
  control here and is right: on a surface like this the fader is motorised and has already been
  driven to where the parameter is, so picking up would be hunting for a value it is sitting on.
  That is only true because the writing half drives it there, which is why the two halves are
  one piece of work rather than two
- `Midi/MackieSurface.cs` is what a surface is told: fader positions as pitch bend, button
  lights as note on at 0x7F or nothing, the ring round each knob as CC 0x30 plus the strip with
  the mode in bits 4 and 5, and the two lines of the display as
  `F0 00 00 66 14 12 <offset> <fifty six characters> F7` with the second line at 0x38. Seven
  characters a strip, the track's name above and its pan below, since the fader is already
  showing the level in the one way a number cannot. Every message is compared with what was
  last sent and dropped if it would say the same again, which is not tidiness: a display line
  is sixty two bytes and the mix moves for all sorts of reasons
- Which port to write to is learned from what arrives rather than configured. A surface speaks
  and listens on the same port, so the first thing it says is also the address to answer on,
  and a device moved to another socket still works
- Two things stop the desk fighting the hand. A hand on a fader is a note in the 0x68 row, and
  while it is there the motor is left alone, because driving it against a hand feels like a
  fault rather than a policy; letting go puts it back. And a fader position that arrives is
  written down as though it had been sent, which breaks the loop where a hand moves the fader,
  the level follows, and the level having changed asks for the fader to be moved to where it
  already is. No timing and no suppression window
- `TrackerViewModel.MixShown` is how the surface hears that the mix moved, since the levels are
  under its own faders and the names on its own display. Deliberately not a subscription to each
  strip: the strips are rebuilt whenever the song is, so anything holding them would be holding
  the last song's
- The five transport notes are refused by name in the Mackie router, because
  `MidiTransportRouter` already answers them and they arrive on the same port. That is the only
  place the two could overlap, and answering twice would stop what the press had started
- Ticking Transport in SETTINGS now gives a real Mackie surface its mixer as well. A device
  speaking that protocol is one device sending one stream and there is no way to have the
  buttons without the faders. The flag's name is narrower than what it does; it is kept because
  it is what is stored and what people have already ticked
- The numbers came off Ardour, which has carried a Mackie implementation under GPL 2 or later
  since 2006, and this is GPL 2, so the licence runs the right way. `libs/surfaces/mackie/
  device_info.cc` and `surface.cc`; copyright John Anderson and Paul Davis, credited in the
  router's own remarks. Nothing was copied. The 250 KB around those tables is written against
  Ardour's Session and Route model and porting it would be a rewrite of somebody else's
  architecture; the tables are facts about hardware and are what nobody should reinvent. Mackie
  themselves never published any of it: the same hardware shipped as Emagic's Logic Control and
  Emagic did
- `controllers/nanokontrol2.json` is the first file here written from somebody else's reading of
  a device rather than from the wire, and it says so at the top. Korg's parameter guide has a
  page per control type explaining what CC Number means and never prints one; the numbers come
  from Mixxx's mapping for the device as shipped, agreeing with every community list. Fifty one
  controls, all fixed: sliders 0-7, knobs 16-23, solo 32-39, mute 48-55, rec 64-71, transport
  41-46 and 58-62. They hold in CC mode, which is the factory mode, and mean nothing in the five
  DAW modes where the same controls speak that DAW's protocol. It buys the most of any file here
  because the device is the plainest surface anybody makes: eight faders on eight track levels
  and eight knobs on the panel in front of you, working before it is unwrapped
- `controllers/keystep-pro.json` is the one that says a device cannot be described, and why.
  Its five encoders have no factory controller number: the manual's Controller page marks a
  default for channel, mode, min and max and marks none for CC, so the omission is deliberate
  and there is nothing to write down even in principle. Measuring one would report what its
  owner assigned. Two facts are in the file instead. The Looper strip sends CC 9 with its MIDI
  send off until a menu is visited, which reads as broken hardware. And its three transport
  buttons send MIDI Machine Control, which is why that is read here at all
- The transport is read in three dialects now, and a device speaks whichever its program or its
  menu chose. Mackie Control notes and the plain controllers a MiniLab sends were already there.
  Added: the realtime bytes 0xFA start, 0xFB continue and 0xFC stop, which are in the
  specification and understood by every sequencer ever built; and MIDI Machine Control,
  `F0 7F <device> 06 <command> F7`, which is what a KeyStep Pro sends unless somebody changes it.
  Continue is play and pause is stop, because this transport has no memory of where it was. The
  unit number is not checked: 0x7F means everybody and is what hardware sends, and refusing
  anything else would mean a button doing nothing for a reason nobody could guess
- System exclusive is read off the wire, which three separate things were waiting on: machine
  control, the universal identity reply, and Arturia's own settings protocol. It is the only
  message MIDI has with no length and so the only one that can arrive in pieces, so
  `MidiService.Gather` keeps a buffer per device, the same way running status is kept per device
  and for the same reason. Only 0xF7 ends one; a realtime byte threaded through the middle is
  not part of it, which is exactly what a device sending clock does while it answers an identity
  request; any other status byte means the sender gave up part way, which is what a pulled cable
  looks like. Capped at 4096 bytes so a broken stream cannot grow without end
- Neither of the two new kinds is a press, and `IsOn` is false on both. That is what keeps a
  transport byte out of the pads without a line being added anywhere: all three of the other
  routers begin by asking for a press. The transport router reads them before its own press
  guard rather than being given a pressed-ness they do not have
- The clock and active sensing are dropped at the wire without a word. They already were, but
  they were also logged as "not a kind read here" once each, and at twenty four clocks a beat
  that line would drown the ones the log is kept for. `MidiService.Chatter` is the difference
  between a message nobody reads and a message dropped on purpose
- A device whose file describes exactly one program is in it, and `ProgramOn` says so without
  waiting. `Saw` declines to work out the program of such a device, rightly, since there is
  nothing to resolve; the cost was that no program was ever running, so a file that put its
  controls inside its one program described a device whose every control came back unknown. The
  MiniLab and the MPD218 have two and three programs and never met it
- SETTINGS has a Control Surfaces page, and it lists what you own rather than what the operating
  system offers. A controller is often several ports with nearly identical names and only one of
  them carries the knobs; the profile says which, so the jobs are ticked once on the device and
  put on whichever port really does them. Transport goes on both the main port and a Mackie one
  where a device has both, since it sends one or the other depending on its program and never
  both. `ControlSurfaceViewModel` is a view over the per-port `MidiDeviceViewModel` rows rather
  than a replacement, so what is stored and loaded did not change and a device with no profile is
  a surface with one port that behaves as it always did. The MIDI page keeps the three pad boxes,
  which is all that was ever really about MIDI on it
- A controller's file decides how a control is read, and beats what `ControlSense` works out by
  watching, because a fact about the hardware beats an inference from three messages. The case
  it exists for: an endless encoder reporting a position walks through its range exactly like a
  fader and is indistinguishable from one until it comes round, so it is sensed as a fader,
  saved as one, and every session then opens with a hunt for the value using a knob that has no
  end to hunt from. Nine links in one song, five of them on encoders, all saved as `Takeover`.
  An encoder in a program that sends positions is read as movement between messages instead,
  which works whether the firmware wraps at the top or stops there. Nothing is claimed for an
  encoder that counts notches, since which of the two conventions it counts in is not in the
  file and guessing wrong throws a parameter across its range
- The kinds a profile may call a control are `encoder`, `fader`, `strip`, `pad`, `button` and
  `knob`, and the last one is the interesting one because it is where a plausible reading of a
  manufacturer's own words turned out to be wrong. An MPD218's six knobs are sold as 360 degree
  potentiometers, which reads as endless, meaning a value that comes round and has to be
  followed rather than picked up. Measured on the wire it is nothing of the kind: one turned
  steadily walked 35 to 127 in two seconds and then sat at 127 for another seven while it was
  still being turned, 254 messages of a full sweep with no repeat and no step bigger than one.
  So a knob is a fader that happens to be round, `Takeover` in the file and ranked among the
  faders by `DefaultLayout`, whose only question is whether a control says where it is or how
  far it moved. 360 degree describes the absence of a detent, not the behaviour of the value.
  The device's own numbers were measured the same way and are in `controllers/mpd218.json`: all
  eighteen knob assignments, since CTRL BANK cycles three sets of six with nothing announced on
  the wire. Bank A is scattered (3, 9, 12, 13, 14, 15) because Akai stepped around the
  controllers everybody else uses; B and C are plain runs from 16 and from 22. Which letter is
  which was confirmed twice, once by the owner reading the device and once by the cycle wrapping
  where it should. None of its six buttons sends anything on the wire, and Full Level is worth
  knowing about for the shape of the fault rather than the fact: a device left with it on
  delivers every pad at velocity 127 however softly it is hit, says nothing about why, and the
  only clue is a lit button on the hardware. Half an hour went on the same shape once before,
  on a MiniLab whose play button turned out to be Shift and a pad. A controller can be in a
  state the wire does not mention, and the wire is all we have. The other half of that lesson
  is cheaper: read the manual in `docs/` before asking somebody to press a button, because two
  rounds went on asking for a 16 Level button that this model does not have and neighbouring
  Akai models do
- The MiniLab 3's file was checked against the device itself on 2026-08-27, with `sysex-controls`
  reading Arturia's own settings protocol rather than anybody turning anything. All twelve of the
  Arturia program's numbers are right, knobs 74, 71, 76, 77, 93, 18, 19, 16 and faders 82, 83,
  85, 17, and its pads are notes 36 to 51 over two banks on channel 10, gated. Two things came
  out of it that the wire could not have said. The Controller page's Transport Mode reads MCU,
  which had only been inferred. And every knob's Option reads Absolute where this file says that
  program counts notches, which is unresolved: what was read is User Preset 1 and not the factory
  Analog Lab program, so both can be true, and it is written up in the file rather than acted on.
  Reading it also turned up the main knob, three controls that were missing from the file
  altogether (CC 114 turning, 112 with Shift, 115 pressed), and the hard justification for the
  Lua codec: the Pitch strip's page has no CC field at all, only a channel and a range, so it
  cannot be made to send a controller on the device and the codec is the only way that strip can
  ever be pointed at anything
- The MPD218 answers the universal identity request and refuses everything else. `F0 7E 00 06 02
  47 34 00 19 00 01 01 02 00 00 7F 7F 7F` and then its serial number in ASCII: manufacturer 47 is
  Akai, family 0034, member 0019. That is in `controllers/mpd218.json` now and it is the one name
  a device has that survives a different operating system, a different socket and a second one
  being plugged in. What it will not answer is Akai's own settings protocol, which is the thing
  that would have read all eighteen knob assignments without anybody turning anything:
  `sysex-controls` asks and gets ETIMEDOUT, and lists this model as supported but untested. So
  the by-hand measurement was not avoidable here, whatever it looked like. It would be avoidable
  on the Arturia hardware, which that tool really does support
- Lua is embedded, MoonSharp, and it is fenced in: `Scripting/LuaScript.cs` names every library
  a script may reach rather than using a preset, so there is no io, no os and no loading more
  code, and a script that throws or takes more than 20ms is switched off rather than called
  again. It is Lua 5.2, which means `bit32.rshift` and not `>>`. The first thing built on it is
  `Scripting/ControllerCodecs.cs`: one `.lua` per controller, matched on the port name, sitting
  between the wire and the routing. A codec can only say that these bytes mean those bytes, so
  it cannot add a feature or remove one, and a device with no codec is passed through untouched.
  Codecs live in `controllers/` beside the program and are copied to the app folder on first
  run, the way machines are, and the folder is watched: saving a codec reloads it, with no
  restart and no replugging. `controllers/minilab3.lua` is the shipped example and does one real
  thing, turning the pitch strip into CC 2 so a control that did nothing becomes linkable
- `docs/scratch-machine.md` is an idea, not a plan for now: a fader as a needle on a record,
  where the sound comes from how fast the position moves rather than where it is. It is the
  first machine that would need an engine of its own rather than a described panel over an
  existing one, and the first thing that would want a parameter delivered uncoalesced
- Two places things are stored, on purpose: instruments (the shelf of sounds you own, where a
  new one starts) and songs (patterns plus their own copies of the instruments they use). There
  was a third, a preset bank, and it went when the library stopped reaching into songs: a sound
  you start from and a sound you own turned out to be the same object. A fresh library seeds
  itself with six starters, and from then on they are ordinary instruments
- A note played by hand on the tracker's keyboard is that track playing. It goes on the track
  the cursor is in, through its inserts and its fader, and moves that track's meter and the
  master's, which is the whole point of auditioning: it tells you what the part will sound like.
  Only the plugin path did that; everything else, synth, mono synth, sampler, kit and recording,
  went to the loose audition bus and moved no track meter at all. Every preview takes the track
  now and defaults to none. The rack's keyboard still names none, and rightly: the instrument it
  is playing may not be in any song, so it goes through nobody's fader
- A note played by hand holds for a fixed moment on a generated sound, which would otherwise
  never stop, and for its own length on a recording: a take cut off part way through is not the
  sound the instrument makes. `SampleVoice.WindowSeconds` is that length, and the hold is passed
  back up through `Audition` and `NotePlayed` so the key that lights and the cursor that runs
  last exactly as long as the sound. Auditions pile up, as a keyboard does, unless the
  instrument says `OneVoice`, which cuts what it was sounding first; in a pattern this changes
  nothing, since a track is one voice already
- The audio engine runs whenever a track has a chain, not only while something is playing. A
  plugin has to be given blocks or it cannot work on the audio, cannot finish a delay's tail,
  and cannot tell the host what its own window did. `TrackMixer` therefore does not rest while
  any track has an insert, and does not skip a silent track that has one
- Changing the output device calls `Bass.Free()`, which takes the tracker's stream with it.
  `SynthOutput.EnsureStarted` checks the stream is really still running rather than trusting its
  handle, and `TrackerViewModel.ReopenAudio` is called after a device change
- A knob turned in a plugin's own window reaches the host differently per standard: VST3 reports
  it at once through `IComponentHandler::performEdit`, CLAP only hands it back at the end of a
  block, so a CLAP plugin with its window open is also read forty times a second. Read-only
  parameters (a compressor's gain reduction) are excluded, or a song could never be saved
- Plugins run out of process, one process per plugin, and so does the scan. A plugin that
  crashes stops on its own: an effect passes its audio through, an instrument goes quiet, and
  the panel offers to start it again. Set `JB_PLUGINS_INPROCESS=1` to load them in this process
  instead, and `JB_PLUGIN_TRACE=1` to have the child write what it is doing to
  `/tmp/jinglebox-plugin-<pid>.log`
- A plugin's own window is given to it only once the window is really on screen at its full
  size. Handing over the one-pixel window Avalonia makes before the first layout is what killed
  Serum
- BASS library binaries are copied to output via build targets in csproj
- managed-midi API has obsolete warnings (suppressed via `<NoWarn>CS0618</NoWarn>`)
- Startup errors logged to `startup.log` for debugging
