namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// A recording opened as a source for a bus, and the four questions a source is asked.
/// </summary>
/// <remarks>
/// **There is one way of making a recording sound here and this is it.** A pad and a take
/// auditioned on RECORD are the same act: a file becomes a decoding channel and the channel goes
/// on a bus. Written out twice they were two acts that had already drifted, and every difference
/// between them was a way for one to be silent on a machine where the other was not. One opened
/// the output first and the other did not; one asked for float and prescan and the other did
/// not; and one kept a second way of playing for the case where there was no bus, which is the
/// fork the pads had already been rid of.
///
/// So the output is opened behind <see cref="Open"/>, exactly as a pad pressed before anything
/// else has asked opens it, and there is nowhere left for a recording to go but the bus.
///
/// **Nothing above this knows what a sample looks like.** Where a recording has got to and where
/// it is sent are seconds here, not bytes, because a byte position is in the channel's own
/// decoded shape: a caller that assumed sixteen bits was silently wrong by half the moment the
/// flags asked for float, and wrong in a way that reads as a region that will not play.
///
/// A channel is named by the number <see cref="Open"/> answered rather than by an object of its
/// own, since that number is what a bus is handed and what the library knows it by. Nought is
/// the one that is not open, here as everywhere else.
/// </remarks>
public interface IRecordingSource
{
    /// <summary>Opens a recording as a decoding channel, with the output open behind it.</summary>
    /// <param name="filePath">The recording.</param>
    /// <param name="loops">Whether it comes round again rather than ending.</param>
    /// <returns>The channel, or nought where it would not open.</returns>
    int Open(string filePath, bool loops = false);

    /// <summary>Stops one and lets it go. Nothing at all for a channel that is not open.</summary>
    /// <remarks>
    /// Taking it off whatever bus it was on is the caller's, since only the caller knows which
    /// bus it asked for.
    /// </remarks>
    /// <param name="channel">What <see cref="Open"/> answered.</param>
    void Close(int channel);

    /// <summary>How long a channel is, in seconds, and nought for one that is not open.</summary>
    /// <param name="channel">What <see cref="Open"/> answered.</param>
    double Seconds(int channel);

    /// <summary>Where a channel has got to, in seconds.</summary>
    /// <remarks>
    /// How far the bus has pulled it, since a source on a bus is decoded by whatever is pulling
    /// and never by itself. A channel nothing is pulling stays where it was put, which is the
    /// honest answer and is what a take making no sound reads as.
    /// </remarks>
    /// <param name="channel">What <see cref="Open"/> answered.</param>
    double At(int channel);

    /// <summary>Moves a channel to a point in itself.</summary>
    /// <param name="channel">What <see cref="Open"/> answered.</param>
    /// <param name="seconds">Where to go, from its own start.</param>
    void Seek(int channel, double seconds);

    /// <summary>Whether a channel has run out.</summary>
    /// <param name="channel">What <see cref="Open"/> answered.</param>
    bool Ended(int channel);
}
