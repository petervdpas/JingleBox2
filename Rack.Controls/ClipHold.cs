using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
public sealed class ClipHold : IClipHold
{
    /// <inheritdoc/>
    /// <remarks>
    /// Full scale, and it is a fact about the number rather than a taste: past one there is no
    /// more room, and what happens next is the master bending it. A threshold under one would be
    /// a warning about loudness, which is a different thing and is what the meter is for.
    /// </remarks>
    public double Over => 1.0;

    /// <inheritdoc/>
    /// <remarks>
    /// Long enough to catch an eye that was somewhere else, short enough that a light still on
    /// is about something that just happened.
    /// </remarks>
    public double HoldSeconds => 2.0;

    /// <summary>When it was last lit, or nothing since it was last put out.</summary>
    private double? _lit;

    /// <inheritdoc/>
    /// <remarks>
    /// A level that is not a number counts as clipping. Nothing should ever hand one here, and
    /// if something does it is a fault worth a light rather than one worth hiding: every
    /// comparison against a NaN is false, so it would otherwise be the one reading that can
    /// never light anything.
    /// </remarks>
    public bool Saw(double level, double now)
    {
        if (double.IsNaN(level) || level >= Over) _lit = now;

        if (_lit is not { } when) return false;

        if (now - when >= HoldSeconds || now < when)
        {
            _lit = null;

            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public void Clear() => _lit = null;
}
