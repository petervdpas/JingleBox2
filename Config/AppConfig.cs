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

    /// <summary>
    /// Whether the machine editor is a page of its own along the top.
    /// </summary>
    /// <remarks>
    /// Off unless you are building instruments. Somebody who fires jingles has no use for a
    /// designer in the way, and somebody who is building one wants it a click away rather than
    /// three pages inside the tracker.
    /// </remarks>
    public bool ShowMachineEditor { get; set; }

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
    /// <summary>
    /// Whether the tracker puts its plugins down when you go and work somewhere else.
    /// </summary>
    /// <remarks>
    /// Off, because the cost is on the way back rather than while you are away. Each plugin is
    /// a process with its patch loaded, and a song of four keeps four of them and the audio
    /// engine running while you are on the pads. Switched on, going to another page lets all of
    /// that go, and coming back to the tracker starts them again, which is the several seconds
    /// a plugin takes to load, every time you switch.
    ///
    /// Worth it on a machine where the memory matters more than the wait, and not otherwise,
    /// which is why it is asked rather than decided.
    /// </remarks>
    public bool FreeTrackerPlugins { get; set; }

    public bool WriteLog { get; set; }

    /// <summary>
    /// Which parts of the app write to it, as the flags of <see cref="Diagnostics.LogArea"/>.
    /// </summary>
    /// <remarks>
    /// Everything, unless somebody has narrowed it. Narrowing matters because the areas are not
    /// alike: most lines are written once or only when something has gone wrong, and a few are
    /// written per message or per block. One noisy area switched on with the rest fills the
    /// queue, and a full queue drops lines, so switching everything on is how you lose the one
    /// line you were looking for.
    ///
    /// Zero reads as everything, so a settings file written before this existed logs the way it
    /// always did.
    /// </remarks>
    public int LogAreas { get; set; }

    /// <summary>
    /// The shortcuts somebody changed, and only those.
    /// </summary>
    /// <remarks>
    /// What is left alone is not written down, so a default that turns out to be a poor choice
    /// can be improved and will reach anybody who never had an opinion about it. A shortcut
    /// deliberately taken off is stored with no keys, which is how that is told from never
    /// having been touched.
    /// </remarks>
    public List<Shortcuts.ShortcutBinding>? Shortcuts { get; set; }

    // 0 means never resized, so fall back to sizing from the pad matrix.
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public List<PadConfig> Pads { get; set; } = new();
}
