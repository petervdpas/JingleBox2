using System;
using System.Globalization;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// The value logic behind a number field: stepping, clamping, and reading typed text.
/// Pure, so the control above it stays about input handling.
/// </summary>
public static class NumericInput
{
    /// <summary>Steps a value and keeps it in range. Returns the value unchanged at a limit.</summary>
    public static double Step(double value, double delta, double step, double minimum, double maximum) =>
        Clamp(value + delta * step, minimum, maximum);

    /// <summary>
    /// Holds a value inside a range, and inside the bottom of it when the range is the wrong
    /// way round.
    /// </summary>
    /// <remarks>
    /// The reversed case is not defensive tidiness. A machine's description comes out of a file
    /// somebody wrote by hand, so a parameter with its maximum below its minimum is an ordinary
    /// arrival, and <see cref="Math.Clamp(double,double,double)"/> throws on one. A control that
    /// sits at the low end is a control somebody can see is wrong; an exception on the drawing
    /// thread takes the whole panel down.
    /// </remarks>
    public static double Clamp(double value, double minimum, double maximum) =>
        maximum < minimum ? minimum : Math.Clamp(value, minimum, maximum);

    /// <summary>
    /// Reads what the user typed. Anything unparseable leaves the value alone rather than
    /// resetting it to zero, so a stray keystroke does not wipe a tempo.
    /// </summary>
    public static double Parse(string? text, double fallback, double minimum, double maximum)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? Clamp(parsed, minimum, maximum)
            : fallback;
    }

    /// <summary>
    /// Words a value the way its control asks for.
    /// </summary>
    /// <remarks>
    /// Invariant culture, deliberately: these readings are measured for width, compared with
    /// each other, and read back through <see cref="Parse"/>, and a decimal comma would break
    /// all three on somebody else's machine. No format at all means whole numbers.
    /// </remarks>
    public static string Format(double value, string? format) =>
        value.ToString(string.IsNullOrEmpty(format) ? "0" : format, CultureInfo.InvariantCulture);

    /// <summary>
    /// The longest a reading can be anywhere in a range, so a control can be measured by what it
    /// might say rather than by what it happens to say.
    /// </summary>
    /// <remarks>
    /// A control measured off its current reading is as wide as the number under it. Two faders
    /// of the same kind then come out different widths, and whatever stands beside one moves
    /// when the value does: on the mixer, "-10.0 dB" is one character wider than "0.0 dB", and
    /// the two strips turned down far enough to need it had their meters pushed into the card's
    /// own border.
    ///
    /// The ends are the candidates because a format widens with magnitude and with the minus
    /// sign, and both of those are at their worst at a limit. The value itself is in it too,
    /// since nothing stops a control being handed one from outside its own ends.
    ///
    /// The longest string rather than the widest one, which is the same thing only in a
    /// monospaced font. That is what readings are drawn in here, and it saves laying out three
    /// pieces of text on every measure.
    /// </remarks>
    public static string Widest(double value, double minimum, double maximum, string? format, string? unit)
    {
        string longest = Format(value, format);
        string low = Format(minimum, format);
        string high = Format(maximum, format);

        if (low.Length > longest.Length) longest = low;
        if (high.Length > longest.Length) longest = high;

        return longest + (unit ?? "");
    }
}
