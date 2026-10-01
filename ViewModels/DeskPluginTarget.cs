using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// The desk's MASTER: everything this application plays goes through this chain on its way out
/// of the machine, the song, the pads, a take and the input being heard. It belongs to the
/// installation rather than to a song, so it is kept in the settings.
/// </remarks>
public sealed class DeskPluginTarget : IChainOwner
{
    /// <summary>The bus the chain is hung on.</summary>
    private readonly IOutputBus _bus;

    /// <summary>The chain, made once and kept on the bus across it being opened again.</summary>
    private readonly PluginChain _chain = new();

    /// <summary>Hangs a fresh chain on the bus.</summary>
    /// <param name="bus">The output bus, which is the desk's MASTER.</param>
    public DeskPluginTarget(IOutputBus bus)
    {
        _bus = bus;
        _bus.Insert = _chain;
    }

    /// <inheritdoc/>
    public string Label => "Master";

    /// <inheritdoc/>
    public PluginChain Chain => _chain;

    /// <inheritdoc/>
    public int SampleRate => _bus.Rate > 0 ? _bus.Rate : PadPluginTarget.AssumedSampleRate;
}
