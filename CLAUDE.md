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
- **Tracker**: TrackerPlayer clock → TrackerSequencer events → sample channels (TrackerSampleBank) or synth voices (SynthMixer → SynthOutput → one BASS stream)

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
- `SynthMixer` (Tracker/Synth/): Every sounding synth voice, summed; one voice per track
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
- `Knob` / `Fader` / `NumberField` (Machines.Ui/): The app's own value controls; a pot knob, a vertical fader, and a compact stepper field. They live in the machine UI assembly because a machine bought from somebody else is built out of the same controls the app's own machines are
- `WaveformView` (Machines.Ui/): A recording's shape, with the window and the loop draggable on the picture
- `MachinePanelView` (Machines.Ui/): A machine's face, built from what the machine says it looks like. Designing, every element can be picked and none can be turned; off, it is an ordinary panel
- `MachinePartSample` (Machines.Ui/): One entry of the designer's library, drawn as the real control it adds
- `ThemePalette` (Machines.Ui/): Theme colours for custom-drawn controls, read as `Color.*` keys so a theme swap lands at once
- `MainViewModel`: Central orchestrator connecting audio, config, and MIDI subsystems
- `PadViewModel`: Single pad state (name, source, volume, playback state)

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
  instruments, and `state/NN.bin` for each plugin instrument's patch, as the plugin handed it
  over. The patches came out of the document because they are almost all of it (one song here
  is 348 KB of which the music is 781 bytes and one synth's patch is 331 KB) and because a
  document is all or nothing: a patch that came back damaged used to cost the song. Recordings
  are named `{app}/...` when they live in the application folder, so a song survives that folder
  moving or being on another machine. Songs written before this are converted on the way in and
  the original is kept as `.json.old`
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
- Automation is not built. `docs/automation.md` is the plan: lanes rather than more effect
  commands, one per parameter per track per pattern, values normalised 0 to 1 and converted
  through `IControlTarget`, which is the same interface remote control writes through. Recording
  a lane from a knob that is already linked is nearly free; the editor is all of the work
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
  nothing announced, but the programs do not overlap so one message is usually enough. A number
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
- A note played by hand holds for a fixed moment on a generated sound, which would otherwise
  never stop, and for its own length on a recording: a take cut off part way through is not the
  sound the instrument makes. `SampleVoice.WindowSeconds` is that length, and the hold is passed
  back up through `Audition` and `NotePlayed` so the key that lights and the cursor that runs
  last exactly as long as the sound. Auditions pile up, as a keyboard does, unless the
  instrument says `OneVoice`, which cuts what it was sounding first; in a pattern this changes
  nothing, since a track is one voice already
- The audio engine runs whenever a track has a chain, not only while something is playing. A
  plugin has to be given blocks or it cannot work on the audio, cannot finish a delay's tail,
  and cannot tell the host what its own window did. `SynthMixer` therefore does not rest while
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
