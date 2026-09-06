using System;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// A channel's own block, out through an effect and back into the same place.
/// </summary>
/// <remarks>
/// This is what a chain on something that is not a track means: the pads have one, the recording
/// input has one, and both are a BASS channel with a hook on it rather than a bus in
/// <c>TrackMixer</c>. What has to happen at that hook is the same either way, and it was written
/// once inside the pad path where nothing else could reach it.
///
/// Four things, and each was got wrong somewhere on the way to being written down here. **The
/// audio is worked through in pieces and never skipped**, since the first block BASS asks for is
/// the whole playback buffer and a block passed over is the start of every pad playing dry. A
/// mono channel is widened into a stereo scratch and folded back, because an effect is a stereo
/// thing. An effect that throws costs the rest of that block and nothing else, since the
/// alternative on this thread is the process. And what comes back goes through
/// <see cref="IOutputCurve"/> before it is written, because an effect handing back a NaN writes
/// it into the library's own buffer and out of the card.
///
/// Nothing here allocates and nothing here takes a lock. It runs on the audio thread while
/// another thread may be inside a BASS call holding one, and BASS waits for the callback to
/// return.
/// </remarks>
public interface IInsertPass
{
    /// <summary>
    /// Runs a whole block through an effect, where it lies.
    /// </summary>
    /// <param name="insert">The effect, or nothing to leave the block alone.</param>
    /// <param name="scratch">The stereo buffer to work in, which decides the piece size.</param>
    /// <param name="buffer">The channel's samples, as BASS handed them over.</param>
    /// <param name="length">How many bytes of them there are.</param>
    /// <param name="channels">How many channels the stream carries.</param>
    void Run(Plugins.Interfaces.IAudioInsert? insert, float[] scratch, IntPtr buffer, int length, int channels);
}
