using System;
using System.Collections.Generic;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Config;

/// <inheritdoc/>
public sealed class MixerDesk : IMixerDesk
{
    /// <summary>The four, in the order the desk draws them.</summary>
    private readonly DeskStrip[] _strips;

    /// <summary>
    /// Builds the desk over the settings and the engine's busses.
    /// </summary>
    /// <param name="settings">The block the desk is a section of.</param>
    /// <param name="monitor">What the recording input is heard through.</param>
    /// <param name="takes">The bus a take being auditioned goes onto.</param>
    /// <param name="pads">And the one every pad goes onto.</param>
    /// <param name="output">Everything, on its way out of the machine.</param>
    /// <param name="gain">How a reading in decibels becomes an amplitude.</param>
    /// <param name="inputLevel">
    /// Where the recording input's own fader lives, which is a gain on what is coming in rather
    /// than a level on what is going out. Read and written through whoever owns it, so the page
    /// showing it hears about a move made on the mixer.
    /// </param>
    /// <param name="writeInputLevel">The other half of that, for a move made on the desk.</param>
    public MixerDesk(
        ISettingsBlock settings,
        Audio.Interfaces.IOutputBus monitor,
        Audio.Interfaces.IOutputBus takes,
        Audio.Interfaces.IOutputBus pads,
        Audio.Interfaces.IOutputBus output,
        UI.Interfaces.IGainScale gain,
        Func<double> inputLevel,
        Action<double> writeInputLevel)
    {
        var kept = settings.Config.MixerDesk;

        void Moved() => settings.Moved();

        In = new DeskStrip(kept.In, monitor, gain, inputLevel, writeInputLevel, Moved);
        Play = new DeskStrip(kept.Play, takes, gain, Moved);
        Pads = new DeskStrip(kept.Pads, pads, gain, Moved);
        Master = new DeskStrip(kept.Master, output, gain, Moved);

        _strips = new[] { (DeskStrip)In, (DeskStrip)Play, (DeskStrip)Pads, (DeskStrip)Master };
    }

    /// <inheritdoc/>
    public IDeskStrip In { get; }

    /// <inheritdoc/>
    public IDeskStrip Play { get; }

    /// <inheritdoc/>
    public IDeskStrip Pads { get; }

    /// <inheritdoc/>
    public IDeskStrip Master { get; }

    /// <inheritdoc/>
    public IReadOnlyList<IDeskStrip> Strips => _strips;

    /// <summary>
    /// Puts the whole desk back onto the busses, which is what a start is.
    /// </summary>
    /// <remarks>
    /// Once, after the engine is open. Before this there was nothing to put back: the desk was
    /// fields on the busses and came up at unity every morning however it had been left.
    /// </remarks>
    public void Restore()
    {
        foreach (var strip in _strips) strip.Restore();
    }
}
