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
- `Music/` - Notes, pitch and keyboards, knowing nothing about patterns: which key sounds which
  note, a note as a playback rate, concert pitch, and sharing a keyboard out among pieces
- `Files/` - The three questions about a file that are about this machine rather than about
  this program, and are asked from everywhere: whether two paths are the same file, where the
  application keeps its things, and how a file is written whole. `AppFolder` and `SafeFile` sat
  in `Config/` and were moved here, since neither is about the settings and `AppFolder`'s own
  remarks said so: a plugin's own process needs it and has no settings to read
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
  chain, a ducker and an instrument apiece, and room made on a track for each new note the way
  that track's instrument asks for it: cut, which is what a tracker has always done, release or
  sustain. Auditions carry no track at all and pile up, which is why a
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
- **Engine, machine and instrument are three words and not one.** They are the three layers of
  the same thing and confusing any two of them leads somewhere wrong, so:
  - An **engine** is what makes the sound. There are six, they are `TrackerInstrumentKind`, they
    are compiled into the application, and their numbers are in every song ever saved so they do
    not move. An engine has no face and no name a person sees
  - A **machine** is a face over an engine: a folder holding `machine.json`, its badge, its
    presets and its own `sounds`, made in the designer and travelling as a zip. It carries no
    engine and cannot: which engine it is on is decided by its id, and `Machine.Register` refuses
    an id it has no engine for. That is why a machine designed under a new id is read off disc
    and never reaches the rack, and it is the one thing standing between here and a machine
    somebody else can ship
  - An **instrument** is a machine in use: your name, your settings, its own id, stored with the
    song. Two of them can come off one machine

- **The registry is what this installation has, and it is the only thing that answers that.**
  Two folders and only one of them is yours. Beside the program is what ships: a source to take
  a machine from, never written to, and never the answer to what is on the rack. Under the
  application folder is what this installation actually has, and that one alone decides. The
  point of the split is that removing a machine is not losing it, since the shipped copy stays
  where it was and can be taken again.

  Registering is a deliberate act and so is unregistering, which is why what has been *offered*
  is recorded rather than what is present. `offered.txt` is that record: a shipped machine this
  installation has never been offered goes on the rack, and one it has been offered is left
  alone whether or not it is still there. So a machine written after the folder was made still
  arrives, and a machine somebody threw out stays thrown out. Deciding by the folder's absence
  meant neither: every new machine needed a trip to SETTINGS before it could be seen at all.

  A machine that ships is kept up to date file by file against the shipped copy, by each file's
  clock rather than by the version in its manifest, and **nothing is ever deleted**. What ships
  is overwritten because that is the machine; anything else in the folder is yours, which is how
  a preset you saved onto a machine survives the next version of it arriving.

  **A machine is its folder, and Save as is the half of saving that knows it.** The manifest
  names pictures, presets and sounds by the names they have inside the folder, so a
  `machine.json` written into an empty folder somewhere else is a machine that draws nothing and
  has no presets. `MachineProject.Save` writes the manifest and only that, rightly, since it is
  called on every ordinary save and copying the whole folder onto itself each time would be
  absurd; `IMachineArchive.CopyInto` is the other half, for the one case where the folder
  changes. The files first and the manifest after them, because the one on disc is behind
  whatever is on screen and copying a stale one would only be overwriting it a moment later.

  The editor is pointed at the new folder afterwards and the old one is left exactly as it was,
  which is what makes it the way to put an edited machine back over the copy that ships beside
  the program. That is the case it was written for: the installed copy is what you actually
  edit, and until this there was no way back to the shipped one but a zip and a hand.

  The id does not change. A machine's id is what songs write down and what decides its engine,
  so a copy of a machine is that machine somewhere else and not a new one; New is how you make
  a different one. Nothing in the destination is deleted, the same rule as above and for the
  same reason. A folder already holding a **different** machine is asked about first and names
  it, since overwriting has to be allowed for the case this exists for and landing on somebody
  else's machine by picking the wrong folder is the same gesture.

  Everything asks it. What the rack shows, what a panel is drawn from, what a song can sound,
  and which machines a song is missing are all one question with one answer, and there is one
  list that gives it: `IMachineRegistry` reads the folders and `IMachineProjects` holds what it
  found for the run. A machine whose id this build has no engine for is read and passed over, so
  a machines folder from a later version is harmless, and that gate is what has to move before a
  machine written by somebody else can be registered at all.

  So an instrument whose machine is not registered here makes no sound and has no panel: it is
  on that machine, and the machine is not here. It goes on naming it until the track is pointed
  at another instrument, it saves unchanged, and it shows a grey "Sampler" named for its
  engine.
  `IMachineProjects.Has` is the test, asked in `TrackerPlayer` before anything sounds. Nothing
  about hearing silence explains it, so it is said twice and in two different moments. Opening
  the song puts a line on the status bar naming what is not registered, which is a note for
  somebody who wants to know before they start. Opening one of those instruments refuses and
  says why, in a real error dialog headed `<Machine>(machine) is not registered`: the machine is
  tagged because an instrument takes its machine's name unless it is renamed, so the two are
  usually the same word. The window does not open behind it, since an empty frame with a
  keyboard that cannot sound a note reads as a machine that is broken rather than absent.

  It says **register**, not install, and points at SETTINGS, System without describing it. That
  page shows the machine either waiting to be added or not there at all, which is itself the
  answer about whether a zip has to be imported, and it shows it while somebody is looking at
  it. A machine's own recordings do travel with it, inside the zip

- A machine is a fixture on the rack: one of each, fixed name, always there. `TrackerInstrument`
  is the data type for both a machine and an instrument, but the rack's types say machine
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

## How this code is written down

Every seam is an interface, and the prose lives on the interface. What a thing is for, why it
works the way it does and what was got wrong on the way there is a fact about the contract, not
about one implementation of it, and a reader who has the interface in front of them should not
have to go looking for the class to find out what they are holding.

So: the interface carries the full XML documentation. The implementation carries `<inheritdoc/>`
and, under it, only what is true of that implementation and untrue of the contract, which is
usually how it does the thing rather than what the thing is. A remark that would still be true of
a second implementation belongs upstairs.

**A class is named for what it is, never for what it is to somebody else.** No `Helper`, no
`Util`, no `Manager`, no `Common`: those name a relationship rather than a thing, so they attract
whatever nobody could place and end up as a bag. `KeyRegions` shares a keyboard out, `SampleUsers`
answers who plays a recording, `RunMarker` is the note a run leaves. If a name cannot be found,
the class is usually two classes.

**Every class that does something gets one.** That is the rule, and it is wider than the one
that used to be written here, which said a pure rule holder was already answerable and could stay
static. It was wrong twice over. A static class cannot be stood in front of, so the moment
anything above it wants testing the static is the thing in the way; and a class that looks pure
often is not, which is the trap `FilePaths` was. It takes two strings and answers a bool, which
reads as arithmetic, and inside it asks `OperatingSystem.IsWindows()`. So the answer changes with
the machine, a program on Linux cannot ask what Windows would have decided, and the half of this
application that keys recordings by path is exactly the half where that is silent when it is
wrong. Handed the rule instead of reading it, both answers can be put a question to on either
machine.

So a static class is a decision to be untestable, and it is almost never worth making. The ones
that were here became sealed instance classes behind interfaces in one pass: the note and
keyboard maths in `Music/`, the viewport and gain rules in `UI/`, the filters and curves in
`Tracker/Synth/`, the machines folder in `Tracker/Machines/`, and the pattern, slice and song
rules at the root of `Tracker/`. A second pass took `Audio/`, `Midi/`, `Config/`,
`Diagnostics/`, `Views/` and the theme; a third took the two machine assemblies, `Shortcuts/`,
`Controllers/`, `Help/` and what was left in `ViewModels/`. What is left static is twenty one
things and every one of them is on the list below.

