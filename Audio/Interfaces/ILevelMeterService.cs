using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Reads how loud something is, either from audio already in hand or from a channel BASS is
/// playing.
/// </summary>
/// <remarks>
/// Two sources because there are two situations. A recording being captured arrives as bytes and
/// has no channel to ask, and a channel being played holds its own audio where the caller cannot
/// reach it. Both answer the same question in the same units, so a meter does not care which of
/// them it was fed.
/// </remarks>
public interface ILevelMeterService
{
    /// <summary>
    /// The loudest sample in a block of interleaved 16 bit audio, whatever its channel count.
    /// </summary>
    /// <param name="data">The block, little endian, or null.</param>
    /// <returns>0 to 1, and 0 for a block too short to hold a sample.</returns>
    float GetLevelFromBytes(byte[]? data);

    /// <summary>The loudest side of a channel BASS is playing.</summary>
    /// <param name="channelHandle">The channel, or 0 for one that is not open.</param>
    /// <returns>0 to 1, and 0 for a handle that is not playing.</returns>
    float GetLevelFromHandle(int channelHandle);

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

    /// <summary>Both sides of a channel BASS is playing.</summary>
    /// <param name="channelHandle">The channel, or 0 for one that is not open.</param>
    /// <returns>Both sides, and <see cref="StereoLevel.Silent"/> for a handle that is not playing.</returns>
    StereoLevel GetStereoFromHandle(int channelHandle);
}
