using JingleBox2.Tracker;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Sounds a single note on an instrument. The tracker owns the audio engine, so the rack
/// borrows it through this rather than opening a second one.
/// </summary>
public interface IInstrumentAudition
{
    /// <summary>Sounds one note on that instrument.</summary>
    /// <param name="instrument">What to play it on.</param>
    /// <param name="note">Which note.</param>
    /// <param name="volume">How hard, or none for the instrument's own level.</param>
    /// <param name="holdSeconds">
    /// How long a generated sound holds before it lets go of itself. A key that comes up hands in
    /// <see cref="TrackerPlayer.HeldNoteSeconds"/>, which is a safety net rather than a length, so
    /// the note sounds for as long as the key is held; a press with nothing to let go of it keeps
    /// the short moment.
    /// </param>
    /// <returns>
    /// How long the note will sound. A generated sound holds for what it was asked to; a recording
    /// holds until it has been heard right through, which is what a keyboard needs to know to
    /// light its key and a picture needs to run its cursor.
    /// </returns>
    double Audition(TrackerInstrument instrument, Note note, int volume,
                    double holdSeconds = TrackerPlayer.PreviewHoldSeconds);

    /// <summary>
    /// Lets go of one note played by hand: the same thing a pattern's OFF does to a track.
    /// </summary>
    /// <remarks>
    /// One note and not the instrument, because two keys held down are two notes and letting go
    /// of one must not silence the other. What was started goes into its release rather than
    /// stopping dead, so a sound with a long tail keeps its tail.
    /// </remarks>
    void Let(TrackerInstrument instrument, Note note);

    /// <summary>
    /// Stops whatever that instrument is sounding by hand, leaving a pattern's notes alone.
    /// </summary>
    void Silence(TrackerInstrument instrument);

    /// <summary>
    /// The pitch wheel beside the keys, for notes played by hand on this instrument.
    /// </summary>
    /// <remarks>
    /// Borrowed through here like the notes themselves and for the same reason: the tracker owns
    /// the engine, and a wheel without the notes it is about would be a bend nothing is bending.
    ///
    /// The instrument is named because a note played here belongs to no track: what is under the
    /// hand on the rack may be in no song at all, so there is no track number that could stand
    /// for it.
    /// </remarks>
    /// <param name="instrument">What is being played by hand.</param>
    /// <param name="lean">Where the wheel is, -1 to 1.</param>
    void Bend(TrackerInstrument? instrument, double lean);

    /// <summary>And the modulation wheel. See <see cref="Bend"/>.</summary>
    /// <param name="instrument">What is being played by hand.</param>
    /// <param name="amount">How far up the wheel is, 0 to 1.</param>
    void Modulate(TrackerInstrument? instrument, double amount);

    /// <summary>
    /// How far through its recording the sample voice on that track is, as a fraction of the
    /// whole file, or -1 when nothing is playing one.
    /// </summary>
    /// <remarks>
    /// Asked rather than told, because the audio thread cannot be made to raise events forty
    /// times a second and the panel is going to redraw on its own clock regardless.
    /// </remarks>
    double SamplePosition(int track);

    /// <summary>
    /// The live plugin behind a plugin on the rack or on a track, loaded if it is not already
    /// open. Null for any other kind, and for a plugin this host cannot open.
    /// </summary>
    /// <remarks>
    /// The editor needs the running plugin, not a description of one: the knobs it shows are the
    /// plugin's own, and the patch it saves has to be read out of the thing that is making the
    /// sound. A plugin's parameters cannot be listed without it, either; there is no manifest to
    /// read and Serum answers with 2622 of them.
    ///
    /// <see cref="IPluginParameters"/> rather than <see cref="IPluginInstrument"/>, because an
    /// effect is on the rack too and everything above this wants the same two things of both:
    /// the knobs and the patch. Playing notes into one is a question only an instrument answers
    /// and is asked elsewhere.
    /// </remarks>
    IPluginParameters? PluginFor(TrackerInstrument instrument);
}
