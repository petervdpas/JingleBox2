using System;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// Where the notes plugins play of their own accord are put, and taken from again.
/// </summary>
/// <remarks>
/// A drum machine running its own pattern hands its notes back at the end of every block, on the
/// audio thread. What is done with them, sending them to a MIDI port, writing them into a
/// pattern, playing them on another track, happens on a thread that is allowed to take its time.
/// So they are put down here as they arrive and picked up from there.
///
/// One thread writes and one thread reads, which is what the ring inside it is built for. Full,
/// it drops what will not fit: a queue nobody is emptying is a queue whose notes have already
/// gone stale, and growing it on the audio thread is the one thing that must not happen.
/// </remarks>
public interface IPlayedNotes
{
    /// <summary>Puts down what one track's plugin played this block. Audio thread.</summary>
    /// <param name="track">The strip it is on.</param>
    /// <param name="notes">What it played.</param>
    void Took(int track, ReadOnlySpan<PlayedNote> notes);

    /// <summary>Takes what is waiting, oldest first, and says how many that was.</summary>
    /// <param name="into">Where they go. Nothing past the end of it is taken.</param>
    int Take(Span<TrackPlayedNote> into);

    /// <summary>Throws away whatever is waiting, for a transport stop or a song being closed.</summary>
    void Forget();
}
