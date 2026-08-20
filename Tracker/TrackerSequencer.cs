using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker;

/// <summary>
/// Reads a song step by step and says what should happen, without touching audio.
/// Holds the per-track memory a tracker needs: a note with a blank instrument column
/// reuses whatever that track played last, which is how patterns stay readable.
/// </summary>
public sealed class TrackerSequencer
{
    private readonly int[] _lastInstrument;
    private readonly float[] _trackGain;

    public TrackerSequencer(int trackCount)
    {
        int tracks = Math.Clamp(trackCount, Song.MinTrackCount, Song.MaxTrackCount);
        _lastInstrument = new int[tracks];
        _trackGain = new float[tracks];
        Reset();
    }

    public int TrackCount => _lastInstrument.Length;

    /// <summary>Forgets the per-track memory. Called whenever playback restarts.</summary>
    public void Reset()
    {
        Array.Fill(_lastInstrument, TrackerCell.NoInstrument);
        Array.Fill(_trackGain, 1f);
    }

    /// <summary>
    /// What to do on this step. Only tracks with something to say produce an event, so a
    /// mostly empty pattern costs almost nothing to play.
    /// </summary>
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
                // No note, but the volume or effect column said something. Adjust in place.
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