**Three doors stay static, and each one has the same reason.** `Log`, `CrashReport`,
`ThemeSwitch`, and the two under them, `LinkKey` and `Pointable`. An application has one log,
one run, one theme and one set of attached properties: handing one about would be handing the
same object about under another name, and `Log` alone has fifty three callers including the
thread that fills the audio buffer. **But nothing in a door decides anything.** What each one
knows became an instance class behind an interface that can be asked without a process, a
window or a disc: `ILogAreas` is which areas are on and what each is called, `ILogLine` the
shape of a line, `ILogFile` the appending and the rolling over, `IRunMarker` the note a run
leaves and which crashes belong to it, `IThemeCatalogue` which themes there are and what a name
out of a settings file really means. The door is left holding a queue, a thread and a file.

That is the pattern for anything that genuinely cannot be handed about, and it is worth naming
because the alternative is what was there before: a rule nobody can reach, inside a class nobody
can stand in front of. `LinkKey.Answers` was the first of these and arrived at from the same
direction.

What else stays static, and why. An ABI is not behaviour: `Vst3Abi` and `ClapAbi` are P/Invoke
declarations, GUIDs and struct layouts, which is data with a compiler attached, and a
`[LibraryImport]` has to be static anyway. `Pointable` is an Avalonia attached property, which
the toolkit requires. `XErrors` installs the one X11 error handler a process has. `PanelPreview`
and `PluginHostProcess` are entry points: this same executable started again, being something
else. `PadMatrix` is three consts and `MixLinks` is nine templates named from XAML by
`x:Static`, both data, and so are `MachineActions`, `MachineStarts`, `MachineElementKinds`,
`MachinePresetWords` and `PatternFont`. `PluginCrashGuard` is a door like the log's and its
rules came out into `IRunMarker`. `ShortcutKeys` is one more door: one map for the application,
hung on every window, and what it knows is `IShortcutMap` and `IShortcutContext` already.

**The two machine assemblies are published, and the rule there is narrower.** `Machines.Abstractions`
is what an outside machine links to and is the assembly `LICENSE.EXCEPTION` names, so everything
public in either of them is a promise. The test is not "can it be stood in front of" but **would
an outside machine ever write this down**. The parts every range control shares are public,
because a machine drawing a control of its own should feel like the ones we ship: `IRangeValue`,
`IMeterScale`, `INumericInput`, `IWaveformGeometry`, `INaming`, and in the contract itself
`IPanelOrder`, `IMachineNotes` and `IPresetStep`. How our own knob sweeps its 270 degrees, how
our own fader reads its track and how our own tick attribute is spelled are not: `KnobMath`,
`FaderMath` and `TickList` are internal, with `InternalsVisibleTo` for the tests. Internal is not
untested, and that line in the csproj is the whole of what it costs to keep the promise small.

Five files in `Machines.Ui` declared `JingleBox2.UI`, the application's own namespace, inside the
assembly this codebase is otherwise careful to keep clear of it. The compiler proved they were
the only ones: taking the namespace away left nothing else in that assembly needing it.

`ControllerProfiles` is the one that had to be shared rather than merely made an instance. It
remembers what a device has been seen doing, and which of a device's programs is running is
worked out from the numbers arriving, so a second one is told nothing and answers for a device
it has never heard speak. `MainViewModel` makes one and hands it to the router, the layout, the
codecs and the three pages; everything that takes one still defaults to its own, so a panel or a
test built on its own works. `Tests/DefaultLayoutTests.cs` failed the moment it stopped sharing,
which is the hazard the static had been hiding.

What still does not get one: data. Records, enums, and the document types you can already build
in a test and hand about (`Note`, `TrackerCell`, `Song`, `Pattern`). An `ISong` with forty
members on it buys no test anything and costs every reader who wanted to know what a song is.
**Where there is no interface the documentation goes on the block itself**, in the same words it
would have had upstairs: the rule is where the prose lives, not whether the prose exists.

A dependency arrives through the constructor, optional, defaulted to the real one:
`public sealed class SongSamples(IFilePaths? paths = null)` holding `paths ?? new FilePaths()`.
No ambient singleton and no static `Default`, because both are the static class again wearing a
different hat: whatever a test put there is still there for the next test. These types are
stateless, so a caller who does not care pays a `new` that costs nothing, and a caller who does
care hands one in.

An interface lives in a file of its own, named after it, in an `Interfaces` folder beside the
code it is the contract for: `Audio/Interfaces/IAudioEngine.cs` is `JingleBox2.Audio.Interfaces`,
and `BassAudioEngine.cs` stays in `Audio/`. It is the half a reader is meant to open first and the
half everything else names, so it does not sit at the top of the class that happens to implement
it: two implementations would then leave the contract living inside one of them, and a reader who
wants to know what they are holding should not have to open somebody's plumbing to find out.

Beside the area rather than one folder at the root, because a contract belongs to the thing it is
about. `Audio/Interfaces` is a list of what the audio can be asked to do, which is worth opening;
fifty four files in one place at the root would be an alphabet, and it would put the machine
assembly's contracts in a namespace rooted in the application, which is the one thing that
assembly is kept clear of.

Enums and records go the same way, in `Enums` and `Records` folders beside their area, one type
to a file named after it. The reason is the same for both: a closed set of names, or a shape with
no behaviour, is what several classes agree to say, so it belongs to none of them, and it was
living wherever the first class to need one happened to be. `Midi/ControlMapping.cs` held five
enums and `Tracker/Synth/MonoSynthPatch.cs` another five; `TrackerPosition.cs` held two records
and an enum.

There was a `Models/` at the root holding four of these with three of them in one file, which is
what the rule looks like when it is not applied: they were `Recording`, `WaveformData`,
`TrimRegion` and `OutputDevice`, all four of them Audio's, and they are one to a file in
`Audio/Records/` now. Two enums were living inside `PluginBridge.cs` for the same reason and are
in `Audio/Plugins/Bridge/Enums/`. Nothing declares a record or an enum outside one of these
folders any more, and that is worth checking rather than believing:

```bash
grep -rn "^public \(sealed \)\?\(readonly \)\?record\|^public enum" --include=*.cs .   | grep -v '/Records/' | grep -v '/Enums/'
```

`Records` holds records and record structs together. Every one of them is a record, and whether it
is also a struct is a decision about copying rather than a statement about what the type is:
splitting on that would put `Note` and `SongFile` in different folders for a reason nobody reading
the song's data cares about. There are no plain structs here at all.

So the whole of a public type surface is in one of the three, and what is left in an area's own
folder is the classes: the things that do something.

**A record referred to by a view has to be told about twice.** XAML names a type through a
`clr-namespace`, so moving one breaks an `x:DataType` in a way only the Avalonia compiler catches,
and only on a build that is not incremental. Four views needed a second `xmlns` for this:
`HelpWindow`, `MachineEditorView`, `PluginStrip` and `SongDialog`. Nothing in XAML names an enum
or an interface but `InstrumentPanel`, which is bound to `IInstrumentDesigner`.

### There are no line comments

Documentation is XML documentation, and it lives above the block it is about. A `//` comment in
the middle of a method is that same prose in the one place no tool can read it and no reader
looking at the type will find it, and it is almost always there because the documentation above
did not say enough. So a line comment is not a style preference, it is a defect report on the
documentation over it: move what it says into the `<summary>` or the `<remarks>` and delete it.

That covers the reasons, the traps and the history, which is most of what was written down here
as `//`. Two things are not comments and stay: XAML, which has no XML documentation and carries
its prose in `<!-- -->`, and a directive the compiler reads, such as a pragma or a region.

### The documentation is generated, and CS1591 is the guard

Every project generates its documentation file, and CS1591 is deliberately left on: the compiler
saying "this public thing has no documentation" is what stops the rule quietly lapsing. The count
is read with:

```bash
dotnet build -c Debug --no-incremental 2>&1 | grep "warning CS1591" | sed 's/: warning.*//' | sort -u | wc -l
```

**1981 members across 284 files when this began** (2026-08-28), and **nought** when it was finished,
the same day. The whole tree went through in one pass, folder by folder: `Audio/` and its plugins
and routing, `Midi/`, `Config/`, `Tracker/` with its synth and its machines, `ViewModels/`,
`Views/`, `Machines.Ui/`, `Machines.Abstractions/`, `Diagnostics/`, `Shortcuts/`, `Scripting/`,
`Controllers/`, `Models/`, `UI/`, `Converters/`, `Waveform/`, `Help/`, `Controls/` and `Tests/`.
About 2600 line comments came out and went upstairs into the documentation over the block they
sat in.

