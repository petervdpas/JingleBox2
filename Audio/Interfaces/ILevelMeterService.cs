using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Reads how loud a block of captured audio is.
/// </summary>
/// <remarks>
/// Bytes and nothing else, which is what a capture hands over: a microphone through BASS, an
/// output's monitor, and a program of its own through the loopback all arrive as 16 bit samples
/// with no channel behind them to ask.
///
/// **There were two by-handle members here and they were the wrong question.** How loud a channel
/// is depends on who is holding its audio, which is a fact about how that channel is driven and
/// not about metering: a playing channel has a playback buffer, a source on a bus has its parent
/// mixer's copy, and a stream an ASIO driver pulls has neither. So it belongs where that is
/// known, which is <see cref="IOutputBus.Reading"/> for a bus and
/// <see cref="ITrackerOutput.Level"/> for the mix, both of which answer it. Nothing ever called
/// either member; what they held was the call that measures a decoding channel by decoding audio
/// out of it and throwing it away, which is the one that costs the music, and leaving it on the
/// interface anybody reaches for next is how that gets paid for a third time.
/// </remarks>
public interface ILevelMeterService
{
    /// <summary>
    /// The loudest sample in a block of interleaved 16 bit audio, whatever its channel count.
    /// </summary>
    /// <param name="data">The block, little endian, or null.</param>
    /// <returns>0 to 1, and 0 for a block too short to hold a sample.</returns>
    float GetLevelFromBytes(byte[]? data);

    /// <summary>Both sides of a block of interleaved 16 bit audio.</summary>
    /// <remarks>
    /// A mono signal reports the same level twice, so the caller does not have to care which it
    /// was handed. Anything past the second channel is stepped over, since a two bar meter has
    /// nowhere to show it.
    /// </remarks>
    /// <param name="data">The block, little endian, or null.</param>
    /// <param name="channels">How many channels one frame holds.</param>
    /// <returns>Both sides, and <see cref="StereoLevel.Silent"/> for a block too short.</returns>
    StereoLevel GetStereoFromBytes(byte[]? data, int channels);
}
