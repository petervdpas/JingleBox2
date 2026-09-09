using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// The knobs of a loaded plugin, whatever the plugin is doing with them.
/// </summary>
/// <remarks>
/// An effect and an instrument have nothing in common in the audio path and everything in
/// common here, so this is what the knob controls are written against. It is also what makes
/// one parameter panel serve both.
/// </remarks>
public interface IPluginParameters
{
    /// <summary>Which plugin this is, for a title bar and for writing the chain down.</summary>
    PluginInfo Info { get; }

    /// <summary>Everything this plugin exposes, in the order it lists them.</summary>
    IReadOnlyList<PluginParameter> Parameters();

    /// <summary>What a parameter is set to right now.</summary>
    /// <remarks>
    /// For a plugin in a process of its own this is a round trip, so it is asked when something
    /// says a value moved rather than polled. Four of them per device per tick, which is what a
    /// chain block printing its first controls would cost, is a price nobody asked for.
    /// </remarks>
    double ValueOf(uint id);

    /// <summary>What every parameter stands at, by id.</summary>
    /// <remarks>
    /// The whole point of it is that a plugin in a process of its own answers this in one round
    /// trip rather than one per parameter. Writing a chain down walks every parameter, and a
    /// plugin with five thousand of them made that five thousand round trips: about a second of
    /// the caller standing still, on every song save and every undo, measured pushing a block of
    /// audio past its own budget.
    ///
    /// The walk here is what a plugin loaded into this process should do, since there is no
    /// crossing to save and <see cref="ValueOf"/> is a call. A parameter that cannot be read is
    /// left out rather than written down as nought, so a caller can tell silence from a reading.
    /// </remarks>
    IReadOnlyDictionary<uint, double> Values()
    {
        var values = new Dictionary<uint, double>();

        foreach (var parameter in Parameters()) values[parameter.Id] = ValueOf(parameter.Id);

        return values;
    }

    /// <summary>How the plugin words a value: "-6.0 dB" rather than -6.</summary>
    /// <remarks>
    /// The only way a VST3 parameter can be printed at all, since every one of them is nought to
    /// one whatever it means. Plenty of plugins answer with "50.000000" and the wording has to be
    /// cut down before anybody sees it.
    /// </remarks>
    string TextFor(uint id, double value);

    /// <summary>Moves a parameter.</summary>
    /// <remarks>
    /// Queued rather than written: both standards expect values to arrive at the start of a
    /// block rather than whenever a knob happens to be dragged.
    /// </remarks>
    void SetValue(uint id, double value);

    /// <summary>Moves every one of these parameters.</summary>
    /// <remarks>
    /// <see cref="Values"/> the other way round, and it is here for the same reason: a plugin in
    /// a process of its own answers this in one crossing, where the walk below is one apiece. A
    /// chain being put back writes every parameter it saved, so opening a song or adding a plugin
    /// with five thousand of them cost five thousand round trips.
    ///
    /// The walk is what a plugin loaded into this process should do, since there is no crossing
    /// to save. Order is the caller's business: what arrives here is applied as it is given.
    /// </remarks>
    void SetValues(IReadOnlyDictionary<uint, double> values)
    {
        foreach (var pair in values) SetValue(pair.Key, pair.Value);
    }

    /// <summary>
    /// The plugin moving one of its own knobs, in its own window. The parameter and its new
    /// value.
    /// </summary>
    /// <remarks>
    /// Without this a plugin's own interface is a picture: the host never learns what was
    /// changed, so nothing is marked as worth saving, and for VST3 the sound does not even
    /// follow, because the half that draws and the half that plays only ever hear about a
    /// parameter through the host.
    ///
    /// The two standards report it at different moments. VST3 says so at once, through
    /// <c>IComponentHandler::performEdit</c>. CLAP only hands values back at the end of a block,
    /// so a CLAP plugin with its window open is read again forty times a second instead.
    ///
    /// Raised on whichever thread the plugin was on, which is not the drawing one. Whoever
    /// listens has to get itself back there.
    /// </remarks>
    event Action<uint, double>? Edited;

    /// <summary>
    /// The plugin saying that everything about it may have changed at once.
    /// </summary>
    /// <remarks>
    /// What loading a preset looks like from the host's side. A knob moved one at a time comes
    /// through <see cref="Edited"/>; a whole patch arriving does not, because no plugin reports
    /// two thousand parameter moves for it. Both standards have a way of saying it and both say
    /// the same thing: read me again, and whatever you were holding about me is out of date.
    /// </remarks>
    event Action? Reloaded;

    /// <summary>
    /// Everything inside the plugin, as a lump to keep. Not the same as its parameters: a
    /// Serum patch is wavetables and samples as much as it is knob positions, and none of
    /// that is a parameter.
    /// </summary>
    /// <remarks>
    /// Here rather than on <see cref="IPluginInstrument"/>, where it used to be, because
    /// wanting a patch back is nothing to do with what the plugin is being used as. Serum is
    /// the same program whether a track plays it or a track's audio goes through it, and its
    /// preset was in both cases the thing that was not saved. The two classes that host
    /// plugins each implement both interfaces already, so this moved no code.
    ///
    /// A plugin asked for its lump twice is under no obligation to answer the same bytes, so
    /// two chains are never compared by their patches. It is a round trip and a third of a
    /// megabyte, so it is asked for where a save is a save.
    /// </remarks>
    byte[] SaveState();

    /// <summary>Puts a saved lump back. Anything unreadable is ignored.</summary>
    /// <remarks>
    /// Put back before the parameters rather than after: a patch moves every parameter at once,
    /// so the values written afterwards are either agreement or the correction for a plugin
    /// whose state did not come back whole.
    /// </remarks>
    void LoadState(byte[]? state);
}
