# JingleBox2

<p align="center">
  <img src="Assets/JingleBox2_1024.png" alt="JingleBox2 icon" width="128" height="128" />
</p>

**JingleBox2** is a cross-platform audio pad launcher built with **.NET** and **Avalonia UI**, for **radio, streaming and live audio**. Fire jingles from pads, record and trim your own takes, and write beds and stings in a tracker that hosts VST3 and CLAP plugins.

The pages you play on are kept apart from the pages you set things up on, so nothing moves under your hand while you are on air.

<p align="center">
  <a href="../../releases/latest">
    <img alt="Download latest release" src="https://img.shields.io/badge/Download-Latest%20Release-brightgreen">
  </a>
</p>

---

## The pages

**RECORD** captures takes and keeps them on a shelf the rest of the app plays from. Pick an input, watch the meter and the clip light, set the gain, record. On Linux you can capture anything in the audio graph, including another program's output; on Windows you can capture what an output device is playing. Click a take to see its waveform, then play, trim, normalise or rename it, and file it under a category of your own. The shelf can then be narrowed to speech, beds or effects, here and in front of every take picker on a machine. WAV files from anywhere on disc can be imported, and anything that is not already 16-bit is converted on the way in.

**PADS** is where a pad is set up: its name, the recording it plays or the stream URL it opens, its colour, whether it loops, its fades and its level, plus an effect slot that takes the same plugins the tracker uses.

**FIRE** is the page you use while the show is running. Large pads, click or MIDI to fire, click again to stop, as many at once as you like. Nothing on this page can be set up by accident, and the transport's only live button is stop.

**TRACKER** writes songs: patterns, an order list, and instruments the song owns rather than borrows. Instruments come off five machines, Zampler, BongaBong, Ouroboros, OddSkilla and Recording, or from a VST3 or CLAP plugin. Every track has a mixer strip with pan, mute, solo and ducking, and an insert chain. A track plays as many notes at once as it has note columns, one to eight, and the instrument says what becomes of a note when the next one arrives in its column: cut it, let it play its own release under the new note, or leave it holding. Machines are registered in SETTINGS and can be added, removed and imported from a zip.

**MACHINES** is the designer, where a machine's front panel is laid out: drag the knobs, faders, switches, pads and keyboards onto the face and say what each one is wired to. It is a page of its own only when you ask for it in SETTINGS, for when the instrument is the work rather than the song; it is inside the tracker either way.

**SETTINGS** holds the output device, the engine's sample rate and plugin cushion, the recording input, MIDI devices and what each one drives, control surfaces, the machine registry, plugin folders, the theme, the shortcuts and the log switch.

---

## Around the app

- **The transport** at the top of the window belongs to the page you are on: a take on RECORD, the pads on FIRE, the song on TRACKER. The space bar works it. When something is running on a page you have left, the transport keeps showing it but only stop works, and stopping hands it back to the page in front of you.
- **MIDI** triggers pads and types notes into the tracker. Each device is given a job in SETTINGS, so a controller can drive the pads, the tracker, or both.
- **Themes**: twelve, as six pairs of dark and light. Dark and Light are the plain pair; Neon, Industrial, Orchid, Citrus and Ember each come both ways.
- **Profiles**: a whole set of pads saved under a name. Built and switched on PADS, and FIRE says which one is loaded.

---

## How it works

Audio runs through **BASS** (ManagedBass). Pads are streams the engine owns; the tracker mixes its own voices into one stream and hands the result to the same device.

Plugins run **in a process of their own**, one per plugin, and so does the scan. A plugin that crashes takes only itself down: an effect passes its audio through, an instrument goes quiet, and the panel offers to start it again. The child process is this same executable started with `--plugin-host`, talking over a socket with the audio in shared memory.

Machines are laid out rather than coded. Six engines are compiled in and a machine is a face over one of them, described in a `machine.json` the designer writes and drawn by the app: what the description does not draw, nobody draws. `Machines.Abstractions` is what a machine is and `Machines.Ui` is what it is drawn with, and those two are the assemblies `LICENSE.EXCEPTION` names.

**The machine registry** is what this installation has, and it is the only thing that answers that. Two folders and only one of them is yours: beside the program is what ships, a source to take a machine from and never the answer to what is on the rack, and under the application folder is what you have actually registered. Removing a machine is not losing it, since the shipped copy stays where it was.

Registering is something you do, and so is unregistering, so what is recorded is what has been *offered* rather than what is present. A machine that ships and has never been offered arrives on the rack; one you threw out stays thrown out. A machine that ships is brought up to date file by file when a new version of the program lands, and nothing is deleted, so a preset you saved onto a machine survives. A machine from somebody else's zip is imported here, and lives alongside them.

Everything asks it: what the rack shows, what a panel is drawn from, what a song can sound, and which machines a song is missing are one question with one answer.

Songs, recordings, instruments and settings are files you can copy, hand to someone else, or back up. Nothing lives only in a database.

Every seam is an interface, and the prose lives on the interface: what a thing is for, why it works the way it does, and what was got wrong on the way there. `CS1591` is left switched on so the compiler says when that lapses, and the build runs at nought warnings of any kind.

---

## Tests

```bash
dotnet test Tests/JingleBox2.Tests.csproj
```

848 of them, in about three seconds, with no window and no hardware. They run in CI on every branch and every pull request, on Linux **and** Windows, because two of them are genuinely platform specific: a path is written with a separator that is not the same character on the two systems, and those are exactly the tests that would pass on one machine for a year and fail on somebody else's. The release workflow runs them first and every job that makes an artefact waits on them, because a release is the one build nobody gets to take back.

