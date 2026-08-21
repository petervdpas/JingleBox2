namespace JingleBox2.Audio.Plugins;

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
    void Process(float[] buffer, int frames);
}
