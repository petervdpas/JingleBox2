using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// The MIDI block in front of a track's chain: the port and channel the track listens to, and
/// the port and channel it sends to.
/// </summary>
/// <remarks>
/// A view over the track's strip, written straight through, so undo, saving and the router all
/// read the one place. Every pick is announced before it is made, which is what gives it an undo
/// step, and after, which is what marks the song changed and opens or closes the ports.
///
/// The ports offered are the ones SETTINGS knows about, plus whatever the song already names:
/// a port that is not plugged in today still shows as what this track means rather than going
/// blank and looking forgotten.
/// </remarks>
public sealed class TrackMidiViewModel : ObservableObject
{
    /// <summary>The word for listening on every open port, which is stored as no name.</summary>
    public const string AnyPort = "Any port";

    /// <summary>The word for sending nowhere, which is stored as no name.</summary>
    public const string NoPort = "No port";

    /// <summary>What an undo step for a pick here is called.</summary>
    public const string Edit = "a track's MIDI";

    private readonly TrackMix _mix;

    private readonly Action<string> _changing;

    private readonly Action _changed;

    /// <param name="mix">The track's strip.</param>
    /// <param name="inputs">The input ports there are.</param>
    /// <param name="outputs">The output ports there are.</param>
    /// <param name="changing">Told what is about to change, before it does.</param>
    /// <param name="changed">Told once it has.</param>
    /// <param name="track">Which track this strip is, so it is not offered to itself.</param>
    /// <param name="tracks">How many tracks the song has, for the list of them.</param>
    public TrackMidiViewModel(TrackMix mix, IEnumerable<string> inputs, IEnumerable<string> outputs,
                              Action<string> changing, Action changed, int track = 0, int tracks = 0)
    {
        _mix = mix;
        _changing = changing;
        _changed = changed;
        _track = track;

        InPorts = Offered(AnyPort, inputs, mix.MidiIn.Port);
        OutPorts = Offered(NoPort, outputs, mix.MidiOut.Port);

        var targets = new List<string> { NoNotes, ToInserts };

        for (int one = 0; one < tracks; one++)
            if (one != track) targets.Add(TrackWord + (one + 1).ToString(CultureInfo.InvariantCulture));

        PluginTargets = targets;
    }

    /// <summary>Which track this strip is.</summary>
    private readonly int _track;

    /// <summary>The word for a plugin keeping its own notes to itself.</summary>
    public const string NoNotes = "Nowhere";

    /// <summary>The word for giving them to the effects on this same track.</summary>
    public const string ToInserts = "Effects here";

    /// <summary>What a track is called in that list, before its number.</summary>
    public const string TrackWord = "Track ";

    /// <summary>Where a plugin's own notes may be sent.</summary>
    public IReadOnlyList<string> PluginTargets { get; }

    /// <summary>
    /// Where the notes this track's plugin plays of its own accord go.
    /// </summary>
    /// <remarks>
    /// A drum machine plugin running its own pattern plays notes as well as sound. This says
    /// what becomes of them inside the song; the track's MIDI out carries them either way.
    /// </remarks>
    public string PluginNotesTo
    {
        get => _mix.PluginNotesTo switch
        {
            TrackMix.PluginNotesToInserts => ToInserts,
            >= 0 => TrackWord + (_mix.PluginNotesTo + 1).ToString(CultureInfo.InvariantCulture),
            _ => NoNotes
        };

        set
        {
            int wanted = WhereNotesGo(value);

            if (wanted == _mix.PluginNotesTo) return;

            _changing(Edit);
            _mix.PluginNotesTo = wanted;
            _changed();

            OnPropertyChanged();
        }
    }

