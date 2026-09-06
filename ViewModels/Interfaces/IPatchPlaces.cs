namespace JingleBox2.ViewModels.Interfaces;

/// <summary>Where the patchbay's blocks were left, kept between one run and the next.</summary>
/// <remarks>
/// A seam of its own so the patchbay can be built and put a question to without a settings file,
/// and so the one thing that touches the disc is the one thing that has to be trusted with it: a
/// picture that wrote its own settings would be writing on every frame of a drag.
///
/// Only what somebody moved is kept, which is the same rule the shortcut map follows: a default
/// that is not written down can still be improved later without arguing with what is on
/// anybody's disc.
/// </remarks>
public interface IPatchPlaces
{
    /// <summary>Where a block was left, or false where it has never been moved.</summary>
    /// <param name="node">The block, by its id.</param>
    /// <param name="x">How far across, when there is an answer.</param>
    /// <param name="y">How far down, when there is an answer.</param>
    bool Placed(string node, out double x, out double y);

    /// <summary>
    /// Writes down where a block has been left.
    /// </summary>
    /// <remarks>
    /// Told once a hand has let go rather than while it is moving, so a drag across the page is
    /// one line written rather than a hundred. A place that is not a real number is refused: the
    /// picture is drawn from what is stored, and a block at NaN is a block nobody can find and
    /// nothing on the screen to say why.
    /// </remarks>
    /// <param name="node">The block, by its id.</param>
    /// <param name="x">How far across it was left.</param>
    /// <param name="y">How far down.</param>
    void Place(string node, double x, double y);
}
