using System;

namespace JingleBox2.Tracker.Synth;

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
/// </remarks>
public sealed class SampleData
{
    private const float Scale = 1f / 32768f;

    private readonly short[] _samples;

    public SampleData(short[] samples, int channels, int sampleRate)
    {
        _samples = samples ?? Array.Empty<short>();
        Channels = Math.Max(1, channels);
        SampleRate = sampleRate > 0 ? sampleRate : 44100;
        FrameCount = _samples.Length / Channels;
    }

    public int Channels { get; }

    public int SampleRate { get; }

    public long FrameCount { get; }

    public bool IsEmpty => FrameCount <= 0;

    public double Seconds => SampleRate > 0 ? (double)FrameCount / SampleRate : 0;

    /// <summary>One frame of one channel, with no interpolation. Outside the file reads silent.</summary>
    public float At(long frame, int channel)
    {
        if (frame < 0 || frame >= FrameCount) return 0;

        int index = (int)(frame * Channels) + Math.Clamp(channel, 0, Channels - 1);
        return _samples[index] * Scale;
    }

    /// <summary>
    /// The value between two frames, mixed in proportion. A sample played at a pitch lands
    /// between frames almost every time, and stepping to the nearest one instead adds a hiss
    /// that gets worse the further the note is from the sample's own.
    /// </summary>
    public float Between(double position, int channel)
    {
        if (position <= 0) return At(0, channel);
        if (position >= FrameCount - 1) return At(FrameCount - 1, channel);

        long frame = (long)position;
        float fraction = (float)(position - frame);

        float first = At(frame, channel);
        float second = At(frame + 1, channel);

        return first + (second - first) * fraction;
    }
}
