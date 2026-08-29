using System;
using System.Collections.Generic;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

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
    /// What each note column last played, so a note with a blank instrument column knows what
    /// to sound. <see cref="TrackerCell.NoInstrument"/> until it has played anything.
    /// </summary>
    /// <remarks>
    /// Per column and not per track, which is Renoise's arrangement and the only one that
    /// holds up once a column is a voice: a blank instrument column means the last one this
    /// voice played. Remembered per track, a chord written across three columns with a
    /// different instrument in the third would leave the next note in the first playing the
    /// third's. Typing a note fills its own instrument column, so a chord entered by hand or
    /// by keyboard carries the instrument in every column of it and this is never asked.
    ///
    /// A song with one column a track cannot tell the two arrangements apart, which is every
    /// song written before now.
    /// </remarks>
    private readonly int[] _lastInstrument;

    /// <summary>
    /// Where each note column's volume was last set. It stays there until something moves it,
    /// which is what lets a level be typed once and hold down the page.
    /// </summary>
    /// <remarks>
    /// Per column for a harder reason than the instrument: a chord is several voices and the
    /// volume column of one of them must not set the level of the others.
    /// </remarks>
    private readonly float[] _trackGain;

    /// <summary>How many note columns the memory has room for on each track.</summary>
    /// <remarks>
    /// The widest a track can be rather than the widest it is, so nothing has to be rebuilt
    /// when somebody adds a column while the transport is running.
    /// </remarks>
    private const int Columns = Song.MaxNoteColumns;

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
        _lastInstrument = new int[tracks * Columns];
        _trackGain = new float[tracks * Columns];
        Reset();
    }

    /// <inheritdoc/>
    public int TrackCount => _lastInstrument.Length / Columns;

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
            int columns = Math.Min(pattern.ColumnsOn(track), Columns);

            for (int column = 0; column < columns; column++)
            {
                var cell = pattern[position.Line, track, column];
                if (cell.IsEmpty) continue;

                if (cell.Note.IsOff)
                {
                    events.Add(TrackerEvent.Stop(track, column));
                    continue;
                }

                int at = track * Columns + column;

                if (cell.Instrument != TrackerCell.NoInstrument)
                    _lastInstrument[at] = cell.Instrument;

                if (cell.Gain is float gain)
                    _trackGain[at] = gain;

                events.Add(new TrackerEvent(
                    track, column,
                    cell.Note.IsPlayable ? TrackerEventKind.Trigger : TrackerEventKind.Adjust,
                    cell.Note.IsPlayable ? cell.Note : Note.Empty,
                    _lastInstrument[at], _trackGain[at], cell.Effect));
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
    ///
    /// A loop range over the order is answered before the end of the order is, and it loops
    /// whatever the <paramref name="loop"/> flag says. Marking a range is somebody saying "go
    /// round these" in as many words, where the flag is a standing preference about what happens
    /// when there is nothing else to play; a range that did nothing while the switch was off
    /// would be a mark on the screen with no effect and nothing to explain it.
    ///
    /// It is answered only at the last slot of the range, so playing from before it runs into it
    /// and then goes round, and playing from after it is not dragged backwards. Somebody who
    /// starts the transport past the range meant to hear what is past the range.
    /// </remarks>
    public static TrackerPosition? Advance(Song song, TrackerPosition position, bool loop)
    {
        var pattern = song.PatternAt(position.OrderIndex);
        if (pattern == null) return loop ? TrackerPosition.Start : null;

        if (position.Line + 1 < pattern.Lines)
            return position with { Line = position.Line + 1 };

        if (song.Loops(position.OrderIndex) && position.OrderIndex >= song.LoopLast)
            return new TrackerPosition(song.LoopFirst, 0);

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
