using System;
using System.Collections.Generic;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class TrackMidiRoutes : ITrackMidiRoutes
{
    /// <inheritdoc/>
    public IReadOnlyList<int> TracksFor(IReadOnlyList<TrackMix>? mix, string? device, int channel)
    {
        if (mix is null || device is null) return Array.Empty<int>();

        List<int>? tracks = null;

        for (int track = 0; track < mix.Count; track++)
        {
            var route = mix[track]?.MidiIn;

            if (route is null || !route.IsOn || route.Channel != channel) continue;
            if (route.HasPort && !Same(route.Port, device)) continue;

            (tracks ??= new List<int>()).Add(track);
        }

        return tracks ?? (IReadOnlyList<int>)Array.Empty<int>();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> InputPorts(IReadOnlyList<TrackMix>? mix)
    {
        var ports = new List<string>();
        if (mix is null) return ports;

        foreach (var strip in mix)
        {
            var route = strip?.MidiIn;

            if (route is null || !route.IsOn || !route.HasPort) continue;

            string port = route.Port.Trim();

            if (!ports.Exists(one => Same(one, port))) ports.Add(port);
        }

        return ports;
    }

    /// <inheritdoc/>
    public TrackMidiRoute? OutFor(IReadOnlyList<TrackMix>? mix, int track)
    {
        if (mix is null || track < 0 || track >= mix.Count) return null;

        var route = mix[track]?.MidiOut;

        return route is { IsOn: true, HasPort: true } ? route : null;
    }

    /// <summary>Two port names that mean one port.</summary>
    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
