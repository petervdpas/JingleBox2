namespace JingleBox2.Music.Interfaces;

/// <summary>
/// Turns the two wheels beside a keyboard into numbers the music can use. Pure, so the reading
/// can be checked with nothing plugged in.
/// </summary>
/// <remarks>
/// Beside <see cref="IMidiNoteInput"/> and for the same reason: what a byte on the wire means is
/// a fact about MIDI rather than about this application, and everything above the wire deals in
/// a lean and an amount and has never heard of a status byte.
///
/// The two wheels are one contract because they are one gesture. Both sit under the same hand,
/// both are played while a note sounds, and both belong to whatever that hand is playing. What
/// differs is only the shape of the number: a bend leans either side of a middle and a
/// modulation runs up from nothing.
/// </remarks>
public interface IMidiWheelInput
{
    /// <summary>
    /// The controller number the modulation wheel sends, which the specification fixes at one.
    /// </summary>
    /// <remarks>
    /// A fact about MIDI rather than about a device, so it is known without a profile. Every
    /// keyboard ever made sends its wheel here, and a controller file naming it as a wheel adds
    /// the word rather than the number.
    /// </remarks>
    int ModulationController { get; }

    /// <summary>Where a pitch wheel rests, which is the middle of fourteen bits.</summary>
    int BendCentre { get; }

    /// <summary>And the top of that range.</summary>
    int BendMost { get; }

    /// <summary>
    /// Where the pitch wheel is leaning, minus one for all the way down to one for all the way up.
    /// </summary>
    /// <remarks>
    /// **The two halves are scaled apart, and that is not fussiness.** Fourteen bits put 8192
    /// steps below the middle and 8191 above it, so one divisor cannot give both an exact end
    /// and an exact middle. A wheel at rest reading anything but nought is a note sitting
    /// permanently off its own pitch, which is the one error here nobody would forgive, so the
    /// middle is exact and the ends are each scaled to reach exactly one.
    ///
    /// Anything outside the range is clamped rather than refused. A bend is a position and a
    /// position off the end of its own scale is the end of the scale; there is nothing else it
    /// could sensibly mean, unlike a note number, which is refused because clamping would pile
    /// every key past the edge onto one pitch.
    /// </remarks>
    /// <param name="bend">The fourteen bit value off the wire.</param>
    double LeanFor(int bend);

    /// <summary>
    /// How far the modulation wheel is up, nought to one.
    /// </summary>
    /// <remarks>
    /// Seven bits, and nought really is nothing: a wheel pushed all the way down asks for no
    /// modulation at all rather than for a little.
    /// </remarks>
    /// <param name="value">The controller's value, 0 to 127.</param>
    double AmountFor(int value);
}
