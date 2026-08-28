namespace JingleBox2.Machines.Ui.Records;

/// <summary>The slice of peak data a viewport puts on screen.</summary>
/// <remarks>
/// Half-open, as every range in this codebase is: the end is one past the last, so the count is
/// a subtraction and an empty range is two equal numbers rather than a special case.
/// </remarks>
/// <param name="Start">First peak on screen.</param>
/// <param name="End">One past the last, so the pair reads as a half-open range.</param>
/// <param name="PixelWidth">How wide one peak is drawn, which is what spaces the columns.</param>
public readonly record struct VisibleRange(int Start, int End, double PixelWidth)
{
    /// <summary>How many peaks are in it.</summary>
    public int Count => End - Start;
}