The build is clean and has to stay clean: **nought warnings, of any kind**. The four that turn up
while writing documentation are all real and all mean something. CS1573 is a method with some of
its parameters described and not the others, which C# treats as all or nothing. CS1572 is a
`<param>` naming something that is not a parameter, usually a tuple element on an event. CS1574
and CS0419 are a `<see cref>` pointing at nothing, or at one of several overloads; a member the
reader cannot reach is `<c>` rather than a cref. CS1587 is a `///` block on something that cannot
carry one, a local function most often, and the answer is never a `//` comment: fold it into the
containing method's `<remarks>`.

What the pass turned up, besides the prose. A `<summary>` left behind when the member under it
moved attaches itself silently to the next member, and there were about thirty of those, several
describing a method fifteen lines away. Three places said the cursor rests on the middle of the
screen except at the two ends of a pattern, which stopped being true when the metrics started
leaving the room unconditionally, and one still said there is no undo anywhere in this
application. Documentation goes stale exactly where nobody is made to read it.

## Tests

```bash
dotnet test Tests/JingleBox2.Tests.csproj
```

1014 of them, in about five seconds, with no window and no hardware. They run in CI on every push
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

Four of the newer files are about rules that used to be a comparison buried in a control, and
each one was written because the buried version was wrong. `Tests/VolumeScaleTests.cs` is the
old 0 to 64 column being brought onto the new one, including the trap that conversion always
falls into: a song of this build going through twice and being doubled the second time.
`Tests/QuantizeGridTests.cs` is which note values a setting can offer. `Tests/PointerDragTests.cs`
is when a press has become a drag, and it exists because a row is under twenty pixels tall and
the old rule read a click as a block. `Tests/MachineSaveAsTests.cs` is a machine's folder being
carried to another place, which is where "nothing in the destination is deleted" and "the folder
it came from is untouched" are actually checked rather than believed.

And the column axis, which is indexed arithmetic over a shape that is no longer a rectangle:
where a cell sits, where it is drawn, where a click lands, what the file says, what the
sequencer plays and what an undo puts back. `Tests/NoteColumnTests.cs` is that, and every test
in it also says what a song with one column a track still does, which is every song written
before now.

What is covered, and it is the parts that can be got wrong quietly: the wire (running status,
pitch bend, one-byte statuses), what kind of control is sending, pickup and endless knobs and
parking, device roles, controller profiles and codecs, shortcuts, all four histories, patterns
and their edits, a song being written down and poured back, the mix, envelopes, portable paths,
the screen's bytes, the transport's two dialects, and the Lua fence. Several of those tests exist
because that exact thing was wrong once.

And the three endings a new note can give the one before it, which are counted rather than
listened to: a cut is four milliseconds, a release is the patch's own and a sustain is neither,
so how many voices are alive after a given stretch of rendering says which of the three
happened without anybody measuring a waveform. The plugin half cannot be counted that way,
because a plugin holds its own voices, so it is asked instead: a plugin that makes no sound and
writes down every note on and note off it is sent, which is the only way to check the one thing
a host has to get right there. And once, out of caution, the buffer itself, since a voice alive
in the list and silent in the audio would pass every count. That measurement had to be taken at
a low level: at full level one loud sine is driven most of the way to a square by the master's
saturation and two a fifth apart cancel at the bottom of every beat, so the pair reads quieter
than the one and the test says the opposite of the truth.

And the seams the second pass made: the filters, the drive curve, the sample window and its
loop, the ducker, pitch motion, the WAV reader, peak normalisation, naming a take, the log's
areas, the run marker, the theme catalogue, writing a file whole, and the bridge's message
bodies. **Five of those found a defect the moment they existed**, which is the argument for the
whole exercise and is worth writing down rather than summarising:

- `SafeFile` destroyed the file it exists to protect. The writing and the moving were inside one
  try, so a writer that threw part way fell into the fallback, which opened the real file,
  emptied it, ran the same writer again and threw again. The old file was gone, the new one was
  never written, and the exception said nothing about either. A song is built an entry at a
  time, so any take that would not read reached it. The writing is its own attempt now: if it
  fails, the old file has not been touched
- The bridge's two longest messages could take the host down from reading them. `ReadWords` and
  `ReadParameters` sized an array from a count read off the wire, so a damaged four bytes asked
  for two thousand million strings, and both read past the end of a truncated payload and threw.
  A payload from a process that has just crashed is exactly what those look like, and the bridge
  exists so that a plugin falling over takes nothing with it but itself
- The ducker never ducked from a quiet key track. The floor that stops the follower creeping
  towards nought for ever was applied on the way up as well, and one attack step from nought at
  five milliseconds is the target times 0.004525, so every key below 0.0221, about -33 dB, was
  snapped back to nought on every frame for ever
- `ToneFilter` guarded its cutoff against NaN and not its resonance, and `Math.Clamp` hands NaN
  back by design, so a patch off disc could make all three coefficients NaN and the voice was
  silent for the whole of its life. `SweepFilter` had always guarded both
- The drive knob stepped 1.6 dB as it left its own minimum, since the makeup levels the curve at
  full scale and nowhere else. Faded in over the first unit of the range now, so a drive of two
  and above is exactly what it always was
- A WAV cut off inside its own headers came back as the runtime saying it could not read beyond
  the end of a stream, which reaches RECORD as a message about a stream rather than about the
  file somebody just tried to open. The audio itself is still salvaged rather than refused, and
  that is deliberate: what a take really holds beats what its header claims
- `MachineNotes.Semitone` chose between a note and a plain number by how long the text was, and
  three characters is both. Every plain number from 100 to 119 was read as a note, failed to be
  one, and came back as nothing: the top two octaves, which are notes a machine can be asked to
  play. The note is tried first now and the number second

## Technical Notes

- **A source folder may not differ from another only in case.** There was a `controllers/` of
  device profiles beside the `Controllers/` that holds the code for reading them, and on Windows
  and macOS those are one folder. Git checks both into it, and the csproj glob over
  `controllers\**\*` then sweeps the C# sources into the output as content. The profiles live in
  `Controllers/Profiles/` and are given `Link="controllers\..."`, so what lands in the output and
  in the application folder is exactly what it always was and `ControllerFolder.Shipped` did not
  have to learn anything
- Pasting a block is an edit like any other and leaves an undo step, and it very nearly stopped.
  The hook that records an edit used to hang off a static class, so there was one of it and every
  caller found it. With `PatternEdit` an instance, `PatternBlock` holding one of its own would
  have rung a bell nobody had tied to anything: the paste would land, leave no step, and undo
  would go back past it to whatever happened before, with nothing said. `Paste` takes the editor
  the caller is using. That is the shape of the risk in making a static an instance, and it is
  worth remembering: the compiler cannot see a listener that is merely never called

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
- **`Midi/MixLinks.cs` is what a mixer strip offers, and a link names the strip it was made on.**
  Level, Pan, Mute, Solo, Duck and the ducking release, which was missing until the strip was
  gone over control by control: every other value had a name for a link to use and that one had
  none. Not the Duck from picker, which names a track rather than a value, for the same reason a
  take picker cannot be pointed at. A mixer link is the song's rather than the desk's, because a
  track only exists in a song. Touching a strip anywhere picks its track, tunnelled so grabbing a
  fader picks the strip on the way past rather than instead of moving it, and it goes through the
  pattern cursor rather than a second answer beside it: the mixer, the pattern, the chain and the
  automation then agree without being told about each other
- **It was `ControlScope.Focused` first and that could not be used.** One shared set of templates
  for every strip, all of them meaning "the track I am on", on the reasoning that a link per
  strip would be eight links to make and eight to remember for a desk that has one fader. That
  reasoning names its own hardware assumption, and a nanoKONTROL2 breaks it: eight faders, eight
  knobs and twenty four strip buttons, where the whole point of the desk is that fader three is
  track three. And it was not merely unhelpful, it was impossible: two links following the cursor
  have the same target, so `SameTarget` read the second as a replacement for the first, and
  pointing fader two at TR-02 quietly unlinked fader one. Reported as "controller strip 2 also
  points to track 1 and I cant really control link that way", which is exactly what it did
