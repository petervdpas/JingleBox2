using System;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class RecordingEdit : IRecordingEdit
{
    /// <inheritdoc/>
    public void Silence(short[]? samples, int channels, long from, long to)
    {
        if (!Holds(samples, channels, ref from, ref to)) return;

        Array.Clear(samples!, (int)(from * channels), (int)((to - from) * channels));
    }

    /// <inheritdoc/>
    public void Reverse(short[]? samples, int channels, long from, long to)
    {
        if (!Holds(samples, channels, ref from, ref to)) return;

        for (long head = from, tail = to - 1; head < tail; head++, tail--)
            for (int channel = 0; channel < channels; channel++)
            {
                long one = head * channels + channel;
                long other = tail * channels + channel;

                (samples![one], samples[other]) = (samples[other], samples[one]);
            }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The step is over one fewer than the frames there are, which is what puts the two ends of
    /// the ramp exactly on silence and exactly on the sample that was there. A region one frame
    /// long has no step at all and is left as it was, since a ramp between one place and itself
    /// is not a ramp.
    /// </remarks>
    public void Fade(short[]? samples, int channels, long from, long to, bool rising)
    {
        if (!Holds(samples, channels, ref from, ref to)) return;

        long frames = to - from;
        if (frames < 2) return;

        for (long frame = 0; frame < frames; frame++)
        {
            double along = (double)frame / (frames - 1);
            double gain = rising ? along : 1 - along;

            for (int channel = 0; channel < channels; channel++)
            {
                long at = (from + frame) * channels + channel;

                samples![at] = (short)Math.Round(samples[at] * gain);
            }
        }
    }

    /// <summary>
    /// Whether there is a region here to work on, and where it really is.
    /// </summary>
    /// <remarks>
    /// The two ends are brought inside the take rather than refused, since a handle is dragged
    /// against the edge of a picture and lands a hair outside it, and a caller that has already
    /// asked somebody about this has no use for an exception on the way back.
    /// </remarks>
    /// <param name="samples">The take's samples, or nothing.</param>
    /// <param name="channels">How many channels they are interleaved across.</param>
    /// <param name="from">The first frame, held inside the take on the way out.</param>
    /// <param name="to">One past the last, held at or after the first.</param>
    /// <returns>True where there is at least one frame to work on.</returns>
    private static bool Holds(short[]? samples, int channels, ref long from, ref long to)
    {
        if (samples == null || channels < 1) return false;

        long frames = samples.Length / channels;

        from = Math.Clamp(from, 0, frames);
        to = Math.Clamp(to, from, frames);

        return to > from;
    }
}
