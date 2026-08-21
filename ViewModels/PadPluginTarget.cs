using JingleBox2.Audio;
using JingleBox2.Audio.Plugins;
using System.Globalization;

namespace JingleBox2.ViewModels;

/// <summary>A pad as a plugin slot: the effect sits on that pad's playback.</summary>
/// <remarks>
/// A pad plays whatever file it was given, at that file's own rate, so the plugin is built for
/// the rate the pad is actually running at rather than the device's. A pad with nothing loaded
/// yet has no rate to report, and the effect is built for the usual one until it does.
/// </remarks>
public sealed class PadPluginTarget : IPluginHost
{
    /// <summary>What a pad's plugin is built for before the pad has played anything.</summary>
    public const int AssumedSampleRate = 44100;

    /// <summary>A pad's blocks are small; this is well past anything BASS asks for.</summary>
    public const int MaxFrames = 2048;

    private readonly IAudioEngine _audio;
    private readonly int _pad;

    public PadPluginTarget(IAudioEngine audio, int pad)
    {
        _audio = audio;
        _pad = pad;
    }

    public string Label => "Pad " + (_pad + 1).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>The pad's chain, put on the pad the first time anything asks for it.</summary>
    public PluginChain Chain
    {
        get
        {
            if (_audio.GetPadInsert(_pad) is PluginChain existing) return existing;

            var chain = new PluginChain();
            _audio.SetPadInsert(_pad, chain);

            return chain;
        }
    }

    public int SampleRate
    {
        get
        {
            int rate = _audio.PadSampleRate(_pad);
            return rate > 0 ? rate : AssumedSampleRate;
        }
    }
}
