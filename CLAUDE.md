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
- `SoundDevices/` - What is on the rack, in three levels. The top of the folder is the device level,
  which is what both worlds share and says nothing about notes or audio: `RackRegistry<T>` and
  its two-folder rule, `RackArchive<T>` and the zip, `RackSoundDevices<T>` and what was found for the
  run, `PanelImages`, and in `SoundDevices/Interfaces/` the five `IRack*` plus `IPanelImages`,
  `IDesignProject` and `IDesignWorld`. `SoundDevices/SoundMachines/` is the soundmachine half: the
  registry, the projects, the rack itself, the preset file and library, and the values adapters
  that put a face over an engine. `SoundDevices/SoundEffects/` is the effect half: its own registry,
  projects and archive, and the engines.

  All of it lived under `Tracker/`, which was true of the first file on the day it was written,
  when a machine was a thing a song played and nothing else, and had been false ever since: not
  one file in any of the three is about a pattern, a clock or a song. What stayed behind is what
  a song owns, and `TrackerInstrument` is the whole reason the line falls there, since **a
  soundmachine used in a song is an instrument**: `SoundMachine` is the device, `SoundMachinePreset` is a
  sound you start from, and neither is in a song.

  The namespaces are `JingleBox2.SoundDevices`, `JingleBox2.SoundDevices.SoundMachines` and
  `JingleBox2.SoundDevices.SoundEffects`, which is the folder said again, and that is the rule: **the
  folder and the namespace say the same thing**. Not under `Rack.*`, although the rack is what
  all of it is about, for two reasons that agree: `Rack.SoundDevices` and `Rack.Controls` are the
  published assemblies and this is the application, and a source folder called `Rack` beside the
  lowercase `rack/` that holds what ships is the `controllers/` fault again, one folder on
  Windows and two here. The word is **soundmachine** rather than machine because it is one of the
  four this file is arranged around, and where a type is about the preset rather than about the
  file format it says so: `SoundMachinePreset` is the record and `SoundMachinePresets` is the
  shelf it comes off. That shelf was in a file called `MachinePreset.cs` beside a record of
  almost the same name, which is what made it look like the same thing written twice; it is one
  type to a file named after it, like everything else here
- `Tracker/` - Song model, sequencing, playback, `.jibx` song files, and the instruments a song
  owns. The rack it used to hold is in `SoundDevices/`
- `Tracker/Synth/` - The synth voice: waves, ADSR, modulation, and the preset bank
- `ViewModels/` - MainViewModel (orchestrator), PadViewModel (per-pad), MidiViewModel
- `Views/` - Avalonia user controls (UseView, PadsView, TrackerView, RecordView, SettingsView)
- The tab along the top is **DESIGNER**, and it holds both worlds on two tabs of its own,
  Machines and Effects. It was MACHINES. The page is where a machine's face
  is laid out, which is a job rather than a list of things: MACHINES read as the place your
  machines are kept, and the place they are kept is the rack in the tracker and the registry in
  SETTINGS. The types keep their names, since `MachineEditorViewModel` is what the thing is
- `Themes/` - XAML resource dictionaries (Dark, Light, Neon, Industrial)
- `native/` - BASS audio library binaries for win-x64, linux-x64, linux-arm64

### Data Flow

- **Playback**: PadViewModel → BassAudioEngine → BASS library → PadPlaybackChanged event → UI update
- **Config**: PadViewModel property change → MainViewModel → ConfigStore.Save() → JSON file
- **MIDI**: MidiService.MessageReceived → MidiDispatcher → (MidiControlRouter.Pads → ControlTargets → PadTriggerAdapter → PadViewModel.TogglePlayCommand) or (MidiNoteRouter → TrackerNoteAdapter → TrackerViewModel)
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
- The pads are pointed at like everything else. A button on a pad box is learned by resting the
  pointer on the pad on FIRE and hitting it, the link lands on the same layer every other link
  does, and it turns up on MIDI CC as one card headed Pads with a line per pad, the way the mixer
  is one card for every strip. FIRE carries the same Menu the mixer's card has, in its own upper
  right corner. `docs/pad-links.md` is the design and the record.

  It replaced a mapping table in SETTINGS with its own storage, its own Learn button and its own
  matching rules in a router of its own, which was a second way of doing the one thing that layer
  is for: two ways of doing one thing that answer differently is the fault this codebase has
  already paid for once. `MidiRouter` and `MidiMapping`'s table are gone; the type is left only so
  a settings file written before this can be read, and `ConfigStore.PadsBecomeLinks` carries every
  row over once, naming no controller because the table never named one, and empties it.

  Two things are genuinely new under it. A link can name a **note**, since a pad box sends notes
  and every link before this was a knob or a button sending a controller: `ControlMapping.Sends`
  says which, absent means controller, and only the press half of a note is ever answered. And the
  pads have a door of their own on the control router, `MidiControlRouter.Pads`, because the job a
  port is given in SETTINGS still decides what it may drive: a pad box that has not been given the
  pads fires nothing whatever it has been pointed at. None of the knob machinery applies there, and
  the reason is worth keeping: the press test the other two press kinds use reads anything under 64
  as a button coming up, which for a note is a velocity, so a pad played softly would have done
  nothing.

  A fresh installation has nothing pointed at the pads, which is a deliberate change. The old table
  was filled in with notes 36 upwards on channel 1 whether or not anybody asked, and `DefaultLayout`
  has said the opposite since it was written: a pad nobody has pointed at should do nothing rather
  than something surprising. Those seeded rows mostly did nothing anyway, since the pad boxes here
  send on channel 10
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
  stream would sound everything twice. `IPanelKeys` lost `Down` and `Up` again as a result:
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
  coming up. `Tests/NoteAdapterTests.cs` is the half-choosing above. `Tests/SoundMachineKeysTests.cs`
  presses keys on a real `SoundDeviceKeys` through `IPanelKeys` and reads what is lit. That last
  one found a fault the moment it existed: `Play` no longer refused a key that was already down,
  so a letter held on the computer keyboard retriggered the machine on every repeat
- `IPanelKeys.Down` and `Up` are that light on its own, for a note that was played elsewhere:
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
- `SoundMachineRack` (SoundDevices/SoundMachines/): What you have, in
  `%APPDATA%/JingleBox2/instruments/`, one file
  each, holding your settings for it. What is retired to `instruments/retired/` on the next open
  is what is neither registered now nor anything this rack has ever been offered, which is a
  plugin somebody shelved before plugins stopped being shelved. **The second half of that test is
  what keeps your settings**: unregistering a device takes it off the rack and must leave what you
  set on it exactly where it was, for when it is registered again, and that was true of the five
  that shipped only because their ids were written into the application. A device made in DESIGNER
  has no such standing, so `shelved.txt` is what gives it one. What sits on the rack cannot be
  renamed there, since that would be renaming the device; a plugin can be deleted but takes its
  name from the VST3 or CLAP
- **Engine, machine and instrument are three words and not one.** They are the three layers of
  the same thing and confusing any two of them leads somewhere wrong, so:
  - An **engine** is what makes the sound. There are six, they are `TrackerInstrumentKind`, they
    are compiled into the application, and their numbers are in every song ever saved so they do
    not move. An engine has no face and no name a person sees
  - A **machine** is a face over an engine: a folder holding `machine.json`, its badge, its
    presets and its own `sounds`, made in the designer and travelling as a zip. **It names the
    engine it plays, in its own manifest, and its id is its own.** Any number of machines can
    name one engine, so two kits are two machines
  - An **instrument** is a machine in use: your name, your settings, its own id, stored with the
    song. Two of them can come off one machine

- **Which engine a device plays was worked out from its id, and that was the fault under half of
  this file.** `SoundMachine.SlotId` was a switch of five strings and
  `SoundEffectEngines.Built` was a dictionary keyed by three, so there could only ever be five
  soundmachines and three effects, their ids were decided here rather than by whoever made them,
  and a device designed in DESIGNER under any other id was read off disc, refused in silence, and
  never reached the registry, the rack or a song. Registering a second machine on one engine
  quietly threw the first away
- **The door for the other way was already there and was dead.** `SoundMachineProject.Engine` was
  a real property, read out of every `machine.json`, and passed to nothing. The field decides now,
  both worlds have one, and `SoundEffectProject` grew the matching property
- **The whole model is four steps and each only sees the one before it: designer, registry, rack,
  song.** A device is made in DESIGNER or imported as a zip; it is registered in SETTINGS, System,
  which is the only list that answers whether this installation has it; a registered device can be
  on the rack; and **a song can only use what is on the rack**. `Tests/DeviceFlowTests.cs` walks
  it, for a soundmachine as far as the track and for an effect one step further onto that track's
  chain, with removal at each point. Every layer had its own tests and the walk had none, which is
  how the coupling survived: each layer was right about the question it was asked
- **The two worlds differ in exactly one thing, which is what a song does with the device.** A
  soundmachine is played, so it becomes an instrument on a track; an effect is not, so it becomes
  a slot on a track's chain. Everything before that is one act done twice, and fixing the
  identity in one world and not the other was the same fault this file names everywhere else
- **The three ids that shipped are grandfathered and nothing else is.** `KindOf` and
  `SoundEffectEngines.Was` map the eight original ids to their engines and are consulted only
  where a manifest is silent, since every song, rack file and chain on anybody's disc names them.
  Compared without regard to case, like every other id here. The eight shipped manifests name
  their engines out loud now, and `Tests/ShippedEngineTests.cs` holds them to it, including that
  the engine each names is the one its id used to imply: a shipped device that quietly moved
  engines would open every song that plays it and sound like something else
- **An effect's engine is resolved from the id the chain wrote down**, since a chain writes an
  id and never an engine and that is still right. `SoundEffectEngines` takes the registered list
  to look it up in, so `PluginChainState`, `TrackerPlayer`, `PadViewModel` and
  `PluginChainViewModel` all hand it one. Without that an effect somebody made would load on the
  rack and vanish off a chain
- **The rack is in alphabetical order, sorted once in `RackSoundDevices<T>.Keep`.** It was the
  order the folders were read in, which is the disc's and is not an order; it only looked like one
  because the five that shipped had a curated reading order written into the application, the
  plainest engine first. There is no such list for a device somebody makes and names themselves.
  By name, then by id, so two devices sharing a name sit still rather than swapping between runs.
  Sorted where the list is kept rather than where it is drawn, so the rack, the pickers and the
  shelf in SETTINGS cannot come out in three different orders
- **Remove in SETTINGS did nothing, and said "Could not remove" while doing it.**
  `SoundMachineArchive` was constructed `base(registry!, paths)`, so a default-built one put a
  null straight into the field `Remove` dereferences; the catch around it reported a
  `NullReferenceException` as a failure to remove. `SoundEffectArchive` wrote `??` where the
  machine one wrote `!`, which is why effects removed fine and machines never had. The
  constructor's own documentation had said all along that one made without a registry "builds one
  and hands itself over". Both shelves now build one registry and hand it to their archive, since
  two registries over one pair of folders are two answers to what this installation has
- **The start-up pass is the most dangerous thing in this half of the program**, because it runs
  unattended on every start over the one folder holding somebody's own work, and
  `Tests/RefreshKeepsYoursTests.cs` is the seven rules it keeps. It walks only the files that
  ship, so a preset you saved, a device you made and anything else in a folder is never looked at
  and **nothing is ever deleted**. It overwrites only where the shipped file is newer, so an edit
  of yours is kept. It never opens `instruments/`, where your settings for a device live
- **The one way to lose work is to give your own device a shipped device's id**, and that is by
  design: a device is known by its id and by nothing else, so the pass brings the shipped copy
  over the top and an afternoon opens as the device that ships. Nothing downstream can tell them
  apart, since there is nothing in a folder that says who wrote what is in it. So it is caught
  where the id is chosen: `IDesignWorld.Ships` is the question and DESIGNER says so on New and on
  Save. A warning and not a refusal, because putting an edited device back over the copy that
  ships is what Save as exists for; what it may not do is happen quietly
- **A path stored under the application folder is stored so it can be found again, and that was
  true of songs alone.** The rack wrote a kit's sixteen pads and a sampler's zones as full paths
  into a home directory, and the settings wrote every pad the same way, so a folder that moved or
  an account that was renamed left them pointing at nothing with nothing said: the pads are simply
  silent. `IPortablePath` in `Files/` is the rule on its own, `{app}/` and forward slashes,
  knowing nothing about songs, devices or pads and unable to, or the thing that keeps every stored
  path honest would depend on all three. `ISongPaths` keeps the instrument walk over it, which is
  forced rather than tidy: the walk knows what a kit, a sampler and a chopped take are
- Applied in `SoundMachineRack` and `ConfigStore` now. **Packed around the write in a `finally`
  rather than on a copy**, because the rack hands out the very objects the pages are looking at
  and one left holding `{app}/` after a save plays nothing until it is read off disc again, which
  reads as saving having broken the sound. A path outside the folder is left exactly as it was,
  since it is somewhere the user chose or somebody else's plugin, and an `http://` stream goes
  through untouched without anything needing to know it is a URL. Nothing to migrate: `Unpack`
  returns an unstored path unchanged, so every file already written reads as it did and the first
  save rewrites it

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
  has no presets. `SoundMachineProject.Save` writes the manifest and only that, rightly, since it is
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
  list that gives it: `IMachineRegistry` reads the folders and `ISoundMachineProjects` holds what it
  found for the run. A machine whose id this build has no engine for is read and passed over, so
  a machines folder from a later version is harmless, and that gate is what has to move before a
  machine written by somebody else can be registered at all.

  So an instrument whose machine is not registered here makes no sound and has no panel: it is
  on that machine, and the machine is not here. It goes on naming it until the track is pointed
  at another instrument, it saves unchanged, and it shows a grey "Sampler" named for its
  engine.
  `ISoundMachineProjects.Has` is the test, asked in `TrackerPlayer` before anything sounds. Nothing
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

- A machine is on the rack while it is registered, and gone from the rack when it is unregistered
  in SETTINGS, System. It used to say always there, which
  was written before the registry existed and had been quietly untrue ever since: the registry is
  what this installation has and is the only thing that answers that. `TrackerInstrument`
  is the data type for both a machine and an instrument, but the rack's types say machine
  (`SoundMachineRack`, `MachineRackViewModel`, `RackSoundMachine`, `MachinesView`) and the tracker's say
  instrument (`Song.Instruments`, `InstrumentSlot`, `AddInstrumentCommand`)
- **The rack is what this installation has registered, in two tabs: Machines and Effects.** A
  plugin is on neither and never should have been on the rack at all: a CLAP or a VST3 is
  somebody else's program, used by a song rather than owned by this installation, and shelving
  one only put Serum in a list beside OddSkilla as though the two were the same kind of thing.
  Anything that is not a registered machine is moved to `instruments/retired` on the next open,
  which now includes the plugins that were shelved before this
- **The registry is the rack's rather than the machines', because there are two worlds on it.**
  `IRackRegistry<T>` and `RackRegistry<T>` are the two folder rules written once: what ships and
  what this installation has, `offered.txt` so that removing something is not losing it and a new
  arrival still arrives, bringing an installed copy up to date file by file with nothing ever
  deleted, and the gate that reads a folder and leaves it there when this build has no engine for
  its id. Not one of those says anything about notes or audio. `SoundMachineRegistry` and
  `SoundEffectRegistry` are its two users and are what is left when the folders are taken out: four
  answers apiece, which folder name, how a folder is read into a manifest, whether this build
  will have it, and how a shipped one is taken. A machine's is the archive, since a machine also
  arrives as a zip and has to be named around a folder that is already there; an effect's is the
  plain copy, folders and all, since a preset folder that did not arrive is the first thing
  anybody would have to make by hand. `IMachineRegistry` is gone: every member of it was a fact
  about a rack
- **The rack page and everything both worlds draw with lost the machine in their names.**
  `RackViewModel` and `RackView` are the page with the two tabs; `PanelElementViewModel`,
  `PanelElementPropertyViewModel`, `MenuOptionViewModel`, `ParameterViewModel` and `PreviewValues`
  are the designer's, which serves both; `IPanelTint`, `PanelTint`, `PanelShades`,
  `PanelColoursDialog` and `PanelColours` are a device's own colours, which an effect has as much as
  a machine. What kept its name is what only the instrument world has: `SoundMachineWindow`,
  `SoundMachineShelfViewModel`, `SoundMachinePresetDesk`, `MissingSoundMachineDialog`, `SoundMachineRack`,
  `SoundMachineProject`, and the preview parts for a kit, a keyboard, zones and slices
- **The transport answers its two keys on every window, not only on the main one.** Space and
  Ctrl+R were handled by the main window's own key handler, so opening anything else stopped them
  working: a machine's panel, an effect off a chain, a plugin's window. A transport that stops
  because you opened a knob is a transport nobody can trust. `Views/DeckKeys.cs` is that door, one
  deck for the application the way `LinkKey` is one gesture and the log is one file, hung on every
  window that opens. The rule is `DeckKeys.Wants`, which has no window in it: which key, what was
  held with it, and whether the keyboard is somewhere a key means something else, since a space in
  a name is a space. Named for the deck because `ITransportKeys` is already the four words a
  control surface sends, and two things with nearly the same name is how two things come to be
  mistaken for each other
- **The pointing gesture works on an effect's face, and had to be told twice to.** The window
  answers Ctrl+Shift+M like every other window, and the face says it is there through
  `LinkKey.Watch`, since the gate counts views that allow pointing and a face nobody counted left
  the gesture dead. Then the face has to be told the mode is on, or the mode is on and nothing on
  the screen says so: `ShowLinks` is the same three lines a machine's panel has, hung on the
  window's own opening rather than on the visual tree, which a window is the root of. What is
  offered reads `link: offering Insert EchoBox time (machine 'effect.echobox' key 'time')`
- **An effect of ours goes on a track's chain and on a pad's, beside the plugins.** The plus
  offers ours first, since that list is short and known and a plugin list runs to hundreds. What
  it puts on is the engine itself: in this process, nothing to load, no window of somebody else's
  to embed, and built for the host's own rate because that is the rate of the audio it is about to
  be handed. `IChainSlot` is the block both kinds draw as, named in XAML for the reason
  `IRackRow` is: a compiled binding needs a type and a block drawn twice would be two templates
  drifting apart
- **A chain entry says which world it came out of, and absent means plugin.**
  `PluginSlotConfig.Effect` is the effect's own id and nothing else is written: no path, since
  it is not a file on this computer, and no state lump, since everything it holds is in its
  parameters. Every chain already on somebody's disc has no such field and is read as what it was.
  An effect this build has no engine for is named rather than passed over, the same as a missing
  plugin, and the rest of the chain still loads
- **`ISoundEffectEngine` answers three questions a chain asks of anything on it**: which effect it is
  standing for, what it can be set to, and where each of those stands. The id is told to it when
  it is made rather than known by the class, because a face is a face over an engine and one
  engine may one day be behind several faces. The keys are the engine's own, so a song can be
  written down without the face: what a chain holds is an id and a handful of numbers
- **Which effect a link on one of ours reaches is the one whose face is in front, and it used to be
  the chain of the track you are on.** That is right while you are working in the pattern and
  wrong in three separate ways the moment a face is open in a window, which is exactly when a hand
  is reaching for a knob. A track's chain follows the cursor, so a face left open while an
  instrument window claims another track resolved against that other track. The master's chain is
  on the mixer and follows nothing, so it never matched the cursor at all. And a pad's chain is
  not on a track in the first place, so no answer phrased as a track number could ever have
  reached it: a knob pointed at an effect on a pad moved nothing, ever. `ISoundEffectInFront` is the
  one answer, `ISoundEffectShown` is the three things a link needs about an effect (which effect, where it
  is standing, and what its knobs stand at), and `SoundEffectWindow` says both halves, on opening as
  well as on being brought forward, since whether a window hears that it was activated is the
  window manager's business
- **And it was written into the engine rather than through the values, so the picture never
  moved.** A machine's parameter goes through the panel's own `IPanelValues`, which is what
  raises `Said` and redraws the face; an effect on a chain wrote straight into the engine. The
  sound changed, every knob on the screen stayed where it was, and from a chair that reads as a
  link that was never made rather than as a picture that is stale. `ControlTargets.Reaching` is
  the one builder both ways of arriving at an effect go through, so there is one answer to what a
  write does
- **A knob is pointed at one of ours on a chain, and the link names the effect and the key.**
  Not the slot and not the track: the same two words a machine link uses, for the same reason,
  which is that both travel. So one link drives whichever EchoBox is on the track you are working
  on, and a template made on one installation means the same thing on another. The face is offered
  from three places now, all of them the same panel: the rack, the effect's own window off the
  chain, and the designer's preview
- **A value written and read back is not the same number**, which cost a test. An engine keeps its
  knobs in single words, so a double set twice reads back a hair different and the panel announced
  every write as a change. `SoundEffectValues` narrows both sides before comparing, which says exactly
  what it means: whether this would move anything at all
- **A rack tab with nothing on it says so, in the middle of the room the panel would have had.**
  It used to fall back to whatever was drawn last, so taking the last effect out of the registry
  left a machine's panel standing beside an empty effects list, which reads as the list being
  broken rather than as the list being empty. Two sentences: what is not there, and that SETTINGS,
  System is where it is added
- **An effect is picked on the rack and its face is drawn beside the list**, in its own colours,
  with its own Menu in the corner. It is not an instrument and has no editor behind it: what its
  knobs stand at there is a bench kept nowhere, since an effect in use is a slot on some track's
  chain and two of the same effect are two sets of values. It is worth drawing all the same,
  because that is where a hardware knob is pointed at one: what a link writes down is the
  effect's id and the parameter's key, which is true of that effect on any chain, in any song, on
  any installation that has it
- **A part handed to a panel after its face was is drawn now.** `PanelView` rebuilt on the face,
  the values, the takes and half a dozen others, and not on the menu, the name badge, the zones or
  the scope. In XAML the properties are set in the order they are written, so a face set before a
  menu meant a panel built without one that never heard about it: the effect's face on the rack
  drew no hamburger, and nothing anywhere said why
- **The rack's two folders live together: `rack/machines` and `rack/effects`**, beside the
  program for what ships and under the application folder for what this installation has. What an
  installation already had at the top of the app folder is carried into the rack folder once, on
  the first run that looks for it, and moved rather than copied: what is in there is somebody's
  own, machines they have edited and presets they saved onto them, and seeding a fresh folder
  from what ships would leave all of that behind under a name nothing reads any more. Only when
  the new place is empty, so it happens once and never argues with a folder somebody has since
  worked in
- **An effect is registered, imported, added and thrown out exactly as a machine is**, and it is
  the same code doing it. `IRackArchive<T>` and `RackArchive<T>` are the zip, the staging folder
  and the swap; `RackShelfViewModel<T>` is the list in SETTINGS, System with its Import, Add and
  Remove. What each world supplies is two answers, the name of the file at the top of a folder and
  how a folder is read, so `SoundMachineArchive` and `SoundEffectArchive` are a dozen lines
  apiece. The one thing that differs downstream is where a device goes afterwards: a machine
  becomes an instrument in a song, an effect goes on a track's chain, which is the same
  difference a plugin instrument has from a plugin effect
- **`Ships` was answering no for every file of every machine that ships.** It compared the path
  under the installed folder with the same path under the shipped one, and those never match: a
  device installs into a folder named after its **id**, since that is the one name that cannot
  collide by accident, while it ships in a folder named whatever its author called it. OddSkilla
  ships in `OddSkilla` and installs as `machine.oddskilla`. So a song packed for somebody else
  carried a copy of the presets they already had. It reads the folder's id and looks the shipped
  device up by that now
- **A preset nobody can pick is a preset nobody has.** The shelf, the page and six files were all
  in place and EchoBox's face had no picker on it, and nothing anywhere filled one for an effect.
  `SoundEffectPresetNames` is the picker's list, made fresh each time a face asks for it so a
  preset saved in the designer turns up without the two being wired together, and supplied in all
  three places an effect's face is drawn: the slot on a chain and its own window, the rack row, and
  the designer's preview. `ElementKinds.Preset` is on EchoBox's face above the Delay group
- **Picking one writes through `IPanelValues` and never into the engine**, which is the rule this
  codebase has already paid for once with a hardware knob: a value written past the panel's own
  values moves the sound and leaves every knob on the screen where it was, and from a chair that
  reads as a preset that did nothing rather than as a picture that is stale. It applies on the
  rack too, unlike a soundmachine's picker on the designer's bench, which does nothing because
  there is no instrument behind it: an effect's face always has values, whether a real engine's on
  a chain or the bench the rack keeps
