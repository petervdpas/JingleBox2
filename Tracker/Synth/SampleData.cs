using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class SampleData : ISampleData
{
    /// <summary>What a 16 bit sample is multiplied by to land in -1..1.</summary>
    private const float Scale = 1f / 32768f;

    private readonly short[] _samples;

    /// <summary>
    /// Takes the frames as the file held them.
    /// </summary>
    /// <remarks>
    /// Nothing here refuses bad input: a null array reads as an empty take, and a channel count
    /// or a rate of nought is corrected rather than thrown on. A decoder that came back with
    /// nonsense should leave an instrument silent, not stop the audio thread.
    /// </remarks>
    /// <param name="samples">Interleaved frames, as the decoder produced them.</param>
    /// <param name="channels">How many values make up one frame.</param>
    /// <param name="sampleRate">What the file was recorded at, which is rarely what the card runs at.</param>
    public SampleData(short[] samples, int channels, int sampleRate)
    {
        _samples = samples ?? Array.Empty<short>();
        Channels = Math.Max(1, channels);
        SampleRate = sampleRate > 0 ? sampleRate : 44100;
        FrameCount = _samples.Length / Channels;
    }

    /// <inheritdoc/>
    public int Channels { get; }

    /// <inheritdoc/>
    public int SampleRate { get; }

    /// <inheritdoc/>
    public long FrameCount { get; }

    /// <inheritdoc/>
    public bool IsEmpty => FrameCount <= 0;

    /// <inheritdoc/>
    public double Seconds => SampleRate > 0 ? (double)FrameCount / SampleRate : 0;

    /// <inheritdoc/>
    public float At(long frame, int channel)
    {
        if (frame < 0 || frame >= FrameCount) return 0;

        int index = (int)(frame * Channels) + Math.Clamp(channel, 0, Channels - 1);
        return _samples[index] * Scale;
    }

    /// <inheritdoc/>
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
