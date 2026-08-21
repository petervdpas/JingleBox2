using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>
/// Sounds a single note on an instrument. The tracker owns the audio engine, so the library
/// borrows it through this rather than opening a second one.
/// </summary>
public interface IInstrumentAudition
{
    void Audition(TrackerInstrument instrument, Note note, int volume);

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
