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
    /// <summary>What a device's link is made of, so a file and a face agree. Holds nothing.</summary>
    private static readonly Interfaces.ISoundDeviceLinks Devices = new SoundDeviceLinks();

    /// <summary>
    /// A device: a box on the rack with a face, which is a soundmachine or an effect.
    /// </summary>
    /// <remarks>
    /// One word for both, because to a link they are one thing: an id its manifest carries and
    /// the key of the control the knob was pointed at. Which of the two it is decides where the
    /// link is looked for and nothing else about it, so a file that said which would be saying
    /// something the reader has to check anyway.
    /// </remarks>
    public const string SoundDevice = "sounddevice";


    /// <summary>
    /// A plugin on a track's chain, which no link may point at.
    /// </summary>
    /// <remarks>
    /// Refused, and the word is kept only so that a file carrying one is counted and left out
    /// rather than failing the whole file. A plugin is somebody else's program and brings its own
    /// MIDI learn, so a link made here would be a second mapping beside the one the plugin
    /// already keeps with nothing able to make the two agree.
    ///
    /// It is not the word for one of our effects and never was: an effect of ours is a box this
    /// installation registered, with an id and a face, which is a <see cref="SoundDevice"/>. Nothing
    /// has ever written this word about one, so a file that says it means a plugin.
    /// </remarks>
    public const string Plugin = "plugin";

    /// <summary>One strip of the mixer, the master included.</summary>
    public const string Mixer = "mixer";

    /// <summary>The transport keys, which belong to no track and no song.</summary>
    public const string Transport = "transport";

    /// <summary>The pads, which belong to no track either.</summary>
    /// <remarks>
    /// One target for all of them, the way the mixer is one for all its strips: what somebody
    /// keeps or hands on is the whole pad layout, and cut by pad it would be sixteen cards
    /// saying the same words with a number changed. Which pad is on each line instead.
    /// </remarks>
    public const string Pads = "pads";

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
    /// <remarks>
    /// The mixer is one target and not one per strip, which is why it is the one kind whose id is
    /// left out of its key. A knob is pointed at the mixer: the desk in front of you has a fader
    /// for every strip and what you want to keep, hand on or lay down again is the whole layout,
    /// not four of them a track. Cut by strip it was four cards saying the same three words with
    /// a number changed, and four files nobody could use.
    ///
    /// The strip is not lost by this. It is still what an individual link names, and a template
    /// writes it on each of its lines: see <see cref="ControlTemplateControl.Strip"/>.
    /// </remarks>
    public string KeyOf(ControlMapping one) =>
        Whole(KindOf(one)) ? KindOf(one) + ":" : KindOf(one) + ":" + IdOf(one);

    /// <inheritdoc/>
    public bool Whole(string kind) =>
        string.Equals(kind, Mixer, StringComparison.Ordinal)
        || string.Equals(kind, Pads, StringComparison.Ordinal);

    /// <inheritdoc/>
    public string KindOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.SoundDevice or ControlKind.Action => SoundDevice,
        ControlKind.Plugin => Plugin,
        ControlKind.Mix => Mixer,
        ControlKind.Pad => Pads,
        _ => Transport
    };

    /// <inheritdoc/>
    public string IdOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.SoundDevice or ControlKind.Action => one.Machine,
        ControlKind.Plugin => one.Plugin,
        ControlKind.Mix => one.Track == Tracker.TrackerPlayer.MasterStrip
            ? Master
            : (one.Track + 1).ToString(CultureInfo.InvariantCulture),
        _ => ""
    };

    /// <inheritdoc/>
    public string ParameterOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.SoundDevice or ControlKind.Action => one.Key,
        ControlKind.Plugin => one.Parameter.ToString(CultureInfo.InvariantCulture),
        ControlKind.Mix => Strip.FirstOrDefault(each => each.What == one.Mix).Said ?? "",
        ControlKind.Pad => (one.Pad + 1).ToString(CultureInfo.InvariantCulture),
        _ => Keys.FirstOrDefault(each => each.Key == one.Transport).Said ?? ""
    };

    /// <inheritdoc/>
    public string TitleOf(IEnumerable<ControlMapping> links)
    {
        var all = links.ToList();

        if (all.Count == 0) return "";

        if (all[0].Kind == ControlKind.Mix) return "Mixer";

        if (all[0].Kind == ControlKind.Pad) return "Pads";

        if (all.Select(one => one.Owner).FirstOrDefault(one => one.Length > 0) is { Length: > 0 } named)
            return named;

        if (all.Select(Guessed).FirstOrDefault(one => one.Length > 0) is { Length: > 0 } worked)
            return worked;

        var first = all[0];

        return first.Kind switch
        {
            ControlKind.SoundDevice or ControlKind.Action =>
                first.Machine.Length > 0 ? first.Machine : "A device",
            ControlKind.Plugin => first.Plugin.Length > 0 ? first.Plugin : "An effect",
            ControlKind.Mix => "Mixer",
            ControlKind.Pad => "Pads",
            _ => "Transport"
        };
    }

    /// <inheritdoc/>
    public int RankOf(ControlMapping one) => one.Kind switch
    {
        ControlKind.SoundDevice or ControlKind.Action => 0,
        ControlKind.Plugin => 1,
        ControlKind.Mix => 2,
        ControlKind.Pad => 3,
        _ => 4
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
    /// So does a plugin, always: see <see cref="Plugin"/>. That count is the whole of what a
    /// person can be told about a file this version will not take.
    /// </remarks>
    /// <param name="kind">One of the four words.</param>
    /// <param name="id">Which one.</param>
    /// <param name="parameter">Which parameter.</param>
    private static ControlMapping? Made(string kind, string id, string parameter)
    {
        if (Same(kind, SoundDevice))
            return parameter.Length == 0 || id.Length == 0
                ? null
                : Devices.On(id, "", parameter);

        if (Same(kind, Plugin)) return null;

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

        if (Same(kind, Pads))
        {
            if (!int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out int shown)
                || shown < 1)
                return null;

            return new ControlMapping
            {
                Kind = ControlKind.Pad,
                Scope = ControlScope.Fixed,
                Pad = shown - 1
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
        if (one.Kind is not (ControlKind.SoundDevice or ControlKind.Action)) return "";
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
