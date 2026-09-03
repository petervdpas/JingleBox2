using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Config.Interfaces;
using JingleBox2.Files.Interfaces;
using JingleBox2.Files;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// JSON, indented, so the file can be read and edited by a person: it is the only place a
/// setting nobody has built a page for can be reached from, and it is the first thing anybody
/// asks for when something is wrong.
/// </remarks>
public sealed class ConfigStore : IConfigStore
{
    /// <summary>
    /// Indented on purpose. The file is meant to be readable by whoever has to look at it, and
    /// it is small enough that the whitespace costs nothing worth counting.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>The profile that always exists, and the one anything falls back to.</summary>
    private const string DefaultProfile = "default";

    /// <summary>What a pad listens for when nothing has said otherwise, one note per pad.</summary>
    /// <remarks>
    /// 36 is the note the bottom left pad sends on almost every pad controller made, so a
    /// keyboard plugged in with nothing configured usually plays the pads straight away.
    /// </remarks>
    private const int FirstPadNote = 36;

    /// <summary>
    /// Turns the table of pad mappings an older settings file holds into links, once.
    /// </summary>
    /// <remarks>
    /// The pads are pointed at by the same gesture as everything else now, so what was a list of
    /// its own beside the links is a list of links. Read once and then emptied, which is the flag
    /// that it has been done: an empty table is a table that has been carried over, and a fresh
    /// installation has an empty one already.
    ///
    /// Nothing is thrown away and nothing is invented. Every row becomes a link naming no
    /// controller, which reads as any of them, because a pad mapping never named one: the router
    /// matched on the kind, the channel and the number alone and left it to the job ticked in
    /// SETTINGS to say which port was allowed to fire pads. So a pad box that worked yesterday
    /// works this morning, including the rows nobody ever learned, which are the notes 36 upwards
    /// that made a pad box work out of the box.
    ///
    /// A row for a pad the matrix no longer has is carried over with the rest. A link naming pad
    /// twelve on a bank of nine fires nothing and is not forgotten either, so growing the matrix
    /// back brings it with it, which is the rule a link already keeps about a controller that is
    /// not plugged in.
    ///
    /// Nothing is seeded for a fresh installation, which is a deliberate change from the table
    /// this replaces. That table was filled in with notes 36 upwards on channel 1 for every pad
    /// whether or not anybody had asked for it, and <see cref="DefaultLayout"/> has said the
    /// opposite since it was written: a pad nobody has pointed at should do nothing rather than
    /// something surprising. The seeded rows mostly did nothing anyway, since the pad boxes here
    /// send on channel 10.
    /// </remarks>
    /// <param name="midi">The settings' MIDI half, whose two lists this moves between.</param>
    private static void PadsBecomeLinks(MidiConfig midi)
    {
        if (midi.Pads.Count == 0) return;

        foreach (var pad in midi.Pads)
        {
            if (pad.PadIndex < 0) continue;

            var link = PadLinks.On(pad.PadIndex);

            link.Sends = pad.Type == MidiMessageType.Note
                ? MidiMessageType.Note
                : MidiMessageType.ControlChange;

            link.Channel = pad.Channel is >= 1 and <= 16 ? pad.Channel : 1;
            link.Cc = pad.Value is >= 0 and <= 127 ? pad.Value : 0;

            if (midi.Controls.Any(one => one.Kind == ControlKind.Pad && one.Pad == pad.PadIndex))
                continue;

            midi.Controls.Add(link);
        }

        midi.Pads.Clear();
    }


    /// <summary>How a file is written whole, so a settings save cannot leave half of one.</summary>
    private readonly ISafeFile _files;

    /// <inheritdoc/>
    public string ConfigPath { get; }

