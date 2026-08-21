using System;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Audio.Routing;

public readonly record struct PipeWirePort(string Node, string Port);

public readonly record struct PipeWireLink(PipeWirePort From, PipeWirePort To);

/// <summary>
/// Reads what the PipeWire tools print. Kept apart from running them, so the parsing can be
/// checked against real output without a sound server in the room.
/// </summary>
public static class PipeWireGraph
{
    /// <summary>
    /// A port line is "node:port". The node never contains a colon and the port often does
    /// not either, but a MIDI port can, so the first colon is the one that counts.
    /// </summary>
    public static PipeWirePort? ParsePort(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        string trimmed = line.Trim();

        // "pw-link -I" puts an id in front; drop it if it is there.
        int space = trimmed.IndexOf(' ');
        if (space > 0 && int.TryParse(trimmed[..space], out _)) trimmed = trimmed[(space + 1)..].Trim();

        int colon = trimmed.IndexOf(':');
        if (colon <= 0 || colon == trimmed.Length - 1) return null;

        return new PipeWirePort(trimmed[..colon], trimmed[(colon + 1)..]);
    }

    public static IReadOnlyList<PipeWirePort> ParsePorts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<PipeWirePort>();

        var ports = new List<PipeWirePort>();

        foreach (var line in text.Split('\n'))
        {
            if (ParsePort(line) is { } port) ports.Add(port);
        }

        return ports;
    }

    /// <summary>
    /// Reads the link listing, where a port is followed by its connections: "|-&gt;" for what it
    /// feeds and "|&lt;-" for what feeds it.
    /// </summary>
    public static IReadOnlyList<PipeWireLink> ParseLinks(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<PipeWireLink>();

        var links = new List<PipeWireLink>();
        PipeWirePort? current = null;

        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string trimmed = line.Trim();
            bool outgoing = trimmed.StartsWith("|->", StringComparison.Ordinal);
            bool incoming = trimmed.StartsWith("|<-", StringComparison.Ordinal);

            if (!outgoing && !incoming)
            {
                current = ParsePort(line);
                continue;
            }

            if (current is not { } port) continue;
            if (ParsePort(trimmed[3..]) is not { } other) continue;

            links.Add(outgoing ? new PipeWireLink(port, other) : new PipeWireLink(other, port));
        }

        return links;
    }

    /// <summary>
    /// True for the stereo audio ports worth offering. Video and MIDI ports live in the same
    /// listing, and neither is something a recorder can use.
    /// </summary>
    public static bool IsStereoAudio(string? port) =>
        port != null &&
        (port.EndsWith("_FL", StringComparison.Ordinal) || port.EndsWith("_FR", StringComparison.Ordinal));

    /// <summary>"FL" or "FR", for pairing a source's ports with the capture's.</summary>
    public static string Channel(string port) =>
        port.EndsWith("_FR", StringComparison.Ordinal) ? "FR" : "FL";

    /// <summary>
    /// Everything with stereo audio to give, one entry per node. A node is judged by its name:
    /// PipeWire's own devices are prefixed, and anything else is a program.
    /// </summary>
    public static IReadOnlyList<AudioRoute> RoutesFrom(
        IEnumerable<PipeWirePort> outputs,
        IReadOnlyDictionary<string, string>? names = null,
        string? exclude = null)
    {
        var routes = new List<AudioRoute>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var port in outputs)
        {
            if (!IsStereoAudio(port.Port)) continue;
            if (!seen.Add(port.Node)) continue;
            if (exclude != null && port.Node.Contains(exclude, StringComparison.OrdinalIgnoreCase)) continue;

            var kind = KindOf(port);
            string name = names != null && names.TryGetValue(port.Node, out var described) && described.Length > 0
                ? described
                : port.Node;

            routes.Add(new AudioRoute(port.Node, name, kind));
        }

        // Devices first, then what is playing: the list is read top down when picking a source.
        return routes
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AudioRouteKind KindOf(PipeWirePort port)
    {
        if (port.Port.StartsWith("monitor_", StringComparison.Ordinal)) return AudioRouteKind.Monitor;
        if (port.Node.StartsWith("alsa_input.", StringComparison.Ordinal)) return AudioRouteKind.Input;
        if (port.Port.StartsWith("capture_", StringComparison.Ordinal)) return AudioRouteKind.Input;

        return AudioRouteKind.Application;
    }
}
