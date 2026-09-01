using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class LinkTargets : ILinkTargets
{
    /// <summary>A machine on the rack, or an instrument on a track, which is one in use.</summary>
    public const string Machine = "machine";

    /// <summary>
    /// A plugin on a track's insert chain, which no link may point at.
    /// </summary>
    /// <remarks>
    /// Kept as a word because a template written before this may carry it, and one that does is
    /// read far enough to be counted and left out rather than failing the whole file.
    ///
    /// A hardware control cannot be pointed at a plugin. A plugin is somebody else's program and
    /// brings its own MIDI learn, so a link made here would be a second mapping beside the one
    /// the plugin already keeps, and nothing could make the two agree. Remote control is for
    /// machines, our own effects and the mixer.
    /// </remarks>
    public const string Effect = "effect";

    /// <summary>One strip of the mixer, the master included.</summary>
    public const string Mixer = "mixer";

    /// <summary>The transport keys, which belong to no track and no song.</summary>
    public const string Transport = "transport";

    /// <summary>What the master strip is called, since it is not a track and has no number.</summary>
    private const string Master = "master";

    /// <summary>
    /// The mixer's six, spelled out both ways.
    /// </summary>
    /// <remarks>
    /// Written out as a map of literal words rather than worked out from the enum's own names,
    /// so the file's vocabulary is a decision here and does not move when a value is renamed in
    /// code. A template on somebody's disc outlives a refactoring.
    /// </remarks>
    private static readonly (MixControl What, string Said)[] Strip =
    {
        (MixControl.Volume, "level"),
        (MixControl.Pan, "pan"),
        (MixControl.Mute, "mute"),
        (MixControl.Solo, "solo"),
        (MixControl.Duck, "duck"),
        (MixControl.Release, "duck-release")
    };

    /// <summary>The transport's five, spelled out both ways, for the same reason.</summary>
    private static readonly (TransportKey Key, string Said)[] Keys =
    {
        (TransportKey.Play, "play"),
        (TransportKey.Pause, "pause"),
        (TransportKey.Stop, "stop"),
        (TransportKey.Record, "record"),
        (TransportKey.Loop, "loop")
    };

    /// <summary>How a control is reconciled with its value, spelled out both ways.</summary>
    private static readonly (ControlPickup How, string Said)[] Pickups =
    {
        (ControlPickup.Sensed, "listening"),
        (ControlPickup.Takeover, "takeover"),
        (ControlPickup.Jump, "jump"),
        (ControlPickup.Relative, "relative"),
        (ControlPickup.Endless, "endless")
    };

    /// <summary>Which way an encoder counts, spelled out both ways.</summary>
    private static readonly (ControlTurn Way, string Said)[] Turns =
    {
        (ControlTurn.Offset, "offset"),
        (ControlTurn.Twos, "twos")
    };

    /// <inheritdoc/>
    public string KeyOf(ControlMapping one) => KindOf(one) + ":" + IdOf(one);

    /// <inheritdoc/>
    public string KindOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.Instrument or ControlKind.Action => Machine,
        ControlKind.Insert => Effect,
        ControlKind.Mix => Mixer,
        _ => Transport
    };

    /// <inheritdoc/>
    public string IdOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.Instrument or ControlKind.Action => one.Machine,
        ControlKind.Insert => one.Plugin,
        ControlKind.Mix => one.Track == Tracker.TrackerPlayer.MasterStrip
            ? Master
            : (one.Track + 1).ToString(CultureInfo.InvariantCulture),
        _ => ""
    };

    /// <inheritdoc/>
    public string ParameterOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.Instrument or ControlKind.Action => one.Key,
        ControlKind.Insert => one.Parameter.ToString(CultureInfo.InvariantCulture),
        ControlKind.Mix => Strip.FirstOrDefault(each => each.What == one.Mix).Said ?? "",
        _ => Keys.FirstOrDefault(each => each.Key == one.Transport).Said ?? ""
    };

    /// <inheritdoc/>
    public string TitleOf(IEnumerable<ControlMapping> links)
    {
        var all = links.ToList();

        if (all.Count == 0) return "";

        if (all.Select(one => one.Owner).FirstOrDefault(one => one.Length > 0) is { Length: > 0 } named)
            return named;

        if (all.Select(Guessed).FirstOrDefault(one => one.Length > 0) is { Length: > 0 } worked)
            return worked;

        var first = all[0];

        return first.Kind switch
        {
            ControlKind.Instrument or ControlKind.Action =>
                first.Machine.Length > 0 ? first.Machine : "Any machine",
            ControlKind.Insert => first.Plugin.Length > 0 ? first.Plugin : "An effect",
            ControlKind.Mix => first.Track == Tracker.TrackerPlayer.MasterStrip
                ? "Master"
                : "Track " + (first.Track + 1).ToString(CultureInfo.InvariantCulture),
            _ => "Transport"
        };
    }

    /// <inheritdoc/>
    public int RankOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.Instrument or ControlKind.Action => 0,
        ControlKind.Insert => 1,
        ControlKind.Mix => 2,
        _ => 3
    };

    /// <inheritdoc/>
    public ControlMapping? Point(string kind, string id, string parameter, string owner = "", string name = "")
    {
        var one = Made(kind ?? "", id ?? "", parameter ?? "");

        if (one is null) return null;

        one.Owner = owner ?? "";
        one.Name = (name ?? "").Length > 0
            ? name!
            : (one.Owner.Length > 0 ? one.Owner + " " + parameter : parameter ?? "");

        return one;
    }

    /// <inheritdoc/>
    public string Said(ControlPickup pickup) =>
        Pickups.FirstOrDefault(each => each.How == pickup).Said ?? "listening";

    /// <inheritdoc/>
    public ControlPickup? Pickup(string said) =>
        Pickups.Any(each => Same(each.Said, said))
            ? Pickups.First(each => Same(each.Said, said)).How
            : null;

    /// <inheritdoc/>
    public string Said(ControlTurn turn) =>
        Turns.FirstOrDefault(each => each.Way == turn).Said ?? "offset";

    /// <inheritdoc/>
    public ControlTurn? Turn(string said) =>
        Turns.Any(each => Same(each.Said, said))
            ? Turns.First(each => Same(each.Said, said)).Way
            : null;

    /// <summary>
    /// The link those three words describe, without its wording or its hardware.
    /// </summary>
    /// <remarks>
    /// Every branch refuses rather than guessing. A machine with no key, a strip that is neither
    /// the master nor a track number, and a word that is not one of the mixer's six or the
    /// transport's five all come back as nothing, and the caller counts what it could not read.
    /// So does a plugin, always: see <see cref="Effect"/>. That count is the whole of what a
    /// person can be told about a file this version will not take.
    /// </remarks>
    /// <param name="kind">One of the four words.</param>
    /// <param name="id">Which one.</param>
    /// <param name="parameter">Which parameter.</param>
    private static ControlMapping? Made(string kind, string id, string parameter)
    {
        if (Same(kind, Machine))
            return parameter.Length == 0 || id.Length == 0
                ? null
                : new ControlMapping
                {
                    Kind = ControlKind.Instrument,
                    Scope = ControlScope.Focused,
                    Machine = id,
                    Key = parameter
                };

        if (Same(kind, Effect)) return null;

        if (Same(kind, Mixer))
        {
            if (!Strip.Any(each => Same(each.Said, parameter))) return null;

            int track;

            if (Same(id, Master)) track = Tracker.TrackerPlayer.MasterStrip;
            else if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int shown) && shown >= 1)
                track = shown - 1;
            else return null;

            return new ControlMapping
            {
                Kind = ControlKind.Mix,
                Scope = ControlScope.Fixed,
                Track = track,
                Mix = Strip.First(each => Same(each.Said, parameter)).What
            };
        }

        if (!Same(kind, Transport) || !Keys.Any(each => Same(each.Said, parameter))) return null;

        return new ControlMapping
        {
            Kind = ControlKind.Transport,
            Scope = ControlScope.Fixed,
            Transport = Keys.First(each => Same(each.Said, parameter)).Key
        };
    }

    /// <summary>
    /// The owner read back out of a link that never wrote one down.
    /// </summary>
    /// <remarks>
    /// Every place a link is made writes its name as the thing and the control run together,
    /// and a machine's parameter key is exactly what follows, so "OddSkilla attack" gives up
    /// OddSkilla without anything having to be looked up. An action's key is written out in
    /// words, so both spellings are tried.
    /// </remarks>
    /// <param name="one">The link to read.</param>
    private static string Guessed(ControlMapping one)
    {
        if (one.Kind is not (ControlKind.Instrument or ControlKind.Action)) return "";
        if (one.Key.Length == 0 || one.Name.Length == 0) return "";

        string spaced = " " + one.Key.Replace('_', ' ');

        if (one.Name.EndsWith(spaced, StringComparison.OrdinalIgnoreCase))
            return one.Name[..^spaced.Length];

        string plain = " " + one.Key;

        return one.Name.EndsWith(plain, StringComparison.OrdinalIgnoreCase)
            ? one.Name[..^plain.Length]
            : "";
    }

    /// <summary>Two of the file's words, compared the way a file written by hand has to be.</summary>
    /// <param name="one">What was read.</param>
    /// <param name="other">What it is being tried against.</param>
    private static bool Same(string? one, string? other) =>
        string.Equals((one ?? "").Trim(), other ?? "", StringComparison.OrdinalIgnoreCase);
}
