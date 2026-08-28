using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;

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
    /// <summary>The tool that lists ports and links, and makes and breaks them.</summary>
    private const string LinkTool = "pw-link";

    /// <summary>The tool that prints the whole graph as JSON, which is where the names live.</summary>
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

    /// <summary>How old the remembered graph is. Not running means there has never been one.</summary>
    private readonly Stopwatch _sinceSnapshot = new();

    /// <summary>
    /// Our own nodes that take audio in, off the last dump. Null until one has been read, which
    /// is what tells the reader there is nothing remembered rather than nothing there.
    /// </summary>
    private HashSet<string>? _captureNodes;

    /// <summary>Friendly names by node, off the same dump.</summary>
    private IReadOnlyDictionary<string, string>? _descriptions;

    /// <summary>Failed operations so far, counted towards giving up on the tools altogether.</summary>
    private int _failures;

    /// <summary>Set once the tools have failed often enough to stop asking. Never cleared.</summary>
    private bool _givenUp;

    /// <inheritdoc/>
    /// <remarks>
    /// Three things at once: this is Linux, the tools are on the path, and they have not already
    /// failed their way out of the feature. The path is walked on every call rather than
    /// remembered, since it is a handful of file checks and the answer decides whether a page
    /// shows the routing at all.
    /// </remarks>
    public bool IsAvailable => !_givenUp && OperatingSystem.IsLinux() && Which(LinkTool) != null;

    /// <inheritdoc/>
    /// <remarks>
    /// The application's own nodes are left out: recording this program into this program is
    /// nobody's intention, and it is the one route that can be built by accident.
    /// </remarks>
    public IReadOnlyList<AudioRoute> GetRoutes() =>
        Guarded(Array.Empty<AudioRoute>(), deadline =>
        {
            var ports = PipeWireGraph.ParsePorts(Run(LinkTool, "-o"));
            if (Expired(deadline)) return Array.Empty<AudioRoute>();

            return PipeWireGraph.RoutesFrom(ports, Descriptions(), OwnNodeMarker);
        });

    /// <inheritdoc/>
    /// <remarks>
    /// Read back out of the graph rather than remembered from the last <see cref="Connect"/>:
    /// PipeWire wires things up on its own, a stream that is reopened comes back attached to
    /// whatever the system thought best, and a remembered answer would then be a claim about
    /// something that is no longer true. A source that is feeding the capture but is not on the
    /// offered list still gets named, since it is plainly there.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Everything already feeding the capture is taken off first. PipeWire mixes what arrives
    /// at a port rather than replacing it, so leaving the old link in place would put the
    /// previous source underneath the new one and both would be recorded.
    ///
    /// The two sides are matched by channel and linked as a pair. A source with only one of
    /// them simply does not link on that side rather than being doubled into both, since a
    /// recording of one channel in stereo is a worse answer than a missing wire.
    /// </remarks>
    public bool Connect(AudioRoute route)
    {
        if (route == null) return false;

        return Guarded(false, deadline =>
        {
            var capture = CapturePorts();
            if (capture.Count == 0) return false;

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
    /// <remarks>
    /// A tool that misbehaves costs a routing change, not the page it was asked from, so the
    /// exception is counted towards giving up and the fallback is handed back instead. A caller
    /// that cannot get through the door inside <see cref="GateTimeout"/> gets the fallback as
    /// well: waiting behind somebody else's tool run is how a page comes to look frozen.
    /// </remarks>
    /// <param name="fallback">What to answer when the work cannot be done or goes wrong.</param>
    /// <param name="work">
    /// The operation, handed a clock it is expected to consult: several tools run inside one of
    /// these and the ceiling is on the lot of them, not on each.
    /// </param>
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
            NoteFailure();
            return fallback;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Whether an operation has already used up all the time it is allowed.</summary>
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
    /// <remarks>
    /// A capture stream is the one whose media class says audio comes in to it; a playback
    /// stream gives audio out and carries the same name, which is exactly the confusion
    /// <see cref="CapturePorts"/> exists to avoid.
    ///
    /// A dump that cannot be read leaves what was remembered alone and says nothing. Nothing
    /// readable means nothing to route into, which the callers already handle as an empty
    /// answer, and throwing here would cost the page rather than the reading.
    /// </remarks>
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

                if (media.GetString() == "Stream/Input/Audio") capture.Add(nodeName);
            }
        }
        catch (JsonException)
        {
            return;
        }

        _descriptions = names;
        _captureNodes = capture;
        _sinceSnapshot.Restart();
    }

    /// <summary>
    /// A port as the tool wants it on a command line. Quoted because a node name is very often
    /// a sentence with spaces in it.
    /// </summary>
    private static string Quote(PipeWirePort port) => $"\"{port.Node}:{port.Port}\"";

    /// <summary>
    /// Runs a tool and returns what it printed, or null when it could not be run.
    /// </summary>
    /// <remarks>
    /// Both pipes are drained at the same time. Reading one to the end while the other fills
    /// up wedges the child, and a wedged child takes the caller with it, which is a hang and
    /// not a slow call.
    ///
    /// A tool that outstays <see cref="ToolTimeout"/> is killed, and whether the kill itself
    /// works is not worth checking: the process is going away either way. The timed wait can
    /// return before the readers have finished, so the plain wait after it is the one that
    /// makes sure the output is really all there.
    ///
    /// Not installed, not permitted, not this platform: all of them come back as null, because
    /// there is nothing a caller could usefully do differently about any of them.
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
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            process.WaitForExit();

            _ = errors.Result;
            return output.Result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Where a tool is on this machine, or null when it is not on the path at all.
    /// </summary>
    /// <remarks>
    /// Walked here rather than left to the process launcher, because the question is asked
    /// before anything is run: <see cref="IsAvailable"/> has to answer whether the feature
    /// exists, and finding that out by starting a program that is not there and catching the
    /// failure is a slower way to learn the same thing.
    /// </remarks>
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
