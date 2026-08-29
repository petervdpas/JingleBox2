using System;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PointerDrag : IPointerDrag
{
    /// <summary>
    /// Six pixels, which is a little past what a hand does by accident and well short of
    /// anything anybody would call a movement.
    /// </summary>
    /// <remarks>
    /// The usual figure is four, which is what a desktop uses to tell a click from a drag. Six
    /// here because a pattern row is under twenty pixels tall and the cost of the two mistakes
    /// is not the same: a selection nobody asked for has to be noticed and dismissed before the
    /// next keystroke does something surprising, and a drag that needs two more pixels is not
    /// noticed at all.
    /// </remarks>
    public const double DefaultThreshold = 6;

    /// <inheritdoc/>
    public double Threshold { get; }

    /// <summary>One with the usual threshold, which is what everything here wants.</summary>
    public PointerDrag() : this(DefaultThreshold)
    {
    }

    /// <summary>One with a threshold of its own, for a test or a control that wants another.</summary>
    /// <param name="threshold">How far the pointer has to move. Never less than nought.</param>
    public PointerDrag(double threshold) =>
        Threshold = double.IsNaN(threshold) ? DefaultThreshold : Math.Max(0, threshold);

    /// <inheritdoc/>
    public bool Begun(double fromX, double fromY, double toX, double toY) =>
        Math.Abs(toX - fromX) > Threshold || Math.Abs(toY - fromY) > Threshold;
}
