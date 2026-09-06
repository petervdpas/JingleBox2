using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Plugins;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>RECORD as a plugin slot: the effect sits on whatever is being captured.</summary>
/// <remarks>
/// One chain for the page rather than one per take, since it is a setup somebody leaves standing:
/// the microphone through a compressor and a delay stays that way until they change it, the same
/// as the input gain does.
///
/// The chain is held here rather than fetched from somewhere else, which is what makes this
/// different from the pad's and the track's. A pad's insert lives on the audio engine and a
/// track's on the mixer, because in both the audio really passes through it while it plays;
/// nothing plays here. What RECORD does with the chain is run a finished take through it, so
/// there is nowhere else for it to live and nothing to keep in step with.
///
/// The rate is the recorder's, and it is right for the same reason a pad's is the file's: a
/// plugin is opened at a rate and keeps it, and the rate the take will be captured at is the one
/// it has to be built for. On the ordinary input path that is settled before anything is
/// recorded; capturing an output it is the output's own, which is only known once that capture
/// has been opened.
/// </remarks>
public sealed class RecordPluginTarget : IChainOwner
{
    /// <summary>The recorder, which knows what rate the take will arrive at.</summary>
    private readonly IRecordingService _recording;

    /// <summary>What is on the chain, and what a take is run through.</summary>
    private readonly PluginChain _chain = new();

    /// <summary>Names the recorder as somewhere a chain can run.</summary>
    /// <param name="recording">Where a take comes from.</param>
    public RecordPluginTarget(IRecordingService recording)
    {
        _recording = recording;
        _recording.Effect = _chain;
    }

    /// <inheritdoc/>
    public string Label => "Recording";

    /// <inheritdoc/>
    public PluginChain Chain => _chain;

    /// <inheritdoc/>
    /// <remarks>
    /// The rate the next take will be captured at. A recorder that has not opened an input yet
    /// answers the rate it would open at, so a chain built before anything has been recorded is
    /// built for the right thing.
    /// </remarks>
    public int SampleRate
    {
        get
        {
            int rate = _recording.SampleRate;
            return rate > 0 ? rate : PadPluginTarget.AssumedSampleRate;
        }
    }
}