    /// <summary>
    /// Settles where the file is and makes sure the folder around it exists.
    /// </summary>
    /// <param name="appName">
    /// The folder under the user's application data. Taken as an argument only so a test can
    /// point the whole thing at somewhere temporary; nothing in the application passes it.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="files">How a file is written whole, defaulted to the real one.</param>
    public ConfigStore(string appName = AppFolder.AppName, IAppFolder? folder = null, ISafeFile? files = null)
    {
        _files = files ?? new SafeFile();

        var dir = (folder ?? new AppFolder()).Path(appName);
        Directory.CreateDirectory(dir);

        ConfigPath = Path.Combine(dir, "config.json");
    }

    /// <inheritdoc/>
    public AppConfig LoadOrCreateDefault()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                Brought(cfg);
                Normalize(cfg);
                return cfg;
            }
        }
        catch
        {
        }

        var fresh = new AppConfig();
        Normalize(fresh);
        Save(fresh);
        return fresh;
    }

    /// <inheritdoc/>
    public void Save(AppConfig cfg)
    {
        cfg.Version = AppConfig.CurrentVersion;

        Normalize(cfg);
        var json = JsonSerializer.Serialize(cfg, JsonOptions);
        _files.Write(ConfigPath, json);
    }

    /// <summary>
    /// Moves a setting whose default was wrong rather than merely different, once.
    /// </summary>
    /// <remarks>
    /// Not the same job as <see cref="Normalize"/>, which repairs a file every time it is read
    /// and must therefore be something that can be run twice. This runs on the way in only, and
    /// it is allowed to change a value somebody could have chosen, which is exactly why it has
    /// to know whether it has run before: <see cref="AppConfig.Version"/> is that record.
    ///
    /// Nothing to move yet, and the empty body is the point rather than an oversight: the
    /// machinery is here because a default that turns out to be wrong cannot otherwise be
    /// corrected for anybody who has already run the program once, and finding that out is
    /// exactly what happened to the mixing cushion. It was going to be changed from none to
    /// twenty milliseconds until the real output was measured and the cushion turned out to buy
    /// nothing; see <see cref="AppConfig.RenderAheadMs"/>.
    /// </remarks>
    private static void Brought(AppConfig cfg)
    {
        if (cfg.Version >= AppConfig.CurrentVersion) return;

        cfg.Version = AppConfig.CurrentVersion;
    }

    /// <summary>
    /// Puts the settings into the shape everything above them assumes, in place.
    /// </summary>
    /// <remarks>
    /// Run on the way in and on the way out, so a file edited by hand, written by an older
    /// version, or left half written by a crash is repaired once rather than being guarded
    /// against everywhere it is read. In place rather than on a copy because the caller is
    /// holding the settings the application is running on.
    ///
    /// The matrix is clamped to the application's ceiling and not to the setting's: a file that
    /// says thirty-two pads keeps them even where the extended switch has since been turned off,
    /// since dropping half somebody's pads on the way in is not something a settings file should
    /// be able to do quietly. What the switch governs is what SETTINGS will let you ask for next.
    ///
    /// The MIDI mappings are per pad and global rather than per profile, so switching layouts
    /// does not move which key fires what. They are grown, trimmed and renumbered to match the
    /// matrix here, which is the only place that count is enforced.
    ///
    /// Two migrations live here and stay: pads written before profiles existed are moved into a
    /// "default" profile, and <see cref="Midi.Interfaces.IMidiPortBindings.Normalize"/> brings a file that
    /// named one MIDI device across to the roles. Both are cheap and both have to keep working
    /// for as long as anybody has an old file, which is for ever.
    /// </remarks>
    private static void Normalize(AppConfig cfg)
    {
        cfg.SelectedProfile = string.IsNullOrWhiteSpace(cfg.SelectedProfile) ? DefaultProfile : cfg.SelectedProfile.Trim();
        cfg.SelectedTheme = string.IsNullOrWhiteSpace(cfg.SelectedTheme) ? "Dark" : cfg.SelectedTheme.Trim();

        cfg.Rows = Math.Clamp(cfg.Rows, 1, PadMatrix.Most);
        cfg.Columns = Math.Clamp(cfg.Columns, 1, PadMatrix.Most);

        if (cfg.Rows * cfg.Columns < PadMatrix.Least)
        {
            cfg.Rows = 2;
            cfg.Columns = 2;
        }

        if (cfg.Rows * cfg.Columns > PadMatrix.Most)
        {
            cfg.Rows = 4;
            cfg.Columns = 4;
        }

        int padCount = cfg.Rows * cfg.Columns;

        cfg.Profiles ??= new List<ConfigProfile>();
        cfg.Pads ??= new List<PadConfig>();

        cfg.Midi ??= new MidiConfig();
        cfg.Midi.Pads ??= new List<MidiMapping>();

        new MidiPortBindings().Normalize(cfg.Midi);

        cfg.Midi.Controls ??= new List<ControlMapping>();

        PadsBecomeLinks(cfg.Midi);

        if (cfg.Profiles.Count == 0 && cfg.Pads.Count > 0)
        {
            cfg.Profiles.Add(new ConfigProfile
            {
                Name = DefaultProfile,
                Pads = cfg.Pads.Select(ClonePad).ToList()
            });
        }

        if (!cfg.Profiles.Any(p => string.Equals(p.Name, DefaultProfile, StringComparison.OrdinalIgnoreCase)))
        {
            cfg.Profiles.Add(new ConfigProfile
            {
                Name = DefaultProfile,
                Pads = new List<PadConfig>()
            });
        }

        if (!cfg.Profiles.Any(p => string.Equals(p.Name, cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase)))
            cfg.SelectedProfile = DefaultProfile;

        foreach (var profile in cfg.Profiles)
        {
            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "unnamed" : profile.Name.Trim();
            profile.Pads ??= new List<PadConfig>();

            while (profile.Pads.Count < padCount)
                profile.Pads.Add(new PadConfig { Name = $"Pad {profile.Pads.Count + 1}" });

            while (profile.Pads.Count > padCount)
                profile.Pads.RemoveAt(profile.Pads.Count - 1);

            for (int i = 0; i < profile.Pads.Count; i++)
            {
                var pad = profile.Pads[i] ??= new PadConfig();
                pad.Name = string.IsNullOrWhiteSpace(pad.Name) ? $"Pad {i + 1}" : pad.Name;
                pad.Volume = Math.Clamp(pad.Volume, 0.0, 1.0);
                pad.Source ??= "";
            }
        }

        var sel = GetSelectedProfile(cfg);
        if (sel != null)
            cfg.Pads = sel.Pads.Select(ClonePad).ToList();
        else
            cfg.Pads = CreateDefaultPads(padCount);
    }

    /// <summary>The profile the settings name, or null where it has gone missing.</summary>
    /// <remarks>
    /// Matched without regard to case, because the name is typed by a person and "Default" and
    /// "default" are the same layout as far as anybody using it is concerned.
    /// </remarks>
    private static ConfigProfile? GetSelectedProfile(AppConfig cfg)
        => cfg.Profiles.FirstOrDefault(p => string.Equals(p.Name, cfg.SelectedProfile, StringComparison.OrdinalIgnoreCase));

    /// <summary>A run of empty pads, named from one up so they read as they are counted.</summary>
    private static List<PadConfig> CreateDefaultPads(int padCount)
    {
        var pads = new List<PadConfig>(padCount);
        for (int i = 0; i < padCount; i++)
            pads.Add(new PadConfig { Name = $"Pad {i + 1}" });
        return pads;
    }

    /// <summary>
    /// A pad copied into the flat list, carrying only what that list has ever held.
    /// </summary>
    /// <remarks>
    /// Not everything: the level, the fades, the colour and the effect chain stay in the profile
    /// and are not copied out. The flat list is the old shape kept for migration and for
    /// anything still reading it, so it holds what the old shape held and nothing added since.
    /// A copy rather than the pad itself, or editing a pad would reach into the profile it came
    /// from through the back door.
    /// </remarks>
    private static PadConfig ClonePad(PadConfig p) => new()
    {
        Name = p.Name,
        Kind = p.Kind,
        Source = p.Source,
        Volume = p.Volume
    };
}
