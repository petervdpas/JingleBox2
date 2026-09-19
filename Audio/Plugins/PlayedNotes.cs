using System;
using System.Threading;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins;

/// <inheritdoc/>
/// <remarks>
/// A ring of a fixed size with two counters: how many have ever been put in and how many have
/// ever been taken out. The difference is what is waiting, and neither counter is ever wrapped,
/// so nothing has to be locked for one thread to add while the other takes.
/// </remarks>
public sealed class PlayedNotes : IPlayedNotes
{
    /// <summary>
    /// How many notes can wait at once.
    /// </summary>
    /// <remarks>
    /// A drum machine at a fast tempo plays a few dozen a second and whoever empties this runs
    /// many times a second, so this is a second or two of room. Enough that a pause in the
    /// emptying loses nothing, and small enough to sit in a cache.
    /// </remarks>
    private const int Room = 512;

    private readonly TrackPlayedNote[] _waiting = new TrackPlayedNote[Room];

    /// <summary>How many have ever been put in, and how many ever taken out.</summary>
    private long _in, _out;

    /// <inheritdoc/>
    public void Took(int track, ReadOnlySpan<PlayedNote> notes)
    {
        long taken = Interlocked.Read(ref _out);
        long put = _in;

        foreach (var note in notes)
        {
            if (put - taken >= Room) break;

            _waiting[put % Room] = new TrackPlayedNote(track, note);
            put++;
        }

        Interlocked.Exchange(ref _in, put);
    }

    /// <inheritdoc/>
    public int Take(Span<TrackPlayedNote> into)
    {
        long put = Interlocked.Read(ref _in);
        long taken = _out;

        int many = 0;

        while (taken < put && many < into.Length)
        {
            into[many++] = _waiting[taken % Room];
            taken++;
        }

        Interlocked.Exchange(ref _out, taken);

        return many;
    }

    /// <inheritdoc/>
    public long Counted => Interlocked.Read(ref _in);

    /// <inheritdoc/>
    public void Forget() => Interlocked.Exchange(ref _out, Interlocked.Read(ref _in));
}