- **`headroom` on EchoBox's knobs was fifty and was pure gap.** It is how far down a dial starts so
  it stands on the same line as the switches beside it, and EchoBox has four knobs and no
  switches, so all it did was hold the name half an inch above the dial. Ouroboros keeps its
  fifty and should: every strip on it mixes knobs and switches, which is the case the property
  exists for
- **EchoBox ships with six of them**, in `rack/effects/EchoBox/presets/`, which is beside the
  program and is therefore content this repository is answerable for: Slapback, Doubler, Quarter,
  Tape Echo, Dub and Wash. `Tests/ShippedPresetTests.cs` walks that folder with the reader the
  application uses rather than trusting it, because a shipped preset goes wrong the way content
  goes wrong: a key spelled differently from the one on the face is dropped as the file is read,
  silently and correctly, and what somebody hears is a control that did not move. It also says out
  loud when it cannot find the folder, since a test that quietly passes where its subject is
  missing reports nothing for the rest of its life
- **A device has to leave room, and every preset this application shipped was at full scale.**
  Eighteen of OddSkilla's twenty peaked between 0.96 and 1.000 on one note with every level knob
  sitting at nought, and three were over it; all eight of Ouroboros's were between 0.45 and 1.00.
  One note filling the output means the second note is already past the end, so a chord, or any
  second track, drove the master's saturation as a waveshaper rather than as a safety net. The
  reported symptom was a song crackling, and the crackle was real: at the drive and the two
  level knobs one song actually used, the peak into the master was 4.21 and 80% of the samples
  were past full scale, with the worst step between two neighbouring samples after bending at
  0.418, which is an edge with content all the way up
- **A level knob cannot answer how loud a preset is**, which is why nobody had noticed. The level
  is one term in a chain whose other terms are not marked in decibels. The drive squares a saw up
  and its makeup is peak-normalised, so it holds the peak while the loudness climbs: measured on
  that song's patch, drive 1 to 8.1 took the RMS from 0.339 to 0.642, **+5.6 dB**, from a control
  whose own remarks say it changes the tone and not the loudness. Then the resonant filter sits
  *after* the drive, boosting a wave that is already squared off: the same patch peaks 0.866 at
  resonance nought and 1.057 at 0.30. So the only honest answer is the one the engine gives
- **`IHeadroom` is the rule and it is in the published assembly**, because this is an SDK and a
  machine somebody else writes has to be able to read what is expected of it. It knows nothing
  about audio: how much room one note has to leave, what room a measured peak leaves, and whether
  that is short. `Headroom.LeastDecibels` is **12**, and that is arithmetic rather than taste.
  Four notes of equal level sum to twelve decibels above one when they line up, so a four note
  chord at unity still arrives under full scale; eight tracks of unrelated material sum to about
  nine
- **There is no standard for a preset, and there are two for the signal around it.** EBU R 68 and
  SMPTE RP 155 put alignment level, where one lone signal is expected to sit, at -18 dBFS and
  -20 dBFS. EBU R 128 puts a finished programme at -23 LUFS with a true peak ceiling of -1 dBTP.
  Neither is about presets and both say the same thing about them, which is that one signal has no
  business being near the top. Twelve lands between those and the ceiling, which is where a device
  that also has to be audible beside somebody's own normalised takes can honestly sit
- **`IPresetLoudness` is the measurement and it is application code**, since it needs an engine
  and the engines are compiled in. It renders one note at nine pitches two octaves either side of
  middle C, because the answer moves with pitch: a filter at a fixed frequency is open under a low
  note and shut over a high one. Fixed rate and a pinned noise seed, so the same preset answers
  the same number on every machine and twice running. It answers **nothing** rather than nought
  for a sampler, a kit, a recording or a plugin: those are as loud as the take somebody put on
  them, and a number there would send whoever read it to turn down a knob that is not the cause
- **The reading is on the designer's presets page, under the preset's own name**, in warm colour
  when it is short, with a help badge on `designer.headroom` beside it. That is the guidance the
  whole exercise is for: the number is chosen on that page and could not be worked out by looking
  at it, so the page plays the preset and says what came out. About fifteen milliseconds, which is
  one frame, so it can be given while somebody is looking. It goes stale the moment a line is
  edited and says so rather than showing the old number, since a measurement of the file read as a
  measurement of the form is worse than none; it comes back on saving
- **Said and not enforced.** Nothing refuses a loud preset, because a machine built to be slammed
  is entitled to exist and whoever built it should have to mean it. What is refused is a *shipped*
  one: `Tests/PresetHeadroomTests.cs` walks `rack/machines`, renders every preset this build has an
  engine for, and names the one that is over. It also pins the other end, that none of them is so
  quiet nobody would pick it, because the fix for the first fault is to turn everything down and
  half a bank dropped too far reads as broken rather than as quiet
- The twenty eight generated presets now land between 12.0 and 12.5 dB of room. OddSkilla's went
  to about -12 dB on the `level` knob and Ouroboros's to about a quarter on its `volume`, each
  normalised to the ceiling rather than shifted by a constant, since a preset is picked on its own
  and every one of them should audition at a comparable level. Nothing in anybody's song moved: a
  song owns its instruments with their own settings, and what changes is where a preset picked
  from now on starts
- **Both reasons a preset went loud are now switches, and a switch here is a parameter on the
  device.** Not a tick in SETTINGS, which would change every song at once and travel with nothing, and
  not a control on a mixer strip, which is levels and sends and has no business holding a fact
  about somebody's drive knob. It is the rule `NewNoteAction` already keeps: a fact about the
  sound, wherever it is played, saved with the instrument and with the preset. As a parameter it
  goes in `machine.json` and `effect.json`, travels in the song and in the zip, and can be pointed
  at by a knob and automated, none of which a tick box can do
- **`SynthPatch.EvenDrive` is what the drive is levelled out by**, and it is two switches rather
  than one because they are two things and one of them is a feature. Peak is `1/tanh(drive)`,
  which maps full scale to full scale: it holds the height of the wave and says nothing about its
  area, and a drive squares a wave up, so the knob added 5.5 dB while its own summary said it
  gets no louder. Loudness works the correction out from the wave the drive is actually handed,
  which is `ISaturation.Evenly` over one period from `IOscillator.Period`
- **`SynthPatch.FilterFirst` is a tone control that happens to be the other half of the fix.**
  Drive into filter and filter into drive are two different instruments, which is why real synths
  put the choice on the front panel; what it also does is stop a resonant peak being applied to a
  wave that has already been squared off, which is the difference between the same patch peaking
  0.866 and 1.057. Measured on the patch the crackle was reported on: both off is peak 1.0566 with
  the knob adding +5.50 dB, Loudness alone is 0.6516 and +1.30, and both together are 0.4511 and
  +0.64
- **The makeup has to be measured against what reaches the drive, not against the oscillator.**
  With the filter first it is the filter's output that is driven, and a correction worked out from
  the raw wave was 3.33 dB out; running the oscillator through a filter of its own for two
  thousand samples and measuring the next thousand brings it to 0.64. A filter of its own rather
  than the voice's, which has not started and must be handed the note with no memory in it. It is
  constructor work on whichever thread started the note, never on the audio thread
- **An effect cannot be handed the wave it is about to work on**, so `ILoudnessMakeup` measures
  what went past instead: two running mean squares either side of the curve at fifty milliseconds
  and the square root of their ratio. One pair for the whole effect and not one a side, since a
  correction worked out per side is a gain that drifts between them and moves the stereo image
  about. One while either follower is under `Faintest`, because a ratio of two numbers that are
  both nearly nought is noise and it would be applied to the first sample of whatever plays next.
  On a steady tone it is exact: Roaster at amount 24 went from +8.79 dB to +0.00
- Roaster gets `even`, Sweeper gets `even` and `filter_first`, OddSkilla gets `even_drive` and
  `filter_first`. **Ouroboros gets neither, and that is not an oversight**: its chain is
  oscillator, filter, amplifier with no drive anywhere in it, so one switch has nothing to level
  and the other has nothing to reorder
- **Every one of them defaults to what happened before**, which is the whole reason they could be
  added without anybody hearing them first. A patch on disc says nothing about either field and
  reads back as off, and `Tests/DriveSwitchTests.cs` pins the off numbers to three decimal places
  rather than only comparing the switches with each other: a test that says two settings differ
  passes just as happily when both of them have moved
- The shipped effect presets gained the new keys at nought rather than being left silent about
  them, since `Tests/ShippedPresetTests.cs` says a shipped preset sets every control the effect
  has, and a preset that leaves one out is a control that quietly stays wherever the last preset
  left it
- **The audio stumbled and the fault was the pattern grid.** Reported as OddSkilla being too much
  for the output, and it was neither OddSkilla nor the output: the same stumble was in another
  song and had been there all along. The log said which of the two faults it was in one line.
  Mean block cost 3% of the time it had, worst 113 to 221%, and beside it 40 gen-0 collections and
  450 ms of every thread stopped in every five second window. **A block that is cheap on average
  and occasionally enormous is not slow code, it is a pause**, and no amount of making the mixing
  faster would have touched it
- Which is why `IRenderCost` says how much was allocated as well as what was collected. Two of the
  three numbers were there and the third is the one that says where to look: collections say
  something is stopping the world, a rate in megabytes a second says how hard it is being asked
  to. Every thread rather than this one, because here the thread that allocates and the thread
  that suffers are never the same: the mixing allocates nothing at all
- **Measured rather than guessed at, by moving one thing at a time.** The same transport running
  with the pattern on screen allocated **48 MB/s**; with the mixer on screen instead, **0.1**; on
  the pattern with the transport stopped, **0.1**. So it was the pattern being drawn while the
  transport ran, and nothing else in the program
- The cause was a property. `PlayingLine` was on the grid and in `AffectsRender`, so every line
  the transport reached repainted the whole page: at 120 beats a minute and four lines to the
  beat that is eight repaints a second, and each one drew a piece of lettering for every field of
  every cell of every line. Thirteen hundred `DrawText` calls to move a highlight bar
- **Laying the lettering out once and keeping it took 48 MB/s to 20**, which is worth having on
  its own since it is paid on every repaint for any reason. `EnsureMetrics` was also making a
  `Typeface` and measuring a probe glyph on every frame for an answer that only moves with the row
  height. The cache is keyed by the colour and not by the brush, because `ThemePalette` hands back
  a **new brush on every read**: keyed on the object it would never have hit once and would have
  grown for ever
- **What was left was Avalonia's own cost of asking for text to be drawn**, about two kilobytes a
  call whatever it says, which no amount of caching on this side reaches. Proved by drawing the
  same page with the cell text skipped: 95 MB became 2.2. So the only way down is fewer calls
- **So the band moved off the grid.** `Views/PlayingLineMark.cs` is one filled rectangle in a
  `Panel` over the grid inside the same scroll viewer, taking no clicks, and the grid no longer
  knows what line is playing. It repaints when somebody edits something; the transport repaints a
  rectangle. **48 MB/s to 0.7**, the pause from 350 ms to 40 in every five seconds, and the worst
  block from 221% of its own time to 11%
- The band is over the lettering now rather than under it, which is the one visible difference and
  is a fifth of an opacity: it washes the text instead of sitting behind it. Under would mean the
  grid painting no background of its own, and a control with no background takes no clicks, which
  the grid very much does
- The shape is worth keeping because it is not about tracker grids. **A property that changes
  many times a second must not be on a control that is expensive to draw.** `AffectsRender` says
  nothing about how much work a repaint is, so the cheapest possible thing to say and the dearest
  possible thing to draw end up on the same invalidation
- **The automation lane had the identical fault and got the identical answer.**
  `Views/AutomationPlayhead.cs` is the line where the song has got to, over the curve rather than
  in it, and `AutomationCurve` no longer knows what line is playing. Its bill was smaller, since a
  line is cheaper than a piece of lettering, but it is the same arithmetic: the ground, a grid line
  for every line of the pattern and the whole shape, redrawn several times a second to move one
  hair-wide rule, and the longest pattern this application allows is 256 lines. Measured with a
  lane open and the transport running: 0.7 MB/s, the same as with the strip folded away, and no
  block over its budget
- Both layers take no clicks, which is what lets them sit on top at all: the pattern is clicked to
  put the cursor down and the lane is clicked to add and drag a point, and both still are. Checked
  by clicking rather than by reading, since `IsHitTestVisible` on the wrong control is exactly the
  kind of fault that looks fine in a screenshot
- **And then the same question was put to the other half of it: typing.** With the transport no
  longer repainting the page, what was left was the hand. The cursor was in `AffectsRender`, so
  every arrow key repainted the wall: sixty cursor moves allocated **25 MB/s** on the drawing
  thread, put 259 ms of stopped threads into every five seconds, and took one block of audio to
  149% of the time it had. Somebody typing a part into a looping song was doing that to their own
  playback, which is exactly when a tracker is used
- `Views/PatternCursorMark.cs` is the third layer and the last of them. The grid works out where
  the box goes and publishes it as `CursorBoxProperty`, because the geometry is the grid's:
  finding a cell takes the character width the font was measured at, the pad above line nought,
  and how many note columns every track to the left is showing. Two spellings of that would
  disagree eventually, and the way that fails is a cursor box beside the cell it is about
- **The track tint is the one thing left in the picture that follows the cursor**, and it moves
  when the track does and at no other time, so the cursor is answered in `OnPropertyChanged`
  rather than repainted for: stepping down a column repaints one rectangle, stepping across the
  page repaints the page. Sixty vertical moves went from 25 MB/s to **0.8**, and sixty sideways
  ones, which really do cross tracks, to 1.9. Nothing goes over budget in either
- **A viewport cull was written, measured and taken out again, and that is worth keeping.** The
  culling in `Render` never fires: it compares each row against the control's own height, which
  inside a scroll viewer is the whole content, so every line of the pattern is drawn however few
  are on screen. Fixing it needs the scroll offset, and an offset in `AffectsRender` means the
  grid repaints on every scroll where before the scroll viewer simply moved an already-drawn
  child. The transport scrolls the view to follow the playhead, so it took playback from 0.7 MB/s
  back up to **9.6** to save half of an editing cost that the cursor layer then removed entirely.
  **A cull is only a saving where the thing being culled is not redrawn more often because of
  it**, and on a 64 line pattern the whole picture is barely two screens anyway