- Worse, it disagreed with the layout a device already gets before anybody points at anything,
  which pins fader three to track three. **Two ways of doing one thing that answer differently is
  the fault underneath the fault**: whichever was right, they could not both be. `MixLinks.On`
  is the one maker now, `TrackStripViewModel` holds its own six, and the master needs no special
  case because it is strip -1 there as it is everywhere else
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
- Polyphony is two features sharing a word and both are built. `docs/polyphony.md` is the plan
  and now the record. The numbers came off the Renoise 3.5.4 install rather than from memory
- **A track is as many voices as it has note columns, and a note column is a whole cell again:
  its own note, instrument, volume and effect.** One to eight, one by default, so every song
  written before this is exactly what it was. Renoise allows twelve; eight because every column
  is width on the screen whether or not anything is in it, and a track with twelve is a pattern
  where you can see two tracks
- The count belongs to the song's track rather than to the pattern, which is Renoise's
  arrangement and right for the reason the track count is the song's: a part is played on so
  many voices whatever pattern it is in, and counts that varied per pattern would make copying
  a track between patterns a question with no good answer. `Song.NoteColumns` is the list and
  every pattern is given it whenever it moves
- The pattern is still one flat array of value types. What changed is the stride: it is the
  row's total column count rather than the track count, and `_starts` is the running total so a
  cell's place is an addition rather than a walk. `MoveTrack` rebuilds the block rather than
  shuffling it in place, because two tracks need not be the same width any more and a move is
  no longer a swap of equal pieces
- **The file's first column keeps the three-field form it always had**, and only a column past
  the first is written `line:track:column:cell`. Not tidiness: a build that predates note
  columns splits an entry into three and reads the third field as a cell, so writing the column
  number into every entry would leave an older copy of this application opening the song and
  finding every cell unreadable. This way it reads what it can play and leaves behind what it
  cannot. Old songs load untouched, no migration and no version flag
- **The volume column is 0x00 to 0x80, which is 128 steps, and it used to be 64.** MIDI has 128
  velocities and the old FastTracker scale could hold half of them, so every second velocity
  landed on the number below it and there was no way to tell a rounding from a hand. A velocity
  is now written in unchanged: what the pattern shows is the number the keyboard sent, and it
  can be read back against whatever the keyboard says it sent, which is the whole point of the
  width. 0x80 is the one level above anything a key can produce, typed rather than played, and a
  key at full velocity is 0x7F, a fifteenth of a decibel under it. It also made the column
  readable: full is 80 rather than 40, so a normal hit reads 5C instead of 2E, which is a number
  nobody could place against a full of 40
- Every song already written is on the old scale and is doubled on the way in, which is exact
  rather than a rescaling, since 64 is precisely half of 128. `IVolumeScale` is that rule and
  `SongDocument.Version` is what asks for it: 3 is this build, 2 was the patches moving into the
  container, and 1 is the default for a file that does not say, deliberately, because a song
  with no version in it is older than the field and reading it as current would skip the
  conversion silently. The V command is doubled with the column, since the two set the same
  thing and the effect wins where both are written: leaving one on each scale would mean 40
  being full in one column of a cell and half in the next
- **Pattern or Song is a transport setting and is answered on the next line, not on the next
  pass.** It was taken once when a pass started, and the picker only moved the ghosted
  neighbours, so switching to Song while a pattern was looping did nothing whatever until the
  transport was stopped and started again. From the outside that is a song that will not move
  past its first pattern, which is exactly what it was reported as. The clock reads
  `TrackerPlayer.Mode` on every line now, and the mode is volatile, since the clock thread reads
  it and the drawing thread writes it
- **A loop range over the order is a strip down the left of the list, dragged.** Renoise's loop
  column, and the manual's own gesture for taking one off: "to remove a loop just click on a
  single slot twice". A column of its own rather than a mark on the row, because a range is
  drawn by dragging and a drag needs somewhere to start that is not the row itself, which is
  already how a slot is moved. The strip is drawn faintly on every row, so there is always
  somewhere to start
- **A range loops whatever the loop switch says**, and that is the one place the two could have
  been made to agree and should not be. Marking a range is somebody saying "go round these" in
  as many words; the switch is a standing preference about what happens when there is nothing
  else to play. A range that did nothing while the switch was off would be a mark on the screen
  with no effect and nothing to explain it
- It is answered only at the last slot of the range, so playing from before it runs into it and
  then goes round, and playing from after it is not dragged backwards: somebody who starts the
  transport past the range meant to hear what is past it. It lives in the song rather than in
  the settings, unlike the switch, because it is about a piece of the music, the eight bars you
  are going round while you write the solo, and it is worth still being there tomorrow. Renoise
  keeps it in the song for the same reason. Absent in an older song file, which reads back as no
  range, which is what that song had
- **Loop is in the bar beside the picker, because the two are one question**: the picker says
  what the end is, the end of this pattern or the end of the order, and the loop says what
  happens when it is reached. It was true on the player from the beginning and nothing ever set
  it, so everything played round for ever and that was a default nobody had chosen. Live and
  volatile, like the mode, and remembered between runs beside the other two tracker preferences
  rather than in the song: it is about how you are working at this moment, and a song handed to
  somebody else has no business telling their transport what to do
- The sequencer itself was never wrong about any of it: it walks slots rather than patterns, so
  the same pattern in two slots is two passes over the same cells. `Tests/SongOrderTests.cs`
  walks it the way the clock does, with no audio and no window, which is how that was ruled out
  in a minute rather than by listening
- **The order list copies a pattern and takes a drag.** Copy pattern is a copy and not a second
  slot pointing at the same one: the order already allows a pattern twice and that is what you
  want for a part that really repeats, while this is for the case where the second one is about
  to become different. `Pattern.Clone` takes the cells and the automation lanes, so nothing moves
  together afterwards, and the copy is named the way a new pattern is so the two ways of getting
  one cannot end up with two ways of naming one. It lands in the order right after the slot it
  was copied from, because copying is almost always the start of a variation on the part you are
  listening to, where a fresh empty pattern still goes on the end
- A slot dragged up or down the order moves the slot and not the pattern, so a pattern in the
  order three times has three slots and only the dragged one is touched. `OrderDragData` is a
  format of its own rather than the track one with a flag on it, for the reason the drag contract
  already gives: an order slot and a track are both a number, and one format would make dragging
  a track onto the order list appear to work. A drop between the rows or below the last is read
  as the nearest row, since somebody dragging to the bottom of a list means the bottom of it
- **And undo of any of it looked dead, which was a fault older than either.** `Pour` puts the
  song back but never rebuilt the order list, and the list holds strings rather than the order
  itself, so nothing told it the numbers underneath had moved: the song went back and the rows on
  the screen were the ones from before. Adding a pattern and removing a slot both had it, and it
  is the kind of fault that reads as the feature not working rather than as the picture being
  stale. The picked slot is held inside the order it now has, since an undo can leave a shorter
  one than the slot that was picked
- **Dragging a block out with the mouse was unusable, and the cause was the centred cursor
  rather than the pointer handling.** A press moves the cursor, the cursor is kept on the middle
  of the screen, so the pattern scrolls under a pointer that has not moved at all. In the grid's
  own coordinates that scroll reads as the hand having flown across the page, and it happens
  between the press and the first movement every single time. Then each movement of the drag
  moved the cursor again, which scrolled again, so the next movement landed further on than it
  was aimed at and the block ran away down the pattern on its own. The old rule, that a drag
  begins once the pointer is over a different cell from the one it was pressed on, could not
  survive any of that: a row is under twenty pixels tall
