using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Midi;

namespace JingleBox2.Controllers;

/// <summary>
/// What this installation knows about the controllers plugged into it.
/// </summary>
/// <remarks>
/// Static, like <see cref="Tracker.Machines.MachineRegistry"/> and for the same reason: what a
/// device is called is wanted in a list on one page, a heading on another and a log line in a
/// third, and threading a registry through five constructors to produce a display string is a
/// worse answer than one place that knows.
///
/// It answers nothing when it has nothing to say, which is the ordinary case. A device with no
/// file is not a problem to report; it is a device with no file, and everything about it works.
/// </remarks>
public static class ControllerProfiles
{
    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly object Lock = new();
    private static readonly List<ControllerProfile> Held = new();

    /// <summary>Which profile answers for a port, worked out once and kept.</summary>
    private static readonly Dictionary<string, ControllerProfile?> Decided =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Which program each device is believed to be in, from what it has sent.</summary>
    private static readonly Dictionary<string, string> Running =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool _read;

    /// <summary>Reads every profile again, from scratch.</summary>
    public static void Reload()
    {
        var found = new List<ControllerProfile>();

        try
        {
            ControllerFolder.FirstRun();

            if (Directory.Exists(ControllerFolder.Installed))
                foreach (string file in Directory
                             .GetFiles(ControllerFolder.Installed, "*.json")
                             .OrderBy(f => f, StringComparer.Ordinal))
                    if (Take(file) is { } one) found.Add(one);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "profiles: cannot read '" + ControllerFolder.Installed + "': " + bad.Message);
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

        Log.Write(LogArea.Midi, () => "profiles: " + found.Count + " read from '" + ControllerFolder.Installed + "'");
    }

