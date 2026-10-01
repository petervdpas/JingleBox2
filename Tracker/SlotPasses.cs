using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Two counts, of the passes of the stretch being played and of the pattern, both starting at one,
/// which is the pass being played. Only one stretch can be under way at a time, since no two share
/// a line, so the count follows whichever stretch the song is in. Stretches are answered first, so
/// one ending on the last line finishes before the pattern goes round again, and every stretch
/// starts again on every pass of the pattern. Only the
/// clock thread asks, so nothing here is locked.
/// </remarks>
public sealed class SlotPasses : ISlotPasses
{
    /// <summary>The slot being counted, or none before the first line.</summary>
    private int _slot = -1;

    /// <summary>Which pass of the stretch being counted is being played.</summary>
    private int _stretchPass = 1;

    /// <summary>The first line of the stretch being counted, or negative for none.</summary>
    private int _loopFrom = -1;

    /// <summary>Which pass of the pattern is being played.</summary>
    private int _patternPass = 1;

    /// <inheritdoc/>
    public TrackerPosition? After(Song song, TrackerPosition played, bool wholePattern)
    {
        if (played.OrderIndex != _slot)
        {
            _slot = played.OrderIndex;
            _stretchPass = 1;
            _loopFrom = -1;
            _patternPass = 1;
        }

        var repeat = song.RepeatAt(played.OrderIndex);

        foreach (var loop in repeat.Lines())
        {
            if (played.Line != loop.To) continue;

            if (_loopFrom != loop.From)
            {
                _loopFrom = loop.From;
                _stretchPass = 1;
            }

            if (_stretchPass < loop.Times)
            {
                _stretchPass++;
                return new TrackerPosition(played.OrderIndex, loop.From);
            }

            _stretchPass = 1;
            _loopFrom = -1;
            break;
        }

        int lines = song.PatternAt(played.OrderIndex)?.Lines ?? 0;
        int last = wholePattern ? repeat.LastLine(lines) : lines - 1;

        if (played.Line < last) return null;

        if (wholePattern && _patternPass < repeat.Times)
        {
            _patternPass++;
            _stretchPass = 1;
            _loopFrom = -1;
            return new TrackerPosition(played.OrderIndex, 0);
        }

        _patternPass = 1;
        _stretchPass = 1;
        _loopFrom = -1;

        return null;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        _slot = -1;
        _stretchPass = 1;
        _loopFrom = -1;
        _patternPass = 1;
    }
}
