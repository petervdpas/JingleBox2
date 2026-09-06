namespace JingleBox2.UI.Interfaces;

/// <summary>How tall a strip folded under something may be dragged.</summary>
/// <remarks>
/// **A strip takes its room off the thing it is folded under, and that thing has a floor.**
/// Under the pattern the floor is music somebody can see, and without one a grip dragged upwards
/// takes the pattern to nothing: the strips end up filling the page and the part being written
/// is gone, which is the one thing that page is for.
///
/// A rule of its own so the answer can be put a question to without a window, and because the
/// numbers that reach it are ordinary ones: how much room there is, what the other strips are
/// taking, and what has to be left.
/// </remarks>
public interface IStripRoom
{
    /// <summary>
    /// The tallest this strip may be, given what is around it.
    /// </summary>
    /// <remarks>
    /// Never negative and never below what the strip already holds: a page too small for a
    /// strip is a page to be made bigger, and answering nought there would collapse a strip
    /// somebody had set rather than merely refusing to grow it.
    /// </remarks>
    /// <param name="room">How tall the whole area is.</param>
    /// <param name="others">What every other strip in it is taking.</param>
    /// <param name="least">The least that has to be left for what the strips are folded under.</param>
    /// <param name="holding">What this strip has to be to show what is in it.</param>
    double Tallest(double room, double others, double least, double holding);
}
