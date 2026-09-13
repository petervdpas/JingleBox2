namespace JingleBox2.Rack.Controls.Interfaces;

/// <summary>
/// What a hand does to a drawn wave: a stroke across it, a plain shape put down, and a smoothing.
/// </summary>
/// <remarks>
/// A pointer reports where it is a few dozen times a second, so a quick stroke across a pad of a
/// hundred and twenty eight points lands on a handful of them. Setting only those would leave the
/// rest where they were and draw a comb; a stroke is a straight line from where the pointer was to
/// where it is, laid over every point between.
///
/// Every answer is a new line and nothing handed in is written into, since a line on a panel is
/// held by the sound as well and is replaced whole.
/// </remarks>
internal interface IWavePen
{
    /// <summary>
    /// The line with a straight stroke laid across it between two places.
    /// </summary>
    /// <param name="line">The line as it stands.</param>
    /// <param name="fromAcross">Where the stroke starts across the pad, nought to one.</param>
    /// <param name="fromLevel">And its level, -1 to 1.</param>
    /// <param name="toAcross">Where it ends across the pad, nought to one.</param>
    /// <param name="toLevel">And its level, -1 to 1.</param>
    double[] Stroke(double[] line, double fromAcross, double fromLevel, double toAcross, double toLevel);

    /// <summary>
    /// A plain shape the length of the drawing, or nothing for a word that names none.
    /// </summary>
    /// <param name="word">One of the shape words the pen knows.</param>
    double[]? Shape(string word);

    /// <summary>
    /// The line with its corners taken off, each point weighed with its two neighbours.
    /// </summary>
    /// <remarks>
    /// Round the ends, since a wave is a cycle and its last point is next to its first.
    /// </remarks>
    /// <param name="line">The line as it stands.</param>
    double[] Smoothed(double[] line);
}
