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
    public TrackMidiViewModel(TrackMix mix, IEnumerable<string> inputs, IEnumerable<string> outputs,
                              Action<string> changing, Action changed)
    {
        _mix = mix;
        _changing = changing;
        _changed = changed;

        InPorts = Offered(AnyPort, inputs, mix.MidiIn.Port);
        OutPorts = Offered(NoPort, outputs, mix.MidiOut.Port);
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