- Three things, and each one is needed. `PatternGrid.Grabbed` says the hand has hold of the
  pattern, from the press rather than from the drag, and `FollowCursor` does nothing while it is
  true. The drag threshold is measured in the window's coordinates, which do not scroll, and is
  `IPointerDrag`: six pixels, a little past what a hand does by accident. And the cell test is
  kept beside it rather than replaced, since the two answer different questions and a drag has
  to pass both
- The page catches up on `Clicked`, which is a press that left no block behind it, and not on
  the cursor moving, because the cursor moves throughout a drag as well. So a click centres its
  line a fraction of a second later than it used to and a drag leaves the view where it is:
  yanking the pattern about the moment somebody lets go of a block they have just drawn moves it
  out from under the eyes that drew it
- Quantizing is offered as note values and not as line counts. A line count means nothing on its
  own, since 4 lines is a beat at four lines to the beat and half of one at eight, and the old
  menu's fixed 2, 3, 4, 6, 8 and 16 asked for that arithmetic before you could tell which entry
  you wanted. `IQuantizeGrid` works the list out from the song's lines per beat and drops
  anything that does not come out whole, which is what earns a setting its triplets rather than
  handing them to it: at four lines to the beat the list is 1/16 through 1/1 with no triplets,
  and at six it is 1/16T, 1/8T, 1/8, 1/4T, 1/4, 1/2 and 1/1. Each entry says what it comes to in
  lines, because that is what the pattern will actually do
- Both of the sequencer's memories are per column. The volume has to be, or one voice of a
  chord would set the level of the others; the instrument is Renoise's arrangement and the only
  one that holds up once a column is a voice, since a blank instrument column means the last
  one *this voice* played. A song with one column a track cannot tell the two apart, which is
  every song written before now
- `NoteColumns` is one walk shared by three places that would otherwise each keep their own:
  where a cell sits in the pattern, where it is drawn, and where the next press of Tab lands.
  Written out three times those would eventually disagree, and the way that fails is a click
  landing on a cell other than the one under the pointer. `PatternMetrics.TrackWidth` is per
  track now and every horizontal question is a walk from the left rather than a multiplication
- A note played while another key is still held goes to the next column of the same track, so a
  chord lands across one line and the cursor steps down once. Renoise's rule. The held-note
  counting is in the view model, because a hand on the hardware and a hand on the letter rows
  are the same hand, and the letter rows needed a key-up they had never had: a note typed into
  the pattern had no release at all, which was enough while a track held one note. It ends the
  chord and not the sound, since a note played by hand runs its own length here
- **Clearing a track gives back the note columns it grew.** A track that widened to three while
  a chord was played into it stayed three wide once the chord was deleted, and every column is
  width on the screen, so emptying a track has to be allowed to give the room back. Clear track
  and Clear pattern both do it. By what the whole song uses rather than what the pattern in
  front does, since the count is the song's: `Song.ColumnsUsed` walks every pattern, because
  narrowing on one pattern's emptiness would throw another pattern's chords away and a song may
  not lose music because a track was cleared somewhere else
- The cells first and the room after, which is the order the two steps have to be pushed in.
  Undo then widens the track back before it puts the notes into it, and each press does
  something you can see; the other way round, the first press would put notes into columns the
  song no longer says are there
- **A chord is written in pitch order, not in the order the fingers landed.** A chord is not
  three simultaneous events: it is three events a few milliseconds apart in whatever order the
  hand happened to arrive, so appending each note to the next free column recorded the same
  shape differently every time it was played, E G B on one take and E B G on the next. Each
  note goes where its pitch belongs and the ones above it are pushed along, which is
  `IPatternEdit.EnterChordNote`. More than tidiness once the new note action is anything but
  cut: a column is a voice and it carries across chords, so a column that is the bass in one and
  the top of the next has a voice leaping about inside it, releasing and sustaining across the
  leap. A chord with nowhere left to go drops its highest note, which is the one that falls off
  the end when the rest are pushed along
- **And the track widens itself to fit the chord**, which is `Song.RoomForChord` and is the
  difference between the feature working and the feature being invisible. A track shows one note
  column until somebody says otherwise, so without this a chord recorded into a fresh track puts
  its second note on top of its first and keeps whichever finger was last down: what you hear is
  three notes and what is written down is one. Somebody playing a chord has already said what
  they want, and making them find a menu before they are allowed to record it is the wrong way
  round. One column at a time, stopping at eight. No undo step of its own: the notes leave one,
  so undo takes the chord off and leaves an empty column behind, and a step here would make a
  three note chord cost three presses to undo, two of which appear to do nothing
- The pattern's cells and the two arrays that say where they sit are one object, swapped as one.
  They were three fields and the clock thread reads all three to place a cell, so a pass running
  while somebody added a track or a note column could read the new running total against the old
  array and walk off the end of it. Widening a track from inside note entry is exactly that case,
  which is what made a pre-existing race worth closing rather than noting
- A selection is still by track, so it covers all of a track's columns: copy, cut and transpose
  carry the whole chord and `PatternBlock` holds every column of it. Taking hold of one voice of
  a chord is the piece that was left, deliberately and written down in `docs/polyphony.md`: a
  selection is something people rely on and it is worth doing on its own
- **What a new note does to the one still sounding is the instrument's to say.** `VoiceEnding`
  is cut, release or sustain, on `TrackerInstrument.NewNoteAction`, cut being what a tracker has
  always done and therefore the default: nothing anybody had already made sounds any different
  for this existing. On the instrument rather than on the track because it is a fact about the
  sound, a piano overlapping and a bass not, wherever either is played, which is also why it
  travels with a preset. Renoise's three, for Renoise's reason: Impulse Tracker's fourth, Fade,
  needs a fadeout rate no patch here has. `TrackMixer.MakeWay` is the whole of it for voices,
  since `IVoice` already had both endings as two methods. The same note arriving where it is
  already sounding is cut under all three, because two copies of one note are a retrigger
  everywhere in music and letting them pile up is how a sustaining part walks into `MaxVoices`
  and starts stealing notes somebody meant to hear
- A kit answers the same question with its choke groups and is left out of it: a crash has to
  ring under the snare that follows it. So BongaBong is the one machine with no `new_note` on
  its face, and the pad overload is the one place in the mixer that still makes no room at all
- **A plugin cannot be asked what it is holding, so the host writes down what it said.**
  `HeldNotes` is that record, one per track and one for the audition slot. Without it the only
  thing a host can say is all notes off, which is right for one note a track and takes a whole
  chord down to end one note of it. Bounded at sixteen and stealing its oldest when it is full,
  which is the answer `MaxVoices` gives and for the same reason: a limit that grows is a limit
  that fails further away, on the audio thread, after somebody has left a part sustaining for
  an hour. Every method that lets go writes the notes out to the caller rather than ending them
  itself, because the mixer holds a lock while it decides and may not hold one while it talks
  to a plugin, and that is also what lets the record be put a question to without a plugin, a
  process or a sound card. Where nothing is remembered the whole plugin is still asked to let
  go, which is what that path always did and is worth keeping: the record is what this side
  said, and a plugin sent a note by anything else is exactly what a per-note off cannot reach
- Two things fell out of that record and were taken. A note played by hand on a plugin piles up
  like every other audition now, each let go of at its own moment rather than the panel holding
  one moment for whatever it last played: a chord is several keys and they are not pressed at
  one instant, so one moment for all of them meant the first key of a chord outliving its own
  hold by however long the hand took to finish the chord. And a key coming up ends that key's
  note, where before it ended nothing, since there was no way to name one note and
  `LetPreview`'s plugin branch did not exist
- A controller with a screen is written to: `ArturiaDisplay` puts the parameter's name, its
  reading and a value bar on it while a knob is turned, and the standing text is "JingleBox2"
  over the open song's name. Arturia's own system exclusive
- **Which controller has a screen is a fact in its file, and used to be a thing nobody asked.**
  The old rule was that every device ticked as Controls got written to, on the reasoning that a
  controller which is not listening costs a few bytes down a port nobody reads. That holds right
  up until the device is listening and is not the one the message was written for: these bytes
  are Arturia's write-a-setting, aimed at where a MiniLab keeps its screen, and what they do on
  an MPD218 is not knowable from here. `IControllerScreen` is the contract, `ControllerScreens`
  decides who hears what, and `IControllerProfiles.ScreenOn` is the question. A device whose file
  says nothing has no screen, and a device with no file has none either, which is the same rule
  the rest of a profile keeps