What is covered is the parts that can be got wrong quietly: the MIDI wire, controller profiles and codecs, shortcuts, the histories, patterns and their edits, a song written down and poured back, the mix, the filters and the drive curve, the sample window and its loop, a WAV read and written, and the bridge's message bodies. Several of those tests exist because that exact thing was wrong once.

---

## Requirements

- .NET SDK 10
- Windows or Linux (x64, and linux-arm64)
- An audio device BASS can open

---

## Build and run

```bash
dotnet restore
dotnet build
dotnet run

dotnet publish -c Release -r win-x64      # Windows
dotnet publish -c Release -r linux-x64    # Linux
```

The BASS binaries in `native/` are copied to the output by the build.

---

## Where things are kept

Everything the app keeps lives in one folder: `%APPDATA%\JingleBox2` on Windows, `~/.config/JingleBox2` on Linux.

```bash
config.json      # settings, pad profiles, window size
recordings/      # your takes, 16-bit WAV
deleted/         # takes you threw away this session, so undo can fetch them back
songs/           # one .jibx per song: a zip holding song.json and each plugin's patch
                 # Pack writes one with the recordings inside it too, for handing over
machines/        # the machines registered here, a folder each
instruments/     # the instruments on your rack, and the plugins you have added
controllers/     # a .json saying what a controller is, a .lua saying what it does
crashes/         # what the app was doing when a run ended badly
jinglebox.log    # off unless switched on, rolled over at a few megabytes
```

---

## Switches

| Variable | What it does |
| --- | --- |
| `JB_LOG=1` | Writes the log without going to SETTINGS first. `JB_LOG=midi,plugin` picks areas |
| `JB_LOG_DIR` | Where the log goes, for a plugin's own process |
| `JB_PLUGINS_INPROCESS=1` | Loads plugins in the app's process instead of their own |
| `JB_PLUGIN_TRACE=1` | Has each plugin process write what it is doing to `/tmp/jinglebox-plugin-<pid>.log` |

---

## Project structure

```bash
JingleBox2/
├─ Audio/              # BASS engine, recording, waveforms, routing
│  └─ Plugins/         # VST3 and CLAP: scanning, hosting, the out-of-process bridge
├─ Tracker/            # Songs, patterns, the player, the machine rack
│  └─ Synth/           # Voices, envelopes, the synth mixer
├─ Midi/               # Input, routing to pads and to the tracker
├─ Music/              # Notes, pitch and keyboards, knowing nothing about patterns
├─ Files/              # Where the app keeps things, and writing a file whole
├─ Config/             # Settings model and JSON persistence
├─ Controllers/        # Controller profiles and their Lua codecs
├─ Shortcuts/          # What a key can ask for, and who answers
├─ Scripting/          # The Lua sandbox
├─ Machines.Abstractions/  # What a machine is: the contract an outside one links to
├─ Machines.Ui/        # What a machine is drawn with: knobs, faders, panels
├─ ViewModels/         # MVVM, CommunityToolkit
├─ Views/              # Avalonia views, and the app's own drawn controls
├─ Themes/             # One resource dictionary per theme
├─ Help/               # What the app explains about itself
├─ Tests/              # xunit, no window and no hardware
├─ Diagnostics/        # The log and the crash report
├─ machines/           # The machines that ship: a folder each, panel and presets inside
├─ native/             # BASS binaries per platform
├─ installer/windows/  # Inno Setup script
└─ packaging/fedora/   # RPM spec and desktop entry
```

---

## Design notes

- **Playing is not setting up.** FIRE and PADS are the same pads seen two ways: one page fires them, the other builds them. Neither page can do the other's job by accident. The matrix is rows by columns, from 4 pads up to 16, or 32 with the extended switch on, set in SETTINGS: 4x4, 2x8 and 1x16 are all allowed.
- **The engine is the truth.** Playback state is read from BASS rather than remembered alongside it.
- **A machine is not an instrument.** A machine is a face over one of the built-in engines: a folder holding a panel, a badge, its presets and its own sounds, made in the designer and travelling as a zip. An instrument is a machine in use, with your name and your settings, stored inside the song. Two instruments can come off one machine.
- **A song owns its instruments.** Opening a song sounds the way it was saved, whatever the rack has become since.
- **The registry decides what you have.** A machine is registered or it is not, and that one list answers what the rack shows, what a panel is drawn from and what a song can sound.
- **An instrument is on a machine.** Without that machine there is no instrument to play, so it makes no sound and has no panel. Opening a song says which machines are not registered, and opening one of those instruments says so and stops.
- **Nothing is only in memory.** Unsaved tracker work is kept in a rescue file while you work, and dropped the moment you save for real.

---

## Status

Actively developed, and in daily use. The pad launcher, recorder, tracker, plugin hosting and MIDI are all working; expect the edges to keep moving.

---

## License

Licensed under the **GNU General Public License v2.0 (GPL-2.0-only)**, the same license the Linux kernel uses. See `LICENSE` for the full text.

Instruments and effects are exempt. `LICENSE.EXCEPTION` grants permission to write a module against JingleBox2's plugin interfaces, the machine interface and the protocols it uses to talk to a plugin in its own process, and to distribute that module under whatever terms you like. Sounds, presets, recordings and songs are data and were never covered by the license at all.

---

## Author

Built by **Peter van de Pas** for radio and live audio use.
