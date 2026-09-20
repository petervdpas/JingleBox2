using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Something that plays what a hand is doing, or passes it to something that does.
/// </summary>
/// <remarks>
/// One contract for both ends of <see cref="MidiRouter"/>, because a source and a sink are the
/// same shape: a keyboard says a key went down, and a plugin is told a key went down, in the same
/// words. That is what lets the router be one of these itself, and what lets a monitor stand in
/// front of another one and pass everything through untouched.
///
/// **It carries where the event is going**, which is the one thing every source knows and no sink
/// should have to work out again. A track's MIDI in names its track; a keyboard under somebody's
/// hand names <see cref="MidiRouter.TheHand"/>, which is wherever that hand is playing: the half
/// of the application in front, and within the tracker the track the cursor is in.
///
/// The four members are what a hand can do to a note and nothing else. Which recording, which
/// machine and which voice are all downstream: this says a key went down on a track, and what
/// that sounds like is the business of whoever is listening.
///
/// Called on whichever thread the event arrived on, and there are three: a port's, the drawing
/// thread for a mouse or a letter, and the clock's for a pattern. Whoever implements it owns
/// getting to its own thread, and may not post a note to the drawing thread on the way: a note
/// that arrives at the frame rate is a note that is late.
/// </remarks>
public interface IPlays
{
    /// <summary>A key went down.</summary>
    /// <param name="track">Which track, or <see cref="MidiRouter.TheHand"/>.</param>
    /// <param name="note">Which note.</param>
    /// <param name="volume">How hard, as the volume column holds it.</param>
    void Press(int track, Note note, int volume);

    /// <summary>
    /// And came up.
    /// </summary>
    /// <remarks>
    /// Both halves always, even where a listener has nothing to write down for the second: a key
    /// coming up is the moment a light goes out and a sound is let go of, and anything that hears
    /// the first and not the second is a place a note can hang.
    /// </remarks>
    /// <param name="track">Which track, or <see cref="MidiRouter.TheHand"/>.</param>
    /// <param name="note">Which note.</param>
    void Let(int track, Note note);

    /// <summary>The pitch wheel moved, minus one for all the way down to one for all the way up.</summary>
    /// <remarks>
    /// A lean rather than a number of semitones, because how far a wheel bends is the
    /// instrument's own business and a hand has never heard of an instrument.
    /// </remarks>
    /// <param name="track">Which track, or <see cref="MidiRouter.TheHand"/>.</param>
    /// <param name="lean">Where the wheel is, -1 to 1.</param>
    void Bend(int track, double lean);

    /// <summary>And the modulation wheel, nought for nothing up to one.</summary>
    /// <param name="track">Which track, or <see cref="MidiRouter.TheHand"/>.</param>
    /// <param name="amount">How far up the wheel is, 0 to 1.</param>
    void Modulate(int track, double amount);
}
