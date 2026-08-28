using System;

namespace JingleBox2.Audio.Plugins;

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
    /// Fills a block with what the plugin is playing, replacing whatever was in it. Runs on
    /// the audio thread.
    /// </summary>
    /// <remarks>
    /// Replacing rather than adding, because a plugin instrument is what the track is: there is
    /// nothing else in the buffer for it to be mixed with by the time this is called.
    /// </remarks>
    void Render(float[] buffer, int frames);
}
