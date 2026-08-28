using System.Collections.Generic;
using JingleBox2.Midi;

namespace JingleBox2.Config;

/// <summary>
/// One pad as the settings file holds it.
/// </summary>
/// <remarks>
/// Plain data, and deliberately so: this is what <c>config.json</c> is made of, and every
/// property here is a name somebody's existing file already uses. Renaming one silently drops
/// whatever it held on the next load, so a name here is as good as public.
/// </remarks>
public sealed class PadConfig
{
    /// <summary>What is written across the pad's face.</summary>
    public string Name { get; set; } = "";

    /// <summary>A recording off the shelf, a stream from the web, or nothing yet.</summary>
    public PadSourceKind Kind { get; set; } = PadSourceKind.None;

    /// <summary>Which recording or which address, read according to <see cref="Kind"/>.</summary>
    public string Source { get; set; } = "";

    /// <summary>The pad's own level, nought to one, applied to whatever it plays.</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Whether it starts again when it reaches the end.</summary>
    public bool Loop { get; set; } = false;

    /// <summary>Seconds to come up to level when it starts.</summary>
    public double FadeIn { get; set; } = 0;

    /// <summary>Seconds to go quiet when it is stopped.</summary>
    public double FadeOut { get; set; } = 0;

    /// <summary>The colour it is drawn in, or empty for the theme's own.</summary>
    public string Color { get; set; } = "";

    /// <summary>
    /// The effects in this pad's path.
    /// </summary>
    /// <remarks>
    /// Null for a pad with none rather than an empty chain, so a profile of sixteen pads nobody
    /// has put an effect on is not sixteen empty objects in the file.
    /// </remarks>
    public Audio.Plugins.PluginChainConfig? Plugins { get; set; }
}

/// <summary>
/// A named set of pads, so one installation can hold several layouts and be switched between
/// them without anything being overwritten.
/// </summary>
public sealed class ConfigProfile
{
    /// <summary>Its name, which is how <see cref="AppConfig.SelectedProfile"/> picks it.</summary>
    public string Name { get; set; } = "default";

    /// <summary>Its pads, kept at exactly rows by columns on every read and write.</summary>
    public List<PadConfig> Pads { get; set; } = new();
}

/// <summary>
/// Everything the application remembers between runs, as <c>config.json</c> holds it.
/// </summary>
/// <remarks>
/// A settings file somebody already has is the reason almost every decision here looks
/// conservative: a property is only ever added, a default has to mean the same thing as the
/// absence of the property, and nothing is dropped on the way in. <see cref="IConfigStore"/> is
/// what enforces the parts that cannot be expressed as a default.
/// </remarks>
public sealed class AppConfig
{
    /// <summary>The output card to play through, or -1 for the system's first.</summary>
    public int SelectedOutputDeviceId { get; set; } = -1;

    /// <summary>Which of <see cref="Profiles"/> is on the pads now, by name.</summary>
    public string SelectedProfile { get; set; } = "default";

    /// <summary>Every layout there is. A "default" always exists, made if it is missing.</summary>
    public List<ConfigProfile> Profiles { get; set; } = new();

    /// <summary>Which resource dictionary under <c>Themes/</c> is applied.</summary>
    public string SelectedTheme { get; set; } = "Dark";

    /// <summary>How many rows of pads.</summary>
    public int Rows { get; set; } = 4;

    /// <summary>And how many columns. Rows times columns is how many pads there are.</summary>
    public int Columns { get; set; } = 2;

    /// <summary>
    /// Whether the pad matrix may go beyond <see cref="PadMatrix.Usual"/>.
    /// </summary>
    /// <remarks>
    /// Off, the matrix goes up to sixteen pads, which is what fits comfortably on a laptop and
    /// what a hand can find without looking. On, it goes to thirty-two, for a desk with the
    /// screen to show them. A switch rather than simply raising the ceiling, because a grid of
    /// thirty-two is a different instrument from a grid of eight and nobody should arrive at one
    /// by holding an arrow key down in the settings.
    /// </remarks>
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

