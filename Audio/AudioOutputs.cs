using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class AudioOutputs : IAudioOutputs
{
    /// <inheritdoc/>
    public int AsioFrom => 1000;

    /// <inheritdoc/>
    public int Numbered(AudioOutputKind kind, int index) =>
        kind == AudioOutputKind.Asio ? AsioFrom + (index < 0 ? 0 : index) : index;

    /// <inheritdoc/>
    public (AudioOutputKind Kind, int Index) Which(int number) =>
        number >= AsioFrom
            ? (AudioOutputKind.Asio, number - AsioFrom)
            : (AudioOutputKind.System, number);
}
