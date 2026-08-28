using JingleBox2.Audio.Plugins;

namespace JingleBox2.Audio.Plugins.Interfaces;

/// <summary>
/// Something a track's audio passes through on its way to the mix. A plugin is one; anything
/// else that wants a whole track rather than a voice can be another.
/// </summary>
/// <remarks>
/// Called on the audio thread with an interleaved stereo block, edited in place. Whatever
/// implements this may not allocate, block, or take a lock that the UI thread holds for long.
/// </remarks>
public interface IAudioInsert
{
    /// <summary>Works on one block of audio, where it lies.</summary>
    /// <param name="buffer">The block, interleaved stereo, read and written in place.</param>
    /// <param name="frames">How many frames of it to work on, which may be fewer than it holds.</param>
    void Process(float[] buffer, int frames);
}
