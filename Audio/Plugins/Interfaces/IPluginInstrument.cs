using System;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// A plugin that makes sound from notes rather than from audio.
/// </summary>
/// <remarks>
/// An instrument is not a voice. The tracker's own instruments make one voice per sounding
/// note; a plugin is polyphonic inside itself and wants to be told about every note on a track,
/// so there is one of these per track rather than one per note.
///
/// Notes are queued and handed over at the start of a block, for the same reason parameter
/// moves are: that is when a plugin is willing to hear about them.
///
/// VST3 is the only format that can be one of these here. CLAP instruments are not hosted, which
/// is why nothing noticed for a long time that CLAP had no state at all.
/// </remarks>
public interface IPluginInstrument : IPluginParameters, IDisposable
{
    /// <summary>Starts a note. Velocity runs nought to one.</summary>
    void NoteOn(int semitone, float velocity);

    /// <summary>Ends a note that was started. Unknown notes are ignored rather than guessed at.</summary>
    void NoteOff(int semitone);

    /// <summary>Ends everything sounding, for a stop button or a track being emptied.</summary>
    void AllNotesOff();

    /// <summary>
    /// The pitch wheel beside the keys, minus one for all the way down to one for all the way up.
    /// </summary>
    /// <remarks>
    /// **Sent as it arrived, and the range is the plugin's.** How far a wheel bends is a setting
    /// on the plugin's own face, which is where whoever plays it will look for it, so a host that
    /// applied a range of its own would be bending twice and neither number would match what is
    /// on the screen. That is the opposite of what one of our own machines wants, where the range
    /// is <c>TrackerInstrument.BendSemitones</c> and there is no other face to disagree with.
    ///
    /// Nought is the wheel at rest, which is where a plugin is until one moves. Nothing by
    /// default, so a plugin that has not been told about the wheels leaves its notes at the
    /// pitch they were played at rather than having a host invent something.
    /// </remarks>
    /// <param name="lean">Where the wheel is, -1 to 1.</param>
    void Bend(double lean)
    {
    }

    /// <summary>And the modulation wheel, nought for nothing up to one.</summary>
    /// <remarks>
    /// What it modulates is the plugin's own business and nothing a host can ask about: it is
    /// whichever parameter the plugin says its wheel is, which is the one thing here that is
    /// genuinely not ours to decide. See <see cref="Bend"/> for why nothing is scaled on the way.
    /// </remarks>
    /// <param name="amount">How far up the wheel is, 0 to 1.</param>
    void Modulate(double amount)
    {
    }

    /// <summary>
    /// Fills a block with what the plugin is playing, replacing whatever was in it. Runs on
    /// the audio thread.
    /// </summary>
    /// <remarks>
    /// Replacing rather than adding, because a plugin instrument is what the track is: there is
    /// nothing else in the buffer for it to be mixed with by the time this is called.
    /// </remarks>
    void Render(float[] buffer, int frames);

    /// <summary>
    /// The notes the plugin played of its own accord during the block just rendered, and how
    /// many there were.
    /// </summary>
    /// <remarks>
    /// A drum machine running its own pattern, an arpeggiator, a plugin echoing what it was
    /// sent: each hands these over at the end of a block, so the host can play them on to
    /// somewhere else. Read straight after <see cref="Render"/> and before the next one, since
    /// what is kept is one block's worth.
    ///
    /// Answers nothing where a plugin plays no notes of its own, which is nearly all of them.
    /// </remarks>
    /// <param name="into">Where they go. Nothing past the end of it is written.</param>
    int Played(Span<PlayedNote> into);
}
