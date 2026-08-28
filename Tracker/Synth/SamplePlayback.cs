using System;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Synth.Interfaces;
using JingleBox2.Tracker.Synth.Records;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
public sealed class SamplePlayback : ISamplePlayback
{
    /// <summary>
    /// Shorter than this and a window holds nothing that can be read.
    /// </summary>
    /// <remarks>
    /// One frame, because interpolation reads the frame after the one it is on: a window that
    /// cannot hold two positions has nothing to play between.
    /// </remarks>
    private const double MinWindowFrames = 1;

    /// <inheritdoc/>
    /// <remarks>
    /// The minimum is applied to the loop as well as to the window, and had to be: a collapsed
    /// window reopens to the whole file, and the loop it was carrying was merely held inside the
    /// new one, so a shape whose two loop marks sat together came back as a loop half a frame
    /// long at frame nought. That is longer than nothing, which is the only thing
    /// <see cref="SampleWindow.IsLooping"/> refuses, so the voice looped inside a single frame
    /// and buzzed there until the note was let go. A loop with nothing in it to play is the
    /// whole window, the same answer the window itself gives.
    /// </remarks>
    public SampleWindow WindowFor(SampleShape? shape, long frameCount)
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

        if (loopEnd - loopStart < MinWindowFrames)
        {
            loopStart = start;
            loopEnd = end;
        }

        return new SampleWindow(start, end, loopStart, loopEnd, clamped.LoopMode, clamped.Reverse);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A step that is not a finite number ends the note rather than moving anything, which is
    /// the one place here that a nonsense value is treated as the end of something rather than
    /// as nothing at all. A step comes from a pitch ratio, so a step like that is a rate nobody
    /// can play; added to the position it makes the position nonsense too, and a looping voice
    /// then fails every bound it is asked about and reads as still sounding for ever. A one shot
    /// happened to escape, since running off the end is how a one shot finishes. Both stop now,
    /// and a voice that stops is a fault somebody can hear the end of.
    /// </remarks>
    public bool Advance(ref double position, ref int direction, double step, in SampleWindow window)
    {
        if (!double.IsFinite(step)) return false;

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
