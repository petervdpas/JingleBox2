using JingleBox2.Tracker.Synth.Records;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// The read head: where in the file the next value comes from, and what happens when it runs
/// off one end. Deliberately free of audio types, so the awkward cases (a loop shorter than a
/// step, a backwards ping-pong, a window of nothing) can be tested on their own.
/// </summary>
/// <remarks>
/// <see cref="Advance"/> is called once per sample per voice on the audio thread and keeps
/// nothing of its own: the position and the direction belong to the voice and are passed in by
/// reference. Nothing here allocates or waits.
/// </remarks>
public interface ISamplePlayback
{
    /// <summary>
    /// Turns the fractions an instrument stores into frame positions in a file.
    /// </summary>
    /// <remarks>
    /// A window with nothing in it is almost certainly a mistake with the handles rather than a
    /// request for silence, so it opens back out to the whole file. An instrument with no shape
    /// at all is the whole file too, unlooped and forwards.
    /// </remarks>
    /// <param name="shape">The handles as the instrument stores them, in fractions of the file, or null for none.</param>
    /// <param name="frameCount">How long the file is, which is what the fractions are read against.</param>
    SampleWindow WindowFor(SampleShape? shape, long frameCount);

    /// <summary>
    /// Moves the read head on by one step and says whether there is anything left to play.
    /// A looping window always has something left; a one-shot ends when it leaves the window.
    /// </summary>
    /// <remarks>
    /// A ping-pong turns round rather than jumping, so the overshoot is reflected back in: at
    /// high pitches a step can be longer than the loop itself, and a loop that simply jumped to
    /// its far end would lose whatever the step had gone past.
    /// </remarks>
    /// <param name="position">Where the head is, in frames. Moved.</param>
    /// <param name="direction">Which way it is going. Turned round by a ping-pong.</param>
    /// <param name="step">How far to move, in frames, which is the pitch expressed as a speed.</param>
    /// <param name="window">The part of the file being played, and how it repeats.</param>
    bool Advance(ref double position, ref int direction, double step, in SampleWindow window);
}
