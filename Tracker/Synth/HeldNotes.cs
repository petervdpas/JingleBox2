using System;
using JingleBox2.Tracker.Synth.Interfaces;

namespace JingleBox2.Tracker.Synth;

/// <inheritdoc/>
/// <remarks>
/// A fixed array and a count rather than a list, because every method here is called from the
/// mixer's lock and one of them from the audio thread: there is nothing to allocate and nothing
/// to collect. Oldest first, so stealing is a look at the front and the order is the order the
/// notes were pressed in.
/// </remarks>
public sealed class HeldNotes : IHeldNotes
{
    /// <summary>
    /// As many notes as one plugin is allowed to be holding at once.
    /// </summary>
    /// <remarks>
    /// Ten fingers and a pedal. Past this a sustaining part is not a chord any more, it is a
    /// leak, and the oldest note is the one nobody is listening to.
    /// </remarks>
    public const int Most = 16;

    /// <summary>The notes, oldest first, and the moment each is let go of. Only the first Count count.</summary>
    private readonly (int Semitone, long Until)[] _held = new (int, long)[Most];

    /// <inheritdoc/>
    public int Count { get; private set; }

    /// <inheritdoc/>
    public bool Holds(int semitone) => IndexOf(semitone) >= 0;

    /// <inheritdoc/>
    public int Press(int semitone, long until = 0)
    {
        int already = IndexOf(semitone);

        if (already >= 0) Take(already);

        int stolen = -1;

        if (Count == Most)
        {
            stolen = _held[0].Semitone;
            Take(0);
        }

        _held[Count++] = (semitone, until);

        return stolen;
    }

    /// <inheritdoc/>
    public bool Let(int semitone)
    {
        int at = IndexOf(semitone);

        if (at < 0) return false;

        Take(at);

        return true;
    }

    /// <inheritdoc/>
    public int LetAll(Span<int> into)
    {
        int written = 0;

        while (Count > 0 && written < into.Length)
        {
            into[written++] = _held[0].Semitone;
            Take(0);
        }

        return written;
    }

    /// <inheritdoc/>
    public int LetExpired(long now, Span<int> into)
    {
        int written = 0;

        for (int i = Count - 1; i >= 0 && written < into.Length; i--)
        {
            if (_held[i].Until == 0 || now < _held[i].Until) continue;

            into[written++] = _held[i].Semitone;

            Take(i);
        }

        return written;
    }

    /// <summary>Where a note is in the list, or -1.</summary>
    private int IndexOf(int semitone)
    {
        for (int i = 0; i < Count; i++)
        {
            if (_held[i].Semitone == semitone) return i;
        }

        return -1;
    }

    /// <summary>Takes one out and closes the gap, so the rest stay in the order they were pressed.</summary>
    private void Take(int at)
    {
        for (int i = at; i < Count - 1; i++) _held[i] = _held[i + 1];

        Count--;
    }
}
