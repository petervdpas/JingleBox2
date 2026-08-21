using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace JingleBox2.Audio.Routing;

/// <summary>
/// Routing through PipeWire, driven by its own command line tools rather than by binding to
/// its C library: the tools ship with PipeWire, the calls are a few a second at most, and
/// there is nothing to marshal or to keep alive.
/// </summary>
/// <remarks>
/// The recorder appears in the graph only while it is listening, which is why the RECORD page
/// holds the input open. Links last as long as that stream, so a chosen route is re-applied
/// each time rather than being remembered by the system.
///
/// This talks to another program, so it is kept at arm's length from the rest of the app. Every
/// call is bounded by a deadline, only one runs at a time, nothing here throws at its caller,
/// and a machine where the tools keep failing has the feature switch itself off rather than
/// retrying into a stall. Nothing in here touches the audio engine or the recorder.
/// </remarks>
public sealed class PipeWireRouting : IAudioRouting
{
    private const string LinkTool = "pw-link";
    private const string DumpTool = "pw-dump";

    /// <summary>Long enough for a busy machine, short enough not to hang the page.</summary>
    private static readonly TimeSpan ToolTimeout = TimeSpan.FromSeconds(2);

    /// <summary>A whole operation runs several tools; this is the ceiling for all of them.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How long a caller waits for one already in progress before giving up on it.</summary>
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Enough failures in a row to conclude the tools are not going to work here.</summary>
    private const int FailuresBeforeGivingUp = 3;

    /// <summary>The graph is asked about often; its shape does not change that fast.</summary>
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromSeconds(2);

    /// <summary>How the app's own nodes are named, for finding its capture stream.</summary>
    private const string OwnNodeMarker = "JingleBox2";

    /// <summary>One at a time: several of these at once would be several tools at once.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly Stopwatch _sinceSnapshot = new();

    private HashSet<string>? _captureNodes;
    private IReadOnlyDictionary<string, string>? _descriptions;
    private int _failures;
    private bool _givenUp;

    public bool IsAvailable => !_givenUp && OperatingSystem.IsLinux() && Which(LinkTool) != null;

    public IReadOnlyList<AudioRoute> GetRoutes() =>
        Guarded(Array.Empty<AudioRoute>(), deadline =>
        {
            var ports = PipeWireGraph.ParsePorts(Run(LinkTool, "-o"));
            if (Expired(deadline)) return Array.Empty<AudioRoute>();

            return PipeWireGraph.RoutesFrom(ports, Descriptions(), OwnNodeMarker);
        });

    public AudioRoute? GetCurrentRoute() =>
        Guarded<AudioRoute?>(null, deadline =>
        {
            var capture = CapturePorts();
            if (capture.Count == 0 || Expired(deadline)) return null;

            foreach (var link in PipeWireGraph.ParseLinks(Run(LinkTool, "-l")))
            {
                if (!capture.Any(p => p.Node == link.To.Node && p.Port == link.To.Port)) continue;

                var ports = PipeWireGraph.ParsePorts(Run(LinkTool, "-o"));
                var match = PipeWireGraph.RoutesFrom(ports, Descriptions(), OwnNodeMarker)
                    .FirstOrDefault(r => r.Node == link.From.Node);

                return match ?? new AudioRoute(link.From.Node, link.From.Node, AudioRouteKind.Input);
            }

            return null;
        });

    public bool Connect(AudioRoute route)
    {
        if (route == null) return false;

        return Guarded(false, deadline =>
        {
            var capture = CapturePorts();
            if (capture.Count == 0) return false;

            // Whatever PipeWire wired up on its own goes first, or the old source is mixed in
            // underneath the new one.
            foreach (var link in PipeWireGraph.ParseLinks(Run(LinkTool, "-l")))
            {
                if (Expired(deadline)) return false;

                if (capture.Any(p => p.Node == link.To.Node && p.Port == link.To.Port))
                    Run(LinkTool, $"-d {Quote(link.From)} {Quote(link.To)}");
            }

            var sources = PipeWireGraph.ParsePorts(Run(LinkTool, "-o"))
                .Where(p => p.Node == route.Node && PipeWireGraph.IsStereoAudio(p.Port))
                .ToList();

            if (sources.Count == 0) return false;

            bool linked = false;

            foreach (var target in capture)
            {
                if (Expired(deadline)) break;

                string channel = PipeWireGraph.Channel(target.Port);

                var source = sources.FirstOrDefault(p => PipeWireGraph.Channel(p.Port) == channel);
                if (source == default) continue;

                // A mono target would take both sides; there is nothing to pair it with, so it
                // simply does not link rather than doubling one channel.
                if (Run(LinkTool, $"{Quote(source)} {Quote(target)}") != null) linked = true;
            }

            return linked;
        });
    }

