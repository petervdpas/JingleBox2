namespace JingleBox2.Audio.Records;

/// <summary>
/// A recording's shape, worked out once so a picture of it can be drawn many times.
/// </summary>
/// <remarks>
/// The peaks and not the audio: decoding a take is seconds and there is no reason to do it again
/// every time a window is resized or a loop point is dragged.
/// </remarks>
public class WaveformData
{
    /// <summary>The peak of each slice of the recording, in the order they play.</summary>
    /// <remarks>
    /// How many slices there are is whatever the reader chose, so a position on the picture is
    /// worked out from the length rather than from a fixed rate.
    /// </remarks>
    public required float[] PeakData { get; set; }

    /// <summary>The recording's own rate, which is what turns a sample number into a time.</summary>
    public int SampleRate { get; set; }

    /// <summary>How many channels it has.</summary>
    public int Channels { get; set; }

    /// <summary>Number of sample frames, so this is the length in samples per channel.</summary>
    public long TotalSamples { get; set; }
}
