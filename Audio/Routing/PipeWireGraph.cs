using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <inheritdoc/>
public sealed class PipeWireGraph : IPipeWireGraph
{
    /// <inheritdoc/>
    public PipeWirePort? ParsePort(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        string trimmed = line.Trim();

        int space = trimmed.IndexOf(' ');
        if (space > 0 && int.TryParse(trimmed[..space], out _)) trimmed = trimmed[(space + 1)..].Trim();

        int colon = trimmed.IndexOf(':');
        if (colon <= 0 || colon == trimmed.Length - 1) return null;

        return new PipeWirePort(trimmed[..colon], trimmed[(colon + 1)..]);
    }

    /// <inheritdoc/>
    public IReadOnlyList<PipeWirePort> ParsePorts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<PipeWirePort>();

        var ports = new List<PipeWirePort>();

        foreach (var line in text.Split('\n'))
        {
            if (ParsePort(line) is { } port) ports.Add(port);
        }

        return ports;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PipeWireLink> ParseLinks(string? text)
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

    /// <inheritdoc/>
    public bool IsStereoAudio(string? port) =>
        port != null &&
        (port.EndsWith("_FL", StringComparison.Ordinal) || port.EndsWith("_FR", StringComparison.Ordinal));

    /// <inheritdoc/>
    public string Channel(string port) =>
        port.EndsWith("_FR", StringComparison.Ordinal) ? "FR" : "FL";

    /// <inheritdoc/>
    public IReadOnlyList<AudioRoute> RoutesFrom(
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
    private AudioRouteKind KindOf(PipeWirePort port)
    {
        if (port.Port.StartsWith("monitor_", StringComparison.Ordinal)) return AudioRouteKind.Monitor;
        if (port.Node.StartsWith("alsa_input.", StringComparison.Ordinal)) return AudioRouteKind.Input;
        if (port.Port.StartsWith("capture_", StringComparison.Ordinal)) return AudioRouteKind.Input;

        return AudioRouteKind.Application;
    }
}
