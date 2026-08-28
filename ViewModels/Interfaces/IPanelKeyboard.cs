using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// How wide a panel's keyboard is, and where it has to be to show a note.
/// </summary>
/// <remarks>
/// Eighty-eight keys shows everything and is a metre of wood on a panel this size; one octave
/// fits and hides most of what a track plays. Three octaves is the trade: wide enough that a
/// bass line and its own upper reaches are on screen together, small enough to sit under the
/// knobs, and it moves when the music goes somewhere it is not.
///
/// It moves in whole octaves, and the OCTAVE lamps are where it starts. That is the point of
/// keeping the two the same number: the lamps are not a separate setting that has to be kept in
/// step with the keyboard, they are the keyboard's own position.
/// </remarks>
public interface IPanelKeyboard
{
    /// <summary>Three octaves and the C on top, which is what a panel has room for.</summary>
    const int Keys = 37;

    /// <summary>The highest octave the lamps count to.</summary>
    const int TopOctave = 9;

    /// <summary>
    /// Which octave the keyboard has to start at for a note to be on it, moving as little as
    /// it can. A note already showing moves nothing.
    /// </summary>
    /// <remarks>
    /// Below the keyboard, the note's own octave becomes the leftmost one. Above it, the note's
    /// octave becomes the rightmost whole one instead, so the keyboard travels the least
    /// distance that puts the note on it rather than jumping the note to the far left and
    /// taking everything under it off the panel.
    /// </remarks>
    int Reveal(Note note, int octave, int keys = Keys);
}
