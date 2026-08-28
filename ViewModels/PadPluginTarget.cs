using JingleBox2.Audio;
using JingleBox2.Audio.Plugins;
using System.Globalization;
using JingleBox2.Audio.Interfaces;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>A pad as a plugin slot: the effect sits on that pad's playback.</summary>
/// <remarks>
/// A pad plays whatever file it was given, at that file's own rate, so the plugin is built for
/// the rate the pad is actually running at rather than the device's. A pad with nothing loaded
/// yet has no rate to report, and the effect is built for the usual one until it does.
///
/// This holds a pad number and nothing else, and goes back to the engine for every answer, so
/// it is cheap to make and safe to throw away. The pad is the one host where reading the
/// plugins' patches has to be rationed: a pad is written down on every property it has, and a
/// level dragged is a hundred of those, so the pad reads its patches when its chain settles, on
/// the same 600ms tick that makes it save at all.
/// </remarks>
public sealed class PadPluginTarget : IPluginHost
{
    /// <summary>What a pad's plugin is built for before the pad has played anything.</summary>
    public const int AssumedSampleRate = 44100;

    /// <summary>A pad's blocks are small; this is well past anything BASS asks for.</summary>
    public const int MaxFrames = 2048;

    /// <summary>Where the pads live, since the chain hangs off the engine and not off this.</summary>
    private readonly IAudioEngine _audio;

    /// <summary>Which pad, counted from nought as everything below the screen counts them.</summary>
    private readonly int _pad;

    /// <summary>
    /// Names a pad as somewhere a chain can run.
    /// </summary>
    /// <remarks>
    /// The pad need not exist yet or hold anything: every answer is asked of the engine when it
    /// is wanted, and the engine holds against a number outside its range rather than throwing.
    /// </remarks>
    public PadPluginTarget(IAudioEngine audio, int pad)
    {
        _audio = audio;
        _pad = pad;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Counted from one and padded to two digits, which is how the pads are numbered everywhere
    /// a person sees them.
    /// </remarks>
    public string Label => "Pad " + (_pad + 1).ToString("00", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    /// <remarks>
    /// Made and hung on the pad the first time anything asks for it. A pad's insert is whatever
    /// was put there, so an insert that is already a chain is that chain: asking twice must not
    /// leave the second caller holding a chain the audio is not going through.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// The rate of the file the pad is playing, which is not the device's and not the same from
    /// one take to the next. A pad that has played nothing yet reports nought, and a plugin has
    /// to be built for something, so <see cref="AssumedSampleRate"/> stands in until there is a
    /// real answer.
    /// </remarks>
    public int SampleRate
    {
        get
        {
            int rate = _audio.PadSampleRate(_pad);
            return rate > 0 ? rate : AssumedSampleRate;
        }
    }
}
