using System;
using System.Collections.Generic;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <inheritdoc/>
public sealed class ControlLinkBlock : IControlLinkBlock
{
    /// <inheritdoc/>
    public List<ControlMapping> Links { get; }

    /// <summary>Names the links this run is working over.</summary>
    /// <param name="links">
    /// What was read at startup, or nothing for a run that has none, which is a fresh
    /// installation with nothing pointed at anything yet.
    /// </param>
    public ControlLinkBlock(List<ControlMapping>? links = null) =>
        Links = links ?? new List<ControlMapping>();

    /// <inheritdoc/>
    public string Name => "Remote control links";

    /// <inheritdoc/>
    /// <remarks>
    /// Kept, and that is what the whole layer is for: a link is a fact about your hardware and
    /// the thing it is pointed at, true of every song and worth still being there tomorrow.
    /// </remarks>
    public bool Kept => true;

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    public void Moved() => Changed?.Invoke();
}
