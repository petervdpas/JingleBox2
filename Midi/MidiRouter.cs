using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Midi;

/// <summary>
/// The one place everything a hand does to a note arrives, and from which everything that
/// listens is told.
/// </summary>
/// <remarks>
/// **In**: a hardware port, after its codec and after the job that port was given in SETTINGS; a
/// drawn keyboard; the letter rows on the computer keyboard; the drawn wheels; and the pattern
/// playing a song. **Out**: our own voices through the mixer, a plugin, a MIDI port, and the
/// lights that show which keys are down.
///
/// It exists because those were separate paths carrying the same events. A key on the hardware
/// reached the music through a service, a codec, a dispatcher and a router; the same key clicked
/// on the screen was sounded by the panel that drew it and told nothing else; and what a note
/// went on to reach was a third arrangement again. One gesture with two implementations chosen
/// by what made it is the fault this codebase names everywhere: either can break while the other
/// works, and neither can be read out of one log. See <c>docs/midi-router.md</c>.
///
/// It is an <see cref="IPlays"/> itself, which is the whole of its shape: it is told what is
/// played and it tells the others. So one can stand in front of another, a monitor can sit in
/// the middle and pass everything through, and a test can hand it a list and read what came out.
/// It decides nothing about sound. Which instrument a track plays, what a machine does with a
/// modulation wheel and how far a bend goes are all downstream.
///
/// Called on whichever thread the event arrived on, and nothing here changes that: a note is not
/// posted anywhere, since a note handed to the drawing thread arrives at the frame rate. What it
/// tells is told on the caller's thread, and each listener owns getting to its own.
/// </remarks>
public sealed class MidiRouter : IPlays
{
    /// <summary>
    /// Wherever the hand is, rather than a track named outright.
    /// </summary>
    /// <remarks>
    /// What a keyboard under somebody's hand means: the half of the application in front, and
    /// within the tracker the track the cursor is in. A track's own MIDI in names its track
    /// instead, and so does the pattern, because both know which one they are about.
    ///
    /// Minus one, which is what this codebase already means by "no track" in the mixer and by
    /// the master in the strips: a number that cannot collide with a real one.
    /// </remarks>
    public const int TheHand = -1;

    /// <summary>Everything that is told what is played, in the order it is told.</summary>
    private readonly IReadOnlyList<IPlays> _heard;

    /// <param name="heard">
    /// What to tell, in order. The engine comes first and the pictures after it, since what has
    /// to be right on time is the sound.
    /// </param>
    public MidiRouter(params IPlays[] heard) => _heard = heard ?? Array.Empty<IPlays>();

    /// <inheritdoc/>
    public void Press(int track, Note note, int volume) =>
        Tell("press", one => one.Press(track, note, volume));

    /// <inheritdoc/>
    public void Let(int track, Note note) => Tell("let go", one => one.Let(track, note));

    /// <inheritdoc/>
    public void Bend(int track, double lean) => Tell("bend", one => one.Bend(track, lean));

    /// <inheritdoc/>
    public void Modulate(int track, double amount) =>
        Tell("modulate", one => one.Modulate(track, amount));

    /// <summary>
    /// Tells every listener, and one that throws costs itself and nothing else.
    /// </summary>
    /// <remarks>
    /// **A listener that falls over may not take the music with it**, which is a rule this
    /// codebase has already paid for twice: once when a plugin crashing took the host down, and
    /// once when a drawn wheel reading one of its own properties from the port's thread threw,
    /// the exception left through the port's own delivery, and the device was dead for the rest
    /// of the session. Keys and all, from touching a strip.
    ///
    /// Said in full, with the stack, because the listener is several classes away and which one
    /// is the entire question. Swallowed quietly this would be worse than the crash: a note that
    /// reaches three of four listeners and says nothing is a fault nobody can find.
    /// </remarks>
    /// <param name="what">What was being done, for the line where one of them fails.</param>
    /// <param name="doing">What to ask of each listener.</param>
    private void Tell(string what, Action<IPlays> doing)
    {
        foreach (var one in _heard)
        {
            try
            {
                doing(one);
            }
            catch (Exception carrying)
            {
                Log.Write(LogArea.Midi, () =>
                    "router: " + one.GetType().Name + " THREW on a " + what
                    + ", so it heard nothing and the rest were told anyway: " + carrying);
            }
        }
    }
}