    /// <summary>
    /// Runs one operation with the door closed and a clock running, and swallows whatever
    /// comes out of it. A caller of this class gets an answer or a shrug, never an exception
    /// and never a wait without an end.
    /// </summary>
    private T Guarded<T>(T fallback, Func<Stopwatch, T> work)
    {
        if (!IsAvailable) return fallback;
        if (!_gate.Wait(GateTimeout)) return fallback;

        try
        {
            return work(Stopwatch.StartNew());
        }
        catch (Exception)
        {
            // A tool that misbehaves costs a routing change, not the page it was asked from.
            NoteFailure();
            return fallback;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool Expired(Stopwatch deadline) => deadline.Elapsed > OperationTimeout;

    /// <summary>
    /// A machine where the tools keep failing gets the feature taken away rather than being
    /// asked again every time the page opens.
    /// </summary>
    private void NoteFailure()
    {
        if (++_failures >= FailuresBeforeGivingUp) _givenUp = true;
    }

    /// <summary>
    /// The app's own capture stream, which only exists while it is listening.
    /// </summary>
    /// <remarks>
    /// The name alone is not enough to find it: the app's playback stream carries the same
    /// name and also has input ports, and wiring a source into that one would put audio where
    /// the recorder never looks. The graph says which is which, so that is what is asked.
    /// </remarks>
    private IReadOnlyList<PipeWirePort> CapturePorts()
    {
        if (!IsAvailable) return Array.Empty<PipeWirePort>();

        var captureNodes = OwnCaptureNodes();
        if (captureNodes.Count == 0) return Array.Empty<PipeWirePort>();

        return PipeWireGraph.ParsePorts(Run(LinkTool, "-i"))
            .Where(p => captureNodes.Contains(p.Node))
            .Where(p => PipeWireGraph.IsStereoAudio(p.Port))
            .ToList();
    }

    /// <summary>
    /// Friendly names, where the graph has them. A device describes itself; a program usually
    /// does not, and its node name is already its name.
    /// </summary>
    private IReadOnlyDictionary<string, string> Descriptions()
    {
        ReadGraph();

        return _descriptions ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>Our nodes that are capture streams, by name, according to the graph itself.</summary>
    private HashSet<string> OwnCaptureNodes()
    {
        ReadGraph();

        return _captureNodes ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// One dump of the graph, answering both of the questions asked of it. Both are wanted on
    /// every call and the page asks every couple of seconds, so this is remembered for about
    /// that long: a stream does not appear and disappear faster than that.
    /// </summary>
    private void ReadGraph()
    {
        if (_captureNodes != null && _sinceSnapshot.IsRunning && _sinceSnapshot.Elapsed < SnapshotLifetime) return;

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var capture = new HashSet<string>(StringComparer.Ordinal);

        string? json = Run(DumpTool, "");
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var document = JsonDocument.Parse(json);

            foreach (var node in document.RootElement.EnumerateArray())
            {
                if (!node.TryGetProperty("info", out var info)) continue;
                if (!info.TryGetProperty("props", out var props)) continue;
                if (!props.TryGetProperty("node.name", out var name)) continue;

                string? nodeName = name.GetString();
                if (string.IsNullOrEmpty(nodeName)) continue;

                if (props.TryGetProperty("node.description", out var description))
                {
                    string? described = description.GetString();
                    if (!string.IsNullOrEmpty(described)) names[nodeName] = described;
                }

                if (!props.TryGetProperty("media.class", out var media)) continue;
                if (!nodeName.Contains(OwnNodeMarker, StringComparison.OrdinalIgnoreCase)) continue;

                // A capture stream takes audio in from the graph; a playback stream gives it out.
                if (media.GetString() == "Stream/Input/Audio") capture.Add(nodeName);
            }
        }
        catch (JsonException)
        {
            // Nothing readable means nothing to route into, which the callers handle.
            return;
        }

        _descriptions = names;
        _captureNodes = capture;
        _sinceSnapshot.Restart();
    }

    private static string Quote(PipeWirePort port) => $"\"{port.Node}:{port.Port}\"";

    /// <summary>
    /// Runs a tool and returns what it printed, or null when it could not be run.
    /// </summary>
    /// <remarks>
    /// Both pipes are drained at the same time. Reading one to the end while the other fills
    /// up wedges the child, and a wedged child takes the caller with it, which is a hang and
    /// not a slow call.
    /// </remarks>
    private static string? Run(string tool, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(tool, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null) return null;

            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit((int)ToolTimeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* it is going away either way */ }
                return null;
            }


            // The timed wait can return before the readers have finished; this one does not.
            process.WaitForExit();

            _ = errors.Result;
            return output.Result;
        }
        catch (Exception)
        {
            // Not installed, not permitted, not this platform: all the same answer here.
            return null;
        }
    }

    private static string? Which(string tool)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths == null) return null;

        foreach (var directory in paths)
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;

            string candidate = Path.Combine(directory, tool);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
