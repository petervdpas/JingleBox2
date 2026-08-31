
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