- Two lines of words and a reading is the whole contract, because it is the least of what any of
  them can do: a MiniLab draws a value as a ring and a Mackie display cannot draw at all, so
  `MackieDisplay` says the name and the reading and drops the picture. `ScreenKind` carries no
  numbers, since a value on it holding Arturia's 0x03 would make every other screen hold it too;
  what a byte has to be is each protocol's own business
- **A KeyLab mkII takes the same screen messages a MiniLab 3 does**, at the same address: write a
  string to preset 02, param 60, control 01. Nobody could have known that from a document, and
  it took writing to it, because reading that address answers 7F, nothing there. It is the
  MiniLab's Selected Preset Name trap again: a field can be write-only, so a sweep finding
  nothing proves nothing. The sweep is worth recording anyway, since it is what made the write
  worth trying: every string id on the device answers, so the whole space can be walked in
  seconds, and the only strings on a KeyLab are the control names
- And it is on the **DAW port**, not the main one, which was settled by writing to each port on
  its own with somebody watching the screen. The opposite of the MiniLab, whose screen is on the
  main port and not the one named for Analog Lab. Neither is guessable, both were measured, and
  that is why the port is in the file beside the protocol
- **It shows the first line of the message and ignores the second, and it must still be sent the
  second.** That is not a contradiction, it is the shape of the thing: the message is taken whole
  or not at all, and one with the second chunk trimmed off renders nothing whatever. This cost an
  afternoon, and the way it cost it is worth remembering because it will happen again. The screen
  worked, somebody reported that only the top line appeared, and the obvious kindness was to stop
  sending a device the half it cannot draw. That change broke it, and because the screen had by
  then also been left holding rubbish from a hunt for the lower row, the two faults looked like
  one fault and every attempt to get it back was made with the trimmed message. A power cycle did
  not help, since the trimming was in the code by then. So: no per device trimming, nothing in any
  file about how many rows a screen has, and one message for every screen. `Tests/ControllerScreenTests.cs`
  holds both halves of that, and the MiniLab's exact bytes are pinned beside them
- Two lessons under that, neither about screens. A protocol's message is a unit and a device is
  entitled to refuse a truncated one, so a caller may not economise on the parts it thinks the
  device will not use. And hunting for an undocumented address by writing guesses at it can leave
  hardware in a state nobody can name: the write to param 60 control 02 put unreadable characters
  on the screen and there is still no way to say what it set. Probing that far past a working
  result cost more than the thing being hunted was worth
- What the KeyLab's screen refuses is Mackie display text, and it refuses it while provably
  speaking Mackie: in Standard MCU its faders arrive as pitch bend, its encoders as CC 16, Play
  as note 94 and a finger on a fader as note 104, and four different model bytes of
  `F0 00 00 66 <model> 12 ...` on both its ports changed nothing on the screen. It answers no
  device query either. So it speaks the control half of Mackie Control and none of the display
  half, and its screen is Arturia's alone
- `MidiService.Send` opens an output on demand and answers false for a device with none, so a
  controller with no output still costs nothing
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
- **The log is kept between runs and is cleared on purpose, in SETTINGS.** Never on start: the
  run you most often want is the one that already ended badly, and a log cleared on start has
  thrown away the crash you restarted because of. It rolls over at four megabytes keeping one
  `.old`, so two files is the bounded cost, and each run writes one boundary line naming the
  areas and the build, which is what to search for to find where a run begins. Clear the log
  takes both files and says the boundary line again at once, so the fresh file starts the way any
  other run does rather than mid-sentence: it is why `Announce` is allowed to run twice in one
  process. Not asked about first, unlike deleting a recording, because a log is not somebody's
  work
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
  was last saved, asked first, dead unless there is both a saved copy and something to lose
- **Unsaved work is two buttons and therefore two colours.** Coloured rather than starred,
  because a star is a character somebody has to know the meaning of and it moves the button's
  width as it comes and goes, where a colour is read from across the room. `Color.Save` is
  green on Save, `Color.Discard` is warm on Cancel changes, and both light on the same fact:
  there is something on screen that is not on disc. That is the whole argument for two rather
  than one. The moment the safe button starts asking to be pressed is the moment the other one
  starts being able to cost you an afternoon, and green go against warm caution says which is
  which without being decoded. Warm and not red on the second, since cancelling is a decision
  taken on purpose and asked about first. One `Color.Unsaved` became the two, per theme as it
  always was, and the same pair is on the machine editor's header, which has the identical two
  buttons doing the identical job
- **Which page you are on is said in the accent colour and in bold, never with an underline.**
  Fluent draws a line under the selected tab and that reads as a web page rather than as a piece
  of gear, so the pipe is hidden on any strip that runs across
  (`TabControl[TabStripPlacement=Top]`) and the word carries the colour the line had. Asked of
  the placement rather than of a class, because which way a strip runs is the whole difference
  and there are four strips: a class would be four chances to forget. The line **stays** on a
  strip that runs down the side, which is not an inconsistency: down a column the words are a
  list, the mark is at the head of the row rather than under it, and it is what tells a selected
  row from a hovered one
- Colour alone was not enough and bold is the second signal. On a light theme the accent and the
  ordinary lettering are near enough in weight and darkness that the strip read as six words
  with nothing chosen, which is worse than the underline was. It costs two pixels of drift
  across the whole strip, measured, which is nothing beside the star this codebase refused on
  the Save button
- The colour is `TabItemHeaderForegroundSelected` and its `PointerOver` and `Pressed` twins in
  `Themes/Base.axaml`, all three pointing at `Color.Accent`. Three because Fluent sets them
  inside the tab's own template where a style on the TabItem cannot reach, and setting only the
  first meant resting the mouse on the page you are on took the colour straight back off it.
  There is nothing to feed back about hovering somewhere you already are
- **Dark and Light are the plain pair and are one theme in two lightnesses.** They were green
  and blue respectively, for no reason anybody wrote down, which the plain pair least of all can
  afford. Blue now, `#0B6BFF` and `#4A93FF`, deeper on the light half and brighter on the dark.
  Citrus, Ember, Industrial, Neon and Orchid are the coloured themes and each is its own thing,
  which is why the same argument does not reach them
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
- `Controllers/Profiles/nanokontrol2.json` was the one file here written from somebody else's
  reading of a device rather than from the wire, and on 2026-08-31 the device arrived and it was
  read. Korg's parameter guide has a page per control type explaining what CC Number means and
  never prints one, so the numbers had come from Mixxx's mapping for the device as shipped. All
  fifty one agreed. Sliders 0-7, knobs 16-23, solo 32-39, mute 48-55, rec 64-71, transport 41-46
  and 58-62, and identity Korg `42` family `0113` member `0000` on firmware 1.3. They hold in CC
  mode, which is the factory mode, and mean nothing in the five DAW modes where the same controls
  speak that DAW's protocol. It buys the most of any file here because the device is the plainest
  surface anybody makes: eight faders on eight track levels and eight knobs on the panel in front
  of you, working before it is unwrapped
- **What the scene added that no community list carries is the shape of a press.** Every one of
  the thirty five buttons reads mode CC, behaviour Momentary, off 0 and on 127, so a button is a
  key with two halves rather than a switch that stays where it is put, and the LED mode is
  Internal, which means a light follows the host rather than the press. Every knob and slider is
  enabled over the full 0 to 127 and every controller group takes the global channel rather than
  naming one. None of that is a number a mapping file would ever list, and all of it decides how
  the thing feels
- **And a shipped profile that is corrected does not reach anybody.** `ControllerFolder.FirstRun`
  copies with `overwrite: false` and records what has been offered, deliberately, so a file
  somebody has edited is theirs. The cost is that a file this repository fixes stays fixed only
  here: the copy in the application folder is whatever was first put there. Machines are kept up
  to date file by file against the shipped copy and controllers are not, which is a difference
  nobody decided