    /// <summary>What one of the words in the list means as a stored value.</summary>
    /// <param name="said">The word picked.</param>
    private int WhereNotesGo(string? said)
    {
        if (said == ToInserts) return TrackMix.PluginNotesToInserts;

        if (said != null && said.StartsWith(TrackWord, StringComparison.Ordinal)
            && int.TryParse(said.AsSpan(TrackWord.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int one)
            && one >= 1 && one - 1 != _track)
            return one - 1;

        return TrackMix.NoPluginNotes;
    }

    /// <summary>Off, then 1 to 16, so a channel's place in the list is its number.</summary>
    public IReadOnlyList<string> Channels { get; } =
        new[] { "Off" }.Concat(Enumerable.Range(TrackMidiRoute.FirstChannel, TrackMidiRoute.LastChannel)
            .Select(one => one.ToString(CultureInfo.InvariantCulture))).ToArray();

    /// <summary><see cref="AnyPort"/> and then every input.</summary>
    public IReadOnlyList<string> InPorts { get; }

    /// <summary><see cref="NoPort"/> and then every output.</summary>
    public IReadOnlyList<string> OutPorts { get; }

    /// <summary>The port listened to, or <see cref="AnyPort"/>.</summary>
    public string InPort
    {
        get => _mix.MidiIn.HasPort ? _mix.MidiIn.Port : AnyPort;
        set => Pick(Stored(value, AnyPort), port => _mix.MidiIn with { Port = port }, route => _mix.MidiIn = route,
            _mix.MidiIn.Port, nameof(InPort));
    }

    /// <summary>The channel listened to, nought for off.</summary>
    public int InChannel
    {
        get => _mix.MidiIn.IsOn ? _mix.MidiIn.Channel : 0;
        set => Pick(value, channel => _mix.MidiIn with { Channel = channel }, route => _mix.MidiIn = route,
            InChannel, nameof(InChannel));
    }

    /// <summary>The port sent to, or <see cref="NoPort"/>.</summary>
    public string OutPort
    {
        get => _mix.MidiOut.HasPort ? _mix.MidiOut.Port : NoPort;
        set => Pick(Stored(value, NoPort), port => _mix.MidiOut with { Port = port }, route => _mix.MidiOut = route,
            _mix.MidiOut.Port, nameof(OutPort));
    }

    /// <summary>The channel sent on, nought for off.</summary>
    public int OutChannel
    {
        get => _mix.MidiOut.IsOn ? _mix.MidiOut.Channel : 0;
        set => Pick(value, channel => _mix.MidiOut with { Channel = channel }, route => _mix.MidiOut = route,
            OutChannel, nameof(OutChannel));
    }

    /// <summary>A port as it is stored: the word becomes no name, and nothing at all is refused as null.</summary>
    private static string? Stored(string? picked, string word)
    {
        if (string.IsNullOrWhiteSpace(picked)) return null;

        return picked == word ? "" : picked.Trim();
    }

    /// <summary>Writes a port pick through, unless it is refused or already there.</summary>
    private void Pick(string? port, Func<string, TrackMidiRoute> made, Action<TrackMidiRoute> store,
                      string was, string property)
    {
        if (port is null || string.Equals(port, was.Trim(), StringComparison.Ordinal)) return;

        Write(made(port), store, property);
    }

    /// <summary>Writes a channel pick through, unless it is refused or already there.</summary>
    private void Pick(int channel, Func<int, TrackMidiRoute> made, Action<TrackMidiRoute> store,
                      int was, string property)
    {
        if (channel < 0 || channel > TrackMidiRoute.LastChannel || channel == was) return;

        Write(made(channel), store, property);
    }

    /// <summary>Announces, stores and announces again.</summary>
    private void Write(TrackMidiRoute route, Action<TrackMidiRoute> store, string property)
    {
        _changing(Edit);
        store(route);
        _changed();

        OnPropertyChanged(property);
    }

    /// <summary>The word, every port there is, and the stored one where it is not among them.</summary>
    private static IReadOnlyList<string> Offered(string word, IEnumerable<string> ports, string stored)
    {
        var list = new List<string> { word };

        foreach (string one in ports)
            if (!string.IsNullOrWhiteSpace(one) && !list.Contains(one, StringComparer.OrdinalIgnoreCase))
                list.Add(one);

        if (!string.IsNullOrWhiteSpace(stored) && !list.Contains(stored, StringComparer.OrdinalIgnoreCase))
            list.Add(stored);

        return list;
    }
}
