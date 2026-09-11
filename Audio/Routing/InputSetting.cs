using System;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <inheritdoc/>
public sealed class InputSetting : IInputSetting
{
    /// <inheritdoc/>
    public AudioRoute? Source { get; private set; }

    /// <inheritdoc/>
    public bool Heard { get; private set; }

    /// <inheritdoc/>
    public string? PlayingOut { get; private set; }

    /// <inheritdoc/>
    public event Action? Changed;

    /// <inheritdoc/>
    /// <remarks>
    /// The source is compared by the node it names rather than by the object, since the list is
    /// read off the graph afresh every couple of seconds and the object from the last reading is
    /// never the one in this one. Compared by the object, every reading would look like somebody
    /// choosing the same source again, and the machine would be rewired for it.
    /// </remarks>
    public void Say(AudioRoute? source, bool heard, string? playingOut)
    {
        bool same = source?.Node == Source?.Node
            && heard == Heard
            && string.Equals(playingOut, PlayingOut, StringComparison.Ordinal);

        Source = source;
        Heard = heard;
        PlayingOut = playingOut;

        if (same) return;

        Changed?.Invoke();
    }
}
