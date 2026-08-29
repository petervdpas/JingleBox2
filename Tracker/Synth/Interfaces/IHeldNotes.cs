using System;

namespace JingleBox2.Tracker.Synth.Interfaces;

/// <summary>
/// The notes one plugin has been told to start and has not been told to end.
/// </summary>
/// <remarks>
/// A plugin holds its own voices, so the mixer cannot look inside it to find out what is
/// sounding: what it can do is remember what it said. That memory is what makes a per-note
/// ending possible at all. Without it the only thing the host can say is
/// <c>AllNotesOff</c>, which is right for one note a track and wrong for anything else, since
/// it takes the whole chord down to end one note of it.
///
/// Two kinds of note are held together, because a plugin cannot tell them apart either. A note
/// from a pattern is held until something ends it, and is written down with no moment. A note
/// played by hand has no key coming up that this side can rely on, so it is written down with
/// the moment it should be let go of and the render lets it go when that moment passes.
///
/// Bounded, and it steals the oldest when it is full, which is the same answer
/// <see cref="TrackMixer.MaxVoices"/> gives and for the same reason: a limit that grows is a
/// limit that fails somewhere further away, on the audio thread, once a song has been left
/// sustaining for an hour.
///
/// Every method that lets go writes the notes out to the caller rather than ending them itself.
/// The mixer holds a lock while it decides and may not hold one while it talks to a plugin, and
/// this type is the piece in between, which is also what lets it be put a question to without a
/// plugin, a process or a sound card.
/// </remarks>
public interface IHeldNotes
{
    /// <summary>How many notes are being held.</summary>
    int Count { get; }

    /// <summary>Whether this note is one of them.</summary>
    bool Holds(int semitone);

    /// <summary>
    /// Writes down a note that is about to start, and answers the one that had to be let go of
    /// to make room for it, or -1 where there was room.
    /// </summary>
    /// <remarks>
    /// A note already written down is moved to the newest rather than written down twice: the
    /// caller has just ended and restarted it, which is one note and not two.
    /// </remarks>
    /// <param name="semitone">The note.</param>
    /// <param name="until">
    /// The moment it should be let go of, in <see cref="Environment.TickCount64"/>, or zero for
    /// a note that is held until something ends it.
    /// </param>
    int Press(int semitone, long until = 0);

    /// <summary>Forgets one note, and says whether it was there to forget.</summary>
    bool Let(int semitone);

    /// <summary>Forgets every note and writes them into <paramref name="into"/>, answering how many.</summary>
    /// <remarks>What a pattern's OFF and a transport stop both do.</remarks>
    int LetAll(Span<int> into);

    /// <summary>Forgets the notes whose moment has passed, and writes them out.</summary>
    /// <remarks>
    /// Runs on the audio thread once a block. A note with no moment is never expired, which is
    /// how a note from a pattern outlives one played by hand.
    /// </remarks>
    int LetExpired(long now, Span<int> into);
}