    private static ControllerProfile? Take(string path)
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
            // A profile that will not read costs its device its names and nothing else, which is
            // why this is a line in the log rather than anything louder.
            Log.Write(LogArea.Midi, () => "profiles: '" + Path.GetFileName(path) + "' will not read: " + bad.Message);
            return null;
        }
    }

    private static void Ready()
    {
        lock (Lock) if (_read) return;

        Reload();
    }

    /// <summary>The profile for a port, or nothing, which is ordinary.</summary>
    public static ControllerProfile? For(string? device)
    {
        if (string.IsNullOrWhiteSpace(device)) return null;

        Ready();

        lock (Lock)
        {
            if (Decided.TryGetValue(device, out var known)) return known;

            var found = Held.FirstOrDefault(one => one.Matches.Any(like => ControllerFolder.Like(like, device)));
            Decided[device] = found;

            if (found is not null)
                Log.Write(LogArea.Midi, () => "profiles: '" + device + "' is a " + found.Name);

            return found;
        }
    }

    /// <summary>What to call a device: its own name where one is known, else the port's.</summary>
    public static string Called(string? device) =>
        For(device) is { } profile ? profile.Name : device ?? "";

    /// <summary>True when that port has a profile, so a page can say the match happened.</summary>
    public static bool Knows(string? device) => For(device) is not null;

    /// <summary>
    /// Another controller message arrived, which is a clue about which program is running.
    /// </summary>
    /// <remarks>
    /// The device will not say, and cannot be asked without speaking its manufacturer's own
    /// language. But its programs do not overlap: a MiniLab's knobs send 86 in one and 74 in
    /// another and never both, so a single number is usually enough to know which is in front of
    /// you. A number that appears in more than one program says nothing and is ignored, and one
    /// that appears in none is somebody's control this file does not describe.
    ///
    /// Self correcting by construction. Switch the device to another program and its first
    /// message moves this along with it.
    /// </remarks>
    public static void Saw(string? device, int channel, int cc)
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
    private static readonly Dictionary<(string Device, int Channel, int Cc), string?> Implied = new();

    private static string? Implies(ControllerProfile profile, string device, int channel, int cc)
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

            // In two programs at once, so it says nothing about which of them is running.
            if (only is not null) { only = null; break; }

            only = program.Name;
        }

        lock (Lock) Implied[asked] = only;

        return only;
    }

    /// <summary>Which program a device is believed to be in, or nothing while nobody knows.</summary>
    public static string ProgramOn(string? device)
    {
        if (string.IsNullOrWhiteSpace(device)) return "";

        lock (Lock) return Running.TryGetValue(device, out string? one) ? one : "";
    }

    /// <summary>
    /// What a control is called on the front of the device, or nothing when nobody knows.
    /// </summary>
    /// <remarks>
    /// Answering nothing is the common case and is not a failure. Everywhere this is asked has
    /// something perfectly good to fall back on, which is the number itself, and a list that
    /// says `CC 89 ch 1` is a list that works.
    /// </remarks>
    public static string Named(string? device, int channel, int cc)
    {
        if (For(device) is not { } profile) return "";

        string program = ProgramOn(device);

        // The program that is running, when that is known.
        if (program.Length > 0
            && profile.Programs.FirstOrDefault(one => string.Equals(one.Name, program, StringComparison.Ordinal))
                is { } current
            && current.Controls.FirstOrDefault(one => Answers(one, channel, cc)) is { } named)
            return named.Name;

        // Then anything true whatever program it is in.
        if (profile.Controls.FirstOrDefault(one => Answers(one, channel, cc)) is { } common)
            return common.Name;

        // And failing that, any program at all, but only when they agree. Before a device has
        // said anything there is no way to tell its programs apart, and a name from the wrong
        // one is worse than a number: a number is merely unhelpful, a wrong name is a lie.
        var across = profile.Programs
            .SelectMany(one => one.Controls)
            .Where(one => Answers(one, channel, cc))
            .Select(one => one.Name)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        return across.Count == 1 ? across[0] : "";
    }

    /// <summary>
    /// What a port is for, as a line to put under its name in a list.
    /// </summary>
    /// <remarks>
    /// Nothing at all for a device with no profile, and nothing for a port the profile does not
    /// mention, which is right: a blank line says the honest thing, and a guess would not.
    /// </remarks>
    public static string PortIs(string? device)
    {
        if (For(device) is not { } profile) return "";

        var port = profile.Ports.FirstOrDefault(one => ControllerFolder.Like(one.Match, device!));
        if (port is null) return profile.Name;

        return port.Note.Length > 0 ? profile.Name + "  ·  " + port.Note : profile.Name;
    }

    /// <summary>
    /// Whether a job belongs on this port, for a device that presents several.
    /// </summary>
    /// <remarks>
    /// The thing a person cannot be expected to know and should not have to. A MiniLab 3 is four
    /// ports, its notes and knobs come out one of them, and the name of that one is no more
    /// suggestive than the other three. Ticking Transport against the port called MCU/HUI is the
    /// obvious guess and it is wrong whenever the device is in a DAW program, which is a whole
    /// evening lost to a checkbox.
    ///
    /// Transport goes on both, deliberately. The two are alternatives on the hardware and the
    /// device sends one or the other depending on its program, never both, so listening to both
    /// costs nothing and removes the only decision that needed a manual.
    ///
    /// A port the profile does not mention takes everything, because nothing is known about it
    /// and a silent refusal is worse than a port that does too much.
    /// </remarks>
    public static bool PortTakes(string? device, MidiDeviceRole role)
    {
        if (For(device) is not { } profile) return true;

        var port = profile.Ports.FirstOrDefault(one => ControllerFolder.Like(one.Match, device!));
        if (port is null) return true;

        return port.Role switch
        {
            "controls" => true,
            "transport" => role == MidiDeviceRole.Transport,
            _ => false
        };
    }

    /// <summary>
    /// How a control should be read, when the file knows the hardware well enough to say.
    /// </summary>
    /// <remarks>
    /// A fact beating a guess, which is the whole of what a profile buys here.
    /// <see cref="ControlSense"/> works out what a control is from what it sends, and it is
    /// right about everything it can see. What it cannot see is the shape of the thing under
    /// the hand. An endless encoder reporting a position walks smoothly through its range and
    /// is, to three messages, indistinguishable from a fader; so it is read as a fader, saved
    /// as one, and from then on every session begins by hunting for the value with a knob that
    /// has no beginning and no end to hunt with. Which is exactly what happened to nine links
    /// in one song, five of them on encoders.
    ///
    /// So a control the file calls an encoder in a program that sends positions is read as
    /// movement between messages instead, which works whether the firmware wraps at the top or
    /// stops there: a wrap unwinds and a stop reads as no movement, and turning back moves it
    /// at once either way.
    ///
    /// Nothing is claimed for an encoder in a program that counts notches. Which of the two
    /// conventions it counts in is not in the file and getting it wrong throws the parameter
    /// across its range, so that one is left to be watched rather than assumed.
    /// </remarks>
    public static ControlPickup? Pickup(string? device, int channel, int cc)
    {
        if (device is null || For(device) is not { } profile) return null;

        // Asked on every message, so the answer is worked out once per control per program and
        // looked up after that. It cannot change without the program changing, and the program
        // is part of the question.
        string program = ProgramOn(device);
        var asked = (device, channel, cc, program);

        lock (Lock)
            if (Decided2.TryGetValue(asked, out var known)) return known;

        var answer = Work(profile, device, channel, cc, program);

        lock (Lock) Decided2[asked] = answer;

        return answer;
    }

    /// <summary>What was decided about a control, per program, so it is decided once.</summary>
    private static readonly Dictionary<(string Device, int Channel, int Cc, string Program), ControlPickup?> Decided2 = new();

    private static ControlPickup? Work(ControllerProfile profile, string device, int channel, int cc, string program)
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

            // A knob is a fader that is round: a pot with ends, reporting where it is. Measured
            // on an MPD218, whose six are sold as 360 degree and are nothing of the kind: one
            // walked 35 to 127 in two seconds and then sat at 127 for another seven while it was
            // still being turned. Nothing claimed when the program counts notches, because a
            // knob's type is settable in Akai's own editor and one set that way, read as a
            // position, would do nothing whatever.
            "knob" when sends != "relative" => ControlPickup.Takeover,
            "button" or "pad" => ControlPickup.Jump,
            _ => null
        };
    }

    /// <summary>Everything the profile says about a control, for a tip or a log line.</summary>
    public static ControllerControl? Control(string? device, int channel, int cc)
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

    private static bool Answers(ControllerControl one, int channel, int cc) =>
        one.Cc == cc && (one.Channel <= 0 || one.Channel == channel);
}
