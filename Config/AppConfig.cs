// ===============================
// Config/AppConfig.cs
// ===============================
using System.Collections.Generic;
using JingleBox2.Midi;

namespace JingleBox2.Config;

public sealed class PadConfig
{
    public string Name { get; set; } = "";
    public PadSourceKind Kind { get; set; } = PadSourceKind.None;
    public string Source { get; set; } = "";
    public double Volume { get; set; } = 1.0;

    public bool Loop { get; set; } = false;
    public double FadeIn { get; set; } = 0;
    public double FadeOut { get; set; } = 0;
    public string Color { get; set; } = "";

    // The effects on this pad. Null for a pad with none, so a profile is not full of empty
    // chains.
    public Audio.Plugins.PluginChainConfig? Plugins { get; set; }
}

public sealed class ConfigProfile
{
    public string Name { get; set; } = "default";
    public List<PadConfig> Pads { get; set; } = new();
}

public sealed class AppConfig
{
    public int SelectedOutputDeviceId { get; set; } = -1;
    public string SelectedProfile { get; set; } = "default";
    public List<ConfigProfile> Profiles { get; set; } = new();
    public string SelectedTheme { get; set; } = "Dark";
    public int Rows { get; set; } = 4;
    public int Columns { get; set; } = 2;

    // Off, the matrix goes up to sixteen pads, which is what fits comfortably on a laptop and
    // what a hand can find without looking. On, it goes to thirty-two, for a desk with the
    // screen to show them. A switch rather than simply raising the ceiling, because a grid of
    // thirty-two is a different instrument from a grid of eight and nobody should arrive at
    // one by holding an arrow key down in the settings.
    public bool ExtendedPadMatrix { get; set; }

    // The bracket between rows and columns in SETTINGS. Kept, because a lock you have to close
    // again every time you open the page is a lock nobody uses twice.
    public bool LinkPadMatrix { get; set; }
    public MidiConfig Midi { get; set; } = new();
    public double RecordGainDb { get; set; } = 0;

    // A velocity sensitive keyboard writes a different level for every hit. Some parts want
    // that; a kick almost never does.
    public bool IgnoreKeyVelocity { get; set; }

    // Off, a key coming up on a MIDI keyboard writes nothing. On, it writes a note-off where
    // the cursor is, the way Renoise's own RecordNoteOffs does. Off by default: with a step of
    // one it fills a pattern quickly, and that suits playing in rather than stepping notes.
    public bool RecordNoteOffs { get; set; }

    // Extra places to look for plugins, on top of the ones the format specifies. Someone who
    // keeps their plugins somewhere of their own says so here.
    public List<string> PluginFolders { get; set; } = new();

    // What the tracker and synth run at. Zero means whatever the output device is running at,
    // which is what stops everything being resampled on the way out.
    public int EngineSampleRate { get; set; }

    // What the last scan found, so the app knows its plugins at startup instead of opening
    // every plugin library again to ask.
    public List<Audio.Plugins.PluginInfo> KnownPlugins { get; set; } = new();

    // Stored by name, not index: device indexes shift when hardware is plugged in or out.
    public string RecordInputDevice { get; set; } = "";

    // How far ahead of the sound card the tracker mixes, in milliseconds. Zero mixes in step,
    // which is tightest and has no slack for a plugin that takes a moment longer than usual.
    // See JingleBox2.Audio.SynthOutput.UseRenderAhead.
    public int RenderAheadMs { get; set; }

    // Off by default. On, the app and every plugin process write what they are doing to
    // jinglebox.log next to this file. See JingleBox2.Diagnostics.Log.
    public bool WriteLog { get; set; }

    // 0 means never resized, so fall back to sizing from the pad matrix.
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public List<PadConfig> Pads { get; set; } = new();
}
