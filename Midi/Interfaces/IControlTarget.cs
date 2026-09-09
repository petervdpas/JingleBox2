
namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// One thing a hardware control can drive.
/// </summary>
/// <remarks>
/// Deliberately the smallest possible shape: what it is called, what range it lives in, where
/// it is, and how to move it. Everything the program has turned out to fit it without being
/// changed, which is the whole reason mapping hardware is a small job here rather than a large
/// one. A machine parameter carries its own <c>Min</c> and <c>Max</c>, a plugin parameter
/// carries the same two under the same names, and a mixer strip is a handful of known ranges.
///
/// So there is no per machine work, and a machine somebody writes next year is mappable the day
/// it lands, because it already says what its parameters are.
/// </remarks>
public interface IControlTarget
{
    /// <summary>What to call it, for a list of mappings and for the status line.</summary>
    string Name { get; }

    /// <summary>The bottom of its range, in whatever the thing measures itself in.</summary>
    double Min { get; }

    /// <summary>And the top. A knob is read as a fraction between the two, never as 0 to 127.</summary>
    double Max { get; }

    /// <summary>Where it is now, which is what pickup compares the knob against.</summary>
    double Value { get; }

    /// <summary>Moves it. Clamping is the target's own business.</summary>
    void Set(double value);

    /// <summary>
    /// Moves it because the song is playing its own automation back, rather than because a hand
    /// moved something.
    /// </summary>
    /// <remarks>
    /// The two are the same write and they are not the same act, and until this existed there
    /// was no way to tell them apart: a lane replaying a fader called <see cref="Set"/>, the
    /// mixer read that as somebody having moved it, and the song was marked as having unsaved
    /// changes in it by the act of playing what it already held. From a chair that is a song
    /// that can never be left alone, and it costs more than a flag: the rescue copy is written
    /// every twenty seconds for ever, and each of those walks every plugin on every track.
    ///
    /// A hand and a lane still want the same sound and the same picture. What only a hand wants
    /// is the undo step and the mark, so this exists to leave those out and nothing else.
    ///
    /// <see cref="Set"/> by default, so a target that has not thought about it behaves exactly
    /// as it did and a lane pointed at one is no worse off than before.
    /// </remarks>
    void Played(double value) => Set(value);

    /// <summary>
    /// Whether this is a switch: two states, rather than a range with values in between.
    /// </summary>
    /// <remarks>
    /// It changes what a button does to it and nothing else. A hardware button that stays where
    /// it is put reports its state, so following the value is right and this need never be
    /// asked. A momentary button reports a finger: full while it is held and nought when it is
    /// let go. Followed, that mutes a track for as long as somebody keeps their thumb down and
    /// unmutes it when they take it off, which is a mute nobody can use.
    ///
    /// False by default, so a target that has not thought about it behaves exactly as it did.
    /// </remarks>
    bool Switch => false;

    /// <summary>
    /// How the value reads, for a controller with a screen to show.
    /// </summary>
    /// <remarks>
    /// A plain number unless the thing knows better. The panel on the screen knows how to print
    /// its own settings and this does not, so a machine parameter says its unit and everything
    /// else says the number: "0.42" is not much, but beside the parameter's name it is enough
    /// to see where you have got to without looking up.
    /// </remarks>
    string Reads(double value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
