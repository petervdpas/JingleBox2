using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Controllers.Interfaces;

namespace JingleBox2.Controllers;

/// <inheritdoc/>
public sealed class ControllerProfiles : IControllerProfiles
{
    /// <summary>Where a controller's own files live, and how one is matched to a port.</summary>
    private readonly IControllerFolder _folder = new ControllerFolder();

    /// <summary>How a profile is read: forgivingly, since these are files people write by hand.</summary>
    /// <remarks>
    /// Comments and a trailing comma are allowed and the casing of a field is not held against
    /// anybody, because the alternative is a file that is refused for a reason nobody can see
    /// and a controller that quietly loses its names.
    /// </remarks>
    private readonly JsonSerializerOptions Reading = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Guards everything below, which is read from the MIDI thread and written from others.</summary>
    private readonly object Lock = new();

    /// <summary>Every profile this installation has, in the order the files were read.</summary>
    private readonly List<ControllerProfile> Held = new();

    /// <summary>Which profile answers for a port, worked out once and kept.</summary>
    private readonly Dictionary<string, ControllerProfile?> Decided =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which program each device is believed to be in, from what it has sent.</summary>
    private readonly Dictionary<string, string> Running =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the folder has been read at all, so the first question reads it.</summary>
    private bool _read;

    /// <inheritdoc/>
    public void Reload()
    {
        var found = new List<ControllerProfile>();

        try
        {
            _folder.FirstRun();

            if (Directory.Exists(_folder.Installed))
                foreach (string file in Directory
                             .GetFiles(_folder.Installed, "*.json")
                             .OrderBy(f => f, StringComparer.Ordinal))
                    if (Take(file) is { } one) found.Add(one);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "profiles: cannot read '" + _folder.Installed + "': " + bad.Message);
        }

        lock (Lock)
        {
            Held.Clear();
            Held.AddRange(found);
            Decided.Clear();
            Running.Clear();
            Implied.Clear();
            Decided2.Clear();
            _read = true;
        }

