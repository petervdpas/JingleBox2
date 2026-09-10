using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// PipeWire, by the description its ALSA plugin carries. Nothing here is asked of the machine:
/// it is a comparison, so it can be put a question to anywhere.
/// </remarks>
public sealed class SoundServerOutput : ISoundServerOutput
{
    /// <summary>What the library calls the server's own entry.</summary>
    /// <remarks>
    /// Matched whole and without regard to case, not as a fragment: <c>PulseAudio Sound Server</c>
    /// sits beside it in the same list and is a different thing, a compatibility layer over the
    /// same server and one hop further from the graph.
    /// </remarks>
    private const string Server = "PipeWire Sound Server";

    /// <inheritdoc/>
    public bool Is(string? name, bool standard) =>
        standard || string.Equals(name?.Trim(), Server, StringComparison.OrdinalIgnoreCase);
}
