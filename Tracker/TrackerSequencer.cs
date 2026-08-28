using System;
using System.Collections.Generic;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// The memory is two arrays sized once at construction, so nothing here allocates per line. The
/// two static walks below are on this class rather than on the contract because they hold
/// nothing: where the next line is is a fact about the song and the position, and both are
/// handed in.
/// </remarks>
public sealed class TrackerSequencer : ITrackerSequencer
{
    /// <summary>
    /// What each track last played, so a note with a blank instrument column knows what to
    /// sound. <see cref="TrackerCell.NoInstrument"/> until that track has played anything.
    /// </summary>
    private readonly int[] _lastInstrument;

    /// <summary>
    /// Where each track's volume column was last set. It stays there until something moves it,
    /// which is what lets a level be typed once and hold down the page.
    /// </summary>
    private readonly float[] _trackGain;

    /// <summary>
    /// Memory for that many tracks, clamped to what a song can have.
    /// </summary>
    /// <remarks>
    /// Clamped rather than trusted, since the count comes off a song file. The arrays are what
    /// every lookup here is bounded by, so a count out of range would be an index fault on the
    /// clock thread rather than a song that plays a track short.
    /// </remarks>
    public TrackerSequencer(int trackCount)
    {
        int tracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        _lastInstrument = new int[tracks];
        _trackGain = new float[tracks];
        Reset();
    }

    /// <inheritdoc/>
    public int TrackCount => _lastInstrument.Length;

    /// <inheritdoc/>
    public void Reset()
    {
        Array.Fill(_lastInstrument, TrackerCell.NoInstrument);
        Array.Fill(_trackGain, 1f);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A cell with no note in it but something in the volume or effect column becomes an Adjust
    /// rather than a Trigger, which changes the voice already sounding instead of starting
    /// another. That is how a fade written down the volume column works, and it is why an
    /// event carries the track's remembered instrument even when nothing is being started.
    /// </remarks>
    public IReadOnlyList<TrackerEvent> EventsFor(Song song, TrackerPosition position)
    {
        var events = new List<TrackerEvent>();

        var pattern = song.PatternAt(position.OrderIndex);
        if (pattern == null || position.Line < 0 || position.Line >= pattern.Lines)
            return events;

        int tracks = Math.Min(pattern.TrackCount, TrackCount);
        for (int track = 0; track < tracks; track++)
        {
            var cell = pattern[position.Line, track];
            if (cell.IsEmpty) continue;

            if (cell.Note.IsOff)
            {
                events.Add(TrackerEvent.Stop(track));
                continue;
            }

            if (cell.Instrument != TrackerCell.NoInstrument)
                _lastInstrument[track] = cell.Instrument;

            if (cell.Gain is float gain)
                _trackGain[track] = gain;

            if (cell.Note.IsPlayable)
            {
                events.Add(new TrackerEvent(
                    track, TrackerEventKind.Trigger, cell.Note,
                    _lastInstrument[track], _trackGain[track], cell.Effect));
            }
            else
            {
                events.Add(new TrackerEvent(
                    track, TrackerEventKind.Adjust, Note.Empty,
                    _lastInstrument[track], _trackGain[track], cell.Effect));
            }
        }

        return events;
    }

    /// <summary>
    /// The step after this one, or null at the end of the song when not looping.
    /// Walks off the end of a pattern into the next order entry.
    /// </summary>
    /// <remarks>
    /// By the order entry rather than by the pattern, since the same pattern can be in a song
    /// twice and what follows it is a different answer each time. An order entry pointing at no
    /// pattern ends the pass rather than being skipped, which is what an empty song does.
    /// </remarks>
    public static TrackerPosition? Advance(Song song, TrackerPosition position, bool loop)
    {
        var pattern = song.PatternAt(position.OrderIndex);
        if (pattern == null) return loop ? TrackerPosition.Start : null;

        if (position.Line + 1 < pattern.Lines)
            return position with { Line = position.Line + 1 };

        int nextOrder = position.OrderIndex + 1;
        if (nextOrder < song.Order.Count)
            return new TrackerPosition(nextOrder, 0);

        return loop ? TrackerPosition.Start : null;
    }

    /// <summary>The step after this one, staying inside one pattern. Used by pattern-loop mode.</summary>
    public static TrackerPosition? AdvanceWithinPattern(Song song, TrackerPosition position, bool loop)
    {
        var pattern = song.PatternAt(position.OrderIndex);
        if (pattern == null) return null;

        if (position.Line + 1 < pattern.Lines)
            return position with { Line = position.Line + 1 };

        return loop ? position with { Line = 0 } : null;
    }
}
