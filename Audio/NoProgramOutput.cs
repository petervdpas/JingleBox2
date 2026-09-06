using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <summary>What every machine without a per-program output gets.</summary>
/// <remarks>
/// Linux among them, and nothing is lost: there a source is taken off its own output by moving a
/// link, which is the better answer of the two since it needs nowhere to send it instead.
/// </remarks>
public sealed class NoProgramOutput : IProgramOutput
{
    /// <inheritdoc/>
    /// <remarks>Always false. There is nothing here to be told.</remarks>
    public bool CanPoint => false;

    /// <inheritdoc/>
    public bool Point(int processId, string endpoint) => false;

    /// <inheritdoc/>
    public bool Release(int processId) => false;
}
