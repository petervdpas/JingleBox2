namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What the capture has heard: the take while one is being made, and the last moment while it
/// is only being watched.
/// </summary>
/// <remarks>
/// One buffer doing two jobs, which is why it is a thing of its own rather than a list inside
/// the recorder. While a take is being made it keeps everything; while only the meter is
/// reading it keeps the last fifth of a second and lets the rest go, or an afternoon of
/// monitoring would fill memory with audio nobody asked for.
///
/// **The two jobs meet at the moment a take stops, and that meeting is what this exists for.**
/// Stopping is not one act: the flag says the take is over and the audio is still in the buffer,
/// and a block arriving in between is read as monitoring and throws the take away down to its
/// last fifth of a second. From the outside that is a four second performance saved as 200
/// milliseconds of silence, with nothing anywhere saying why. So <see cref="Stop"/> lifts the
/// take out in the same breath as it clears the flag, and every other member takes the same
/// lock: there is no window left for a block to arrive in.
/// </remarks>
public interface ITakeBuffer
{
    /// <summary>Whether everything that arrives is being kept.</summary>
    bool Recording { get; }

    /// <summary>Throws away whatever is held and keeps only the last moment from now on.</summary>
    /// <remarks>
    /// Apart from <see cref="Start"/> because opening an input can fail: the old take has to go
    /// before the attempt, and nothing may be said to be recording until the attempt worked.
    /// </remarks>
    void Reset();

    /// <summary>Begins keeping everything that arrives.</summary>
    void Start();

    /// <summary>
    /// Stops keeping, and hands back everything that was kept.
    /// </summary>
    /// <remarks>
    /// The take is also held on <see cref="Take"/> afterwards, so whoever writes it down does
    /// not have to catch the return value on the thread that stopped the recording.
    /// </remarks>
    /// <returns>The take, or nothing at all where none was being made.</returns>
    byte[] Stop();

    /// <summary>The take from the last <see cref="Stop"/>, until the next <see cref="Reset"/>.</summary>
    byte[] Take { get; }

    /// <summary>Adds a block that has just arrived from the capture.</summary>
    /// <param name="block">The audio, as it arrived.</param>
    void Add(byte[] block);

    /// <summary>The last moment of audio, for a meter to read.</summary>
    /// <remarks>
    /// Always a whole number of frames, so the samples in it stay in step with their channels.
    /// </remarks>
    /// <param name="maxBytes">At most this much, taken from the end.</param>
    /// <param name="bytesPerFrame">How wide a frame is, so the answer can be cut on one.</param>
    byte[] Recent(int maxBytes, int bytesPerFrame);
}