- **The gen-2 collections were chased and are not a fault, but what keeps them harmless is.** On
  the tracker page with the transport running the runtime does about three or four full
  collections a second on a heap that never grows: 26 to 30 MB, gen 2 flat at 22, the large object
  heap flat at 3.2, nothing leaking anywhere. Ruled out one at a time by measuring rather than
  reasoning: not memory pressure (the load was 6.2 GB against the runtime's own 6.9 GB threshold),
  not the gen-0 budget (raising it to 16 MB took 20 collections to 17), not the transport's band
  (hiding it changed nothing), and not the finalizer queue (it reaches a thousand on the mixer
  page with no collection at all). What is left is the residual allocation itself, a little under
  a megabyte a second, about half of it the drawing thread's per-line work
- **They cost nothing only because collection runs in the background, and that was a default
  rather than a decision.** Concurrently each one pauses between a third and one and a third of a
  millisecond, some forty in every five thousand, which is a percent of the clock. The identical
  collections under `gcConcurrent=0` cost forty to fifty milliseconds each and **720 ms in every
  five seconds**, which is worse than the stumble this whole exercise started with. So the csproj
  says `ConcurrentGarbageCollection` and `ServerGarbageCollection` out loud now, with the
  measurement as the reason: a publish profile or a later hand can turn either of them over
  without ever touching audio code, and the way that fails is an application that stutters on
  somebody else's machine for no reason anybody can see
- **And then a busy song was built rather than argued about, and it says something the empty one
  hid.** Eight tracks, twenty note columns, four patterns of 128 lines, 6624 filled cells with
  notes, instruments, volumes and pan commands, eight OddSkillas, at 140 to the minute in song
  mode: about a hundred and twenty notes a second and twenty voices sounding. Everything measured
  before this was an empty four track pattern, which exercises the drawing and nothing else
- On that song blocks **do** go over: mean 25 to 32% of the time each block has, worst 106 to 227%,
  and one to nine blocks over budget in every five seconds. That is the first of the two fault
  shapes rather than the second, and the two want opposite answers: **the mean is the mixing
  itself**, twenty voices costing a quarter of every block in a Debug build, where the empty
  pattern's mean was one per cent
- Where the allocation is was settled by measuring each thread rather than reasoning about it.
  `GC.GetAllocatedBytesForCurrentThread` on the audio thread: **0.01 MB/s**, so the render path is
  allocation-free exactly as its own remarks claim. On the tracker's clock thread, which starts a
  hundred and twenty notes a second: **0.12**. On the drawing thread: **2.7**. The process total is
  six to fourteen, so the rest is the toolkit's own compositing, which on this machine is software
  rendered with no GPU at all
- Ruled out on the way past, each with a measurement: not the pattern being drawn, since the mixer
  page with the same song playing is the same or worse; not the logging, since turning every area
  but Audio off changed nothing; and not the transport merely running, since the same song loaded
  and stopped is 0.1 MB/s with nothing collected
- **What cannot be concluded from any of it is what happens on somebody's real machine**, and that
  is worth writing down beside the numbers. This was a Debug build on a software renderer with no
  GPU and no sound card, where both the mixing and the compositing are inflated. The song is kept
  out of the way rather than put on anybody's shelf, and the answer to whether it matters is the
  one line the log now prints on the machine that is actually playing it
- **Then a complex one: sixteen tracks across and sixteen patterns down**, 44 note columns, 53984
  notes scattered through every pattern, 64 automation lanes and eight effect chains. It reaches
  48 voices, which is `MaxVoices` and therefore the ceiling, and blocks go over between nineteen
  and thirty two times in every five seconds. Thinned to a density somebody would actually write,
  13704 notes rather than 53984, it is **the same**: 39 to 48 voices and the same mean. That is the
  finding. **The voice count is set by how many note columns are sounding, not by how many notes
  are written**, so sixteen tracks of two to four columns is 44 voices whether the part is busy or
  sparse
- Which made it worth measuring the mixer on its own, with no window and no song, at 441 frames
  into a 10 ms block. Two patches, because the patch turns out to matter as much as the count:

  | voices | plain, filter open | saw, drive 2, resonance 0.5 |
  |---|---|---|
  | 8 | 6.8% | 6.6% |
  | 16 | 8.4% | 13.2% |
  | 24 | 12.6% | 20.3% |
  | 32 | **17.2%** | 31.0% |
  | 40 | 21.8% | 31.4% |
  | 48 | 27.1% | **41.4%** |

- The 17.2% at 32 voices is the measurement this file already recorded as "15 to 16%", so the old
  number stands and what it was measured on is now clear: a plain patch. **A saw through a drive
  into a resonant filter costs about twice as much per voice**, which is a filter and a hyperbolic
  tangent per sample per voice and is exactly where it should be. The mixer's 41% at the ceiling
  is also the whole of the 40 to 50% mean the complex song shows through the application, so the
  mean really is the mixing and nothing else is hiding in it
- Debug, on a laptop, with no GPU and no sound card, which is half the story and has to be said
  beside the numbers rather than after them. What they are good for is the shape: the cost is
  linear in voices, it doubles with a rich patch, and the ceiling the engine sets itself is where
  it lands at about forty per cent of a block here
- **And then the mixing was made cheaper, which is what that measurement was for.** Three things
  were worked out for every sample of every voice that had been settled before the note started,
  and all three are the same mistake in different clothes:
  - **the frequency, through a power.** `Ratio(MotionAt(...))` is `Math.Pow(2, x/12)`, and with
    nothing bending the pitch it answers exactly one, every sample, on every voice. At the
    ceiling that is two million powers a second computing nothing
  - **a random number**, worked out as an argument before the oscillator was asked which wave it
    is, and thrown away by five of the six waves
  - **the drive's fade**, a subtract, a divide and a clamp derived from a knob that cannot move
    while a note lasts. The makeup beside it was already handed in for exactly this reason, so
    the fade was the one term that had been left behind
- Plus the mixer reading each track's peak in the same pass that sums it rather than walking the
  same buffer twice
- **Not one sample changes**, and that is arithmetic rather than hope: `Ratio(0)` is exactly 1.0
  so the multiply it replaces is exactly nothing, a saw ignores the random number it is handed,
  and the fade is the same number whether it is worked out per sample or once.
  `Tests/VoiceShortcutTests.cs` pins each of those facts rather than the speed, since the facts
  are the whole of why this is a shortcut and not a change to the sound
- Measured best of seven runs, 441 frames into a 10 ms block, Debug:

  | voices | plain before | plain after | rich before | rich after |
  |---|---|---|---|---|
  | 16 | 9.0% | 6.4% | 16.5% | 9.6% |
  | 32 | 18.3% | 12.3% | 24.4% | 18.0% |
  | 48 | **27.1%** | **17.1%** | **35.3%** | **26.0%** |

- Best of seven and not one run, because the first attempt at this reported the change as a
  regression: the same code measured 30.6% and then 34.7% five minutes apart on a laptop with six
  gigabytes in use. **A single timing on a loaded machine is not a measurement**, and the way that
  fails is a real improvement being thrown away or a real regression being shipped
- End to end on the complex song, which is the number that matters: mean 40 to 46% became 33 to
  36%, worst 290% became 193%, and **blocks over budget went from nineteen to thirty two in every
  five seconds down to three to six**. Not nothing left, but the song sits at the engine's own
  voice ceiling in a Debug build on a laptop
- **The two things left on that path were then chased, and they answered opposite ways.** They had
  been written down here as deliberate costs, which is a way of saying nobody had measured them.
  One of them was not a cost at all and the other was more than half the voice
- **The interface call per sample is an artifact of the optimiser being off, and there is nothing
  there.** Measured at the ceiling, a bare saw with the filter open and no drive costs 15.1% of
  each block in Debug and **2.7% with the optimiser on**: five and a half times, which is the whole
  of what a call that nothing inlines is worth. Typing the four shared statics in `SynthVoice`,
  `SampleVoice`, `MonoSynthVoice` and the mixer's own curve as their sealed classes rather than as
  their interfaces is bit for bit the same arithmetic, and it moved the plain patch from 18.3% to
  18.1% and the rich one not at all. **Written, measured, and taken out again**, which is the
  second time on this path that a change good enough to argue for was not good enough to keep
- So the seams cost what this file always said they cost, which is nothing worth counting, and the
  sentence that said otherwise was reasoning about a Debug build as though the arithmetic in it
  were the arithmetic that ships. **A call that disappears under an optimiser is not a design
  cost**, and a design contorted around one is work done for a number that is not there
- **The hyperbolic tangent is the other half and it is most of a voice.** The same decomposition
  at forty eight voices, each line the one above it plus one thing:

  | | Debug | optimised |
  |---|---|---|
  | saw, filter open, no drive | 15.1% | 2.7% |
  | with the resonant filter | 16.4% | 2.9% |
  | with the drive instead | 23.4% | 7.2% |
  | with both | 24.8% | 7.9% |

- Which says something the Debug column alone hides. **The drive is a third of a rich voice in a
  Debug build and 57% of one in the build that ships**, and it is twenty times the filter. The
  reason is in one measurement: `Math.Tanh` costs 11.7 nanoseconds a call in Debug and **11.0 with
  the optimiser on**, because it is a call into the system's maths library either way. Everything
  around it got five times cheaper and it did not
- **`ITangent` is that curve behind a contract, because there are two honest ways to work it out
  and which one runs is a setting rather than a fact.** `Tangent` is the system's own and is what
  off means. Everything that bends a signal here goes through it: a machine's drive, Roaster's and
  Sweeper's, and the makeup each of them corrects with, so a curve and the correction for it can
  never be worked out from two different shapes. `TableTangent` is the curve drawn once at even spacing and read off, with the two
  terms of its own Taylor series filling in between the points: the derivative of a hyperbolic
  tangent is `1 - t*t` and its second derivative is `-2t(1 - t*t)`, both of which are the value
  already in hand, so a step off a grid point is one reading and three multiplies. Plain
  interpolation between two entries of the same grid is a hundred times worse and reads twice
- **Only the positive half is drawn and the sign is put back, which is not merely half the
  memory.** It is what makes the thing exactly odd. A table running from one end to the other
  works a point below nought and its mirror above out from different anchors, so the two disagree
  in the last few digits, and **an odd curve that is not quite odd is a saturation that leans**,
  which is a direct voltage in the mix that nothing downstream takes out again
- Four thousand and ninety six points over the first twelve, which is thirty three kilobytes and
  stays in the cache a mixing pass is already living in. Twelve rather than the drive knob's own
  ten, since what reaches the curve is the signal times the drive and a resonant filter in front
  can hand over more than full scale; past it the answer is flat one, which the curve is within
  eight parts in a hundred thousand million of by then. **Worst difference from the system's own,
  over the whole range: 161 dB.** A sample leaves here as a 32-bit float, whose own steps at full
  scale are 144 dB down, so the difference is smaller than the rounding the output does anyway
- Something that is not a number is answered before anything else, and that guard is the one line
  in it that is not about accuracy. Every comparison against NaN is false, so a guard written as a
  range test lets one through, and what it is let through into is an array index, on the audio
  thread, which is the one place here where a fault is the process gone rather than a message on a
  status bar
- **Measured, best of seven, 441 frames into a 10 ms block, at the mixer's own ceiling:**

  | | exact | drawn |
  |---|---|---|
  | rich 32 voices, optimised | 5.3% | 3.6% |
  | rich 48 voices, optimised | **8.0%** | **5.4%** |
  | rich 48 voices, Debug | 26.2% | 25.0% |

- **A third off the whole voice in the build that ships, and a twentieth off in Debug**, which is
  the honest shape of it and is said beside the numbers rather than after them, since a Debug
  build is what `dotnet run` gives and is where this will first be heard. The two disagree because
  the saving is the tangent and nothing else: optimised it is most of the voice, and in Debug it
  is a small share of a loop that is five times slower everywhere
- Folding on the sign is part of why. It is one absolute value, a compare and a negate, free
  optimised and about a point of Debug, and it buys a curve that is odd exactly rather than
  nearly. **A point of a build nobody ships is the right price for not putting a direct voltage
  into somebody's mix**
- The switch off costs 1.4 points of Debug and nothing at all optimised, which is the indirection
  that was added to have a switch at all. That is the price of the thing being switchable, it is
  paid where it does not matter, and it is smaller than what turning the switch on gives back
- **The switch is a tick in SETTINGS, Engine, and that is a different answer from the last two
  switches on this path.** `EvenDrive` and `FilterFirst` are parameters on the device because they
  are facts about the sound: saved with the instrument, carried in the song and in the zip, and
  worth pointing a knob at. This is a fact about how much time this computer has, which is where
  the buffer sizes and the real-time switch already live. **A song that sounded different on two
  machines for a reason nobody chose is exactly what a parameter here would have bought**, and
  automating an accuracy setting means nothing
- Off unless somebody says otherwise, like everything else on this path, and off is exactly what
  happened before rather than nearly: `Tangent` is `Math.Tanh` and nothing else. It is read per
  sample rather than taken when a voice starts, so throwing the tick lands inside the block being
  mixed and a song can be sat with and heard both ways without stopping it. `Saturation` asks the
  switch each time rather than holding an answer, deliberately, because the voices share one of
  them in a static field built before any settings file has been read
- **`TangentSwitch.Wants` writes a line, and the line is what makes the comparison possible.**
  Reading the two curves against each other means running the same music twice and reading the
  render cost either side, and the switch moves without stopping the transport, so the two halves
  of that experiment land in one log file with nothing between them. Written in the switch rather
  than at the tick, so the startup call marks it too and there is one place saying it
- **And the first time it was written it landed nowhere**, which is worth keeping because nothing
  about it looks like a fault. It was said at startup two lines *before* `Log.Open`, and a line
  written into a log that is not open yet is dropped in silence: the experiment it exists for was
  run, the file had every render cost in it and not one marker, and there was no way from inside
  the log to tell that the line had ever been attempted. `MainWindow` says it after the log is
  opened now, and `Tests/TangentTests.cs` reads the file back rather than trusting the call
- `Tests/TangentTests.cs` pins the bound rather than the speed, since the bound is the whole of why
  the drawn curve is allowed to exist. Walked at a step far finer than the table's own, so the
  sweep lands between the grid points rather than on them, where the drawn curve is the system's
  own answer read back and the test would be measuring nothing. Then the claim where it can be
  heard: **a whole note at drive 8.1 through a resonant filter, rendered both ways, differs by less
  than one 32-bit float step at the output**
- The master's own soft clip is deliberately left alone. It is `MathF.Tanh` on floats and only runs
  above the knee, so there is little to win, and putting it through a curve that deals in doubles
  would move its last digit **with the switch off**, which is the one thing an off position may not
  do
- **There is one clock and both pages that show a time draw it.** `Views/TimeReadout.cs`, on the
  tracker's bar after the pattern's help badge and in the middle of RECORD's row of buttons, where
  the recorder's own clock already was. It is a drawn control rather than a text block bound to a
  string, and the reason is the rate: **a clock with thousandths on it is a property that changes
  many times a second**, which is the shape this codebase has already paid for twice, and one
  piece of lettering in a box of its own is the whole of what should be invalidated when it moves
- Monospaced, in the pattern's own face, since a proportional font makes the digits shuffle
  sideways as they count. Measured against the widest reading it can hold rather than against the
  one it has, which is the rule `NumericInput.Widest` already keeps for the mixer's faders and for
  the same reason: a box that resized under its own reading would shove the whole bar along twenty
  times a second. `Time` is in `AffectsRender` and deliberately never in `AffectsMeasure`
- **Told the time rather than keeping it.** Both pages already run a timer at the rate their meters
  want, so a clock that ran one of its own would be a third one ticking on a page nobody is
  looking at. The recorder reads it off the level poll and the tracker off the meter poll, which
  runs whenever the transport does
- The recorder's clock was `hh:mm:ss` built as a string in the view model. Both halves of that were
  wrong in the same way: the wording belonged to whatever draws it, and there was about to be a
  second page wanting the same words. `RecordingTime` is a `TimeSpan` now and so is
  `TrackerViewModel.Elapsed`
- **An hour is sixty minutes rather than a field of its own**, so a long take reads 74:12.480. One
  fewer thing to read, no hour sitting at nought through every ordinary use, and nothing that
  appears halfway through a take and moves everything beside it
- The tracker's is **wall time from the moment play was pressed** and not where the song has got
  to. The two agree from the top of a song and part company the moment somebody starts halfway
  down, and this is the one that answers what a clock is usually being asked, which is how long
  this has been going. Where the music is is already on the screen twice, as the playhead and as
  the order slot. It holds on a pause and goes to nought on a stop, since a pause is somewhere you
  come back from and the next play starts from wherever the cursor is
- **The plugin bridge was chased next, and the answer is not in the bridge.** The song that started
  it is one or two of our voices and five plugins in five processes, and the mixing was reading a
  mean of 55% of every block. `IRenderCost` cannot say what that was spent on: a block spent inside
  somebody's synthesis and a block spent getting there and back are one number to the mixing thread
  and they want opposite answers. A plugin that is expensive is a plugin, and the only things to do
  about it are fewer of them or a longer block; a crossing that is expensive is this application's
  own, is fixed cost paid once per plugin per block whatever the plugin is doing, and grows with
  the number of plugins rather than with the music
- **So the same block is measured at both ends now.** `IBridgeCost` is the parent's half, one per
  plugin process, said in the same words the mixing already reports in so the two lines can be read
  together: `bridge: ZamAutoSat 500 crossings, worst 5% of the time they had, mean 2%, 0.231 ms
  each`. The child's half is on the line it already wrote every two seconds, everything between the
  block arriving and the answer going back, which is the plugin's own work and the two buffer
  copies either side of it. **What the parent saw and the child did not is the crossing**
- Measured on a trivial CLAP saturator, so that nothing in the answer is somebody's synthesis:
  round trip **0.227 ms**, the child's side **0.050**, so **0.177 ms is the crossing**, which is
  78% of it. Five plugins at a 512 frame block is 0.89 ms of every 11.6, or **7.6% of every block
  spent getting there and back before any plugin does any work**
- **Real-time scheduling does not touch it**, which was the obvious suspect and is worth writing
  down as ruled out. The same plugin with the tick on and its audio thread confirmed at real time,
  priority 5: 0.237 ms against 0.231. Priority decides who runs when the machine is awake
- **What it is, measured rather than reasoned about: waking a thread that has been asleep for ten
  milliseconds.** The identical Unix socket round trip, thread to thread, in the same build on the
  same machine, is **8.0 microseconds back to back and 145.1 once every 10 ms**, which is the whole
  of the bridge's 177 and eighteen times the socket itself. Not the socket, not the process
  boundary, not the serialising, and nothing in this application's own code. It is the machine
  coming out of idle, and that is also why priority changed nothing: a priority says who runs, and
  a core that has gone to sleep has to be woken first
- **Which looked like the answer to the oldest question in this file, and was not.** The section
  above opens by asking why the buffer here has to be twice another program's, and this looked
  like the cause: the wakeup is paid once per plugin per block, so it is a fixed cost per block,
  and the only thing that makes it smaller as a share is a longer block. At 512 frames five
  plugins cost 7.6% of the block in wakeups alone; at 2048 the same wakeups are 1.9%. **A host
  with the plugins in its own process pays none of it**, which is the difference being compared
  against. All of that is true and none of it explains the buffer, which the Windows column
  settled: see below
- **Then it was measured on Windows, and the finding is that the bridge is ruled out rather than
  guilty.** `docs/plugin-bridge-on-windows.md` is the column, taken on an i3-13100 on the Balanced
  power plan with the default shared audio path, and `Assets/jinglebox.log.txt` is the run it was
  read out of. The floor is the same finding: a socket round trip is 11.4 microseconds back to
  back and 60 to 136 once every ten milliseconds, a factor of five to twelve, so the cost is the
  machine coming out of idle rather than the socket, exactly as here. `afunix.sys` is about three
  microseconds dearer than the kernel-native socket, which is nothing
- **The crossing is twenty times cheaper there, and load is why rather than the platform.** Under
  three busy Serum processes it is 0.06 to 0.10 ms against this machine's 1.8, on a box running at
  33% of its block against 69% here, so nothing is queueing for a core. That confirms what the
  Linux note already suspected, that the crossing grows with how loaded the machine is and the
  platform is the smaller term. **And it reopens the buffer question**: three plugins at 0.10 ms
  apiece is under three per cent of a 512 frame block, and no buffer was ever doubled for that.
  Whatever forces the bigger buffer is still unfound
- **Overlapping stays off by default, and the Windows run is not the argument for changing it.**
  The mean came down 33.6% to 30.4%, the same three or four points and the same reason: one plugin
  is the critical path, Serum 2 at 2.33 ms of round trip against Serum 2 FX's 0.47, and
  overlapping cannot make the longest chain shorter. The blocks over budget look decisive at
  eleven against one in three minutes and are not, since that is nearly nought against nearly
  nought on a machine with nothing wrong with it, and the two mean ranges overlap almost
  completely where here they barely touched. **A default is turned over on the machine the switch
  exists for, which is one that is actually struggling**
- **Every plugin process on Windows was declared dead thirty seconds after its last control
  message, while alive and rendering.** `PluginProcess.Start` gives the listening socket thirty
  seconds so a plugin that never connects cannot hold the caller for ever, which is right. **On
  Windows a socket handed back by `Accept` carries a copy of the listening socket's options,
  timeout included; on Linux it does not.** The audio socket had its own patience written over it
  on the next line and the control socket did not, so a number meaning "how long to wait for a
  plugin to turn up" silently became "how long a running plugin may go without speaking", and a
  control socket is quiet by design since it carries knob moves rather than audio
- Measured twice rather than reasoned about: a listener set to 12345 ms hands back an accepted
  socket reporting 12345 ms there and nought here, and in a real session four plugin processes
  were buried at 30.001, 30.001, 30.000 and 30.001 seconds after each one's last control message,
  every one still alive to be closed on purpose later. The epitaph carried no exit code, because
  the child had not exited to have one. The fix is one line,
  `controlSocket.ReceiveTimeout = PluginBridge.WaitForEver`, and `Tests/BridgeSocketTests.cs`
  pins that waiting for ever is nought, that the inheritance really is what each platform does,
  and that a link saying nothing for longer than the listener's patience is still there when it
  speaks
- **Worth keeping for the shape rather than the fault, and it is the shape this file keeps
  naming.** A socket option set in the right place for the right reason, inherited somewhere
  nobody looked, on one platform only. From a chair it is every plugin crashing at once. And the
  run that caught it was the long one: twenty seconds would have passed cleanly, produced a
  plausible column for the table, and left the cliff under every number in it. What to ask of the
  next platform this is carried to is what else is inherited, defaulted or assumed there that was
  set once here and never looked at again
- Three ways out, and none of them is a change to the bridge's own code. The blocks can be made
  longer, which works today and is what the buffer slider already does. The machine's idle
  governor can be told not to go so deep, which is what every Linux audio guide says and is
  somebody's machine rather than this program. Or the crossings can stop being serial: plugins on
  one chain must go in order, since the audio flows through them, but **two tracks' chains are
  independent and are currently waited on one after another**, so five plugins pay five wakeups
  where they could overlap and pay about one. That last one is the real lever and it is done, below
- **`IOverlappable` is a run of audio work that can be started, left in flight and come back to**,
  and it is one contract because a plugin and a chain are the same shape: `Begin` starts and says
  whether anything is now outstanding, `Advance` collects what is and starts whatever comes next.
  A bridged plugin's `Begin` puts the block in the shared memory and asks; its `Advance` waits and
  copies the answer out. A chain's `Begin` walks its own slots until it reaches one that can be
  left in flight, doing everything before it where it stands
- **So the width is the number of tracks and never the number of plugins**, which is the whole of
  what may be overlapped and is worth being exact about. A chain is audio flowing through slots in
  order, so the second cannot start until the first has finished; two tracks' chains work on
  their own busses and nothing on one reads the other, so those really are independent. The mixer
  begins every track, then drives rounds: each round collects what was outstanding and asks for
  whatever is next, so at any moment every track has one crossing in flight
- Both places a crossing happens go through it, the plugin instruments and the insert chains, and
  they are one walk rather than two phases: see `TrackMixer.RunTracks` and the finding below that
  made the phases go. **Not one sample changes**, and that is checked rather than argued:
  `Tests/OverlappedMixerTests.cs` renders the same three tracks both ways and compares the block
  sample for sample. It also pins the interleaving, since **a run that collected each track
  before starting the next would leave an identical block and save nothing whatever**, which is a
  change that passes every test about audio and does not work
- `PluginProcess.Render` is now `Ask` and `Collect` with the old name calling both, so the
  blocking path is the two halves run together rather than a second spelling of them. A block too
  long to cross in one go is refused by `Begin`, which answers false, and the caller does the
  ordinary chunked thing: there is one buffer each way, so a chunked block is several round trips
  in a row and there is nothing to overlap inside it
- **A run once begun is always driven to its end**, and the one comparison that makes that certain
  is worth its cost. A slot left holding an answer nobody collected refuses every block after it,
  for the rest of the session, and from a chair that is one plugin going silent for no reason
  anybody can see. So a chain settles anything left in flight before it starts a new run, and the
  abandoned answer lands on the block at hand rather than being thrown away, which is a moment of
  a plugin's output in the wrong place against that plugin being dead until a restart
- `Audio.OverlapSwitch` is the switch and it is a tick in SETTINGS, Engine, beside the other three.
  **Off by default although the audio is identical**, which is a different reason from the fast
  drive curve's: there is nothing to listen to here, and what ships off is a change to the audio
  path in a program where a plugin lives in another process and can die between the asking and the
  answer
- **Then it was measured on a real song, and the song is what made the numbers worth having.**
  Gruber: three tracks with plugin chains on them, one plugin instrument, and five plugin
  processes between them. Twelve five second windows each way, the transport stopped in between,
  the switch the only thing changed:

  | | one track at a time | begun together |
  |---|---|---|
  | mean of each window's mean | **69.0%** | **64.3%** |
  | the range those means fell in | 66 to 72 | 60 to 68 |
  | blocks over budget in every five seconds | **28.6** | **19.3** |
  | worst block | 255% | 259% |

- **A third fewer blocks go over**, and the two ranges barely touch, so it is a real shift rather
  than a quiet afternoon: the serial half never came under 66 and the overlapped half never went
  over 68
- **The worst block did not move and should not have.** 255% of the time it had is a pause and not
  the mixing, which is the distinction the render cost line was built to make in the first place,
  and nothing about when a plugin is asked for its block reaches it
- **And the mean only came down four points because one plugin is the critical path.** The same
  log says what Gruber is actually spending its block on, which is the thing measuring both ends
  of the crossing was for:

  | | round trip | its own side |
  |---|---|---|
  | Serum 2 | 4.946 ms | **3.2 ms** |
  | Serum 2 FX | 1.593 ms | 1.8 ms |
  | Serum 2 FX, the second one | 0.907 ms | 1.7 ms |
  | ZamDelay | 0.907 ms | **0.09 ms** |

- Three Serum processes are about 6.8 milliseconds of an 11.6 millisecond block, and that is
  somebody else's arithmetic: a wavetable synth oversamples and this application has no reach into
  it. **Overlapping cannot make the longest chain shorter**, and what it removed is the other three
  queueing behind that one. ZamDelay beside it at 0.09 ms is the whole point of measuring both
  ends rather than one: two effects on the same chain, and one of them costs twenty times the other
- **The gap between the two ends is 1.8 ms here against 0.177 in the empty case**, which is the
  same finding from the other side. With four plugin processes each wanting two or three
  milliseconds of every eleven, the mixing thread is not merely waking an idle core, it is waiting
  to be given one back. So the crossing grows with how loaded the machine is, which is exactly why
  overlapping is worth more on a busy song than the empty measurement suggested
- **None of this is the OS-specific half of the bridge, which is worth being exact about since
  that was the worry.** The audio path is a Unix domain socket and a shared memory block, and both
  are one code path on Windows and here; what differs per platform is the window handover, the
  message pump a plugin's process needs, and the sharing flag on the mapped file, and not one of
  those is on the path a block takes. The cause found here exists on both, since both have idle
  states and a scheduler, and the numbers will not be the same. The line the log now prints is what
  says what they are on the machine that is actually playing
- **An effect has presets, and the page for them is a form rather than a file.** It said no for a
  while, on the reasoning that a machine's preset is an instrument file and an effect has no
  instrument. That was an argument about how presets happened to be stored here rather than about
  what an effect is: every delay ever built ships them, and an effect's preset is a handful of
  numbers, which is less to write down than a machine's and not more. `SoundEffectPreset` is a
  name and where each control stands, keyed by the parameter's own key so a preset written today
  still means the same thing after somebody adds a knob in the middle of the face.
  `SoundEffectPresets` is the shelf, `presets` inside the effect's own folder, so they travel in
  the zip
- The two presets pages differ because the two worlds keep different things in one. A
  soundmachine's page shows the JSON, and has to: a preset there holds a pad pointing at a wave, a
  keyboard map, where a take is cut, or nothing but a pointer at the recordings you already have,
  and a form with a box for each of those would be four forms that still could not say the fifth
  thing somebody wants next. An effect's can only ever be numbers, so it gets the form the machine
  could not have, one row per control with a slider and a box, and nobody has to see a brace
- **What a slider hands back is not a number anybody can read.** Dragging the time across the page
  produced 527.2144522144523 on a control that moves in whole milliseconds. Snapped to the
  parameter's own step where the value is written down rather than only where it is drawn, since
  the file is what somebody reads and what travels, and rounded to what the step can express:
  snapping is a division and a multiplication, and 0.35 on a step of a hundredth comes back as
  0.35000000000000003, which is the same number to a listener and a different one to a file
- Everything unhappy is where the tests are. A folder that is not there, a file that is not JSON,
  a value naming a control the effect has not got, a value past the end of its range, NaN, a name
  with a separator in it, New pressed twice, a rename onto a name that is taken, a rename to
  nothing, and an effect that has never been saved. Every one of those is a way to lose a preset
  somebody made, which is why the shelf answers rather than throws: one preset that will not read
  is one preset, not the whole effect
- **EchoBox is the first effect of ours, and it is a delay.** `SoundDevices/SoundEffects/Delay.cs` is the
  engine, `rack/effects/EchoBox/effect.json` is the face, and `SoundEffectEngines` is the one line
  that ties the id to the class. Four knobs: time, feedback, damp and mix. The time glides rather
  than jumps, since a delay line read from a different place on the next sample is a click and
  every hardware delay either crossfades or slides; a time set before anything has been rendered
  is where it starts rather than somewhere to glide from, so a song opening does not smear its
  first repeats. The damping is one pole on what comes out of the line, which is both what you
  hear and what goes back in, so each pass round loses more of the top
- **A read position a hair below nought wraps to the length of the line itself**, which is one
  frame past the end, and that is an index outside the array on the audio thread. Adding the line
  back to a negative position lands exactly on the length once the arithmetic rounds, so the wrap
  is done at both ends. It took an eight thousand frame block to find it and it would have shown
  up as a crash on somebody else's buffer size. `Tests/DelayTests.cs` is where the rest of it is
  measured rather than listened to: where the repeat lands, how far each one falls, that no mix
  is bit for bit what went in, that the block size changes nothing, and what happens when it is
  handed NaN, a time of a million seconds, a block longer than the buffer, or no buffer at all
- **The effect world was built empty on purpose, and it holds three now.** `SoundEffectProject`
  is the manifest, `effect.json`, deliberately not `machine.json` with a flag on it: a folder is
  one thing or the other, and a reader that had to open the file to find out which is a reader
  that can be wrong. `SoundEffectRegistry` reads `effects/` by the rack's rules, `SoundEffectProjects`
  holds what was found for the run, and the rack's Effects tab is that list. `ISoundEffectEngines`
  is the gate, and its table was empty for exactly as long as there were no engines: an effect
  that could be had and makes no sound is the device this codebase refuses to put on a rack, so the
  first entry arrived with the engine that does the work rather than before it. EchoBox, Sweeper
  and Roaster are in it now, keyed by the **engine** rather than by the effect's id, which is what
  lets any number of effects name one. There is deliberately no enum of effect engines with
  numbers in it, unlike `TrackerInstrumentKind`: a song says which engine an instrument is on, and
  a chain writes down an effect's id and never its engine, so nothing here is ever written to a
  file and there is no number to keep still. Which is why the id has to be looked up in what is
  registered to reach an engine at all, and why `SoundEffectEngines` is handed that list
- **Registered and on the rack are the same fact for an effect, which they are not for a
  machine.** A machine can be taken off the rack and put back, because what sits there is an
  instrument you own with your own settings on it; an effect has nothing of the sort, since one in
  use is a slot on a track's chain. So the Effects tab has no picker beside it and no shelf file:
  what is registered is what is there, and losing one is unregistering it in SETTINGS. The rows
  are `RackSoundEffect` against the machines' `RackSoundMachine`, drawn by one template and washed by one
  set of styles through `IRackRow`, which is named in XAML because a compiled binding needs a type
  and a row drawn twice would be two templates drifting apart
- **There is one designer and it is told which world it is in.** Laying out a face, dropping
  parts on it, naming parameters, sizing the columns and keeping the undo are the same work
  whether the face belongs to a machine or to an effect, so `DesignerViewModel` and `DesignerView`
  are one page used twice, on two tabs **inside DESIGNER**: Machines writes `machine.json` and
  Effects writes `effect.json`. Inside the one page rather than two pages along the top, because
  it is one workshop and which of the two you are laying out is a choice within it, the same way
  the rack itself has Machines and Effects. Tabs rather than a picker, so nobody is halfway
  through a machine and finds they were drawing an effect, and two instances rather than a mode,
  so each holds its own project, its own undo and its own unsaved changes and switching between
  them loses nothing
- **New follows the tab, and the difference between the two is a help topic.** It was briefly a
  dialog asking which you meant, and that was wrong twice over: a question in the way of a button
  that used to do something is bad to work with, and the paragraph explaining a machine against an
  effect was a sentence in the page when this application already has one place for that. The
  badge beside New opens `designer.worlds`, which says what each is, that both are a face
  over an engine that lives in the application rather than in the folder you are making, and why
  the id New gives you never reaches the rack
- **`IDesignWorld` is everything that differs, and it is small.** What a fresh one is, what its id
  begins with, what the manifest is called, the word in a sentence on the status line, whether the
  folder can be carried somewhere else, and whether there is a zip and a presets page. Nothing
  else in the page knows which world it is in, and the wording is built from the word rather than
  written twice: `The machine` and `The effect` are one heading. `IDesignProject` is what the page
  edits, implemented by both projects, and not one member of it is about notes or audio.

  **This paragraph said for a while that an effect offers no Export and no Presets page, and
  both had stopped being true.** Export was refused because a zip needs an importer at the other
  end and there was none, and SETTINGS imports an effect now; the presets page was refused
  because a machine's preset is an instrument file and an effect has no instrument, which was an
  argument about how presets happened to be stored rather than about what an effect is, and it
  is answered a few bullets down by the form `SoundEffectPresets` fills. `SoundEffectWorld.Exports`
  and `HasPresets` both read true. Worth leaving the correction in rather than the sentence
  alone: **a note in this file goes stale the same way help text does**, and the tell here was
  that it disagreed with another bullet in the same document
- **The two things that were written for machines and were never about them came out.**
  `IPanelImages` is the pictures in a folder, added under the next free number, swept when nothing
  names them, renumbered so there are no gaps, and one at a time when the last element showing one
  goes; `IFolderCopy` is a folder carried whole, empty folders included, which is what taking a
  shipped device and what Save as both are. `DesignHistory` went the same way: a step is the
  project's own JSON, and it used to name the machine's type on both sides of the trip, where it
  now asks the project what it is. So a world added later needs nothing in any of the three
- **The rack holds devices, and a device is a soundmachine or an effect.** That is the word the
  whole of this half of the program is arranged around now, and it is worth being exact about the
  four:
  - an **engine** is compiled in and makes or works on the sound. It has no face and no name a
    person sees
  - a **device** is a face over an engine: a folder with a manifest, an id, parameters, presets
    and a picture, registered here, on the rack, drawn by one library, laid out in one designer,
    zipped and handed to somebody by one archive
  - a **soundmachine** is a device that is played. Notes go in and sound comes out, and in a song
    it becomes an **instrument**: your name, your settings, its own id
  - an **effect** is a device that is not played. A whole track's audio goes in and comes back
    changed, and in a song it is a slot on a track's chain
  So the only difference between the two kinds of device is what happens to it once it is in a
  song, which is the same difference a plugin instrument has from a plugin effect. Everywhere
  else, where there were two of something there is now one: `IRackRegistry`, `RackArchive`,
  `RackShelfViewModel`, `RackView`, the designer, and the link
- **Pointing a controller at something is a device thing, and used to be two.**
  `ControlKind.SoundDevice` is that one kind, which is `Instrument` renamed and the same number, so
  every link already in somebody's settings is what it was. `ISoundDeviceLinks` is the one maker: a
  link names the device's id and the key of the control under the pointer, nothing about the
  track, the slot, or which of the two worlds it came from. `ControlTargets.OnDevice` is the one
  resolver and has two places to look, the machine the track plays and the effects on its chain.
  A file says `device`, reads `machine` as one, and still refuses `effect`, since that word has
  only ever meant a plugin in a template and a plugin cannot be pointed at
- **It was written out three times and the two copies were wrong.** A machine's face offered
  `Instrument`; the rack's effect face and an effect's own window offered `Insert`, which is the
  word for a plugin. The settings reader throws every plugin link away as it reads, and the glow
  only counts device links. So a knob pointed at an effect lit nothing, worked until the
  application was closed, and was gone in the morning, with nothing anywhere saying why. One
  maker, one kind, one resolver: `Tests/SoundDeviceLinkTests.cs` is the whole rule, including that
  an effect's link is still there when the settings are read
- **An effect of ours is a fourth word and not a kind of machine.** Engine, machine and instrument
  were three; an effect sits beside machine. It is a face over an effect engine, it takes audio
  rather than notes, and so it has no keyboard, no zones, no pads and no kit. Its own folder, its
  own manifest, its own register and its own page in the designer, registered by the same rules a
  machine is: two folders, `offered.txt`, and an id whose engine this build has not got read and
  passed over. What the two share is the drawing, which is why the panel types stopped being
  named for machines. An effect in use is a slot on a track's chain and takes no name of its own,
  the way two of the same plugin on one track already read. `docs/effects.md` is the design, the
  rename, the six engines and the order they are built in. Three of the six are: the delay, the
  filter and the drive. Reverb, EQ and the compressor are not
- **The rack decides which machines a song can be given, so a machine can be taken off it.** It
  could not be, on the reasoning that a machine is not something you can be without, and that was
  the wrong shape: a machine you never reach for is one that should not be in the list a song
  picks from. Taking it off is not losing it. The machine stays registered, the picker underneath
  offers it back, and losing one is unregistering it in SETTINGS, System
- Which means the rack cannot be rebuilt from the registry on every open, or a machine taken off
  would be back the next morning with nothing to say why. `ISoundMachineRack.Shelved` is the record,
  `instruments/shelved.txt`, one id to a line: a machine this rack has never been offered gets
  its place, and one it has been offered is left alone whether or not it is still there. The
  registry's own `offered.txt` is the same rule for the same reason, and this is that rule one
  layer in
- Each tab's picker offers what is registered and not already on the rack, which is empty in the
  ordinary case and is the truth: there is nothing to add. What it puts back is the machine's own
  place, under the machine's own slot id, because it is that machine's and not a second one. A
  variant set up differently is a Duplicate, which is a different act with a different button.
  The effects tab has no picker at all, since an effect cannot be shelved: what is registered is
  what is there
- **A song picks its instrument from one list: the rack's machines and the instrument plugins on
  this computer.** `MachineRackViewModel.Offered`, drawn as a coloured dot and a name, since to a
  track those are one question with one answer: what plays this part. Instruments only, because
  an effect goes on a track's chain under the pattern, which is where it belongs and where it
  already worked. A plugin says its format only where the same name is installed twice, which
  happens whenever a plugin ships as both a CLAP and a VST3: those are two plugins here, two ids
  and two sets of parameter numbers, so neither can be dropped and a list saying one word twice
  is one nobody can pick from
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
- `Knob` / `Fader` / `NumberField` (Rack.Controls/): The app's own value controls; a pot knob, a vertical fader, and a compact stepper field. They live in the controls assembly because a soundmachine bought from somebody else is built out of the same controls the app's own machines are
- `WaveformView` (Rack.Controls/): A recording's shape, with the window and the loop draggable on the picture
- `PanelView` (Rack.Controls/): A machine's face, built from what the machine says it looks like. Designing, every element can be picked and none can be turned; off, it is an ordinary panel
- `PartSample` (Rack.Controls/): One entry of the designer's library, drawn as the real control it adds
- `ThemePalette` (Rack.Controls/): Theme colours for custom-drawn controls, read as `Color.*` keys so a theme swap lands at once
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

**And `Sound` is part of the noun, not a guard against a clash.** A sound card is a sound card
whether or not anything else in the world is called a card, and the same goes for a sound
machine, a sound effect and a sound device: the word says what the thing works on, which is
information a reader wants and not padding. So the rule is not "prefix it when the plain word
is taken". It is the ordinary rule said again, name it what it is, and the prefix earns its
place by carrying meaning.

Which then says something about every other use of those words. If two things answer to one
name, either they are the same thing, in which case there should be one of them, or one of
them is misnamed. `device` was the bad case: it meant a device on the rack, a MIDI port, a sound
card and a plugin on a chain, four things, so it named none of them and had become the bag
`Helper` and `Manager` always become. Each of the four has its own word now: `SoundDevice`,
`MidiPort`, `AudioOutput`, and a `Slot` on a chain. `TrackerEffect` was the same fault in
miniature, since the pattern's effect column holds a letter and a value, which is a
`TrackerCommand`, and a link that said `effect` meant a plugin and now says so.

The soundmachine world
says it now, right through: `SoundMachine` is the record, and `SoundMachineRack`,
`SoundMachineProject`, `SoundMachineProjects`, `SoundMachineRegistry`, `SoundMachineArchive`,
`SoundMachinePaths`, `SoundMachineWatch`, `SoundMachineWorld`, `SoundMachineValuesFor`,
`SoundMachinePresetFile`, `SoundMachinePreset`, `SoundMachinePresets`, `MissingSoundMachine` and
`MissingSoundMachines` with their interfaces. `SoundMachineWindow` and `SoundEffectWindow` are the
two windows a device opens in, named the same way and named as a pair, since before this one said
machine and the other said effect and only one of them was a word this file uses.

The pages and dialogs went the same way, because a name half applied is worse than the old one:
`SoundMachineShelfViewModel`, `SoundMachinePresetDesk`, `SoundMachinePresetForm`,
`SoundMachinePresetNames`, `SoundMachinePresetSlot`, `SoundMachinePresetWords`,
`SoundMachineProjectShape`, the six `SoundMachinePreview*`, `RackSoundMachine`,
`MissingSoundMachineDialog`, and the tests that name the world.

`MachineUtilities` had a second fault on top of the first and is `SoundMachinePresetTools`.
Utilities is the same role name `Helper` and `Manager` are, and it had already done what a role
name always does, which is attract two unrelated jobs. What it really is, is the bench of tools
over the open soundmachine's presets: rename one, and level the recordings it names. `PresetTool`
is a tool on that bench, which is what `UtilityTool` was calling itself.

The sound effect world says it too: `SoundEffectProject`, `SoundEffectProjects`,
`SoundEffectRegistry`, `SoundEffectArchive`, `SoundEffectWorld`, `SoundEffectValues`,
`SoundEffectEngines`, `ISoundEffectEngine`, `RackSoundEffect`, `SoundEffectShelfViewModel`,
`SoundEffectInFront` and `SoundEffectViewModel`, which is one of ours standing in a slot on a
chain. And the umbrella: `ISoundDevice`, `SoundDeviceLinks`, `SoundDeviceRemote`,
`ControlKind.SoundDevice`, and `sounddevice` as the word a template file uses.

**The SDK has no two worlds in it, and thinking it did is what kept a folder empty.**
`Rack.SoundDevices` held `Faces/` for what both worlds draw out of and `SoundMachines/` for what
only a played device has, and the obvious question was where the matching `SoundEffects/` had got
to. The answer was not that an effect needs nothing. It was that the split is not world-shaped at
all.

Asked one at a time, most of the nine were not about being played. `IPanelNotes` is what a key is
called, and a delay with its time in note values wants exactly that. `IPanelTakes` is where a
panel finds out about the recordings a device names, which is a convolution reverb's impulse
response as much as a sampler's. `IPanelLocation` is where the track has got to, and an effect
sits on a track. `IPanelPatch` is a device's settings as it keeps them. The same test caught two in
the shared folder going the other way: `IPanelScope` had been written up as a synth tracing its
own wave, when the contract is fill this buffer with the shape you are making, and a compressor
tracing its gain reduction is the same call; and `IPanelPresets` was called a machine thing
because `SoundEffectWorld.HasPresets` is false, which is a gap in this application rather than a
fact about effects, since every delay ever built ships presets.

So the contracts are named for the part they serve and they live together: `IPanelKeys`,
`IPanelPads`, `IPanelZones`, `IPanelSlices`, `IPanelTakes`, `IPanelNotes`, `IPanelPatch` and
`IPanelLocation`, beside `IPanelValues`, `IPanelMenu`, `IPanelPresets`, `IPanelScope` and
`IPanelOrder`. Whether a given device answers one is a fact about that device rather than about which
of two worlds it is in. The two worlds are real where the difference is real, which is what a
song does with the device, and that is application code: `SoundDevices/SoundMachines/` and
`SoundDevices/SoundEffects/`.

`IInstrumentName` kept its own word, because it is the instrument's name in the song and an
effect on a chain takes no name of its own. `IPluginEffect` and `ClapEffect` keep theirs as well,
because a plugin that works on audio really is a plugin effect and both already say plugin.

Still open, and it is the fault this uncovered: `DesignerViewModel.Library` is one unfiltered
list bound straight into the page, and `IDesignWorld` says nothing about parts, so the Effects
tab offers `Keys`, `Pads`, `Pad`, `PadPicker`, `Zones`, `ZonePicker`, `Slices`, `Take`,
`Location` and `InstrumentName` on an effect's face. `Scope` and `Preset` are deliberately not on
that list: they belong on an effect and are only unwired.

**The panel a face stands in is not a designer, and was called one in three places.**
`ISoundDevicePanel` is what a face needs behind it: the editor, the octave to test at, the note
trigger, the two scope numbers, the hold length and the two halves of a key press. Nothing in it
designs anything, and DESIGNER is `DesignerViewModel` and `DesignerView`, which lays a face out, so
the old `IInstrumentDesigner` put two unrelated things one letter apart. It is also not per track:
`RackViewModel` implements it, over a soundmachine or an effect on the rack, where there is no
song and no track. `TrackInstrumentPanel` is the implementation over the instrument one track
plays, which is the one place the word instrument is right, since a soundmachine used in a song is
an instrument. `SoundDeviceKeys` is the keyboard on that face and `Views/SoundDevicePanel.axaml`
is the face.

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
`Tracker/Synth/`, the machines folder, which is `SoundDevices/SoundMachines/` now, and the
pattern, slice and song rules at the root of `Tracker/`. A second pass took `Audio/`,
`Midi/`, `Config/`, `Diagnostics/`, `Views/` and the theme; a third took the two machine
assemblies, `Shortcuts/`, `Controllers/`, `Help/` and what was left in `ViewModels/`. What
is left static is twenty one things and every one of them is on the list below.

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
`x:Static`, both data, and so are `PanelActions`, `PanelStarts`, `ElementKinds`,
`SoundMachinePresetWords` and `PatternFont`. `PluginCrashGuard` is a door like the log's and its
rules came out into `IRunMarker`. `ShortcutKeys` is one more door: one map for the application,
hung on every window, and what it knows is `IShortcutMap` and `IShortcutContext` already.

**The two machine assemblies are published, and the rule there is narrower.** `Rack.SoundDevices`
is what an outside machine links to and is the assembly `LICENSE.EXCEPTION` names, so everything
public in either of them is a promise. The test is not "can it be stood in front of" but **would
an outside machine ever write this down**. The parts every range control shares are public,
because a machine drawing a control of its own should feel like the ones we ship: `IRangeValue`,
`IMeterScale`, `INumericInput`, `IWaveformGeometry`, `INaming`, and in the contract itself
`IPanelOrder`, `IPanelNotes` and `IPresetStep`. How our own knob sweeps its 270 degrees, how
our own fader reads its track and how our own tick attribute is spelled are not: `KnobMath`,
`FaderMath` and `TickList` are internal, with `InternalsVisibleTo` for the tests. Internal is not
untested, and that line in the csproj is the whole of what it costs to keep the promise small.

**The parts both worlds draw themselves out of are named for neither of them.** An effect of ours
is not a machine and has a described face all the same, so the types a face is made of were
called `MachinePanel`, `MachineElement`, `MachineElementKinds`, `MachineParameter` and the rest
while half their callers were about to be effects. They are `Panel`, `PanelElement`,
`ElementKinds`, `Parameter`, `IPanelValues`, `IPanelMenu`, `PanelMenuItem`, `MenuCorners`,
`MenuOptionWords`, `IPanelPresets`, `IPanelScope`, `PanelActions`, `PanelStarts`, `PanelTheme`
and `Face` now, and in the library `PanelView` and `PartSample`. The line is **do both worlds use
it**, not does the word machine appear: `IMachine`, `IPanelKeys`, `IPanelNotes`,
`IPanelPads`, `IPanelZones`, `IPanelSlices`, `IPanelTakes`, `IPanelLocation`,
`IPanelPatch` and `IInstrumentName` keep their names, because a keyboard and a kit's pads are
the instrument world and there the word is true. Nothing on disc moved: a `machine.json` names
element words and property names, never a type.

`Rack.SoundDevices` is published, so it is a breaking change, and it was cheap exactly once.
Nothing outside this repository ships against it yet, which is a window that closes on its own.

**And the assembly itself was called `Rack.Abstractions`, which is a role name.** Abstractions
says what the assembly is to somebody else, exactly the way `Helper`, `Util` and `Manager` do, so
nobody could tell from it whether a new contract belonged in there. It said the wrong thing twice
more: the folder did not say the namespace, since `Rack.Abstractions/Faces/` was
`JingleBox2.Rack.Faces` with the middle silently dropped, and inside it the `Machines/` folder
went on saying machine after the application had started saying soundmachine. It is
`Rack.SoundDevices` now, folder, assembly and root namespace all the same words, holding
`Rack.SoundDevices/Faces/` and `Interfaces/ISoundDevice.cs`. `LICENSE.EXCEPTION` names the new
one, and it had to change in the same breath, since that document naming an assembly nothing
builds any more is a licence that grants nothing.

`Rack.Ui` went with it and is `Rack.Controls`, for a milder version of the same reason: Ui names
the layer a thing sits in rather than the thing, and what is in there is a knob, a fader, a
switch, a meter, a keyboard and a waveform, which are controls. It is also the half an outside
soundmachine draws its own panel with, so the name it is written down under wants to say what
you get.

**The namespaces say which world a contract belongs to, and the assemblies are named for the
rack rather than for machines.** `JingleBox2.Rack.SoundDevices` is what both worlds draw themselves out
of, `JingleBox2.Rack.SoundMachines` is what only an instrument has (a keyboard, zones, pads, slices,
takes, a patch, a place in the pattern, the name badge), `JingleBox2.Rack.SoundEffects` is what only
an effect will have, and `JingleBox2.Rack.Controls` is the controls. The folders say the same thing:
`Rack.SoundDevices/Faces/`, with its own `Interfaces` and `Records` under it. The assemblies are `JingleBox2.Rack.SoundDevices` and `JingleBox2.Rack.Controls`,
which is what `LICENSE.EXCEPTION` names.

**The shared level being a namespace of its own is what makes the `Panel` collision loud.**
`Panel` is also `Avalonia.Controls.Panel`, and a namespace that encloses another is searched
before any using: while the faces lived in `JingleBox2.Machines` and the controls in
`JingleBox2.Machines.Ui`, ours won silently throughout the controls assembly and the toolkit's
had to be spelled out where it was really wanted. `JingleBox2.Rack.Controls` encloses nothing, so a
bare `Panel` there is the toolkit's again and a file that imports the faces is refused rather
than answered. Which is what the first build after the split said, in one place: `PanelView`,
now `Faces.Panel`. A cref that crosses the split is the same story in documentation, and is
written out in full, since a `<see cref>` resolves by what the file it sits in can see.

Five files in `Rack.Controls` declared `JingleBox2.UI`, the application's own namespace, inside the
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
`TrimRegion` and `AudioOutput`, all four of them Audio's, and they are one to a file in
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
or an interface but `SoundDevicePanel`, which is bound to `ISoundDevicePanel`.

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
`Views/`, `Rack.Controls/`, `Rack.SoundDevices/`, `Diagnostics/`, `Shortcuts/`, `Scripting/`,
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

1872 of them, in about twenty five seconds, with no window and no hardware. They run in CI on every push
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

**And a renamed project outlives its own rename, because `obj/` is not tracked.** The machine
assemblies became `Rack.Abstractions` and `Rack.Ui`, which is what they were called at the
time, git took the sources with the rename, and
`Machines.Abstractions/` and `Machines.Ui/` stayed on the disc holding nothing but an old `obj/`
and `bin/`. The csproj removes the two new names from its globs and has never heard of the two
old ones, so the app swept `Machines.Ui/obj/.../JingleBox2.Machines.Ui.AssemblyInfo.cs` into
itself and every assembly attribute was defined twice: sixteen errors of `CS0579`, all of them
naming generated files, none of them naming anything anybody wrote. `dotnet clean` does not touch
it, since clean only knows the projects the solution still has.

The shape is worth more than the instance, because it comes back every time a project is renamed
or dropped: **the compile glob is over the folder, and the folder outlives the project.** So a
rename is only finished once the old directory is gone, and the tell is an error inside `obj/`
naming an assembly that is not in the solution any more. The two published assemblies had to be
removed from the glob for the same reason `Tests/` did, which is that everything here lives under
one folder.

Four of the newer files are about rules that used to be a comparison buried in a control, and
each one was written because the buried version was wrong. `Tests/VolumeScaleTests.cs` is the
old 0 to 64 column being brought onto the new one, including the trap that conversion always
falls into: a song of this build going through twice and being doubled the second time.
`Tests/QuantizeGridTests.cs` is which note values a setting can offer. `Tests/PointerDragTests.cs`
is when a press has become a drag, and it exists because a row is under twenty pixels tall and
the old rule read a click as a block. `Tests/SoundMachineSaveAsTests.cs` is a machine's folder being
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
- `PanelNotes.Semitone` chose between a note and a plain number by how long the text was, and
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
  - Maximum: 16 pads total (e.g., 4x4, 2x8, 8x2), or 32 with the extended switch on, which is
    `PadMatrix.Usual` against `PadMatrix.Most` and is a switch of its own because a grid of 32 is
    a different instrument from a grid of 8
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
- **A song could be packed and could not be brought back, which is half a feature.** Pack writes a
  `.jibx` with the recordings inside it to wherever you choose, and the Open song dialog is a list
  of the songs folder rather than a file picker, so a packed song carried to another machine was a
  file nothing in this application would open. Found by trying to move a song to another computer,
  which is the one thing packing exists for
- **Import... on that dialog is the other half**, and it is the same word the recordings and the
  rack already use for the same act. `ISongStore.Import` copies the file into the songs folder,
  because that folder is what the list shows and what saving writes to: a song opened off somebody's
  desktop and then saved would land somewhere other than it came from with nothing saying so
- Read before it is copied, through the ordinary `Load`, so a file that is not a song is refused at
  the moment of asking rather than landing in the folder as a row that will not open and has to be
  deleted. **Nothing already there is overwritten**: a name that is taken gets a number after it,
  since a song arriving from another machine under a name you already use is the ordinary case and
  losing the one you had to it would be unforgivable
- **There is deliberately no unpack.** What a packed song carries is put on the shelf by
  `SongStore.Load` on every open, which is the one path that already does it and is the same path a
  song that never left takes. So the import is a copy and nothing else, and the button beside it
  is the Open it always was
- It sits on the left of that dialog rather than beside Cancel and Open, since it is not one of the
  two answers the dialog is asking for: it brings a row into the list rather than closing on one.
  The arriving song is picked, so Open is the next press
- What a pack does not carry is somebody else's plugin, and the help says so now: a VST3 or a CLAP
  has to be installed on the machine the song is going to. What the song does hold is the plugin's
  own patch and its knob positions, and `PluginSlotConfig` is looked up **by id before path**, so
  a bundle living somewhere else on the other machine is still found. That was already true and
  nothing anywhere said it
- **The letter rows had no octave key and no velocity, and both were absences rather than
  decisions.** The octave could only be changed by the number field in the bar, so a phrase that
  crossed one meant stopping, reaching for the mouse and finding the place in the music again.
  Every tracker has a key for it and this one had nothing at all, which is a gap nobody could see
  because no key did the wrong thing
- Read off Renoise's own `KeyBindings.xml` rather than invented, since it is on this machine and
  is what this file already says to do: `*` and `/` on the numeric keypad, with `Ctrl+]` and
  `Ctrl+[` as the second pair, which is the one that matters on a keyboard with no numpad. Held at
  the ends rather than coming round, because an octave that wrapped from nine to nought would put
  a part eight octaves out for one keystroke too many and the note is written as it is typed
- **What a key with no velocity sensor sends is a decision.** Nothing written leaves the volume
  column blank, which the sequencer reads as the instrument's own level and is consistent; a fixed
  level says the same thing out loud and reads back as what a keyboard that always plays at one
  strength would have sent. Renoise keeps both and so does this: `TypedVelocity` is off by default,
  which is exactly what happened before and is Renoise's own `ComputerKeyboardVelocityEnabled`
- **0x7F and not 0x80**, which is the whole of why the column runs to 128. The top step is the one
  level no key can produce, typed rather than played; a letter row standing in for a keyboard is
  producing a key press, so it writes what the hardest possible key press writes. Renoise's
  `ComputerKeyboardVelocity` is 127, which is the same number said in decimal
- `IgnoreVelocity` still wins over it, deliberately: that switch says no velocity is written into
  this pattern at all, whatever produced the note, and two switches disagreeing about one column
  is the fault this codebase keeps naming
- `Ctrl+Shift+V` turns it over, and it had to go **above** the paste in the same switch statement:
  `Ctrl+V` is matched by asking whether Control is held, which Shift does not stop being true, so
  written underneath it the new key would have pasted instead. Both keys are in
  `Help/Topics/app.shortcuts.md`, since a key nobody can find is a key that is not there
- **And a test that waited for the log file to exist was waiting for the wrong thing.** It flaked
  once in a full run: the file was there and one of the two lines it was looking for was still in
  the log's own queue. It waits for the content now. Worth keeping because the shape is general,
  which is that **the existence of a file written by another thread says nothing about what is in
  it**, and a test that asserts on the difference passes almost every time
- **The instrument list stays in instrument order and lights the row the cursor's track plays.**
  Asked for the other way round, sorted by track, and that could not be had: **the number on a row
  is what the pattern writes into every cell**, so sorting the rows would leave the numbers running
  02, 00, 01, 03 down the page, and renumbering them to match would mean rewriting the instrument
  column of every cell of every pattern. It also has no answer for the two ordinary cases, an
  instrument on no track and two tracks sharing one
- So nothing moves and one row is marked, which is what the chain and the automation strip under
  the pattern already do with the cursor's track. `InstrumentSlot.UnderCursor`, set from
  `Song.GetTrackInstrument` rather than from the rows, so a track with nothing on it lights nothing
  and two tracks on one instrument light the row they share
- Accent and bold together, which is the pair the tab strip settled on and for the reason written
  there: on a light theme the accent alone is near enough the ordinary lettering that nothing reads
  as chosen. The track badge is filled in rather than washed at the same time, so the row is legible
  from the badge column as well as from the name
- **It also answers the question that led to it.** A note typed into a track carries that track's
  own instrument number, and blank means the track has none pointed at it, which was invisible: the
  badges said which track each instrument was on and nothing said what the track under your hand
  was playing. Now a track with no instrument lights no row
- **A song named its plugins by where they were, and where they were is the one thing about them
  that does not travel.** Both places a song names one, the instrument a track plays and a slot on
  a chain, built what the host loads straight out of the stored path. So Gruber carried to a
  Windows machine found Serum installed, scanned and listed, and asked the host for
  `/home/peter/.vst3/Serum2.vst3`. Reported as the plugins being there and not working, which is
  exactly what it was
- **The identity was written down all along and nothing read it.** The song holds
  `56534558667350736572756D20320000` for Serum 2 beside the path, and the field documentation on
  the chain's own id said in as many words that it was tried first. It was not, and **this file
  had already recorded that shape once**: a paragraph describing work still to do outliving the
  work, with the tell being that the document disagreed with the code. It cost the same mistake
  again, since the prose was read out as fact twice before anybody opened the reader
- `IPluginsHere` is the one lookup and `PluginHost.Open` is where it goes, since that is the
  single funnel both an instrument and a chain slot reach loading through. Three comparisons,
  first one wins: **the id, then the name, then the path and only where exactly one plugin has
  it.** What was asked for comes back unchanged when nothing matches, which is not a failure: a
  plugin this machine has not got keeps its name so it can be reported as missing
- **The path is last because of a fault that has nothing to do with travelling**, and this
  repository's own test song is the proof: Serum 2 and Serum 2 FX have different class ids and
  **the same path**, since they are two classes in one bundle. Matched by path a song could be
  handed the synthesiser where it asked for the effect, on the machine it was saved on. So a path
  shared by more than one of them decides nothing and the answer falls through
- The name is second and it is the step that will carry the load. Whether a VST3 class id really
  is the same bytes on two platforms cannot be settled from one of them: `Vst3Abi.HexId` reads the
  sixteen raw bytes of the class id out of the plugin's own factory and hexes them in order, with
  no GUID formatting and no byte swapping, so it is whatever the developer compiled in. **A step
  that can only ever be right or silent is worth keeping even when you doubt it fires**, and the
  log says which one found it, so the first run on another machine answers the question rather
  than either of us being right in advance
- **And a song says which kind of machine wrote it now.** `Song.WrittenOn` is one word, stamped on
  every save rather than kept from where the song began, since what anybody wants to know is
  whether the paths in the file in front of them mean anything on this computer and those were
  written by whoever saved it last. Empty in every song already on anybody's disc, which reads
  back as unknown, and unknown behaves exactly as before
- **Written on and not made on, and not `HostOS` either.** Made on implies where the song began,
  which is the thing it deliberately does not record: a song begun here and saved on Windows has
  Windows paths in it and has to say Windows. The log line was already saying "was written on",
  so the field and the sentence about it disagreed until the field was renamed. Host is worse
  again, since a host in this codebase is the thing that hosts a plugin and nothing else, and one
  word meaning two things is the fault `device` already cost a rename for; nothing in the song
  model abbreviates either, which is why it is `LinesPerBeat` and not `LPB`
- Which lets the path comparison be skipped rather than merely failing. That looked like it bought
  nothing, since two paths from two operating systems cannot match anyway, and there is one case
  where it does: **a settings file carried between machines** puts the other computer's paths into
  the list of what was scanned, and then a path match succeeds and hands back a plugin that is not
  on this disc. A question that can only answer no is not worth asking; one that can answer yes
  and be wrong is worth refusing
- It is said out loud rather than only acted on, because a song quietly behaving differently
  because of where it was made is worse than one that says so: opening a travelled song writes a
  line naming both machines and what it means
- RECORD asks the songs as well as the rack before deleting a take (`SampleUsers` over
  `SoundMachineRack` and `SongStore`). A song owns its instruments, so a recording nothing on the
  rack plays can still be the sound of three songs, and deleting it used to empty them with
  nothing said. Only `song.json` is read for this, and the answer is cached per song by its
  write time: the shelf asks once per take, so the uncached version opened every song file
  once per recording
- A hardware knob is pointed at a software one by resting the pointer on it in the other mouse
  mode (Ctrl+Shift+M) and touching the control on the desk. The mapping names the machine and
  the parameter key, never a track or an instrument id, so it is Zampler's cutoff on every track
  and in every song; which track is a separate question answered by `ControlScope`. Only things
  that name a `Parameter` can be pointed at, and buttons separately as actions; a label
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
  take picker cannot be pointed at. A mixer link is the desk's, like every other link: it names a
  strip number, and strip three is strip three in every song. Touching a strip anywhere picks its track, tunnelled so grabbing a
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
- **Both of those are about one controller, and the second one had lost it.** The same physical
  control pointed somewhere else is obviously one box; something else pointed at the same target
  had no controller in the test at all, so a second desk pointed at a machine deleted the first
  desk's links on it as they were learned. Which is the opposite of what is written a page down
  and what the whole design rests on: a link answers only its own controller's messages, so two
  desks pointed at one machine can never both fire and are not competing, and hardware A and B
  against machines 1 and 2 is four templates rather than a fight. `ControlLink.SameDesk` is that
  half of the rule, and a link naming no controller is the wildcard it reads as everywhere else,
  displaced by any of them since it really would answer beside one
- It cost twice over, because a template here is the links themselves rather than a file: the
  surfaces line on a face lists what survived, so somebody with two boxes on the desk lost half
  a template and then found the repair was made out of the damage. Reported as both halves of
  one sentence, the CCs not being saved per hardware and the hamburger restoring only half the
  knobs, and it was one fault. `Tests/ControlDeskTests.cs` is the rule; four knobs learned on one
  box and two of them learned again on another is the shape of it
- **And two threads writing one file were writing through one half-written file.** The settings
  are saved from the drawing thread whenever anything on a page moves and from the MIDI thread
  when a knob is learned or a control's own behaviour is worked out, and `SafeFile` named the
  half-written file after the path and nothing else. The second writer could not create it,
  deleted it on the way out, and left the first with nothing to move into place; the fallback
  then opened the real file and could leave that broken too. From outside it is a settings file
  that occasionally loses whatever was last put in it. Each write has its own now, the process in
  the name as well as the count, since this executable runs again as a plugin's host
- **Where a link is kept changed and what decides whether it fires did not.** A link answers
  only while the thing it names is the thing in front of you, which is the point of the whole
  design and is untouched by the layers going: `ControlTargets.Find` reads the focused track,
  `OnMachine` refuses unless that track's machine id is the mapping's, `OnRack` refuses unless
  the machine open on the rack is, and `OnPlugin` refuses unless the track really has that
  plugin. Knob one is not the first knob on this track, it is OddSkilla's cutoff, so a track
  playing a drum machine is not driven by it at all
- **A plugin cannot be pointed at, and that is a decision rather than a gap.** A VST3 or a CLAP
  is somebody else's program and brings its own MIDI learn, which it keeps itself: a link made
  here would be a second mapping beside the plugin's own with no way to make the two agree. So
  remote control is for machines, our own effects and the mixer, which are the things this
  installation is the only owner of. `PluginParameters` offers nothing, `PluginControlsViewModel`
  no longer offers the parameter you last touched, `ControlLink` drops a plugin link as it reads
  the settings, and `LinkTargets.Point` refuses the `effect` word so a template carrying one is
  counted and left out rather than failing the whole file
- It was built first and taken out with a session's work behind it, so the reasoning is worth
  keeping. Pointing at a plugin really did work: the host draws a knob per parameter behind the
  **Knobs** button, and for a plugin with a face of its own both standards say which parameter
  you just touched, VST3 at once and CLAP at the end of the block, so turning Vital's own Level
  knob offered `Insert Vital Oscillator 1 Level` with no host knob involved. What could never
  work is the other half. There is no way to draw inside another program's window, VST3 has no
  call asking a plugin to highlight a control, and the CLAP one that does, `param-indication`, is
  no use to a VST3. So the gesture had no confirmation where it mattered, and a knob learned that
  way sat beside whatever the plugin had learned for itself
- Ctrl+Shift+M on a plugin's window says so rather than doing nothing. `PluginWindow` answers the
  keystroke itself instead of calling `LinkKey.Listen`, and swallows it, which is the opposite of
  what `LinkKey` does with a keystroke it will not answer: there it is left alone because it may
  mean something to whatever is in front of you, here it is being answered with a sentence. It
  cannot be caught while the plugin's own interface has the keyboard, since those keys never
  reach this process
- Automation is the one thing that still points at a plugin, and it is untouched. A lane names an
  insert on a track's chain through the same `ControlKind.Plugin` and the same
  `ControlTargets.OnPlugin`, which is why neither was removed: a lane is this song saying what a
  parameter does over these lines, which is not a fact about your hardware and does not want to
  be a template
- **There is one layer, and everything pointed at anything lands on it.** A knob pointed at a
  machine's filter is a fact about your hardware and that machine, true of every song that plays
  it, and so is a knob on a mixer strip, since a strip is a number and strip three is strip three
  everywhere. Kept in the settings, listed in SETTINGS and in the tracker under MIDI CC
- Which also means a link made inside a song fills the template for that same pair. Point the
  nanoKONTROL2 at OddSkilla's cutoff on track three and it lands on the card headed OddSkilla,
  nanoKONTROL2, the same card pointing at OddSkilla on the rack fills, because the link writes
  the machine's id and the parameter key and never the track or the instrument's own id. A strip
  writes the strip number and goes the same way. `Tests/ControlCardTests.cs` says so
- It was two layers, the desk's and the song's, decided by where you pointed: an instrument on a
  track or a strip on the mixer put the link in the song's own `.jibx`, and the song's won where
  both named the same control. Templates are what that was reaching for and could not be. A copy
  of the same layout per song is the same work done again for every song, it cannot be handed to
  anybody, and the layer it lands in depended on which of two identical-looking panels the
  pointer happened to be over. `ControlLink.Handle` writes to the desk and nothing else does,
  `Pointable.InSong` and `SoundDevicePanel.InSong` are gone with the decision they carried, and
  what an older song is still holding is still read and is still displaced by an arriving link,
  so nothing laid down before this starts fighting what is laid down now
- **A card is one controller against one target, which is a template.** `Views/ControlLinksView.axaml`
  draws one for each pair, headed with the thing pointed at, the sort of thing it is and the
  controller, and opened by the same chevron the machine editor's cards use. **Folded away to
  begin with, and one open at a time**, which is what the shape of the thing asks for: a card is
  ten or twenty rows, so a desk pointed at six machines is a page nobody can hold in their eye,
  and folded the list is a heading apiece, which is the shelf of templates and is what somebody
  opens the page to see. Which one is open is the list's own answer and not a flag per card,
  since there is only ever one, and it is held by key so the list being thrown away and built
  again does not fold up the card you are working in. It was a card per target with a section per
  controller nested inside, which drew the same templates one level down and made the card a
  thing no file could be written from. `docs/control-templates.md` is what is built and what is
  next
- **The mixer is one card and not one per strip**, and it is the one kind whose id is left out of
  its key. A knob is pointed at the mixer: the desk in front of you has a fader for every strip,
  and what you keep, hand on or lay down again is the whole layout. Cut by strip it was a card per
  fader saying the same three words with a number changed, and a file per fader nobody could use.
  The master goes in with them, being a strip of the same desk, and the card is headed Mixer
  rather than with whichever strip happened to come first
- The strip is not lost by that. It is still what an individual link names, and a mixer template
  writes it on each of its lines rather than once in the target: `ControlTemplateControl.Strip`,
  the word master or the track's number counting from one, which is what the screen says. A
  template written before the strip moved onto the line named its one strip in the target, and is
  still read that way, since a file on somebody's disc outlives a decision about how cards are cut
- The word for a machine, an effect or a mixer strip, taken together, is **target**, which is
  what `IControlTarget` has meant since the beginning rather than something invented for the
  page. It is deliberately not called a device, although that is what Renoise, Bitwig and
  Ableton all call it: this is a page about MIDI, where device already means the thing on the
  desk, and the two ends of the wire may not share a name. `ControlDeviceLinks` became
  `ControllerLinks` and then `ControlTemplateLinks` for the same reason. The umbrella is not shown at all in the interface,
  where a card is headed with the thing itself and the sort of thing is a quiet word beside it,
  so nobody has to learn the word to read the page
- The list is in one place, which is **MIDI CC**, the last word along the top.
  `Views/ControlLinksView.axaml` is that one drawing, bound to the list rather than to whoever
  holds one. It had once been written out twice and the two had already drifted apart by a column,
  which is why it is one drawing however many pages want it.
  It was a button on the tracker's own bar first, on the reasoning that the tracker is where the
  pointing gesture is made, and that was wrong about what a template is: the same page holds what
  a nanoKONTROL2 does to the mixer, which is a fact about the desk and about no song at all. A
  word of its own, because a template is worked with rather than set up once. It was also drawn
  at the foot of SETTINGS, Control Surfaces, which is where the hardware is looked after and is
  therefore the one page it looks like it belongs on. It does not: two ways in to one list is two
  places to go looking and one of them is always the wrong guess, and the list is long enough to
  bury the device rows that page is actually for
- `ControlMapping.Owner` is what a link is pointed at in the words on the front of it, beside
  the ids that decide. Separate from `Name`, which is the owner and the control run together:
  under a card headed OddSkilla the rows want the rest of the sentence, and there is no way back
  to the two halves once they are one string. A link made before it existed has the name read
  back out of it instead, since every one of those was written as the owner and the parameter
  key run together, and removing the key leaves the owner. Machines only: a plugin's parameter
  is named by the plugin and was never written down here, so an old effect link keeps its id as
  a heading, which is plain and is still the right card
- **A template is a file, `*.jbtl`, and it is written and read where the cards are drawn.** JSON,
  written whole, in `templates/` under the application folder by default and openable from
  anywhere, since the point of one is that it travels. Every value in it is a word rather than a
  number out of an enum, so it can be read, corrected and sent on by somebody who has never seen
  this code, and `parameter` is one field for all four kinds because to a knob a machine's key,
  a plugin's number, one of the mixer's six words and one of the transport's five are one
  question in four vocabularies. Export is on the card, because the card is the template: it was
  on a line inside the card while a card could hold two controllers, and a file holding both
  would have landed on somebody who has one of them
- The port is the only thing in a link that cannot travel, and settling it is the only conversion
  an import does. The same nanoKONTROL2 is `nanoKONTROL2 _ CTRL` to the ALSA sequencer and
  `nanoKONTROL2 _ SLIDER/KNOB` to rawmidi, and Windows spells it a third way, so a file names the
  controller as its profile calls it and the ports are looked through on arrival. A controller
  that is not plugged in keeps the name the file carried and its links wait for it, which is the
  rule a link already kept: a controller left in the other room is not a decision to unwire it.
  Said out loud, because a template that applies perfectly and moves nothing until the device
  arrives reads exactly like a file that failed to open
- Conflicts needed no new rule. `ControlLink.Take` lays a batch down by the rules a link made by
  hand keeps, so an arriving link displaces whatever held its control and whatever else was
  pointed at its target, and importing the same template twice leaves what once did. One act
  rather than a run of them: the list is said to have changed once, so the page is not rebuilt
  forty times, and the settings are written once. A caller looping over `Handle` would be right
  about every link and wrong about the whole
- What cannot be read is left out and counted rather than failing the lot, which is what a
  template from a newer version looks like: mostly this version's, and the useful answer is the
  part that works plus a line saying how much did not
- **The instrument's name in the song is a part too, and used to be the exception that proved
  the rule.** `ElementKinds.InstrumentName` is the badge, dropped on the panel like a Knob
  and placed by whoever builds the machine. It was drawn over every panel from code, in a corner
  this program chose, which is the one thing a machine's face is never supposed to have done to
  it: a machine that had never asked for a badge grew one, and a machine that put a Menu in that
  corner had the two drawn on top of each other. Two goes at moving it out of the way, beside the
  Menu and then centred, both looked like what they were, which is furniture shuffled around
  somebody else's design
- It turns no parameter and cannot: a machine is called what the machine is called and an
  instrument off it is yours to call anything, so the name belongs to the song. `IInstrumentName`
  is the two questions a panel asks about it, what it says and whether it may be changed here,
  and a machine on the rack answers the second no, since renaming there would be renaming the
  machine. A machine with no badge shows no name, which is a machine saying its face is its own;
  nothing is lost, because the window is titled with it, the rack lists it and the song's
  instrument list renames it. `Tests/SoundMachinePartsTests.cs` reads the machines that ship off the
  disc and says each carries exactly one badge and at most one Menu, that neither is pointed at
  a parameter, and that the words in those files are the words the code spells
- **A machine's face can carry a Menu, and it is a generic part.** `ElementKinds.Menu` is
  dropped onto the panel in the designer like a Knob, placed where the person building the
  machine wants it, and carried in `machine.json` with the rest of the face. It turns no
  parameter and never will: what is in it comes from the host through `IPanelMenu`, exactly the
  way `Keys`, `Take`, `Preset` and `Zones` are already filled. It is not named after what it
  holds, because what it holds is going to grow
- **Which options it drops down is chosen in the designer**, tick by tick, from
  `MenuOptionWords.All`. Three today: `help`, the device's own page, `surfaces`, the control
  surfaces there is a template for on this machine, and `learn`, which turns over the same mode
  Ctrl+Shift+M turns over. An option
  added later turns up on the ticks and in every machine that has never been near that page,
  because a Menu naming no options carries all of them. `IMenuOptions` is that rule on its own so
  it can be asked without a window: a machine naming an option this build has never heard of
  carries the ones it does understand rather than refusing the part, and a line belonging to no
  option is always carried
- **A corner of the machine, and not of the window around it**, which is the whole reason it had
  to be a part. A button on the editor's card would be the host talking about the machine from
  outside it, would exist only in the designer, and would be gone in the rack's window and in a
  track's instrument window, which is where somebody actually sits with a machine and a
  controller. It is also the only shape that does not break the rule this file keeps in three
  places, that nothing is added to a machine's face from code, since here the machine asks for it
- **The three bars on it are drawn and not written.** They were U+2630, and a character is at the
  mercy of whichever font the machine running this falls back to: it came out a third of the
  height of the cap and left of the middle, since the fallback's advance is wider than its ink.
  `CapMark.Bars` on `PushButton` draws them to the cap's own size, so a machine asking for a
  bigger button gets a bigger mark and nobody keeps two numbers in step. A cap with a word on it
  draws the word: the mark is for the button whose meaning is a picture
- Every machine that ships carries one, in the upper right, said in each file rather than left to
  the default so they cannot drift apart the first time one is edited. It sits sixteen pixels in
  from the corner, which is not decoration: the panel is almost always inside a scroll viewer and
  the bar is drawn over the content's own right edge rather than beside it, so a Menu hard against
  the corner has the bar through it. A machine that wants it elsewhere says so with a margin of
  its own, which is honoured instead
- **It is drawn over the panel rather than in it.** `Build` gives nothing back for a Menu and the
  panel puts it on a layer of its own, so where it is dropped in the tree makes no difference and
  the corner is the whole of where it is: laid out with the machine's controls it would take a
  row of the face and push everything else about. Two corners, both at the top, chosen from a
  list in the designer rather than typed. The top right is the default, and the top left is there
  for a machine whose own artwork wants that side
- **The two lower corners were offered and had to go.** A panel taller than the window it is
  shown in scrolls, and the bottom of the panel is then below the fold: the button was really
  there and nobody could see it, which reads exactly like a machine whose change had not taken.
  That is what "it is not updated, only a restart fixes it" turned out to be, and the registry
  was innocent: a machine removed and added again in SETTINGS really does redraw the panel from
  the new file, which was measured by doing it. A machine saved while the lower two existed still
  opens and its menu comes back to the corner a hand looks in
- **One menu to a machine**, and it is the only part with a limit: a second is either in the same
  corner drawing over the first or in another offering the same lines twice. Adding one where
  there is one already says so and names what to do instead, and turning another part into a menu
  is refused the same way; the one that exists may still be turned into something else and back,
  since the rule is about a second menu and not about the menu
- **A template is the links themselves and not a file**: the card the MIDI CC page draws, cut by
  `ILinkTargets`. So the surfaces option lists one line per controller pointed at this machine,
  and picking one re-applies that template through `ControlLink.Take`, which takes back anything
  pointed elsewhere on that machine since. Hardware A and B against machines 1 and 2 is four
  templates and there is no conflict between them: a link records the controller it was learned
  on, so A and B both drive machine 1 and neither displaces the other
- **The mixer has the same button and it is not the same thing.** The Menu part is generic: a
  machine asks for one, says which corner, and ticks which options it carries, and there will be
  more options. The mixer is drawn by this program rather than described by anybody, so there is
  no description to drop a part into and no options to tick: it is a button in the mixer card's
  own header, always showing the control surfaces pointed at the mixer and the line that starts
  learning. What the two share is the lines behind them, and `IMenuLines` is the one place a line
  becomes something on a screen, so "a line with no command is dead" is not spelled twice
- A mixer link is on a strip, and the mixer's menu names no strip: the whole desk is one thing to
  point a controller at, so what somebody wants to see is what their nanoKONTROL2 does to the
  mixer rather than a menu per fader. That is the whole of what an empty id means to
  `Midi/ControlMenu.cs`, which is what `MachineLinks` became when it stopped being only about
  machines
- `Midi/ControlMenu.cs` is what fills the menu today. It keys by `ILinkTargets.KeyOf`, the same
  rule the cards are cut by, so the page and the part cannot drift into listing different things,
  and nothing in it compares an id itself: how exact an id is is that rule's business. It reaches
  the links through a question defaulting to `ControlLink.Current`, which is the door the
  instrument panel already goes through, and a question rather than the door itself so that
  having no desk at all can be tested
- `RackSoundMachine.DetailText` was a second copy of `TrackerInstrument.Detail` and is the instrument's
  own answer again. The two had already drifted, which is what that duplication always does: only
  one of them had been told that effects exist, so the row called an effect a plugin. This file
  already recorded the same lesson once, when the sentence was briefly written out on both of the
  two things that print it
- **No plugin could be pointed at, and that was the whole of the link remote's remaining hole.**
  A knob is pointed at by resting the pointer on a control the host drew, and the host drew none
  for a plugin with a face of its own: `PluginControlsViewModel.Prepare` stopped the moment it
  had opened the editor, so the knobs were never built. Every plugin worth having has a face, so
  this was impossible rather than awkward, and it was impossible for instruments as much as for
  effects. Nothing in the log ever said so, because a gesture nobody can make writes no line
- `ShowsKnobs` is the switch and the Knobs button in the plugin window's header is where it is
  thrown, beside Bypass and offered only where there is a face to switch away from. The knobs
  are built the first time somebody asks and never otherwise, since reading two thousand
  parameters into two thousand controls is a visible pause and Serum answers with 2622: a plugin
  opened for its own face pays nothing
- Hiding the face takes the native child window with it, which is the one order that matters:
  `PluginEditorHost` detaches the plugin before the window goes, because a plugin still drawing
  into a window that has gone is a crash inside its own toolkit. Create and destroy are
  symmetric, so switching back puts it in again
- Proved on the wire rather than by reading: `link: offering Insert ZamComp Attack` is the first
  Insert offer that has ever appeared in this application's log. ZamAutoSat is worth knowing
  about as a witness that is not one: it reports no parameters at all, so the host draws nothing
  for it however it is asked, and its chain block prints no readings either
- `ILinkTargets` is what a link points at, said in words and read back out of them, and it is one
  rule because the page cuts its cards by it and the file is written by it. Two spellings would
  eventually disagree, and the way that fails is a template that means one thing to whoever
  exported it and another to whoever opened it. Not to be confused with `IControlTargets`, which
  reaches the live thing so a value can be written into it; this one only ever deals in words
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
- **The log was quietly losing lines, and it was in the one part of it that is deliberately not
  behind an interface.** The rules came out into `ILogAreas`, `ILogLine` and `ILogFile` and each
  can be asked without a process; what the door was left holding is a queue, a thread and a file,
  and nothing stood in front of the handover between those three. `Flush` drained the queue and
  appended, with no lock, and **there is always more than one flusher**: the writing thread runs
  on its own clock and anything may flush by hand, which the way out of the process does and
  `Clear` does. Two of them inside at once each took a share of the queue and then opened the same
  file, and the one that lost the open had its share swallowed with the exception, since a log may
  not throw in the thing it is a log of
- So the symptom was **a line that was written and is not in the file**, at random and under load,
  which is the worst thing there is to be chasing with a log. Found by a test of something else:
  the tangent switch's own line, written twice and read back once
- An interface would not have caught it and it is worth saying why, since the obvious lesson is
  the wrong one. `ILogFile.Append` is already a seam and did exactly what it promised; the fault
  was two threads in one method of the door. What was missing was a test of the door's own
  concurrency, and `Tests/LogFlushTests.cs` is that: two thousand numbered lines written while
  three threads flush by hand beside the log's own. **Checked by taking the lock out**, which
  loses lines 0, 2, 3, 9 and 14 of the first fifteen, because a guard that no test notices the
  absence of is a guard that is testing nothing
- **The log is kept between runs and is cleared on purpose, in SETTINGS.** Never on start: the
  run you most often want is the one that already ended badly, and a log cleared on start has
  thrown away the crash you restarted because of. It rolls over at four megabytes keeping one
  `.old`, so two files is the bounded cost, and each run writes one boundary line naming the
  areas and the build, which is what to search for to find where a run begins. Clear the log
  takes both files and says the boundary line again at once, so the fresh file starts the way any
  other run does rather than mid-sentence: it is why `Announce` is allowed to run twice in one
  process. Not asked about first, unlike deleting a recording, because a log is not somebody's
  work
- `LogArea.Machines` is the sixth area, and everything under `SoundDevices/SoundMachines/` writes to it
  rather than to the app's. It is a whole half of this program and it says almost nothing while
  nothing is wrong; the day a machine draws an empty panel or comes back from a zip missing a
  picture, the last thing anybody wants is to read that out of everything the application did at
  startup. The tick box in SETTINGS appeared on its own, since that page is built from
  `Log.Everywhere`
- The log switch is per area everywhere now. Ten places gated on `Log.IsOn`, which is any area at
  all, and then wrote to one: two of them on the audio thread, so switching MIDI logging on made
  the mixer do census work per block that nothing would ever print
- A panel hears about a value it did not write. `IPanelValues.Said` is raised alongside the
  owner's `Changed` callback, and `PanelView` subscribes to it and reads itself again,
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
- **And it stopped a song that was playing, for no reason anybody watching could see.** It went
  through `Adopt`, which is opening a song: a fresh object swapped in, the plugins put down and
  loaded again, the playhead sent to the top. `RunClock` takes the song and the sequencer once
  at the top of a pass and keeps them, so a new object really does mean a new pass, and the
  stop was honest about what `Adopt` does rather than about what cancelling is. **Cancelling is
  an undo taken all the way back to the file**, which this codebase already had the shape for:
  `Restore` pours the file's contents into the song that is open, exactly as `Pour` pours a
  history step in, so the player, the mixer, the panels and the tracker go on holding the object
  they already hold and nobody has to be told. The transport is left running and the playhead is
  left where it was
- The chains are made to agree only where they differ, since rebuilding one is seconds a plugin
  and most cancels change none, and a plugin instrument keeps its process wherever the track
  still names the same id, since the players are held by track and matched by id. What `Restore`
  does that an undo does not is empty the history and mark the song clean, which is the point of
  the button, and drop the copy kept for a crash. The cursor is held inside what came back rather
  than sent to the top: the file may hold a shorter pattern than the work being thrown away, and
  moving somebody's cursor is not part of what they asked for
- `Tests/CancelChangesTests.cs` is the four facts under it, and the last of them is the one that
  fails if the button ever goes back to opening the song. A running pass follows the contents of
  the object it was started on, which is why pouring works; it does **not** follow a different
  object handed to `Use`, which is why it has to be a pour; pouring keeps the object everything
  is holding; and cancelling over a real tracker with the clock running leaves it running. The
  dialog is not in any of them, since asking needs a window and the asking was never what was
  wrong
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
- **Every page starts the same distance under the tab strip, and `tabRoot` is that distance.**
  The rule was already written in `App.axaml` and reached nothing: its selector named
  `StackPanel` and no page is one, so all six set their own and drifted, twelve above the takes,
  six above the pattern, and the settings rail adding more of its own. Any control now, and each
  page says which it is rather than how much. A page that paints its own background leaves the
  room inside the paint rather than outside it, or the strip above shows through the gap in
  another colour, which is the one reason `Border.tabRoot` is a second rule and not a special
  case. Measured off the screen rather than judged: the card's top edge is on the same pixel on
  all six pages, where it used to vary by twelve
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
- **Closing the window is Cancel changes, and the copy kept for a crash goes with it.** The
  tracker writes the open song into `<name> (recovered)` every twenty seconds while it is dirty,
  which exists for the twenty minutes between two saves that a plugin taking the application down
  would cost. Closing the window costs nothing of the sort: what was unsaved then was left unsaved
  on purpose, and a rescue file in the songs list the next morning saying the last session never
  saved reads as a fault rather than as a rescue. `TrackerViewModel.Finished` stops the timer
  first, or the tick arriving while the window goes would write the file back after it had been
  thrown away. The crash report is untouched: the run marker still comes off only on the way out
  of the process, so a real crash still leaves one and still writes a report
- **The splash says what is happening rather than that something is happening.** Every line it
  writes is set from the drawing thread in the middle of the work it describes, so nothing would
  be painted until that work was over and the line had already been replaced: `SplashWindow.Said`
  runs the queue as far as the frame and then holds the line for `LineHold`, which is 250 ms.
  Two lines, a heading and the thing under it, and `Doing` clears what was under it, since a
  device name left standing beneath a new heading is the one thing on there that could say
  something untrue. It reads the devices out by name, which on a first run is also the list of
  what was just copied into the application folder, and then the engine settings one at a time
  before the output is opened. `IStartupLines` is the seam, so a window built with nobody
  watching hands in nothing and every line is skipped
- **A take was saved as 200 milliseconds of silence, and the cause was stopping being two acts.**
  Reported as recording not working at all: a four second performance on the shelf under the
  right name, 8820 samples long, drawing a flat line. 8820 frames is exactly the monitor's own
  buffer, which is the tell and is what named the fault. While no take is being made the capture
  buffer is a meter's: every block that arrives trims it back to the last fifth of a second.
  `StopRecording` set the flag and left the audio where it was, so **a block arriving between the
  flag and the save read the take as monitoring and threw it away**
- It was a race before RECORD had a chain and it was a race that mostly won: the window was a few
  instructions. Working out the clean take's name widened it by a walk of the shelf, which was
  enough to lose almost every take. **A window of a few instructions is still a window, and what
  falls through this one is somebody's only copy of a performance**
- `ITakeBuffer` is that seam and the fix is what the type is for: `Stop` clears the flag and lifts
  the audio out **under one lock**, and every other member takes the same lock, so there is no
  moment left for a block to arrive in. The flag and the audio are one fact rather than two, which
  is the shape `docs/threads.md` already names: a value wants a lock, a shape wants to be one
  object swapped whole
- `Tests/TakeBufferTests.cs` is the seam with two threads on it, since one thread cannot fail the
  way this failed: a capture thread adding as fast as it can while another stops the take, run a
  hundred times because a race that fires one time in twenty passes a test that runs it once.
  **Checked by putting the fault back**, and it answers 35280 bytes, which is the monitor's length
  to the byte and is what was on his shelf
- **Two copies of this application fight over the recording input, and neither says so.**
  `PipeWireRouting` finds its own capture by looking for nodes named `JingleBox2`, so a second
  instance's capture node answers to that name too: each one's Connect deletes every link into
  both and rebuilds its own, and the two second refresh means they do it to each other for ever.
  From a chair that is Capture from flipping back to nothing every couple of seconds and
  recording that will not work. Nothing in the log mentions it, because from inside each process
  everything succeeded
- Worth keeping because it is not a bug so much as a thing to know: **the routing is machine wide
  and a second instance is not sandboxed by pointing it at another settings folder.** The graph
  is shared, the node name is the only identity, and `pw-link -i | grep -i jingle` is what says
  whether anything is left behind

- **The mixer's IN strip drew a meter that never moved, and nothing was broken.** Its reading is
  `Record.LevelLeft`/`Right`, the input was opened only while RECORD was the page in front, and
  with the capture closed that reading really is nought: the meter was reporting the truth about
  an input nobody was listening to. The fader still worked, since a gain is a stored number
  rather than something measured, which is what made it look like a broken meter beside a
  working fader
- `IInputWatch` is the answer and it is **counted rather than switched**: two pages show that
  meter, either is reason enough to have the input open, and a flag would have whichever page
  left last close it under the one still up. The delay before it really closes lives there too,
  since a theme swap detaches a page and puts it straight back, and closing in between loses the
  routing every time: the system wires a new capture stream to its own default
- **Watching the input and reading the audio graph are two things, and only RECORD does the
  second.** Reading the routes puts the preferred one back when the system has wired something
  else up, which is rewiring the machine's graph, and a page with no route picker on it has no
  business doing that every two seconds. `WatchRoutes` is RECORD's alone
- `Tests/InputWatchTests.cs` is the count, and the two that earn it are one page leaving not
  closing the input under the other, and a page that comes straight back keeping it. The second
  caught the double rather than the code: an input already open is left alone, exactly as
  `StartMonitoring` answers at once where it is already listening, and without that a
  re-template would reopen the capture and lose the routing

- **A take is not on the shelf until somebody names it, and the scratchpad is where it waits.**
  Pressing Record used to write straight into the recordings folder under whatever the name box
  held, so every false start, every level check and every accident was a row in the list with a
  search box over it as though it were work worth finding again. `ITakeScratch` is a folder of
  its own under the application folder: what comes off the input is written there, and reaching
  `recordings/` is a separate act with a name in it
- **`Sweep` runs on the way in as well as on the way out.** What is left when the application
  closes was never asked for, and a run that ended badly leaves files that are by definition the
  ones nobody kept, so a folder that is only emptied at closing time fills up on every crash
- It holds **one take**, which is the whole meaning of the word: recording again is starting
  again and what was on it goes. Chosen over a session's worth of them deliberately, and the cost
  is named rather than guarded against: a take you did not save is gone when you record the next
  one. No dialog in front of Record, which is the rule this file already keeps about a question
  in the way of a button that used to do something
- **A name already on the shelf is refused rather than numbered.** By the time `Keep` is reached
  the name has been through the box's own check, so a second answer here would be two rules
  disagreeing about one name; and what refusing protects is the one thing that cannot be undone,
  which is last week's take written over by this week's. Kept by moving rather than copying, the
  same reasoning the bin already keeps: a take is the one thing here that can be a hundred
  megabytes
- **The scratchpad is a card of its own and the name box moved onto it**, because naming is what
  saving is: the box over the Record button said a take was going to be called something before
  anything had been recorded, and nothing was ever written under that name. Where a chain made
  two of them both are on the card, picked between by two buttons over one picture, and Save
  keeps both
- `Tests/TakeScratchTests.cs` is the seven rules, and the one worth naming is that a take that
  was kept survives the sweep, which is the whole design said as one test

- **RECORD has an effect chain, and what it does is done after the take rather than during it.**
  The same `PluginStrip` the tracker and the pads use, pointed at `RecordPluginTarget`, so ours
  and somebody's plugin go on it the same way they go anywhere else. It is a setup somebody
  leaves standing rather than a per take choice, so it lives beside the input gain in
  `AppConfig.RecordEffects`
- **Nothing is heard while a take is being made**, which is what decides the whole shape of it.
  With no monitoring there is no reason whatever to put a plugin on the capture callback: a
  crossing is a fixed cost per block, and paying it where a late block is a hole in the only copy
  of a performance is the worst trade in this application. `ITakeEffects` runs the finished take
  through the chain on the pool, where it may take as long as it likes, and **the answer is the
  same** because a chain is a stream processor: handed a take in blocks it makes what it would
  have made in real time
- **Both takes are kept, and the name says which is which.** The take under the name that was
  typed is the one through the chain, since that is the sound somebody set a chain up to record,
  and the capture as it arrived is `<name> (clean)` beside it, because an effect cannot be taken
  off a take afterwards. A chain holding nothing, or nothing but bypassed slots, writes one file:
  two names over the same audio is not a safety net, it is clutter. The clean name is numbered if
  it is taken, the rule an arriving song already keeps
- **The processed take is exactly as long as the take it came from**, so the two lie on top of
  each other frame for frame, which is most of what keeping both is worth. A delay still ringing
  at the last frame is cut off with it, deliberately
- The chain is given two seconds of silence before each take. It holds its own state between
  takes, so a delay line full of the end of one would repeat it over the beginning of the next,
  which reads as the recorder having captured something that was never there. It cannot help a
  chain that never decays, and there is no way to ask an insert to forget: adding one is a change
  to a contract this does not need
- **The scaling is the same number in both directions and that is not a detail.** 32768 out and
  32767 back would mean a take through an empty chain coming back a hair quieter than it went in,
  which is the sort of difference nobody can account for a week later. `Tests/TakeEffectsTests.cs`
  pins the exact round trip along with the block boundary, where an offline pass goes wrong in the
  two ways that both leave a take of the right length: a frame played twice and a frame skipped.
  Only the order of what the effect actually saw says which happened
- Mono in comes back stereo, since an effect places things in the stereo field and narrowing the
  answer would throw half of what it did away; a take of more than two channels is read as its
  first two. NaN is written out as silence, which matters more here than at the converters: a file
  full of it plays as full scale noise the first time anybody opens it
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
- **Ctrl+H opens the help on one page listing every key the application answers.** `Views/HelpKey.cs`
  is that door, a class handler on `Window` like `DeckKeys` and for the same reason: hung window
  by window it is a call every new window has to remember, and the one that forgets is a window
  where the key silently does nothing. `HelpKey.Wants` is the rule with no window in it. Nothing
  is asked about where the keyboard is, unlike the transport's two: a space in a name is a space,
  and no text box anywhere does anything with Ctrl+H, so somebody stuck halfway through a dialog
  is exactly who wants it
- The page names the keys that are written into the application and reads the four that are a
  setting off the map as it is asked for. A page that spelled Save, Delete, Undo and Redo out
  would go on saying Ctrl+Z after somebody had moved undo to F2, which is two spellings of one
  fact drifting apart, the fault this codebase has already paid for twice. `IShortcutSheet` is
  those four lines and the file carries a `{keys}` hole they go into, so the prose around them is
  written where all the other prose is. It walks every action rather than naming four, so one
  added later turns up without anybody being told
- **And writing that page is what found out that the settings page for shortcuts did not
  exist.** Everything under it did: `ShortcutMap` sets, `AppConfig.Shortcuts` stores only what
  differs from the defaults so a default can still be improved, and `IShortcutActions.Everything`
  says in its own remarks that a settings page builds itself from it. Nobody had built the page,
  so the keys were what they shipped as and there was nowhere to change them, while the help said
  "in SETTINGS under Shortcuts" for about an hour. That is the shape of fault worth naming: help
  text is the one place in an application where a feature can be described into existence, since
  nothing compiles it and nothing runs it. The page exists now, and the bullets under this one are
  what it turned out to need
- **A device's help is the device's, and is not a topic in this application's.** What this
  program does is written under `Help/Topics/` and changes when the program changes; what a
  soundmachine's third knob does is written by whoever built the machine, and it has to travel to
  somebody who has never seen this repository. So it is `help.md` in the device's own folder,
  which means the zip carries it, Save as carries it, and a shipped device is brought up to date
  with it file by file like everything else it has. `ISoundDeviceHelp` is the read and the write,
  emptied means the file goes, and `IRackProject.Help` is what everything showing a device already
  holds, so nothing looks it up by folder a second time
- Written in DESIGNER on a **Helptext** tab, per world, with the writing on the left and the page
  as it will be read on the right, drawn by the control the window uses so the preview cannot
  disagree with the thing. Shown from the device's own Menu, which is `MenuOptionWords.Help` and
  is therefore ticked per device like the other options; `SoundDeviceMenu` is the wrapper that
  puts it above whatever the host was already offering, since what a thing is comes before which
  knob is driving it. A device whose author wrote none keeps the line and loses the press: a line
  that is not there says the host cannot do it, a grey one says this device has nothing to say
- `Views/SoundDeviceHelpWindow.axaml` is the window, and it is deliberately not
  `HelpWindow`: no topic list, no search, the device's name and its one line at the top, and the
  page on a plate in the device's own colours. One window per device, so a page can be left open
  beside the device while somebody works it. All eight devices that ship carry one, and
  `Tests/ShippedHelpTests.cs` walks both rack folders and says so, including that a page starts
  with its own device's name, which is how a copied file that was never edited fails.
  `verify-rack.sh` refuses a release payload that lost one, since `help.md` is neither json nor
  wav and nothing it counted would have noticed
- **The help is markdown files, one to a topic, in `Help/Topics/` and linked into the output as
  `help/`.** Lowercase and linked out for the reason the controller profiles are: a folder called
  `help` beside the `Help` the code is in differs only in case, which is two folders here and one
  on Windows. The file's name is the topic's id, so `Topic="settings.engine"` beside the engine
  card is `settings.engine.md` and adding a topic is adding a file. The first heading is the
  title, the paragraph under it is the summary the list and the tooltip show, and the rest is the
  page, so a file read by somebody who has never seen this code is a page that reads correctly on
  its own
- It was ten string literals in a C# file, and prose in source is prose nobody edits: it cannot
  be read without the braces around it and a paragraph rewritten is a rebuild. The ids stay as
  constants all the same, because a file is what somebody writes and a constant is what a search
  finds. That the two agree is not left to anybody's memory: `Tests/HelpTopicTests.cs` reads the
  folder and the constants and says they are the same set in both directions, since a constant
  with no file is a badge that opens nothing and a file with no constant is a page somebody wrote
  and quietly lost. It also reads every `HelpBadge` in every layout, which is the only thing that
  would ever catch one pointing at a topic that was renamed: XAML cannot reach a const, so the
  compiler has nothing to say about it
- **`Help/Markdown.cs` is the markdown this application understands, and it is ours.** Sections,
  paragraphs, list lines, bold and the code marks a key name is written in. It was a package for
  about ten minutes: the only build of the obvious one that works with this toolkit is an alpha,
  and this is the application whose release is the one build nobody gets to take back. What it
  would have bought is the half of markdown the help does not use
- The rule that earns the whole thing on its own is the plain one every markdown has: **a run of
  lines with nothing blank between them is one paragraph, and where the line ends is not where
  the paragraph breaks**. Shown as it is written in a control that wraps, prose breaks twice,
  once where somebody typed and again where the window ran out, and comes out ragged at every
  width but the one it was written for. That was in the help window for exactly as long as it
  took to drag the splitter across. An indented line under a bullet belongs to that bullet for
  the same reason, and without it a list line stopped in the middle of its own sentence and the
  rest stood underneath as prose
- It reads rather than refuses. A mark that is never closed is the characters it is made of, so
  an asterisk somebody meant as an asterisk does not turn the rest of a page bold; a file that
  will not open at all is written down and passed over, since one topic that will not read is one
  topic and not the whole help. `Views/MarkdownView.cs` is the other half and is only the look:
  one TextBlock per block, which is the whole reason this exists, since a TextBlock is one size
  and one weight and a heading inside one had to be shouty capitals. A key is drawn in the same
  monospaced face the pattern uses
- Drawing it rather than showing it fixed something that was already wrong: the engine topic has
  said something in double asterisks since it was written, and until now it rendered as the
  asterisks
- The help window's two panes have the same handle the designer's do, and both columns have a
  floor, since a splitter with none can be dragged until one pane is not there and a pane that is
  gone is one nobody can take hold of to bring back. A topic's title and its summary are two
  lines of prose in a fixed column, so a list of them is either wrapping where it need not or
  taking room the page it explains would rather have, and which of those is true depends on the
  wording rather than on the window
- **Writing that page found out that the shortcuts page in SETTINGS did not exist.** Everything
  under it did, and had for a long time: `ShortcutMap` sets, `AppConfig.Shortcuts` stores only
  what differs from the defaults, and `IShortcutActions` says in its own remarks that a settings
  page builds itself from it. Nobody had built the page, and the help said where it was for about
  an hour. That is a shape worth naming: **help text is the one place in an application where a
  feature can be described into existence**, since nothing compiles it and nothing runs it
- **The keys the pattern answers are one table, and were three places.** The view's own switch
  statement decided what each key did, a list beside it filled the card in SETTINGS, and the help
  page said them again in prose. So a key added to the pattern appeared on neither the card nor
  the help, and a key changed quietly disagreed with the two descriptions of it. That is the
  fault `ISystemKeys` was made to end, arrived at again from the other direction: **the answer to
  a key being missing from the card is one list of what the application answers, not a second
  list beside the first**
- `IPatternKeys` is that table: the key, what has to be held with it, what it asks for and what it
  is called, one row apiece. `PatternAction` is the closed list of what it may ask for, and what
  is left in the view is which method each answer calls, which is not a second spelling of
  anything. `SystemKeys` reads `Listed` off it, so the card and the help fill themselves and a key
  added is a row added
- **A table cannot be got wrong the way an ordered switch can**, which is not theory: `Ctrl+V` is
  matched by asking whether control is held, and holding shift as well does not stop that being
  true, so `Ctrl+Shift+V` written underneath it pasted instead of doing its own job. It was found
  and fixed by reordering the cases, which is a fix that depends on nobody ever adding a case in
  the wrong place. `Find` takes the most particular row instead, and `Tests/PatternKeyTests.cs`
  pins that, along with every action having a key and words, every key answering, and a key the
  pattern does not own being left alone, which is what keeps the letter rows a keyboard
- One line per action rather than per key, so the octave reads as `Numpad * or Ctrl+]` on one row
  instead of two rows saying nearly the same thing
- **There are two kinds of shortcut and the difference is who decides.** System shortcuts are
  what the application does rather than where it goes, and they are not yours to move: the
  transport's `Space` and `Ctrl+R`, the pointing mode's `Ctrl+Shift+M`, the help's `Ctrl+H`, and
  Save, Delete, Undo and Redo. Page shortcuts are a key onto a page along the top, they ship on
  nothing at all, and they are the whole of what the page in SETTINGS sets
- The guard is in `IShortcutMap.Set` rather than only off the page, because the page is not the
  only way in: a settings file is a file, and one edited by hand to move Save is asking for
  something this does not offer. Refused quietly, since the same call reads that file at startup
  and a line in a settings file is not worth a start that fails
- **`ISystemKeys` is one list of what cannot be changed, and it has to be, because they come from
  two places.** Four are actions delivered through the map; the rest are written into a door of
  their own and nothing delivers those through the map, since a door answers before the map is
  consulted at all. The card in SETTINGS showed only the map's four for about an hour, and the
  answer to a key being missing from it is not to put that key in the map, which would be two
  ways of delivering one keystroke, but to have one list of what the application answers. Both
  the settings card and the help page's system section are filled from it
- **A page shortcut is `Ctrl+Alt` and a letter, and nothing else**, which is `ShortcutCatcher`.
  The narrowness is what makes it safe: everything else here is a letter with Ctrl, or with Ctrl
  and Shift, or on its own, so a page key cannot land on top of something that already works and
  nobody has to know what is taken before choosing one. Letters rather than any key, since a
  digit with those two modifiers is a character on several layouts and a function key is where a
  window manager tends to live. A keystroke it refuses leaves the row still waiting rather than
  stopping, which would read as the press having been taken and gone wrong
- Three of the catcher's answers are not a shortcut, and each is a hand doing something other
  than choosing. **A modifier on its own is a hand arriving**, and a row that took the first key
  it was given would learn Ctrl every single time, since Ctrl goes down before the letter does.
  Escape is changing your mind and Backspace is taking the key off, both only when they arrive
  alone: what those two mean on their own is a fact about them being alone
- **And a page shortcut does nothing while a caret is blinking, which is the rule that decided
  the whole thing.** On a Dutch or a German layout AltGr is delivered as Ctrl+Alt, and the
  characters behind it are how somebody types a bracket or a euro sign. A name that jumped to
  another page halfway through being typed would be the worst kind of fault, since nothing on the
  screen would say why. `ShortcutKeys` now answers only Save while typing, where it used to name
  three actions it would refuse
- **`LearningKeys` is the gate every key door asks before answering, and it exists because of the
  order keys arrive in.** Every key here is heard on the way down, at the window, before whatever
  has the keyboard sees it, which is right the rest of the time and is what stops the last button
  pressed keeping the space bar. While a row is listening it is exactly wrong. Four places asking
  one question rather than one place answering for all of them, since they are four keystrokes on
  four routes and what they share is only the moment. The page clears it on every way out, the
  page losing the keyboard included: a gate left set would leave the application deaf to its own
  keys for the rest of the session
- The row's buttons are clicked rather than bound to a command, because the page answers the
  keystroke that follows and the two belong together: which row is listening and which row is
  about to hear a key are one fact
- **The tab strip underlines the letter its page's shortcut uses**, which is how every
  application that has ever had a menu bar tells you the key without spending a line on it, and
  the only place that can say it where somebody is looking when they want it. `ShortcutLabel` is
  the header on each tab and `IShortcutLetter` is the rule: the first occurrence, case blind. A
  letter that is not in the word marks nothing rather than guessing, since Ctrl+Alt+Q on MIXER is
  a perfectly good shortcut and there is nothing in MIXER to underline; a page on no key is drawn
  plain, which is every page on a fresh installation
- **Which made the map say when it moves.** `IShortcutMap.Changed`, raised only when something
  really changed, because a page shortcut is now drawn in two places: on the settings page that
  set it, which is looking at it already, and as an underline along the top, which is not. The
  strip is built when the window is, so without it the mark would sit under the letter of a key
  somebody had just moved
- **And that turned up a fault in the settings page itself, which was writing to the map and not
  reading it.** The page is built while the window is, before the settings file has been read
  into the map, so every row said "not set" on a machine with eight keys saved while the strip
  beside it drew all eight underlines. From a chair that is a settings page that has lost your
  work. It follows `Changed` now, like the strip: the map is what knows
- **The help was then read against the code, and three topics described things that had been
  taken out.** The MIDI page's still explained the Pad Mapping table, which went when the pads
  moved onto the link layer; two of them sent somebody to an INSTRUMENTS page that has never
  existed under that name; and one named an "Add to library" button that is nowhere in any
  layout. None of that is a defect a compiler or a test could have found, which is the whole
  point of reading it: **help text is the one part of an application that goes stale silently**,
  since nothing builds it and nothing runs it
- Three more were true and had stopped being the whole truth, which is the commoner shape. The
  mixer's said nothing about the master, the meters, or touching a strip picking its track; the
  engine's said nothing about ASIO or the two switches under the buffer; and the chain's still
  said plugin everywhere, after our own effects went on chains beside them
- **Nine topics were missing altogether**, and the pages with the most explaining to do had the
  least: the pads, RECORD, the pattern, songs and packing, automation, the registry, the
  templates page, and the output device. `Help/Topics/` is twenty one files now, and every one is
  reachable from the page it explains: `Tests/HelpTopicTests.cs` reads every `HelpBadge` in every
  layout and says the topic it names exists, which is the only thing that would ever catch a
  badge pointing at a renamed topic, since XAML cannot reach a const
- **RECORD's picture was drawn by hand and so had no play cursor at all.** The position was
  arriving from the player ten times a second and nothing put it on the screen: the page held a
  bare `Canvas` and built a `Path` into it from its own code-behind, where `WaveformView` has
  drawn a playhead all along. It is that control now, `Playhead` bound to a new property on the
  view model, and about 130 lines of drawing came out of `RecordView.axaml.cs`. **Three pictures
  of one recording is two too many**: the machine panels already used the control, and what was
  left was this page and the trim dialog
- The trim dialog is the one that is still its own, and it is the harder half: it has zoom
  buttons, and `WaveformView` keeps its viewport private. Unifying it means deciding what a
  published control exposes about zooming, since `Rack.Controls` is what an outside machine links
  against and everything public in it is a promise
- **What is playing in the trim dialog is the selection, and the selection moving now reaches
  it.** The end was told to the player when Play was pressed and stayed where it was told, so
  dragging a handle inwards while a take played left the cursor running past the selection and on
  to the end of the file, which is exactly what it looks like: a cursor outside the marked
  region. `WaveformPlayer.PlayUntil` moves the end while it plays and stops it where the new end
  is already behind the position, which is what dragging the end back past what you are hearing
  means. The place a click put the cursor is forgotten at the same time, since after the handles
  have moved the thing somebody means by Play is the selection they have just made
- **And then the trim dialog stopped drawing its own waveform.** It had a canvas, a viewport,
  two trim handles, a selection tint, a playhead marker and the pointer handling for all of it,
  some six hundred lines, every piece of which `WaveformView` already had. It is 340 lines now
  and the picture is the control. **Two things kept them apart and both were small**: the control
  could not be zoomed from a button, and it had no way to drag a region out from nothing. Neither
  is a reason to keep a second waveform; both are now the control's, so a machine's face gets
  them too
- `WaveformView.Zoom` and `Scroll` are the new public surface, two way, clamped rather than
  refused, since a caller doubling the zoom at the far end means as far as it goes. They are each
  other's cause, the wheel writing them and a caller writing the viewport, so one flag guards
  both directions: what is written back is where the viewport settled rather than what was asked
  for
- **The minimum gap between two handles is a share of what is on screen and not of the whole
  recording.** The dialog had that rule and the control had a fixed five thousandths, which at
  ten times zoom is a tenth of the window, exactly where somebody is working when they want a
  fine cut. The number is a distance on the screen, so what it is a fraction of has to be the
  screen
- `IWaveformRegion` is the rule with no control in it: how far each end may travel and what a
  drag marks out. It is `TrimSelection` moved into the published assembly and made stateless,
  which is what let its tests survive the deletion. A rule that decides where a handle lands is
  the kind of thing that is wrong by a hair and stays wrong for a year, and it had a test suite
  that would otherwise have gone in the bin with the class
- A press on a picture with markers now marks a stretch; one on a picture without them pans, as
  it always did. Holding the pan modifier pans either way and is asked first. Nothing is marked
  on the press itself, or every press would throw away what was already marked
- **The zoom goes to four hundred rather than ten, and raising the number was the smaller half.**
  Drawing never limited it: `WaveformGeometry.Build` walks only the peaks on screen, so zooming
  in is cheaper to draw rather than dearer. What limited it was the picture, read into a fixed
  5000 peaks whatever the recording's length. At ten times zoom a peak was already 1.7 pixels
  wide in an 850 pixel window, so sixteen bought a little and twenty bought less: past that, more
  zoom only draws the same peaks bigger. Raising the ceiling alone would have been the fault this
  file keeps naming, a setting that says something the thing behind it cannot do
- **So `WaveformService` reads 200000 peaks now, and it is nearly free.** The samples are walked
  once either way, since the buckets divide the frames between them, so what the extra buys is
  bookkeeping: 9 to 12 ms became 20 to 28 on a sixteen second stereo take, and 20 KB became
  800 KB. Held to the frame count for a short one, or a take is read into more peaks than it has
  frames, which is buckets of one sample repeated and a picture claiming detail that is not there
- Which puts 500 peaks across the window at the far end, the same 1.7 pixels each it always had,
  and forty milliseconds of a sixteen second take on screen: close enough to see a click and take
  it out. **Audacity is the yardstick and does it differently**, keeping summaries at two
  resolutions and reading the samples themselves once you are close enough, so it goes past one
  sample to the pixel, which on that take would be about 830 times. Doing the same here means the
  picture asking for what it needs at the zoom it is at rather than being handed one array; this
  is the cheap nine tenths of it
- Measured on a real recording rather than on a tone. A steady sine's envelope is a solid block
  at every zoom and says nothing about resolution, which is an hour nobody needs to spend twice
- **A plugin showing the host's knobs is the fallback working, and the fault is upstream of it.**
  `docs/plugin-faces-on-windows.md` is the note for whoever picks up the Serum symptom on that
  machine: what it means exactly, the three places the chain can end, the log lines each branch
  writes, and two things already ruled out from here. The crash guard is the one worth naming,
  since it is the obvious suspect and is wrong: `IsBlocked` stands down whenever plugins are
  isolated, and Windows has been isolated since the bridge went everywhere, so the blocked list
  in SETTINGS cannot cause this any more
- The tell to look for is a **missing** line rather than a present one. `Vst3Editor` writes at
  every step of the handover on purpose, so silence after `about to hand the plugin window` means
  the call never returned, which is how the original Windows fault was found
- **Two more effects of ours: Sweeper and Roaster**, which is the plan's filter and drive.
  `SoundDevices/SoundEffects/Sweep.cs` is four poles with a drive in front of them, three modes
  and a cutoff that glides in cents rather than in hertz, since a sweep that moves evenly in
  hertz crawls through the two octaves anybody is listening to and leaps through the eight nobody
  is. `Drive.cs` is a tilt into a curve into a centring filter. Six presets apiece, and the ids
  are `effect.sweeper` and `effect.roaster`, which a chain writes down and which never change
- The plan said these two were cheap because the maths was written already, per voice, in
  `Tracker/Synth/`, and that held: **what moves across is the arithmetic and not the class**,
  since a voice is mono and short lived and a track is two channels running for the length of a
  show
- **Three faults, and the tests found all three.** The high pass was the band pass under another
  name, because four poles read as high then low is a band exactly as low then high is. The drive
  stepped a fifth of its level the moment the knob left its stop, because the fade was on the
  makeup rather than on the curve: the makeup levels the curve at full scale and nowhere else, so
  fading it in leaves the curve arriving at full strength. That is the synth's own trap arrived
  at from a new direction, which is worth knowing about a trap that has been written down once
- The third only a measurement would have found. **A bias cannot be taken out by subtracting what
  the curve does to it**: driven hard, a signal leaned by 0.4 spends most of its time against the
  top of the curve, so what comes out is nearly constant and subtracting `tanh(bias * amount)`
  leaves a step three quarters of full scale. It comes out with a filter, since what is left when
  a signal is taken away from itself a moment ago is whatever moved, and an offset is precisely
  the part that does not
- **A held key stacked up voices, and the log said so in two numbers.** One key held for a
  couple of seconds took the mixer from one voice to forty eight, which is where it starts
  stealing, and what reached the master summed to 4.34 where full scale is one: four times too
  much into the master's saturation, heard as crackle. The collector heard it too, 345 ms of
  every thread stopped in one five second window with blocks hitting 182% of the 11.6 ms they
  had. **The machine was never the problem**: the quiet windows either side read two per cent and
  nothing collected
- The cause was one rule covering one of its two cases. `EnterNote` dropped a repeated key only
  while another key was down, which is the chord case and is written up in its own remarks; a
  single held key fell through to the preview, and each repeat started a voice holding for
  `HeldNoteSeconds`, which is ten. At thirty repeats a second that is sixty voices alive at once
  from one finger
- **Writing and sounding are two acts and a repeat wants one without the other.**
  `INotePress` is the three answers on their own, out where they can be put a question to without
  a song or a keyboard: nothing under a chord, write without sounding on a repeat, sound and
  write on a fresh key. It was one call returning early, which is why the missing case was
  invisible
- Worth keeping the shape as well as the fix: **the symptom was audio and the fault was a
  keyboard**, and no amount of reading the synth would have found it. What found it was the log's
  own voice count beside its render cost, and the first thing that ruled the audio out was
  rendering the song's exact patch in a test: worst sample step 0.043, nothing over 0.15 in 51200
  samples
- **Nothing that is not a real number reaches the sound card, and there was more than one way
  out.** `IOutputCurve` is the last thing a sample goes through: what is merely too loud is bent,
  since a chord summing past full scale is music and a hard corner on it sounds like a fault, and
  what is not a number is silenced, since it is the absence of a sample rather than a loud one.
  The master already bent, through `SoftClip`, and it could not survive a NaN: every comparison
  against one is false, so it failed the test against the knee and `Tanh` handed one straight
  back
- The other way out had no guard at all. A pad never touches the tracker's mixer, so an effect on
  a pad's chain handing back a NaN wrote it into the sound library's own buffer and out of the
  card. One rule, one layer down, and both ways out go through it
- **What that protects is not the card.** A converter puts out a bounded voltage whatever the
  bits say, and no signal a program plays will damage one; software that sits on the hardware and
  writes its registers, its clocks or its firmware is a different matter entirely and is real,
  which is worth saying plainly rather than calling it a myth. This application has no path to
  any of that, since everything leaves through BASS. What a bad buffer genuinely endangers is the
  speakers and whoever is in the room: NaN at the converters commonly arrives as full scale noise
- The test that was there said a poisoned block does not **outlive** the plugin that poisoned it,
  which is a weaker promise than it looks: it rendered with the poison, took it away, and checked
  the block after. What was missing was the block **during**, which is the one that reaches
  somebody's speakers
- **Every meter carries a clip lamp, and it belongs to the meter rather than to the strip.** A
  light assembled per strip in the layout is the same three lines written three times with one
  eventually forgotten; in `LevelMeter` it reaches the tracks, the master, the recorder, the pads
  and any machine's face at once. It reads the level before the meter clamps it, since the bar
  clamps to full scale and a lamp worked out from the clamped number could never fire
- `IClipHold` is the rule, with the moment handed in rather than read off a clock, which is what
  lets a two second hold be asked about without waiting two seconds. Latched, held, and put out
  by a click, because a clip is an instant and a light nobody sees is a light that is not there.
  A clock that jumps backwards puts it out rather than stranding it lit for ever
- **A strip is six rows now, and everything on it lands in one of them.** Badge, pan, mute and
  solo, the fader with its meter, the level reading, and the side chain. Only the fourth grows,
  so the strips fill the page and every other row is at the same height on every strip, which is
  what lets a mixer be read across. All three strip shapes use the same six, and the two that
  have no side chain leave the last row empty rather than being a different shape
- It was a `DockPanel`, and a dock panel has no places in it: it gives the rest of the room to
  whichever child is **written last**. Adding a line of text after the fader handed the room to
  the text and squeezed the fader and its meter into a band along the top of the strip. That is
  not a thing to remember, it is a thing to stop being possible, which is what the rows are for
- **The level reading is the strip's rather than the fader's**, and that is forced rather than
  chosen: a fader is as wide as the longest reading it can show, so with the word inside that
  string it claimed the width of `Level: -60.0 dB`, which on a 134 pixel strip is all of it, and
  the meter beside it was squeezed out of existence, clip lamp and all. The strip is wider than
  the fader and has the room the fader has not. `Fader.ShowValue` is how a fader is told the
  reading is somebody else's, and it leaves it out of the measuring as well as the drawing
- The reading is signed, `+0.0;-0.0;0.0`, so a fader above unity says so rather than leaving the
  sign to be inferred from a number without one
- The meter is inset by the clip lamp's own room and the fader is pushed down by the same amount,
  so the two travels line up. Done the other way, by pulling the meter up, the lamp went through
  the mute and solo buttons above it
- It took three goes and two broken layouts to get there, and the reason each time was the same:
  changing the shape before reading the container. The rows are the fix for that as much as for
  the reading
- It was drawn wrong three times before it was right, and every one is the same mistake: building
  something beside the thing that already existed. Four pixels at a quarter opacity, which could
  not be told from the meter's own frame, and **a lamp nobody can find while it is off is one
  nobody trusts when it is on**. Then a cap across the bar, which reads as the bar running out of
  room rather than as a lamp. Then a round lamp that called `Led.DrawLamp` when lit and drew its
  own circle when dark, so a dark clip lamp and the dark lamp on a machine's face were two
  different drawings. Both states go through the one call now, and the halo comes with it, which
  matters because nobody is looking straight at a clip light when it fires
- **A face is written in the panel's own vocabulary, and a property it does not know is ignored
  in silence.** Both faces were written first with invented names, `spacing` and `padding` and
  `size`, and drew as a cramped default with the labels truncated: the words are `cell`,
  `columns`, `span`, `dial`, `gap`, `caption` and `corner`. Silence is right, since a face from a
  later version has to open at all, and it is also how a face comes to look wrong for no visible
  reason. `Help/Topics/designer.laying-out.md` says so, which is the topic that was missing: the
  designer had one about what a machine and an effect are and nothing about laying a face out
- The chain under the pattern is blocks rather than pills, and the point of the change is that
  a row of blocks with names on them tells you the order of the effects and nothing at all about
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
  `SoundMachinePresetFile`, and both wrote it while doing something else. It is now
  `SoundDevices/SoundMachines/SoundMachineValuesFor.cs` and the editor calls it. The view models are optional
  there because that is the only way the two callers differ: an editor owns the one the panel
  edits and must hand it over, or the panel and the values would be looking at two copies of one
  patch, and anything only reading wants a throwaway. Which three controls is `PanelOrder`, so
  they are the first three your eye lands on when you open the machine
- `ViewModels/ControlReading.cs` is one control and its reading, and one row template draws it for
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
  both. `ControlSurfaceViewModel` is a view over the per-port `MidiPortViewModel` rows rather
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
- **A pad that is playing keeps its own colour and walks through the ones beside it.** It used to
  be repainted in the theme's checked colour, which cost the thing a wall of pads is for: every
  playing pad turned the same colour, so which one you had fired was a question about which one
  had changed rather than something you could see, and a pad whose own colour happened to be that
  one said nothing at all. `Views/PadPulse.cs` is the control and `IPulseColour` is the rule, in
  hue rather than in red, green and blue, since the neighbours of a colour are a fact about the
  wheel: twenty two degrees either side, which is about the width of one colour on the palette, so
  a red pad reaches towards orange and never arrives. The brightness moves a little with it, which
  is for the pads with no colour of their own: grey has no hue to walk. A control that draws
  rather than a style that animates, because what it draws depends on the pad's own colour and an
  animation in a style can only move between colours written into it

- Two things had to change for that and the second is why the first looked like it had failed.
  `PadViewModel.PadBackground` deliberately answered nothing while a pad played, so the theme
  would paint it; and this toolkit paints a checked `ToggleButton` with the accent from inside its
  own template, where a background set on the button cannot reach. Both are said now, and the
  ring around a playing pad stays: a colour that moves says a pad is going and an edge says which

- It is a setting, `PulseWhilePlaying`, on SETTINGS, Control Surfaces, beside toggle mode, and on
  unless somebody says otherwise. It is the one thing on FIRE that draws while nothing has
  changed, so somebody running a show on a machine with nothing to spare can say no. What it
  costs was measured rather than assumed, by throwing that switch with four pads playing: 108.6%
  of one core against 110.5%, which is about half a percent of a core per walking pad, in a Debug
  build on a software rendered display with no GPU. It asks for a frame at the screen's rate and
  redraws at most thirty times a second, keeps one brush and one delegate rather than making them
  per frame, and asks for nothing at all once the pad stops

- **A press from a control surface is not coalesced, and a knob's position is.** Writes from the
  hardware are queued for the drawing thread and were coalesced per link, which is right for a
  knob, where a sweep sends a hundred positions and only where it ended up matters, and wrong for
  a press: two pad hits arriving before the screen was drawn became one, so a pad in toggle mode
  was left playing when it had been told to stop and the light disagreed with the hand that played
  it. Measured on the wire at two note ons in the same millisecond, one toggle; ten milliseconds
  apart it always worked, which is why it read as random. `IControlWrites` and `ControlWrites` are
  that rule, with the trip to the drawing thread handed in so it can be pumped by hand in a test:
  a value replaces whatever has not landed, a press is kept beside every other press in order,
  presses run first, and one trip carries the lot. Bounded at sixty four presses a trip, since a
  hand cannot make sixty four between two frames. It reached the transport's keys and a machine's
  buttons too, which have gone through that queue since they were written

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
- **Every platform, and Windows was the exception for two years for a reason that was not
  true.** `IPluginHost.Isolated` read `!OperatingSystem.IsWindows() && !InProcessAsked`, and the
  remark under it said the embedding used here only works within one process, so a VST3 plugin
  had to be loaded into this one for its interface to answer a mouse. That is not how Windows
  works: a window whose parent belongs to another program draws, resizes and answers a mouse
  exactly as one in the same process does, which is how every host that bridges plugins does it.
  The only thing crossing a process boundary really costs there is the keyboard, because Windows
  keeps focus per thread, and that is two calls
- **What the exception actually cost was a plugin split across two threads, and it is worth
  knowing the shape because nothing about it looks like a threading fault.** Loading a plugin is
  seconds, so it is done off the drawing thread, rightly, and `TrackerPlayer.Start` says so in
  its own remarks: it runs off the lock and off whatever thread asked, which is the thread pool.
  The window is then made and handed over on the drawing thread. VST3 asks for a view and the
  controller behind it to live on one thread, and a toolkit that binds its own message thread
  where it was built, which is most of them, blocks for ever when `attached` arrives on a
  different one. Two plugins loading in the same millisecond in the log is the tell that they
  were never on the drawing thread at all
- From a chair that is a grey rectangle where an interface should be, so it reads as a window
  that will not draw and every hour spent on it goes into the window. It is not: it is a call
  that never comes back. `IPlugView::attached` was entered and never returned, and the log said
  so by omission, since the line after it never appeared while the lines before it all did. **A
  path where every branch writes a line is one where a missing line is evidence**, which is the
  whole reason `Vst3Editor.Attach` says what the plugin answered as well as what it was asked
- A plugin in its own process cannot have that fault, because there is one thread and it does
  everything in turn: the process loads the plugin, makes its view, hands it the window, and
  then goes on pumping for it. That is what fixed it, and it is why the fix is one line
- **A plugin's process has no toolkit, so nothing was draining its Win32 queue, and that half
  had to exist first.** `PluginRunLoop`'s own remarks said Windows "has a message pump already
  running before the plugin arrives", which is true of the application's process and false of a
  plugin's: a window nobody pumps gets no paint, no timer and no mouse. `IWindowMessages` is that
  half, and it is the one place the two platforms differ in the child: `WindowsMessages` waits on
  `MsgWaitForMultipleObjectsEx` and drains with `PeekMessage`, and `NoWindowMessages` waits on
  the knock and nothing else, since X11 has no per-thread queue and a plugin asks for a run loop
  instead. The wait belongs to the pump rather than being a wait on the knock because a thread
  asleep on an event alone is not woken by a message arriving
- The two are deliberately separate and neither is the other. `PluginRunLoop` is what a plugin
  asks the host to hold for it; `IWindowMessages` is what the system holds for any thread with a
  window on it. Bounded at 512 messages a turn, or a plugin repainting under a drag would keep
  the bridge waiting for the length of the gesture
- **One sharing flag was the whole of what stopped the bridge running on Windows.**
  `BridgeBlock.Open` used `MemoryMappedFile.CreateFromFile(path, ...)`, and the overload taking a
  path opens with sharing for reading only while the parent holds the same file mapped: a sharing
  violation, an unhandled exception, and a child gone before it had loaded anything. Linux has no
  mandatory locking, so it had always worked there and the difference was one default nobody had
  to think about. Both sides open a `FileStream` with `FileShare.ReadWrite` now, which is still
  one code path
- And it cost a round trip because the child died silently. All the parent can see is an exit
  code, which it reports as the plugin having stopped unexpectedly: true, and 0xE0434352 for
  every managed fault there is. `PluginHostProcess.Run` catches and writes what happened now, and
  flushes before the code goes back, since the log is written by a thread about to stop existing.
  `BridgeBlock.Open`'s remarks had promised exactly this and only the missing file kept it
- **Two things cross-process costs on Windows and both are in `NativeWindow`.** `ShareInput` is
  `AttachThreadInput`, because focus is kept per thread and without it a plugin takes the mouse
  perfectly and never sees a key, which is a preset name typed into nothing. `ReadScalingProperly`
  says this process reads the screen per monitor, because a process with no toolkit says nothing
  and Windows quietly tells it a screen at 150% is a smaller screen at 100%, so the window it
  draws is stretched by the system inside an aware parent. Said before the plugin loads, since
  Windows refuses to change its mind once anything has asked. `Account` is the third and reads a
  window and what is inside it, the counterpart of `XEmbed.Complete`'s account: the before and
  after of a handover, where "nothing inside it" becoming a `VSTGUI` or `JUCE_` child at the full
  size is the proof that a plugin really drew
- A plugin's own window is given to it only once the window is really on screen at its full
  size. Handing over the one-pixel window Avalonia makes before the first layout is what killed
  Serum
- **ASIO is read and the tracker goes out of it, and it is not finished.** ASIO is Steinberg's
  driver standard on Windows, and the point of it is that the system's mixer is not in the path:
  the buffer is the card's own, so the delay is a few milliseconds rather than the twenty a shared
  path costs. `ManagedBass.Asio` is the wrapper and `bassasio.dll` is un4seen's add-on, which is
  **not** in the package: it is shipped here in `native/win-x64/` beside `bass.dll`, version
  1.4.3. The csproj copies it only where it exists and only on Windows, so a checkout without it
  still builds and runs and says why the list is empty
- **Two silences, and they are not the same silence.** No ASIO library at all is a file that was
  not shipped or a system ASIO was never made for; a library with nothing behind it is a machine
  where no driver has been installed, which is most Windows machines until a card's own driver or
  something like ASIO4ALL puts one there. In the picker both are an empty list and no reason, so
  `IAudioEngine.OutputsMissing` says which of the two it is and SETTINGS shows it under the device
- **The libraries were checked against what un4seen ships, by hash rather than by version.**
  `bass.dll` 2.4.18, `basswasapi.dll` 2.4.4 and both `libbass.so` are byte for byte what is
  current. `bass_aac` was a release behind on all three platforms and is 2.4.7.2 now; the two
  Linux builds are exactly the same size as the ones they replace and a different hash, which is
  why a size is no test. It is a decoder on the audio path and nobody has listened to it yet
- **An ASIO driver is not a device BASS can be opened on**, which is the whole shape of the
  change. The driver owns the card, so BASS is opened on its own silent device instead, the
  tracker's stream is made a decoding one, and the driver pulls from it. A stream that plays
  itself and is also pulled is the same audio leaving by two routes, which is why `OutputKind` is
  asked before the stream is made rather than after
- **One stored number names a device out of two lists that both start at nought.** The system's
  endpoints keep the numbers they always had, so every settings file written before ASIO existed
  goes on meaning what it meant, and the drivers are lifted clear at 1000 and up. `IAudioOutputs`
  is the one place that composes and takes apart, because two fields that have to agree is how
  the same fact comes to be written twice and then diverges
- **A missing native library throws on the first call into it, not when the assembly loads**, so
  whether ASIO is there at all can only be found out by asking and seeing. `AsioDevices.Present`
  asks once and remembers, since the answer cannot change while the program runs and the question
  costs a thrown exception where it is no. `Tests/AsioDevicesTests.cs` is that path and nothing
  else: every machine the suite runs on has no ASIO, so what is pinned is that asking is safe,
  the list is empty rather than an error, and opening one is refused rather than fatal
- **How big an ASIO block is is the driver's, and this program asks for nothing.** It used to pass
  the buffer slider's frame count straight into `BassAsio.Start`, so a card whose own panel was set
  to 256 was made to run blocks of 1024 because a slider about the system's output path happened to
  be there. That slider is a BASS setting about the shared path, which is the exact path an ASIO
  driver takes out of the picture, so it has no business deciding this. `AsioDevices` reads the
  driver's preferred length, which is its panel setting said back, uses it, and reports it: SETTINGS
  says what the card is really running rather than a number about a path that is not in use
- The rate is asked for and never insisted on either. Setting it throws rather than answering when
  the card will not have it, and a card clocked from something else is that case, so what it is
  really on is read back afterwards and the mix is resampled into it through
  `BassAsio.ChannelSetRate`. The alternative is a stream pulled at a rate it was not made at, which
  is the whole song playing sharp with nothing anywhere saying why
- **A song stuttering was read out of the log rather than guessed at, and the line naming it had
  been there all along.** Moog: three tracks of ours and three bridged plugins, reported as hiccups
  and grumbles. `the cushion ran dry: N frame(s) of silence so far` appears 33 times in five
  minutes and reaches 24403 frames, which is half a second of literal silence handed to the sound
  card in fragments. **That is the symptom, and it is not the same thing as a block going over its
  budget**: a block over budget eats the cushion, and only a cushion that empties is a gap
- What the block was spent on is somebody else's arithmetic and nothing of ours. One to four
  voices in the mixer, and:

  | | round trip | its own side | its worst |
  |---|---|---|---|
  | Vital, the instrument on track 3 | 4.1 ms | 2.4 to 4.1 ms | 12.8 ms |
  | Serum 2 FX, an insert on track 2 | 1.8 ms | 1.3 to 1.8 ms | 4.7 ms |
  | ZamDelay, an insert on track 0 | 0.14 ms | 0.05 ms | 0.15 ms |

- Six of the 11.6 ms a 512 frame chunk has, run one after another, which is the 43 to 66% mean the
  render line reports. **Vital alone reaches 12.8 ms, which is longer than the whole block**, and
  the worst whole chunk measured is 489%, or 57 ms
- **The cushion was 40 ms and 40 was the largest the picker offered, while `MostAheadMs` is 200.**
  A cushion is drained by one long block, so the size to choose it against is the worst rather than
  the mean: 40 ms cannot absorb 57. So the fault was not the cushion setting, it was that the
  choices stopped a long way under what the engine allows, and the log's own advice, that a bigger
  one in SETTINGS is what this is asking for, could not be taken. 80, 120, 160 and 200 are on the
  picker now. Nothing anybody has changes, since a row is only what the settings already name
- **Overlapping bought almost nothing on that song, and why is the interesting half.**
  `TrackMixer.Render` ran `RenderBusses` to its end and then `ApplyInserts`, so each phase could
  only overlap within itself. Vital is a bus and was alone in that phase; ZamDelay and Serum are
  inserts on two tracks, so those two did overlap and saved the smaller of them, 0.15 ms of 11.6.
  **The two crossings worth overlapping were on opposite sides of the phase boundary and waited
  for each other for no reason**, which is a shape the switch could not reach however it was set
- **So the phases went and the mixer is a per track pipeline.** `RunTracks` walks the tracks once:
  a track's instrument is begun, and a track that has no instrument or whose instrument has come
  back goes straight on to its own voices and its own insert while another track's instrument is
  still out. The block is the longest single track rather than the sum of everything on it, which
  on Moog is about 4.1 ms against 6.1
- **One track is three things in order and two tracks are in no order at all**, which is the whole
  of what it is arranged around. A plugin instrument fills its track's bus rather than adding to
  it, so the voices land on top of what it played and the insert reads the two together; nothing
  on one track reads another. `PrepareBusses` is what a bus holds before any of that, and it
  finishes the loose bus outright, since an audition belongs to no track and nothing downstream
  touches it
- **The voices are threaded onto a chain per track and appended at the tail**, which is the one
  place in this where the easy way is wrong. Pushing at the head plays a track's voices backwards,
  and adding floating point numbers is not associative, so the mix would differ in the last few
  digits. `A_chord_on_one_track_keeps_the_order_the_notes_were_taken` is that, and **it takes three
  notes**: two floats added are the same either way round, so the test was written with two first
  and passed happily with the chain built backwards
- Four tests hold the shape and each was checked by breaking the thing it is about: an instrument
  on one track and an insert on another are in flight together, which fails under the phases; the
  voices are played after the instrument and before the insert; the block is the same sample for
  sample with an instrument in the mix, which the old identity test had none of; and the chord's
  order
- **A buffer that has to be twice another program's is one of two faults and they want opposite
  answers**, and from a chair the two are the same stutter. Either the mixing is genuinely
  expensive, in which case the block is nearly all used up and the work has to get cheaper, or it is
  cheap and late, in which case the block is mostly idle and what is wrong is when the thread runs.
  `IRenderCost` is that measurement: every block is timed against its own length in real time, and
  one line every five seconds names the worst, the mean and how many went over. Both places that
  render go through it, since the question is the same wherever the mixing is being done
- Beside it, what the runtime collected over the same stretch, because the second fault has an
  obvious suspect in a managed language and the block timings alone cannot see it. A mean of 15%
  with a worst of 200% is a pause, and no amount of making the mixing faster would touch it. The
  pause total is every thread that was stopped rather than this one in particular, so it is an
  upper bound rather than a measurement, which is the right direction: no collections at all rules
  the theory out
- **It was measured before it was argued about.** 32 synth voices through `TrackMixer.Render`, in
  Debug, on Linux: mean 15 to 16% of each block's own time at 128, 256, 512 and 1024 frames, and
  nothing collected over thousands of blocks. Flat regardless of block size, so there is no fixed
  per-block overhead worth naming, and allocation-free on the render path. Whatever is forcing a
  bigger buffer on Windows than another host needs, the mixer's own arithmetic is not it, and
  rewriting that arithmetic in another language would buy the sixth of a block it already uses
- **The pads reach the driver too, and they do it through the output bus.** This paragraph said
  for a long time that they did not, that ASIO was the tracker's alone and that picking a driver
  silenced FIRE, and that stopped being true when `OutputBus` went in: it is a BASSmix stream,
  which is the add-on this note said would have to be added, and the pad bus and the take bus are
  plugged into it. `BassAudioEngine.OpenBussesLocked` hands **the bus** to the driver, so with one
  picked the tracker, the pads and a take being auditioned all leave through it. Tested on Windows
  on real hardware, which is the only place that can be tested
- **It was a tick and it is not one any more, which is the whole answer to the gap this used to
  describe.** `AppConfig.OutputBus` was off in every settings file that had never heard of it and
  `OpenBussesLocked` returned at once, so ASIO with the bus on was the whole application and ASIO
  with the bus off was the tracker alone: the pads and RECORD played into the silent device BASS
  had been opened on, and nothing anywhere said so. The tick's own hint said it, in SETTINGS under
  Engine, which is not where anybody picks a driver
- **Off bought nothing and cost four things**, which is what settled it. Solo went grey on the
  PLAY and PADS strips, since a solo silences everything else and only the one bus knows what
  everything else is; their pan and mute went with it, being the bus's; ASIO silenced the pads and
  RECORD; and the patchbay drew cables into the desk for audio that was not going there.
  `IBusSwitch`, `BusSwitch`, the tick and `AppConfig.OutputBus` are gone and the bus is the only
  path
- **The switch existed for a reason and the reason expired.** It shipped off because the last time
  the summing was rearranged it arrived beside five other changes, the sound came apart, and the
  whole lot went back rather than the one that did it; so the bus went in behind one switch over
  one change, off until it had been listened to. It has been. **That is the shape to keep: a
  switch that says "until this has been heard" is finished when it has, and leaving it is leaving
  a way to silence half the application by accident**
- **And the second path went with it, which was seven forks rather than the one fallback it
  looked like.** Whether a pad's stream got `Decode`, whether it got `AutoFree`, whether sounding
  it was `_padBus.Add` or `ChannelPlay`, the same for silencing, whether "is it playing" asked the
  bus or the channel, whether the level was read with the add-on's call or the plain one, and
  which end sync it got: every one asked `_padBus.IsOpen`. All seven are gone
- **"Before an output is opened" was never one of the cases**, which is worth writing down because
  it was said out loud here and was wrong. `PlaySample` calls `EnsureInitLocked`, which opens BASS
  and then the busses, so a pad fired on a cold engine opens the output on its way in and finds
  the bus already there. `A_pad_on_a_cold_engine_opens_the_bus_and_lands_on_it` pins exactly that,
  since with no second path a pad that missed the bus would be a pad that makes no sound
- What could really reach it was one thing: **no BASSmix**. Both natives are in `native/`, three
  targets copy them and the release workflow greps the payload for them, so it takes a checkout
  with the library missing. `OpenBussesLocked` throws there now rather than logging and carrying
  on, and a pad catches and puts the message on itself: better than pads that play a different
  way and lose solo, pan, mute and ASIO in silence. `SaySoloable` already asked `Output.IsOpen`
  rather than the switch, so it needed nothing
- **A chain is reordered by dragging, and the instrument is not in the dance.** The strip already
  had Move earlier and Move later on its context menu, which is a step at a time through a menu
  nobody opens, and `PluginChain.Move` already took any offset rather than only one. What was
  missing was the gesture. One strip is drawn over a track's chain, the master's, a pad's and the
  recording input's, so it arrived on all four at once
- **The instrument stays first by construction rather than by a guard.** It is drawn outside the
  row of devices, so it is never picked up and there is no place in the row for it to be dropped
  into. What the whole row takes is the drop, so letting go over the instrument means in front of
  the first effect, which is what a hand dragging leftwards means and would otherwise have been a
  drop that quietly did nothing
- **A block with no background took no presses, which is this application's oldest pointer fault
  turning up in a fourth place.** `Border.device` set a thickness, a radius and a brush and no
  background, so a press on the face of a block fell straight through to the card behind it: the
  only presses the row ever heard were the ones on its buttons, and those are handled. From a
  chair that is a drag that does not exist, and it was reported in exactly those words, "how to
  drag then". Transparent, so it looks exactly as it did. The patchbay's pan was the same
  sentence about a `Panel`, and the ghost's own painting is the same sentence about clearing a
  brush rather than setting it to null
- **The picture in the hand is built and never the block itself**, which is not a nicety: a
  control has one parent, so handing the live block to `DragGhost.Show` takes it out of the row
  it is being dragged along and the toolkit refuses it outright, as "The Control already has a
  parent" on the first movement of the first drag. The ghost's layer is a canvas and takes a
  control rather than a data context, which is why the tracker writes its own picture out too
- Two things were added at the same time and they are not decoration. The pointer over a block
  that moves is the west-east one, since what the block does is move along a row, and the buttons
  on it put the arrow back because what they do is not moving anything. The instrument keeps the
  arrow, deliberately: the same cursor over it would be a promise nothing keeps
- **The press bubbles rather than tunnels, and that is the whole of how it stays out of the way of
  the buttons on a block.** A button answers a press and marks it handled, and a handled event is
  not delivered to the row, so the name, the power switch and the cross work exactly as they did.
  Letting go without moving ends a drag with no effect, so a plain click on the body is still a
  click, and the right button is untouched, which is where the two move commands already live
- **`ISlotDrag` carries the chain beside the number, and that is why it is not `IDragPayload`.** A
  place in a chain means nothing without the chain it is a place in: slot 1 is four different
  devices on four different chains and two strips can be on the screen at once, so a number alone
  would let a device let go over the wrong strip reorder a chain nobody was dragging. Compared by
  which object it is rather than by anything about it, since a track and the master can hold the
  same effects in the same order. A device from another chain is refused rather than carried
  across, since moving one between chains would mean loading a plugin somewhere else, which is
  what the plus is for
- **`IChainDrop` is the arithmetic, and it is out of the view because it is the half that is wrong
  by one.** Which gap a point across the row means, half a block at a time, with before the first
  and past the last being the start and the end; and what that gap comes to once the device has
  left where it was. **A gap is counted with the device still in the row and a chain counts
  without it**, so a device dragged rightwards lands one short of the gap it was dropped in and
  one dragged leftwards does not. A version with that wrong works perfectly half the time, which
  is exactly the kind of thing that survives being dragged about by hand
- **The recording input is heard through its own chain now, which is what an insert on a desk's
  input channel has always meant.** A microphone through a pitch effect is heard as the pitched
  thing while you play it. Every piece of it existed and none of them were joined: the chain ran
  in `TakeEffects.Through` after a take was stopped, `StartMonitoring` only opened the input so
  the meter could read it, and the IN strip's own documentation said **on the desk and not in the
  mix**. So a chain on the input was a post-process on a file and nothing else
- **`IMonitorFeed` is the path and it is a push stream on a bus of its own.** `MonitorBus` is the
  fourth strip beside the pads and the takes, so the IN strip's mute, placement and solo are the
  bus's like every other strip's, and a solo anywhere on the row pauses it with everything else.
  Its own bus rather than the take bus, although both are RECORD's: a take being auditioned is a
  file playing and this is the input arriving, and one fader over both would be one fader for two
  jobs
- **The capture thread only ever copies, and that is the whole architecture.** It hands the block
  over and returns; the chain runs where the bus is pulled, which is the same thread a pad's chain
  already runs on. This file already refused the other arrangement in as many words, that there is
  no reason whatever to put a plugin on the capture callback since a crossing is a fixed cost per
  block and a late block there is a hole in the only copy of a performance. **The take is still
  written from the captured bytes and nothing on this path can reach them**, so the refusal
  stands and the monitor is beside it rather than in front of it
- **`IInsertPass` came out of the pad path, where it had been written once and could not be
  reached.** A block through an effect is four things and each was got wrong somewhere before it
  was written down: the audio is worked through in pieces and never skipped, since the first block
  BASS asks for is the whole playback buffer; a mono channel is widened and folded back, because an
  effect is a stereo thing; an effect that throws costs the rest of that block only; and what comes
  back goes through `IOutputCurve`, because an effect handing back a NaN writes it out of the card.
  `Tests/InsertPassTests.cs` is the first test that path has ever had, and it covers the pads as
  much as the input
- **`IStereoFloats` is the other new rule and it is the one that goes wrong quietly.** A capture
  hands over 16 bit samples and a bus and an effect deal in interleaved stereo floats, and every
  way that conversion fails is inaudible as a fault: the two halves of a sample the wrong way round
  is noise that reads as a broken cable, an unsigned read is a signal sitting half a scale off
  nought, and the wrong divisor is a monitor that is very nearly right and always a hair too loud.
  32768 and not 32767, the same number `SixteenBit` multiplies by on the way out, or a take heard
  going in and written coming out would differ by something nobody could account for
- **What an output is playing cannot be heard this way, and that is the case the picker defaults
  to.** That source is the output's own monitor, so hearing it through the output feeds it back
  into itself at full scale through whatever the chain is doing. `IInputSource.CanHear` is the
  rule and it is asked from both directions: the switch is grey for such a source, and choosing
  one while the switch is already on turns it off and says why, since grey arrives at the moment
  the source changes and by then the audio is already going round. A program is fine either way,
  heard twice if it is still playing out of its own output and heard here alone if it has been
  taken aside
- **Off unless somebody says so, and not kept between runs**, which is the rule `TakeAside`
  already keeps and for a sharper reason: a switch that came back on at the next start would make
  a loop, or point a microphone at the speakers, before anybody had asked for anything
- **What it cannot do is be quick.** What is heard is a capture buffer plus an output buffer late.
  A desk avoids that by not going near a computer and the only thing that moves it here is the
  sizes in SETTINGS, which is said in the help rather than left to be discovered
- The cable out of RECORD carries two things now, a take being auditioned and the input being
  listened to, and `PatchSignals.Takes` is either of them: it is one cable because there is one,
  and it is solid while either is sounding. The port on that block is still called `takes`, which
  is now half the truth and is the smaller of two evils until somebody decides to rename it
- **What the switch left behind was a whole seam, and no warning would have found it.**
  `IAudioEngine.ReopenOutput` and `BassAudioEngine.ReopenOutput` existed for one caller,
  `MainViewModel.ReopenDevice`, which existed for one tick: opening the device again is what made
  a change to how it is opened take effect now, and there is no such change left. The tick went
  and the private method under it stayed, since **an unused private method is not a warning**, and
  from there the interface member under that read as part of the contract. Gone, both doubles in
  the tests with it. Reopening the device is still what picking another one does, through
  `SetOutputDevice`, which is the only reason to do it
- Three sentences went with them, and each said the tick was still there: the `Output` property's
  own summary offered "a bus that is not open where the switch is off", `SourceStripViewModel.CanSolo`
  said soloing was possible "only while there is one output stream", and the solo button's tooltip
  on the mixer told somebody to go and tick One output stream in SETTINGS, which is **help text
  naming a setting that does not exist**. `CanSolo` is still asked and still right: it is on in
  every ordinary run and false only where the bus could not be opened at all
- **Taking a source aside is put back when the output moves.** "Only here" unplugs somebody else's
  program from its own output on the promise that it is heard through this application instead,
  and where here comes out is the output in SETTINGS: pick another and the arrangement stands over
  a device nobody is listening to, with the source still unplugged. Worse where the new output is
  the one the source was taken off or the one it was sent to. `RecordViewModel.OutputMoved` puts it
  back rather than carrying it over, which is the rule the switch already keeps at the other
  moment: it reaches out of the application and quiets another program, so it goes on only when
  somebody says so. Said on the status line and in the log, since a switch that turns itself off
  with nothing saying why reads as one that does not stay put, and it does nothing whatever where
  nothing was aside, which is every ordinary run
- **The way that went stale is the one this file keeps warning about.** The note was written when
  it was true, the bus was built afterwards, and nothing made the two meet: a paragraph describing
  work still to do outlives the work. It cost a reading of this codebase that recommended building
  something that already exists, which is exactly the failure mode named up in the help section,
  and the tell was the same one: the document disagreed with the code, and only the code runs
- **A folder that moves takes CI's checks with it, and a check counting nought is the only
  reason the release did not go out empty.** The rack moved from `machines/` at the top of the
  tree to `rack/machines` and `rack/effects` when the two worlds were split, and the release
  workflow went on counting the old folder in five places: the shared script, the Windows step
  written out in PowerShell, and the RPM and the .deb, which grep a path inside the package. Every
  one of them found nought files, which is what the guard reads as the check itself being broken,
  so v2.4.0 failed on all four platforms rather than shipping an application with no machines,
  no effects and nothing to make a sound with. That guard is why the shape is worth keeping: a
  count that must not be nought says so, since a check that silently has nothing to check reports
  nothing for the rest of its life
- The script is `verify-rack.sh` now rather than `verify-machines.sh`, and it walks both worlds
  because a folder is one or the other: a machine is described by `machine.json` and an effect by
  `effect.json`, which is the one word that differs between the two passes. It was proved by
  publishing and breaking the payload four ways rather than by reading it, which is the only way
  this is ever caught: a whole device gone, an effect without its manifest, a machine without its
  manifest, and a machine missing a preset, the last three with the file count made right again
  so that only the branch under test could catch them

- **Whether the libraries are current is asked once a month by CI, and asked by hash.**
  `.github/scripts/check-natives.sh` downloads what un4seen ships, pulls out the eight files this
  program carries, and compares them; `.github/workflows/natives.yml` runs it on the first of the
  month and by hand. Not on a push, since the answer cannot change because of anything in a
  commit, and monthly because these see a release once or twice a year each and `basswasapi` has
  gone three years without one
- Three answers, not two. Current, behind, and **moved**: every archive carries its version in its
  own name, so `bassasio14.zip` becomes `bassasio15.zip` the day 1.5 ships and the download simply
  answers 404. That is not a failure to check, it is the loudest answer there is, and it is
  reported as one rather than passing quietly. A file that is not in the checkout at all is the
  third way to fail
- A failure there is a note rather than a broken build: what to do about a new decoder or output
  driver is a decision, since both are on the audio path and nothing has been listened to. The
  script was run against a checkout with a byte added, a file removed and an archive renamed, and
  it reported all three and came back with 1, because a check that has never failed reports
  nothing
- **A native is copied by three targets that name each file one by one, and a fourth place lists
  them again.** `CopyBassToOutput` for a plain build on Windows, `CopyBassToLinuxOutput` for one
  here, `EnsureBassDllInPublish` for a Windows publish, and the release workflow's own check that
  the payload really has them. The `<None>` item alone is not enough: it lands the file under
  `native/win-x64/` in the output, which is not where a program looks, so the targets are what put
  it beside the executable. Adding `bassasio.dll` and forgetting them meant a publish that carried
  it in a folder nothing reads, and an installer that packed it there, and nothing anywhere saying
  so. Proved by publishing for win-x64 and looking rather than by reading the csproj, which is the
  only way this is ever going to be caught
- BASS library binaries are copied to output via build targets in csproj
- managed-midi API has obsolete warnings (suppressed via `<NoWarn>CS0618</NoWarn>`)
- Startup errors logged to `startup.log` for debugging
