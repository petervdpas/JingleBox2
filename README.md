# JingleBox2

<p align="center">
  <img src="Assets/icon.ico" alt="JingleBox2 icon" width="128" height="128" />
</p>

**JingleBox2** is a lightweight, cross-platform audio pad launcher built with **.NET** and **Avalonia UI**, designed for **radio, streaming, and live audio workflows**.

It separates **live operation** from **configuration**, supports **multiple simultaneous pads**, and focuses on reliability and speed rather than visual clutter.

---

## Features

- 🎛 **Pad launcher (USE view)**
  - Large, always-visible pads
  - Click to play, click again to stop
  - Multiple pads can play simultaneously
  - No scrolling during live use

- ⚙️ **Pad configuration (CONFIG view)**
  - Per-pad name, source, and volume
  - File playback and stream URLs
  - Test / Stop / Clear per pad
  - Persistent configuration

- 🔊 **Audio engine**
  - Built on **ManagedBass (BASS)**
  - Per-pad playback state tracking
  - Concurrent playback support
  - Output device selection

- 🖥 **UI**
  - Avalonia (cross-platform)
  - Clean, dark, radio-friendly layout
  - Resizable window with responsive pad grid

---

## Project Structure

```bash
JingleBox2/
├─ Audio/              # Audio engine (BassAudioEngine, IAudioEngine)
├─ Views/              # Avalonia views (UseView, ConfigView)
├─ ViewModels/         # MVVM view models
├─ Config/             # App and pad configuration models
├─ Models/             # Shared domain models
└─ MainWindow.axaml    # Main window layout
```

---

## Requirements

- .NET SDK (8.0 recommended)
- Windows / Linux / macOS (depending on BASS support)
- Audio output device supported by BASS

---

## Build & Run

```bash
dotnet restore
dotnet build
dotnet run
```

---

## Design Philosophy

- **USE ≠ CONFIG**
  - USE is for performance: no scrolling, no surprises
  - CONFIG is for setup: clear structure, readable controls
- **Predictable behavior**
  - No implicit state
  - Engine is the source of truth for playback
- **Radio-first**
  - Fast interaction
  - Minimal UI friction
  - No unnecessary animations or gimmicks

---

## Status

This project is **actively developed** and currently in an early but usable state.

Planned improvements may include:

* MIDI / keyboard triggering
* Per-pad colors
* Pad groups or pages
* Visual activity indicators (VU / glow)
* Preset management

---

## License

This project is licensed under the **GNU General Public License v2.0 (GPL-2.0-only)**.

This is the same license used by the Linux kernel.

See the `LICENSE` file for the full license text.

---

## Author

Built by **Peter van de Pas**
for radio and live audio use.
