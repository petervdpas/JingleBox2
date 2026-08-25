using System;
using System.Collections;

namespace JingleBox2.Machines;

/// <summary>
/// The keyboard on a machine's own face: what is sounding, what has something on it, and which
/// key the controls beside it are about.
/// </summary>
/// <remarks>
/// The keyboard used to stand at the foot of every panel, outside the machine, because it was
/// the same on all of them. On a kit it is not: sixteen keys out of a hundred and twenty do
/// anything, and which key fires which drum is the one thing a kit's keyboard is there to
/// answer. So a machine that wants a keyboard puts one on its own face and says where.
///
/// None of this is a setting. Which notes are sounding is what the machine is doing this
/// instant, and which key is in hand is where somebody is looking. The octave on show is the
/// one part that is a setting, and it stays where it was: on the element, as a parameter.
///
/// <see cref="Changed"/> for the same reason <see cref="IMachinePads"/> has one. A key lights
/// while a note sounds and goes out when it stops, and neither is a redraw of the panel.
/// </remarks>
public interface IMachineKeys
{
    /// <summary>The semitones sounding now, as absolute note numbers.</summary>
    IEnumerable Lit { get; }

    /// <summary>
    /// The semitones that have something on them.
    /// </summary>
    /// <remarks>
    /// Empty on a machine where every key plays, which is most of them: a keyboard with all
    /// hundred and twenty keys banded says nothing, so a machine that answers everywhere answers
    /// with nothing here.
    /// </remarks>
    IEnumerable Filled { get; }

    /// <summary>The one key the controls beside the keyboard are about, or -1 for none.</summary>
    int Marked { get; }

    /// <summary>
    /// Which octave is on show, and moved by the arrows on the keyboard itself.
    /// </summary>
    /// <remarks>
    /// Here rather than among the machine's settings, and deliberately. Where a keyboard is
    /// looking is not a thing about the sound: two instruments off one machine do not differ in
    /// it, and a song that remembered it would be a song remembering where somebody's hand was.
    ///
    /// A machine is still free to name a parameter on the element, for the one that really does
    /// keep an octave of its own. Where it names none, this is what the keyboard shows.
    /// </remarks>
    int Octave { get; set; }

    /// <summary>Plays it, which is what clicking a key has always done.</summary>
    void Play(int semitone);

    /// <summary>Told when any of the above has moved.</summary>
    event EventHandler? Changed;
}
