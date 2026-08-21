using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Where a voice reads from, in frames: the part of the file it plays and the part it repeats.
/// Worked out once when the note starts, since none of it moves while the note lasts.
/// </summary>
public readonly record struct SampleWindow(
    double Start,
    double End,
    double LoopStart,
    double LoopEnd,
    SampleLoopMode Loop,
    bool Reverse)
{
    public double LoopLength => LoopEnd - LoopStart;

    /// <summary>A loop of no length is not a loop; it would be a division by zero and a stuck note.</summary>
    public bool IsLooping => Loop != SampleLoopMode.None && LoopLength > 0;

    /// <summary>Where a note starts reading: the far end when it plays backwards.</summary>
    public double Entry => Reverse ? End : Start;

    public int Direction => Reverse ? -1 : 1;
}

/// <summary>
/// The read head: where in the file the next value comes from, and what happens when it runs
/// off one end. Deliberately free of audio types, so the awkward cases (a loop shorter than a
/// step, a backwards ping-pong, a window of nothing) can be tested on their own.
/// </summary>
public static class SamplePlayback
{
    /// <summary>Turns the fractions an instrument stores into frame positions in a file.</summary>
    public static SampleWindow WindowFor(SampleShape? shape, long frameCount)
    {
        long last = Math.Max(0, frameCount - 1);

        if (shape is null)
            return new SampleWindow(0, last, 0, last, SampleLoopMode.None, false);

        var clamped = shape.Clone();
        clamped.Clamp();

        double start = clamped.Start * last;
        double end = clamped.End * last;

        // A window with nothing in it is almost certainly a mistake with the handles rather
        // than a request for silence, so it opens back out to the whole file.
        if (end - start < 1)
        {
            start = 0;
            end = last;
        }

        double loopStart = Math.Clamp(clamped.LoopStart * last, start, end);
        double loopEnd = Math.Clamp(clamped.LoopEnd * last, start, end);

        return new SampleWindow(start, end, loopStart, loopEnd, clamped.LoopMode, clamped.Reverse);
    }

    /// <summary>
    /// Moves the read head on by one step and says whether there is anything left to play.
    /// A looping window always has something left; a one-shot ends when it leaves the window.
    /// </summary>
    public static bool Advance(ref double position, ref int direction, double step, in SampleWindow window)
    {
        if (step <= 0) return true;

        position += direction >= 0 ? step : -step;

        if (!window.IsLooping)
            return position >= window.Start && position <= window.End;

        if (direction >= 0 && position >= window.LoopEnd)
        {
            if (window.Loop == SampleLoopMode.Forward)
            {
                position = Wrap(position, window.LoopStart, window.LoopLength);
                return true;
            }

            // Ping-pong turns round rather than jumping, so the overshoot is reflected back
            // in: at high pitches a step can be longer than the loop itself.
            position = Reflect(window.LoopEnd - (position - window.LoopEnd), window);
            direction = -1;
            return true;
        }

        if (direction < 0 && position <= window.LoopStart)
        {
            if (window.Loop == SampleLoopMode.Forward)
            {
                position = window.LoopEnd - Wrap(window.LoopStart - position, 0, window.LoopLength);
                return true;
            }

            position = Reflect(window.LoopStart + (window.LoopStart - position), window);
            direction = 1;
            return true;
        }

        return true;
    }

    private static double Wrap(double position, double from, double length)
    {
        double offset = (position - from) % length;
        if (offset < 0) offset += length;

        return from + offset;
    }

    private static double Reflect(double position, in SampleWindow window) =>
        Math.Clamp(position, window.LoopStart, window.LoopEnd);
}
