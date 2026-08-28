namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// A recording held in memory, ready to be read at any speed and in either direction.
/// </summary>
/// <remarks>
/// Kept as the 16 bit samples the file already holds rather than as floats: it halves what a
/// long take costs to keep around, and the conversion is one multiply on a path that is doing
/// interpolation anyway.
///
/// Reads are by fractional frame, because a note is almost never played at the rate the file
/// was recorded at. Between two frames the value is interpolated, which is what stops a
/// resampled sample from sounding gritty.
///
/// Read from the audio thread and never written after it is made, so any number of voices can
/// play the same take at once without a lock.
/// </remarks>
public interface ISampleData
{
    /// <summary>How many values one frame holds. One is mono and two is a stereo take.</summary>
    int Channels { get; }

    /// <summary>What the file was recorded at, which is half of how fast a voice reads it.</summary>
    int SampleRate { get; }

    /// <summary>How many frames there are, which is the length in the file's own time.</summary>
    long FrameCount { get; }

    /// <summary>Nothing to play, which is what a take that failed to decode looks like.</summary>
    bool IsEmpty { get; }

    /// <summary>How long the take is, for anything showing a length rather than reading one.</summary>
    double Seconds { get; }

    /// <summary>One frame of one channel, with no interpolation. Outside the file reads silent.</summary>
    /// <param name="frame">Which frame, counted from the start of the file.</param>
    /// <param name="channel">Which side, held inside what the take actually has.</param>
    float At(long frame, int channel);

    /// <summary>
    /// The value between two frames, mixed in proportion. A sample played at a pitch lands
    /// between frames almost every time, and stepping to the nearest one instead adds a hiss
    /// that gets worse the further the note is from the sample's own.
    /// </summary>
    /// <param name="position">Where the read head is, in frames, fraction and all.</param>
    /// <param name="channel">Which side, held inside what the take actually has.</param>
    float Between(double position, int channel);
}
