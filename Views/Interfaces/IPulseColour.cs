using Avalonia.Media;

namespace JingleBox2.Views.Interfaces;

/// <summary>
/// The colour a pad wears at a moment of its cycle while it is sounding.
/// </summary>
/// <remarks>
/// A pad that is playing has to say so without stopping being itself. It used to be repainted in
/// the theme's checked colour, which cost the thing a wall of pads is for: every playing pad
/// turned the same colour, so which one you had fired was a question about which one had changed
/// rather than something you could see. Then it was a white wash over its own colour, which is
/// honest and dull: what moves is only how bright it is, and across a room that reads as the
/// screen flickering rather than as a pad going.
///
/// So what moves is the colour itself, through the ones either side of it on the wheel. A teal
/// pad walks to green and to blue and back; a red one walks to orange and to pink. It is still
/// plainly that pad, because the walk is short and it comes home twice a cycle, and it is plainly
/// doing something, because a colour that moves is the one thing an eye at the far side of a
/// room cannot miss.
///
/// The brightness moves a little with it, which is not decoration: a pad with no colour of its
/// own is grey, grey has no hue to walk, and a rule that only moved the hue would leave those
/// pads dead while every other one moved.
///
/// A rule of its own rather than lines inside the control, so what a colour becomes at a given
/// moment can be put a question to without a window, a frame clock or a pad.
/// </remarks>
public interface IPulseColour
{
    /// <summary>
    /// Where that colour has got to at this point of the cycle.
    /// </summary>
    /// <param name="own">The pad's own colour, which is where the walk starts and ends.</param>
    /// <param name="phase">
    /// How far round the cycle, from 0 to 1. Nought is the pad's own colour, and so is one:
    /// the walk is a there and back rather than a lap, or the pad would spend half the cycle
    /// wearing a colour that is not its own.
    /// </param>
    Color At(Color own, double phase);
}
