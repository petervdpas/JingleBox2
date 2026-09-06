using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <summary>What every machine that cannot be asked gets.</summary>
/// <remarks>
/// Linux among them, and nothing is lost: there a source is taken off its own output by moving
/// a link, so there is no need to name a place to send it instead.
/// </remarks>
public sealed class NoPlaybackEndpoints : IPlaybackEndpoints
{
    /// <inheritdoc/>
    /// <remarks>Nothing, always.</remarks>
    public IReadOnlyList<AudioEndpoint> Outputs() => Array.Empty<AudioEndpoint>();
}