        Log.Write(LogArea.Midi, () => "profiles: " + found.Count + " read from '" + _folder.Installed + "'");
    }

    /// <summary>Reads one file, or says in the log why it did not.</summary>
    /// <remarks>
    /// A profile that will not read costs its device its names and nothing else, which is why
    /// this is a line in the log rather than anything louder. A file with no name is skipped for
    /// the same reason: a device called nothing is worse in every list than a device called by
    /// its port.
    /// </remarks>
    private ControllerProfile? Take(string path)
    {
        try
        {
            var one = JsonSerializer.Deserialize<ControllerProfile>(File.ReadAllText(path), Reading);

            if (one is null || one.Name.Length == 0)
            {
                Log.Write(LogArea.Midi, () => "profiles: '" + Path.GetFileName(path) + "' has no name, so it is skipped");
                return null;
            }

            Log.Write(LogArea.Midi, () =>
                "profile: " + one.Name + ", " + one.Programs.Count + " programs, for ports like "
                + string.Join(", ", one.Matches));

            return one;
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "profiles: '" + Path.GetFileName(path) + "' will not read: " + bad.Message);
            return null;
        }
    }

    /// <summary>Reads the folder if nobody has yet, so nothing has to be started in any order.</summary>
    private void Ready()
    {
        lock (Lock) if (_read) return;

        Reload();
    }

    /// <inheritdoc/>
    public ControllerProfile? For(string? device)
    {
        if (string.IsNullOrWhiteSpace(device)) return null;

        Ready();

        lock (Lock)
        {
            if (Decided.TryGetValue(device, out var known)) return known;

            var found = Held.FirstOrDefault(one => one.Matches.Any(like => _folder.Like(like, device)));
            Decided[device] = found;

            if (found is not null)
                Log.Write(LogArea.Midi, () => "profiles: '" + device + "' is a " + found.Name);

            return found;
        }
    }

    /// <inheritdoc/>
    public string Called(string? device) =>
        For(device) is { } profile ? profile.Name : device ?? "";

    /// <inheritdoc/>
    public bool Knows(string? device) => For(device) is not null;

    /// <inheritdoc/>
    public void Saw(string? device, int channel, int cc)
    {
        if (string.IsNullOrWhiteSpace(device)) return;
        if (For(device) is not { } profile || profile.Programs.Count < 2) return;

        string? only = Implies(profile, device, channel, cc);

        if (only is null) return;

        lock (Lock)
        {
            if (Running.TryGetValue(device, out string? was) && string.Equals(was, only, StringComparison.Ordinal))
                return;

            Running[device] = only;

            Log.Write(LogArea.Midi, () =>
                "profiles: '" + device + "' is in its " + only + " program"
                + (was is null ? "" : ", which it was not a moment ago (" + was + ")")
                + ", worked out from CC " + cc);
        }
    }

    /// <summary>What a controller number on a device implies about its program, worked out once.</summary>
    /// <remarks>
    /// The answer never changes for a given number on a given device, so it is worked out on the
    /// first message and looked up on the three hundred a second after it. The scan it replaces
    /// walked every control of every program with a lambda per control, which for a MiniLab is
    /// fifty-eight comparisons and a couple of hundred bytes of rubbish per message, to arrive
    /// at the same answer it arrived at last time.
    /// </remarks>
    private readonly Dictionary<(string Device, int Channel, int Cc), string?> Implied = new();

    /// <summary>
    /// Which program a number belongs to, or nothing when it belongs to none or to several.
    /// </summary>
    /// <remarks>
    /// A number found in two programs at once says nothing about which of them is running, so it
    /// is thrown away rather than resolved by whichever was walked first.
    /// </remarks>
    private string? Implies(ControllerProfile profile, string device, int channel, int cc)
    {
        var asked = (device, channel, cc);

        lock (Lock)
            if (Implied.TryGetValue(asked, out string? known)) return known;

        string? only = null;

        foreach (var program in profile.Programs)
        {
            bool has = false;

            foreach (var one in program.Controls)
                if (Answers(one, channel, cc)) { has = true; break; }

            if (!has) continue;

            if (only is not null) { only = null; break; }

            only = program.Name;
        }

        lock (Lock) Implied[asked] = only;

        return only;
    }

    /// <inheritdoc/>
    public string ProgramOn(string? device)
    {
        if (string.IsNullOrWhiteSpace(device)) return "";

        lock (Lock)
            if (Running.TryGetValue(device, out string? one)) return one;

        return For(device) is { Programs.Count: 1 } profile ? profile.Programs[0].Name : "";
    }

    /// <inheritdoc/>
    public string Named(string? device, int channel, int cc)
    {
        if (For(device) is not { } profile) return "";

        string program = ProgramOn(device);

        if (program.Length > 0
            && profile.Programs.FirstOrDefault(one => string.Equals(one.Name, program, StringComparison.Ordinal))
                is { } current
            && current.Controls.FirstOrDefault(one => Answers(one, channel, cc)) is { } named)
            return named.Name;

        if (profile.Controls.FirstOrDefault(one => Answers(one, channel, cc)) is { } common)
            return common.Name;

        var across = profile.Programs
            .SelectMany(one => one.Controls)
            .Where(one => Answers(one, channel, cc))
            .Select(one => one.Name)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        return across.Count == 1 ? across[0] : "";
    }

    /// <inheritdoc/>
    public string PortIs(string? device)
    {
        if (For(device) is not { } profile) return "";

        var port = profile.Ports.FirstOrDefault(one => _folder.Like(one.Match, device!));
        if (port is null) return profile.Name;

        return port.Note.Length > 0 ? profile.Name + "  ·  " + port.Note : profile.Name;
    }

    /// <inheritdoc/>
    public string ScreenOn(string? device)
    {
        if (For(device) is not { } profile) return "";
        if (profile.Screen is not { } screen) return "";
        if (screen.Protocol.Length == 0) return "";

        return _folder.Like(screen.Port, device!) ? screen.Protocol : "";
    }

    /// <inheritdoc/>
    public bool ScreenWakes(string? device) =>
        ScreenOn(device).Length > 0 && For(device) is { Screen.Wake: true };

    /// <inheritdoc/>
    /// <remarks>
    /// True for a device nobody has written a file for, which is the promise the protocol is
    /// read on at all, and true for one whose file names Mackie on this port. False only when
    /// there is a file and it says something other than Mackie about this device.
    /// </remarks>
    public bool SurfaceOn(string? device)
    {
        if (For(device) is not { } profile) return true;
        if (profile.Surface is not { } surface) return false;
        if (!string.Equals(surface.Protocol, Mackie, StringComparison.OrdinalIgnoreCase)) return false;

        return surface.Port.Length == 0 || _folder.Like(surface.Port, device!);
    }

    /// <summary>The one surface protocol read here.</summary>
    private const string Mackie = "mackie";

    /// <inheritdoc/>
    /// <remarks>
    /// Spelled out rather than parsed, so the words a file may use are visible here and a word
    /// nobody implements reads as no transport key rather than as an error.
    /// </remarks>
    public TransportKey? TransportOn(string? device, int channel, int cc)
    {
        if (Control(device, channel, cc) is not { } control) return null;

        return control.Transport.ToLowerInvariant() switch
        {
            "play" => TransportKey.Play,
            "stop" => TransportKey.Stop,
            "record" => TransportKey.Record,
            "pause" => TransportKey.Pause,
            "cycle" or "loop" => TransportKey.Loop,
            _ => null
        };
    }

    /// <inheritdoc/>
    public bool Momentary(string? device, int channel, int cc) =>
        Control(device, channel, cc) is { } control
        && string.Equals(control.Press, "momentary", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool PortTakes(string? device, MidiPortRole role)
    {
        if (For(device) is not { } profile) return true;

        var port = profile.Ports.FirstOrDefault(one => _folder.Like(one.Match, device!));
        if (port is null) return true;

        return port.Role switch
        {
            "controls" => true,
            "transport" => role == MidiPortRole.Transport,
            _ => false
        };
    }

    /// <inheritdoc/>
    public ControlPickup? Pickup(string? device, int channel, int cc)
    {
        if (device is null || For(device) is not { } profile) return null;

        string program = ProgramOn(device);
        var asked = (device, channel, cc, program);

        lock (Lock)
            if (Decided2.TryGetValue(asked, out var known)) return known;

        var answer = Work(profile, device, channel, cc, program);

        lock (Lock) Decided2[asked] = answer;

        return answer;
    }

    /// <summary>What was decided about a control, per program, so it is decided once.</summary>
    private readonly Dictionary<(string Device, int Channel, int Cc, string Program), ControlPickup?> Decided2 = new();

    /// <summary>
    /// What the file says a control should be read as, before anything is remembered about it.
    /// </summary>
    /// <remarks>
    /// A knob is a fader that happens to be round: a pot with ends, reporting where it is.
    /// Measured on an MPD218, whose six are sold as 360 degree potentiometers and are nothing of
    /// the kind: one turned steadily walked 35 to 127 in two seconds and then sat at 127 for
    /// another seven while it was still being turned. So 360 degree describes the absence of a
    /// detent rather than the behaviour of the value.
    ///
    /// Nothing is claimed for a knob in a program that counts notches, because a knob's type is
    /// settable in Akai's own editor and one set that way, read as a position, would do nothing
    /// whatever.
    /// </remarks>
    private ControlPickup? Work(ControllerProfile profile, string device, int channel, int cc, string program)
    {
        var control = Control(device, channel, cc);
        if (control is null || control.Kind.Length == 0) return null;

        string sends = "";

        foreach (var one in profile.Programs)
            if (string.Equals(one.Name, program, StringComparison.Ordinal)) { sends = one.Sends; break; }

        return control.Kind switch
        {
            "encoder" when sends == "absolute" => ControlPickup.Endless,
            "fader" or "strip" => ControlPickup.Takeover,
            "knob" when sends != "relative" => ControlPickup.Takeover,
            "button" or "pad" => ControlPickup.Jump,
            _ => null
        };
    }

    /// <inheritdoc/>
    public ControllerControl? Control(string? device, int channel, int cc)
    {
        if (For(device) is not { } profile) return null;

        string program = ProgramOn(device);

        if (program.Length > 0
            && profile.Programs.FirstOrDefault(one => string.Equals(one.Name, program, StringComparison.Ordinal))
                is { } current
            && current.Controls.FirstOrDefault(one => Answers(one, channel, cc)) is { } named)
            return named;

        return profile.Controls.FirstOrDefault(one => Answers(one, channel, cc));
    }

    /// <summary>Whether that control is the one that sent this.</summary>
    /// <remarks>
    /// A channel of nought or less means any, which is what almost every control in every file
    /// is: a device that sends its knobs on one channel says so once, and the few controls that
    /// are pinned to a channel of their own are the exceptions worth writing down.
    /// </remarks>
    private bool Answers(ControllerControl one, int channel, int cc) =>
        one.Cc == cc && (one.Channel <= 0 || one.Channel == channel);
}
