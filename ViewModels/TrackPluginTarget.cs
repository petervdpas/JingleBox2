using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>A tracker track as an effect host: the chain runs on that track's own bus.</summary>
/// <remarks>
/// A track number and nothing else, so the same object serves whichever song is open and can be
/// made again whenever the cursor moves. Minus one is the master, which is a strip without being
/// a track: it is not in the song's mix, nothing that walks the tracks reaches it by counting,
/// and it does not move when they are reordered.
/// </remarks>
public sealed class TrackPluginTarget : IPluginHost
{
    /// <summary>Where the buses are, since a track's chain belongs to the player.</summary>
    private readonly TrackerPlayer _player;

    /// <summary>Which strip: nought upwards for the tracks, minus one for the master.</summary>
    private readonly int _track;

    /// <summary>Names a strip as somewhere a chain can run.</summary>
    public TrackPluginTarget(TrackerPlayer player, int track)
    {
        _player = player;
        _track = track;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The same two-digit form the pattern header and the mixer use, or the master by name.
    ///
    /// The master is a strip without being a track, and it is named rather than numbered for the
    /// same reason it is strip minus one everywhere else: numbering it would make it a track the
    /// day somebody adds a thirty-third.
    /// </remarks>
    public string Label => _track < 0
        ? "MASTER"
        : "TR-" + (_track + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    /// <remarks>
    /// The player makes one on the first ask, so a track that has never had an effect on it is
    /// not a special case here. It is also the reason the audio engine does not rest while any
    /// track has a chain: a plugin has to be given blocks or it cannot finish a delay's tail.
    /// </remarks>
    public PluginChain Chain => _player.ChainFor(_track);

    /// <inheritdoc/>
    /// <remarks>
    /// One rate for the whole song, since every track is summed into one stream. Changing the
    /// output device takes that stream with it, which is why the player checks its stream is
    /// really still running rather than trusting the handle it was given.
    /// </remarks>
    public int SampleRate => _player.SampleRate;
}