- **Ticking a controller for Transport used to read it as a Mackie surface whether or not it
  spoke Mackie, and the numbers collide.** Mackie's eight V-pots are continuous controllers 0x10
  to 0x17, which is 16 to 23, and a nanoKONTROL2's eight knobs are 16 to 23. So one knob turned
  was decoded twice, once as the position it is and once as a count of notches it is not, and the
  second reading threw a pan to either end. From a hand on the desk that is a knob far too
  sensitive to use, which is exactly how it was reported, and 2519 lines of the log say
  `mackie: 'nanoKONTROL2 _ CTRL' moved Pan on TR-01` beside the same number of lines saying the
  link did the right thing. An MPD218 collides on 16 to 21 and had the same fault waiting
- The gate is `IControllerProfiles.SurfaceOn`, and it is the one question here whose default is
  yes. Mackie Control is read precisely because it needs no file, so a device nobody has
  described is still read as a surface exactly as before; a device whose file describes it and
  names no protocol is not, because a file lists what a device sends and one listing fifty one
  plain controllers is saying there is nothing underneath them. The port is named as well as the
  protocol, in a `surface` block beside `screen` and for the same reason: a MiniLab 3 speaks it
  on the port named MCU and a KeyLab mkII on the one named DAW, and reading it off the port the
  knobs are on is the fault again in a different place
- **A button pointed at a switch is momentary or latching and nothing on the wire says which.**
  Both send nought and a hundred and twenty seven and they mean opposite things: a latching
  button reports its own state, so following the value is right, and a momentary one reports a
  finger, so following the value mutes a track for exactly as long as a thumb is on the button.
  Every one of a nanoKONTROL2's thirty five buttons is Momentary, which is in the device's scene
  and in no mapping list anywhere. `ControllerControl.Press` is that fact and
  `IControllerProfiles.Momentary` the question; nothing said means followed, so no controller
  anybody has already pointed at anything changes
- **And nothing that is a press may park, which took two goes to see.** Parking is a rule about a
  control that reports a position driving a value into an end: it stops a fader held against the
  top writing the top over and over, and the way out is a message going the other way. A button
  has no position and a switch has no in between, so every one of parking's terms is meaningless
  there and its answer is arbitrary. The transport showed it worst: a press writes the target's
  maximum, so the hand parked upward, and a release read through the wrap unwinding is a step
  upward too, same direction, still parked. It fired about one press in three, which is worse
  than never working because it reads as a loose cable. The two press branches are asked before
  parking now, and `Tests/ControlSurfaceTests.cs` counts three presses and their releases,
  because the releases contributing nothing is half of what has to be true
- **And under that, a switch was parking.** Parking is a rule about positions: a fader held
  against the top must stop writing the top over and over, and the way out is a message going
  the other way. A switch is only ever at one of its two ends, so the press that flips it lands
  on an end and parks, and the press that would flip it back is a jump from 127 to nought, which
  the wrap unwinding reads as one step upwards rather than a hundred and twenty seven downwards:
  same direction, still parked, thrown away. So a button pointed at a mute muted the track once
  and did nothing ever again, momentary or not. `IControlTarget.Switch` is the test and a switch
  does not park. This one was found by a test rather than reported, and it is most of what
  "linking the M and the S works very strange" actually was
- **Ticking Transport meant two different things depending on the device, and that was the
  real fault.** A MiniLab 3 and a KeyLab mkII speak Mackie Control, plain controllers or machine
  control, so the tick made their transport buttons work with nothing pointed anywhere. A
  nanoKONTROL2's play button is plain controller 41 like its mute buttons, which no dialect
  covers, so the identical tick did nothing whatever and the transport could only be reached by
  pointing a button at it by hand. Nobody could have worked that rule out from the outside, and
  it was reported in exactly those words: that is not how the transport works for the minilab3
  or the mk2
- So there is a fourth dialect and it is the file. `ControllerControl.Transport` is the legend
  printed on the button, `IControllerProfiles.TransportOn` the question, and
  `MidiTransportRouter` asks it only after its three protocols have all declined, so a device
  speaking one of them is untouched. It adds no capability the hardware lacks: the device really
  does have a play button and this says which one it is. A device with no file has no transport
  buttons, which is what it had before
- **And cycle turns looping on, which every dialect already carried and none of them read.**
  Mackie Control has it as note 0x56, a MiniLab sends controller 105, a nanoKONTROL2's CYCLE is
  controller 46, and all three were named in the router and answered with a line saying this does
  nothing with it yet. It has somewhere obvious to go: Loop sits in the tracker's bar beside the
  Pattern or Song picker, because what the end is and what happens when you reach it are one
  question, and a control surface puts its cycle key in the transport row for the same reason.
  `ITransportKeys.Loop` is the fourth word and `ITransportDeck.Loop` is defaulted to nothing,
  which is not laziness: a take on RECORD and a bank of pads have nothing to go round, so a cycle
  key pressed on those pages should do nothing rather than something invented. Tap tempo is the
  last one named and left alone
- **The transport had nothing on it to point at.** `Pointable.Offers` appeared in exactly one
  view, the mixer's, so the pointing gesture found nothing on the bar and no hardware button
  could be linked to play. Ticking the device for Transport looks like the answer and is not:
  that switch reads the three protocols, and a nanoKONTROL2's play button is a plain controller
  41, where the plain dialect read here is a MiniLab's 105 to 109. `Midi/TransportLinks.cs` is
  the four keys, `ControlKind.Transport` the kind and `ITransportPresses` the seam, which is
  four keys where `ITransportKeys` is three: a protocol folds pause into stop because it cannot
  send one, and a person pointing at the pause on the screen means the pause on the screen
- The bar offers its own, the way a machine panel does and for the same reason: it is one
  control drawing four caps, so only it knows which is under the pointer, where `Pointable` hangs
  one mapping on one control. It calls `LinkKey.Watch` too, which is what makes Ctrl+Shift+M mean
  something on a page that is not the mixer. A transport key is `ControlScope.Fixed` and belongs
  to the desk rather than to a song: there is one transport, and a link that travelled in a file
  would arrive on somebody else's machine telling their hardware what to do
- `Controllers/Profiles/keystep-pro.json` is the one that says a device cannot be described, and why.
  Its five encoders have no factory controller number: the manual's Controller page marks a
  default for channel, mode, min and max and marks none for CC, so the omission is deliberate
  and there is nothing to write down even in principle. Measuring one would report what its
  owner assigned. Two facts are in the file instead. The Looper strip sends CC 9 with its MIDI
  send off until a menu is visited, which reads as broken hardware. And its three transport
  buttons send MIDI Machine Control, which is why that is read here at all
- `Controllers/Profiles/keylab-mkii.json` is the first file here filled in without anybody
  touching the hardware. A KeyLab mkII 49 arrived on 2026-08-29 and answered Arturia's own
  settings protocol for every field of every control, so the whole of User mode came back over
  SysEx in one pass: ten presets, three banks apiece, nine encoders, nine faders, nine select
  buttons, sixteen pads and ten DAW command buttons. `Controllers/Profiles/mpd218.json` had to
  be measured knob by knob because Akai's equivalent protocol answers ETIMEDOUT, and that is
  the whole difference between the two files
- What it says, and every line of it is the device's answer rather than a reading of the manual.
  All ten User presets carry the same numbers, so a preset is not a program here and the bank is:
  bank 1 is Arturia's Analog Lab layout, encoders 74, 71, 76, 77, 93, 18, 19, 16, 17, which is a
  MiniLab 3's Arturia program with a ninth on the end, and it counts notches. Banks 2 and 3 are
  a plain absolute layout, encoders 35 to 43, and are identical to each other, which is written
  as one program rather than two: two programs sharing every number would leave neither able to
  be told from the other. Every control's channel reads User, meaning it follows the global User
  MIDI channel rather than naming one, so the file claims no channel at all
- The reading was checked against `sysex-controls` itself, page by page on screen, because a
  reader written from somebody else's source is worth exactly one cross-check. Firmware, global
  settings, the preset name and Column 1's knob down to its acceleration all agreed
