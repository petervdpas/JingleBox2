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
    /// <returns>
    /// How long the note will sound. A generated sound holds for a fixed moment; a recording
    /// holds until it has been heard right through, which is what a keyboard needs to know to
    /// light its key and a picture needs to run its cursor.
    /// </returns>
    double Audition(TrackerInstrument instrument, Note note, int volume);

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
