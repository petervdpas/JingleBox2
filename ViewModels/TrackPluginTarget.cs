using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>A tracker track as an effect host: the chain runs on that track's own bus.</summary>
public sealed class TrackPluginTarget : IPluginHost
{
    private readonly TrackerPlayer _player;
    private readonly int _track;

    public TrackPluginTarget(TrackerPlayer player, int track)
    {
        _player = player;
        _track = track;
    }

    /// <summary>The same two-digit form the pattern header and the mixer use.</summary>
    public string Label => "TR-" + (_track + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    public PluginChain Chain => _player.ChainFor(_track);

    public int SampleRate => _player.SampleRate;
}
