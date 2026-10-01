using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Two counts, of the passes of the stretch and of the pattern, both starting at one, which is the
/// pass being played. The stretch is answered first, so a stretch ending on the last line finishes
/// before the pattern goes round again, and it starts again on every pass of the pattern. Only the
/// clock thread asks, so nothing here is locked.
/// </remarks>
public sealed class SlotPasses : ISlotPasses
{
    /// <summary>The slot being counted, or none before the first line.</summary>
    private int _slot = -1;

    /// <summary>Which pass of the stretch is being played.</summary>
    private int _stretchPass = 1;

    /// <summary>Which pass of the pattern is being played.</summary>
    private int _patternPass = 1;

    /// <inheritdoc/>
    public TrackerPosition? After(Song song, TrackerPosition played, bool wholePattern)
    {
        if (played.OrderIndex != _slot)
        {
            _slot = played.OrderIndex;
            _stretchPass = 1;
            _patternPass = 1;
        }

        var repeat = song.RepeatAt(played.OrderIndex);

        if (repeat.HasStretch && played.Line == repeat.To)
        {
            if (_stretchPass < repeat.StretchTimes)
            {
                _stretchPass++;
                return new TrackerPosition(played.OrderIndex, repeat.From);
            }

            _stretchPass = 1;
        }

        int lines = song.PatternAt(played.OrderIndex)?.Lines ?? 0;

        if (played.Line != lines - 1) return null;

        if (wholePattern && _patternPass < repeat.Times)
        {
            _patternPass++;
            _stretchPass = 1;
            return new TrackerPosition(played.OrderIndex, 0);
        }

        _patternPass = 1;
        _stretchPass = 1;

        return null;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _slot = -1;
        _stretchPass = 1;
        _patternPass = 1;
    }
}