- Two ports, not the three that were guessed here while nobody owned one, and no MCU port: they
  are `MIDI` and `DAW`, and the second carries whichever protocol Global Settings, DAW Map names.
  That setting read Live, which is Ableton's and is not read here. Set it to Default MCU and this
  application has the whole surface today, since `MidiMackieRouter` and `MackieSurface` are
  already written. Its faders offer Pickup or Jump, which are this application's Takeover and
  Jump under other names
- What cannot be described that way is DAW mode, and that is a fact about the device rather than
  a gap in the file. In DAW mode the front panel is hard mapped, the settings protocol says
  nothing about it, and the numbers belong to whichever of the nine DAWs is chosen. The same
  holds for Analog Lab. So this file describes User mode and says so
- The pads are the one thing that differs between presets and the one thing the file cannot hold,
  since a pad sends a note and `ControllerControl` is about controllers. Sixteen on channel 10,
  gated: User 1 runs 36 to 51 straight up and User 2 to 10 have the rows the other way about,
  48 to 51 along the top and 36 to 39 along the bottom. It is in the file's note instead
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
  The device's own numbers were measured the same way and are in `Controllers/Profiles/mpd218.json`: all
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
  Akai, family 0034, member 0019. That is in `Controllers/Profiles/mpd218.json` now and it is the one name
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
  restart and no replugging. `Controllers/Profiles/minilab3.lua` is the shipped example and does one real
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
  the cursor is in, through its inserts, and moves that track's meter and the master's, which is
  the whole point of auditioning: it tells you what the part will sound like. Through its fader,
  its mute and its placement as well, and for a long time not: the level and the pan were applied
  to a pattern note's voice when it started, in `WithMix`, and an audition went straight past it,
  so a muted track still sounded under your hands and a fader anywhere but unity auditioned at a
  level the part would never play at. Invisible on a fresh song, because a strip opens at unity,
  which is how it sat there
- Which needed the player to know which song is open, and it only ever learnt that when a pass
  started. `Use` is that, called when the song changes rather than when it is played, and it
  fixes a second thing quietly wrong for the same reason: `ApplyMix` reads the same field, so a
  fader moved with the transport stopped reached nothing that was sounding
  Only the plugin path did that; everything else, synth, mono synth, sampler, kit and recording,
  went to the loose audition bus and moved no track meter at all. Every preview takes the track
  now and defaults to none. The rack's keyboard still names none, and rightly: the instrument it
  is playing may not be in any song, so it goes through nobody's fader
- A note played by hand holds for a fixed moment where nothing is going to let go of it, which
  is a key that was clicked, and for its own length on a recording: a take cut off part way
  through is not the sound the instrument makes. `SampleVoice.WindowSeconds` is that length, and
  the hold is passed back up through `Audition` and `NotePlayed` so the key that lights and the
  cursor that runs last exactly as long as the sound. Auditions pile up, as a keyboard does,
  unless the instrument says `OneVoice`, which cuts what it was sounding first
- **A key that is really held sounds while it is held**, and the moment is a safety net rather
  than the length of the note. Four tenths of a second was the length of every note played on the
  tracker's keyboard however long the key was down, which is the difference between what a chord
  sounds like under your hands and what it sounds like coming back: three short stabs against
  three notes ringing until the pattern plays something else. It is what a chord makes obvious
  and a single note hides. Both of that page's keyboards let go now, the hardware because it
  always sent the other half of the press and the letter rows because they were given a release
  they never had, so ten seconds is only ever reached when something went wrong
- **And nothing on that page could be let go of at all**, which is what made the fixed moment
  the only thing ending a note. `LetAudition` and `CutAuditions` asked for a voice carrying no
  track *and* the right audition id. Every preview takes the track now, so no voice on the
  tracker's keyboard has ever answered that test since. The id is what says which panel is
  holding a note, and the track was thrown away on top of it; matched on the id alone, a key
  coming up reaches the note it started
- A key already down arriving again is the letter row repeating, which is how a column is filled
  quickly and stays that way. It is dropped while another key is down, because there it is a
  hand resting on a chord rather than somebody filling a column, and every repeat sprayed a
  single note down the pattern under the chord that had just been written. Hardware never
  reaches it, since a key that is down cannot be pressed again
- **`docs/threads.md` is the thread contract, and it is written down because the alternative
  has already cost real work.** Which threads this application has, what each may touch, and the
  rule at every seam two of them meet. Around eleven on a busy session and five that matter: the
  drawing thread, the sound card's, the one that mixes ahead, the tracker's clock and the MIDI
  port's. Each of those seams now says so on its own interface as well, since a reader holding
  `ITrackMixer` should not have to find a document to learn that two threads can be in it
- Every threading fault this codebase has had was one shape, and the document says so in those
  words: a rule that was true when it was written, held in somebody's head, and quietly untrue
  once a second caller arrived. Never a lock forgotten. Always a lock guarding the wrong thing.
  The mixer guarded its state and not its arrays; the pattern was three fields where the clock
  needed one object; a control target read where a parameter was rather than where it was going.
  So the question to ask of a new seam is not "is this locked" but **is what they share a value
  or the shape of something**: a value wants a lock, a shape wants to be one object swapped
  whole
- And on the audio path the loser refuses rather than waits. One quiet block is a click; a
  blocked callback is every stream on the device stuttering. That rule was already there for a
  queue that has run dry and is now the mixer's answer to a second renderer as well
- **One thread renders the mixer at a time, and a second one asking is given silence.** It
  crashed with an index outside the bounds of the array, on the audio thread, inside the loop
  that adds the preview onto the loose bus. That loop is only where it showed: everything the
  mixing uses is sized from the frame count it was called with, and `EnsureBusses` reallocates
  the bus, the loose bus and the scratch whenever that count changes, so two threads rendering
  at once with different counts is one of them shortening the arrays the other is halfway
  through
- There are two threads for one moment. The sound card's own thread renders in step, or a thread
  of its own renders ahead into a queue, never both, except while one is being swapped for the
  other: `SynthOutput.StopMixingAhead` waits two tenths of a second for the ahead thread and
  then carries on regardless, which is right, since a plugin holding it up must not hang the
  application, and it leaves that thread still inside the mixer while the sound card's thread
  starts. Changing the output device or the render-ahead setting is exactly that moment, which
  is why it turned up an afternoon into a session rather than at startup
- A buffer shorter than the frame count claimed was the same crash from the other direction, and
  it was found while writing the tests rather than by it happening. The top of the method clamped
  its clear to the buffer and the three stages after it did not, so the bus mixing, the loose bus
  and the master all wrote past the end. Half a fault guarded is worse than none: the guard that
  is there reads as the question having been asked. The block is decided once now, held to what
  the buffer can take and rounded down to whole frames
- The guard is on `TrackMixer.Render` and not on the thing with the two threads in it, because
  there it is true whoever calls and it can be put a question to without a sound card, which is
  `Tests/MixerRenderTests.cs`. Refused rather than waited on, which is the rule this file
  already keeps for a queue that has run dry: one quiet block is a click and a blocked callback
  is every stream on the device stuttering. Its own lock rather than the mixer's state lock,
  which is taken and let go of several times during a render and by callers who are not
  rendering at all: sharing them would have a note played by hand wait behind a block of audio.
  A thread that would not stop is now said in the log, since it means a plugin took longer than
  a fifth of a second over one block and that is worth knowing on its own
- `Tests/MixerRenderTests.cs` is twenty one tests on that path and almost none of them are happy:
  it is the one place in the application where a fault is the process gone rather than a message
  on the status bar, so what it asks is never "does it sound right", it is "what does it do when
  it is lied to". Threads at six block sizes at once, the mix being edited while two threads
  render it, no frames, a negative count, a count that overflows when doubled, a buffer with no
  room, plugins that throw, that write past their block, that hand back NaN, that never return,
  inserts that throw on a track and on the master, more voices than the mix holds, and track and
  column numbers off both ends. Each guard was checked by taking it out and seeing which tests
  noticed: the render guard four, the block clamp four others, the instrument catch two, the
  master insert catch one. A test that passes with the fix removed is testing nothing
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
