using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <summary>
/// Reads what the PipeWire tools print. Kept apart from running them, so the parsing can be
/// checked against real output without a sound server in the room.
/// </summary>
public static class PipeWireGraph
{
    /// <summary>
    /// Reads one line of a port listing, or null when the line is not one.
    /// </summary>
    /// <remarks>
    /// A port line is "node:port". The node never contains a colon and the port often does
    /// not either, but a MIDI port can, so the first colon is the one that counts.
    ///
    /// The listing is asked for with an id in front when "pw-link -I" is used, so a leading run
    /// of digits followed by a space is dropped before the name is read. Anything else in front
    /// is left alone, which makes a line that is not a port fail the colon test and come back
    /// as null rather than as a port with a strange name.
    /// </remarks>
    public static PipeWirePort? ParsePort(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        string trimmed = line.Trim();

        int space = trimmed.IndexOf(' ');
        if (space > 0 && int.TryParse(trimmed[..space], out _)) trimmed = trimmed[(space + 1)..].Trim();

        int colon = trimmed.IndexOf(':');
        if (colon <= 0 || colon == trimmed.Length - 1) return null;

        return new PipeWirePort(trimmed[..colon], trimmed[(colon + 1)..]);
    }

    /// <summary>
    /// Every port in a listing, in the order the tool printed them.
    /// </summary>
    /// <remarks>
    /// Lines that are not ports are dropped rather than reported: the tools print headings and
    /// blank lines, and a listing that has grown a line nobody expected should cost the line
    /// rather than the whole reading.
    /// </remarks>
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
    /// <remarks>
    /// Every link comes back pointing the same way, from the giver to the taker, whichever of
    /// the two arrows it was printed under. A connection line before any port line is dropped,
    /// since there is nothing for it to be about.
    /// </remarks>
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
    /// <remarks>
    /// Anything that is not the right channel is the left one, which is only ever asked of a
    /// port that has already passed <see cref="IsStereoAudio"/>.
    /// </remarks>
    public static string Channel(string port) =>
        port.EndsWith("_FR", StringComparison.Ordinal) ? "FR" : "FL";

    /// <summary>
    /// Everything with stereo audio to give, one entry per node. A node is judged by its name:
    /// PipeWire's own devices are prefixed, and anything else is a program.
    /// </summary>
    /// <remarks>
    /// One entry per node although a node has a port per channel, so the picker offers a thing
    /// rather than a pair of wires. The result is devices first and then what is playing, since
    /// the list is read top down when picking a source, and alphabetical within each of those.
    /// </remarks>
    /// <param name="outputs">Every port with audio to give, as the tool listed them.</param>
    /// <param name="names">
    /// Friendly names by node, where the graph has them. A node with none keeps its own name,
    /// which for a program is already what it is called.
    /// </param>
    /// <param name="exclude">
    /// A fragment of a node name to leave out, which is how the application keeps itself off
    /// its own list. Nobody wants to record this program into this program.
    /// </param>
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

        return routes
            .OrderBy(r => r.Kind)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// What sort of thing a port belongs to, read off the names.
    /// </summary>
    /// <remarks>
    /// The graph does not say it in one field, so it is three tests in order. A monitor port is
    /// named as one whatever it hangs off. A node named as an ALSA input is a capture device,
    /// and so is a port named as a capture, which covers the devices the naming misses.
    /// Everything left is a program, which is the right way round: a program is what you get
    /// when nothing about a node says it is hardware.
    /// </remarks>
    private static AudioRouteKind KindOf(PipeWirePort port)
    {
        if (port.Port.StartsWith("monitor_", StringComparison.Ordinal)) return AudioRouteKind.Monitor;
        if (port.Node.StartsWith("alsa_input.", StringComparison.Ordinal)) return AudioRouteKind.Input;
        if (port.Port.StartsWith("capture_", StringComparison.Ordinal)) return AudioRouteKind.Input;

        return AudioRouteKind.Application;
    }
}
