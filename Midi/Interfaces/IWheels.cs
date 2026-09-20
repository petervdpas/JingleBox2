namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where the two wheels beside a keyboard come out, once they are a lean and an amount rather
/// than bytes.
/// </summary>
/// <remarks>
/// The seam between the wire and the music, the same one <see cref="INoteTrigger"/> is for a
/// key. Everything above it deals in numbers and has never heard of a status byte; everything
/// below it is the wire and has never heard of a track.
///
/// **A wheel goes where the keys go.** It is on the same instrument, under the same hand, and
/// what it is about is the notes that hand is playing, so it follows them: the same port, the
/// same half of the application, and the same track. Anything that hears a key and not the
/// wheel beside it is a place a bend gets stranded, which is a track left permanently off its
/// own pitch with nothing on the screen saying why.
///
/// One contract for both wheels rather than two, because they are one gesture with two shapes.
/// A device that has only one of them simply never calls the other.
///
/// Called on whichever thread the port delivers on. Whoever implements it owns getting to its
/// own thread, and a wheel is tens of messages a second, so getting there must not cost a trip
/// through the drawing thread: a bend that arrives at the frame rate is a bend that steps.
/// </remarks>
public interface IWheels
{
    /// <summary>
    /// The pitch wheel is here, minus one for all the way down and one for all the way up.
    /// </summary>
    /// <remarks>
    /// A lean rather than a number of semitones, because how far a wheel bends is the
    /// instrument's own business and the wire has never heard of an instrument. Nought is the
    /// wheel at rest, which is a note at the pitch it was played at.
    /// </remarks>
    /// <param name="lean">Where the wheel is, -1 to 1.</param>
    void Bend(double lean);

    /// <summary>
    /// And the modulation wheel, nought for nothing up to one.
    /// </summary>
    /// <remarks>
    /// What it modulates is the device's to say and no part of this: see
    /// <c>SoundMachineProject.Wheel</c> for one of ours and the plugin's own settings for
    /// somebody else's.
    /// </remarks>
    /// <param name="amount">How far up the wheel is, 0 to 1.</param>
    void Modulate(double amount);
}
