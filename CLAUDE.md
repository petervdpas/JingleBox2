# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

JingleBox2 is a cross-platform audio pad launcher built with .NET 9 and Avalonia UI for radio, streaming, and live audio workflows. It features a USE tab for performance (triggering audio pads) and a PADS tab for setup, with MIDI controller support.

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
- `Tracker/` - Song model, sequencing, playback, JSON song files, and the instrument library
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
- `InstrumentLibrary` (Tracker/): The instruments you own, in `%APPDATA%/JingleBox2/instruments/`, one file per instrument named by its id. Where a sound starts: taking one into a song copies it, and the copy is then the song's own. Editing it in the song changes that song alone. A synth or plugin patch travels inside the song; a recording does not, since the instrument keeps only the path to it
- `SampleSlicer` (Tracker/): Where a recording gets cut into pieces. Finds the attacks off the
  peak data by beating a decaying peak-hold, walks each cut back to where its sound began, and
  falls back to an even division when there is nothing to find. Knows nothing about what takes
  the pieces
- `Slices` / `KeyRegions` (Tracker/): The parts a kit and a map do identically. Reading the cuts
  back off the pieces, and sharing a stretch of keyboard out among them
- `SliceEditorViewModel` / `SliceEditor` (ViewModels/, Views/): One take, its picture and its
  boundaries, used by both machines that hold pieces
- `Knob` / `Fader` / `NumberField` (Views/): The app's own value controls; a pot knob, a vertical fader, and a compact stepper field
- `ThemePalette` (Views/): Theme colours for custom-drawn controls, read as `Color.*` keys so a theme swap lands at once
- `MainViewModel`: Central orchestrator connecting audio, config, and MIDI subsystems
- `PadViewModel`: Single pad state (name, source, volume, playback state)

## Technical Notes

- Configurable pad matrix size (rows x columns) via SETTINGS tab
  - Minimum: 4 pads total (e.g., 2x2, 1x4, 4x1)
  - Maximum: 16 pads total (e.g., 4x4, 2x8, 8x2)
  - Default: 4 rows x 2 columns = 8 pads (backward compatible)
- Two source types per pad: local files (WAV/MP3/OGG/FLAC) or HTTP streams
- Tracker instruments come in two kinds: a recording pitched by resampling, or a synth patch
  generated at playback time (the parameter set mirrors MappoGraph's chiptune synth, plus a
  drive control). The
  tracker only ever loads instruments; whether one is a sample or a synth is its own business
- Both Zampler and BongaBong can be filled from one recording rather than many: chopping. A
  chopped instrument is one whose pieces all point at the same file with different windows, so
  it needs no new storage and the take is decoded once for all of them. The cuts are not stored
  separately; they are read back off the pieces. Put a different sample on one piece and it
  stops being a chop, which is why `IsSliced` is asked rather than the `Sliced` flag
- Two places things are stored, on purpose: instruments (the shelf of sounds you own, where a
  new one starts) and songs (patterns plus their own copies of the instruments they use). There
  was a third, a preset bank, and it went when the library stopped reaching into songs: a sound
  you start from and a sound you own turned out to be the same object. A fresh library seeds
  itself with six starters, and from then on they are ordinary instruments
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
