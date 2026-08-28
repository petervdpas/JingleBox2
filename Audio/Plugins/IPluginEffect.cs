using System;

namespace JingleBox2.Audio.Plugins;

/// <summary>
/// A loaded plugin with audio running through it, whatever standard it speaks.
/// </summary>
/// <remarks>
/// Process runs on the audio thread; everything else is called from the UI. A parameter move
/// is queued rather than written, because both standards expect values to arrive at the start
/// of a block rather than whenever a knob is dragged.
///
/// A plugin has to go on being given blocks even while nothing is playing, or it cannot finish
/// a delay's tail and cannot tell the host what its own window did. That is why the mixer does
/// not rest while any track has an insert on it.
/// </remarks>
public interface IPluginEffect : IAudioInsert, IPluginParameters, IDisposable
{
    /// <summary>True once the plugin has been switched on and can be given audio.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Hands over anything queued now rather than on the next block, for a plugin nothing is
    /// being played through.
    /// </summary>
    /// <remarks>
    /// Without it a knob turned on a chain nobody is playing would sit in the queue until
    /// somebody pressed play, so the panel would show a value the plugin had never been told.
    /// </remarks>
    void FlushParameters();
}
