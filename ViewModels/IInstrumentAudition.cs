using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

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
    /// The live plugin behind a plugin instrument, loaded if it is not already open. Null for
    /// any other kind, and for a plugin this host cannot play.
    /// </summary>
    /// <remarks>
    /// The editor needs the running plugin, not a description of one: the knobs it shows are
    /// the plugin's own, and the patch it saves has to be read out of the thing that is
    /// making the sound.
    /// </remarks>
    IPluginInstrument? PluginFor(TrackerInstrument instrument);
}