    /// <summary>
    /// Whether rows and columns move together in SETTINGS.
    /// </summary>
    /// <remarks>
    /// Stored, because a lock you have to close again every time you open the page is a lock
    /// nobody uses twice.
    /// </remarks>
    public bool LinkPadMatrix { get; set; }

    /// <summary>The MIDI devices, their roles, and what each pad listens for.</summary>
    public MidiConfig Midi { get; set; } = new();

    /// <summary>Gain applied to whatever is being recorded, in dB. Nought is unity.</summary>
    public double RecordGainDb { get; set; } = 0;

    /// <summary>
    /// Whether a note typed in from a keyboard is written at full level whatever it was played
    /// at.
    /// </summary>
    /// <remarks>
    /// A velocity sensitive keyboard writes a different level for every hit. Some parts want
    /// that; a kick almost never does.
    /// </remarks>
    public bool IgnoreKeyVelocity { get; set; }

    /// <summary>
    /// Whether letting a key go writes a note-off into the pattern.
    /// </summary>
    /// <remarks>
    /// Renoise's own RecordNoteOffs, and off here for the same reason it is worth having as a
    /// switch: with a step of one it fills a pattern quickly, which suits playing a part in
    /// rather than stepping notes into it.
    /// </remarks>
    public bool RecordNoteOffs { get; set; }

    /// <summary>
    /// Extra places to look for plugins, on top of the ones each format specifies.
    /// </summary>
    /// <remarks>Somebody who keeps their plugins somewhere of their own says so here.</remarks>
    public List<string> PluginFolders { get; set; } = new();

    /// <summary>
    /// What the tracker and the synth run at, in Hz.
    /// </summary>
    /// <remarks>
    /// Zero means whatever the output device is running at, which is what stops everything being
    /// resampled on the way out.
    /// </remarks>
    public int EngineSampleRate { get; set; }

    /// <summary>
    /// What the last scan found.
    /// </summary>
    /// <remarks>
    /// Kept so the application knows its plugins at startup rather than opening every plugin
    /// library again to ask, which is seconds a plugin and is why a scan is a thing you press
    /// rather than something that happens on the way in.
    /// </remarks>
    public List<Audio.Plugins.PluginInfo> KnownPlugins { get; set; } = new();

    /// <summary>
    /// Which input to record from, by name.
    /// </summary>
    /// <remarks>
    /// By name rather than by number, because a device's number shifts when hardware is plugged
    /// in or taken out and the name is the only part of it that survives that.
    /// </remarks>
    public string RecordInputDevice { get; set; } = "";

    /// <summary>
    /// How far ahead of the sound card the tracker mixes, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Zero mixes in step, which is tightest and has no slack at all for a plugin that takes a
    /// moment longer than usual. See <c>JingleBox2.Audio.SynthOutput.UseRenderAhead</c>.
    /// </remarks>
    public int RenderAheadMs { get; set; }

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

    /// <summary>
    /// Whether the application and every plugin process write what they are doing to
    /// <c>jinglebox.log</c> beside this file.
    /// </summary>
    /// <remarks>
    /// Off by default, and off costs one comparison. See <c>JingleBox2.Diagnostics.Log</c>.
    /// </remarks>
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

    /// <summary>
    /// How wide the window was left.
    /// </summary>
    /// <remarks>
    /// Nought means it was never resized, and the window sizes itself from the pad matrix
    /// instead. A stored nought and an absent property therefore mean the same thing, which is
    /// what lets this be read out of a file written before it existed.
    /// </remarks>
    public double WindowWidth { get; set; }

    /// <summary>How tall it was left, on the same terms as <see cref="WindowWidth"/>.</summary>
    public double WindowHeight { get; set; }

    /// <summary>Whether it was left filling the screen, which beats the stored size.</summary>
    public bool WindowMaximized { get; set; }

    /// <summary>
    /// The selected profile's pads, copied out flat.
    /// </summary>
    /// <remarks>
    /// This is where the pads lived before there were profiles, and it is kept in step with the
    /// selected profile on every load and save. Two roles: it is what a file written before
    /// profiles existed is migrated out of, and it is what anything still reading the old shape
    /// gets. The profile is the truth; this is a copy of it.
    /// </remarks>
    public List<PadConfig> Pads { get; set; } = new();
}
