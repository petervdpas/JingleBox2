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
- `Config/` - Configuration models and JSON persistence to `%APPDATA%/JingleBox2/config.json`
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
- `ConfigStore` (Config/): JSON persistence with profile migration support
- `MidiRouter` (Midi/): Maps MIDI messages to pad triggers with toggle/start modes
- `MidiDispatcher` (Midi/): Sends each message to the pads, the tracker, or both, by the device's role in SETTINGS
- `MidiNoteRouter` (Midi/): Turns keyboard notes into tracker note entry
- `TrackerPlayer` (Tracker/): Owns the clock and routes each event to a sample channel or a synth voice, through the track's mixer strip
- `MixLevels` (Tracker/): What the mix adds up to, mute and solo included
- `SynthMixer` (Tracker/Synth/): Every sounding synth voice, summed; one voice per track
- `InstrumentLibrary` (Tracker/): The instruments you own, in `%APPDATA%/JingleBox2/instruments/`, one file per instrument named by its id. Songs store a copy of each instrument they use and rebind it by id on load
- `SynthPresetStore` (Tracker/Synth/): Preset bank in `%APPDATA%/JingleBox2/presets/`, with built-in starters. A preset is where a new instrument starts
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
- Three places things are stored, on purpose: presets (a starting sound), instruments (a voice
  you own, shared by every song), and songs (patterns plus the instruments they use)
- BASS library binaries are copied to output via build targets in csproj
- managed-midi API has obsolete warnings (suppressed via `<NoWarn>CS0618</NoWarn>`)
- Startup errors logged to `startup.log` for debugging
