using System;

namespace JingleBox2.Tracker.Synth;

/// <summary>
/// Where a voice reads from, in frames: the part of the file it plays and the part it repeats.
/// Worked out once when the note starts, since none of it moves while the note lasts.
/// </summary>
/// <param name="Start">The first frame of the part that sounds.</param>
/// <param name="End">The last frame of it.</param>
/// <param name="LoopStart">Where a repeat goes back to, held inside the window.</param>
/// <param name="LoopEnd">Where a repeat turns round or jumps, held inside the window.</param>
/// <param name="Loop">Whether it repeats at all, and which way round.</param>
/// <param name="Reverse">Whether the whole window is read backwards.</param>
public readonly record struct SampleWindow(
    double Start,
    double End,
    double LoopStart,
    double LoopEnd,
    SampleLoopMode Loop,
    bool Reverse)
{
    /// <summary>How much of the file one repeat covers, in frames.</summary>
    public double LoopLength => LoopEnd - LoopStart;

    /// <summary>A loop of no length is not a loop; it would be a division by zero and a stuck note.</summary>
    public bool IsLooping => Loop != SampleLoopMode.None && LoopLength > 0;

    /// <summary>Where a note starts reading: the far end when it plays backwards.</summary>
    public double Entry => Reverse ? End : Start;

    /// <summary>Which way the read head moves to begin with: forwards, or back from the end.</summary>
    public int Direction => Reverse ? -1 : 1;
}

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
public static class SamplePlayback
{
    /// <summary>
    /// Shorter than this and a window holds nothing that can be read.
    /// </summary>
    /// <remarks>
    /// One frame, because interpolation reads the frame after the one it is on: a window that
    /// cannot hold two positions has nothing to play between.
    /// </remarks>
    private const double MinWindowFrames = 1;

    /// <summary>
    /// Turns the fractions an instrument stores into frame positions in a file.
    /// </summary>
    /// <remarks>
    /// A window with nothing in it is almost certainly a mistake with the handles rather than a
    /// request for silence, so it opens back out to the whole file. An instrument with no shape
    /// at all is the whole file too, unlooped and forwards.
    /// </remarks>
    public static SampleWindow WindowFor(SampleShape? shape, long frameCount)
    {
        long last = Math.Max(0, frameCount - 1);

        if (shape is null)
            return new SampleWindow(0, last, 0, last, SampleLoopMode.None, false);

        var clamped = shape.Clone();
        clamped.Clamp();

        double start = clamped.Start * last;
        double end = clamped.End * last;

        if (end - start < MinWindowFrames)
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
    /// <remarks>
    /// A ping-pong turns round rather than jumping, so the overshoot is reflected back in: at
    /// high pitches a step can be longer than the loop itself, and a loop that simply jumped to
    /// its far end would lose whatever the step had gone past.
    /// </remarks>
    /// <param name="position">Where the head is, in frames. Moved.</param>
    /// <param name="direction">Which way it is going. Turned round by a ping-pong.</param>
    /// <param name="step">How far to move, in frames, which is the pitch expressed as a speed.</param>
    /// <param name="window">The part of the file being played, and how it repeats.</param>
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

    /// <summary>
    /// Brings a position that has run past the loop back inside it, however far past it went.
    /// </summary>
    /// <remarks>
    /// A modulo rather than a subtraction, since a step at a high pitch can be several loops
    /// long. The negative case is folded back up because a backwards loop reaches here too.
    /// </remarks>
    private static double Wrap(double position, double from, double length)
    {
        double offset = (position - from) % length;
        if (offset < 0) offset += length;

        return from + offset;
    }

    /// <summary>
    /// Where a ping-pong lands after turning round: inside the loop, whatever the arithmetic said.
    /// </summary>
    /// <remarks>
    /// A step longer than the loop reflects past the other end, so the result is held rather
    /// than trusted. Without it a very high note on a very short loop walks out of the window
    /// and the voice is never heard from again.
    /// </remarks>
    private static double Reflect(double position, in SampleWindow window) =>
        Math.Clamp(position, window.LoopStart, window.LoopEnd);
}
