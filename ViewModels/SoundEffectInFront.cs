using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// One field and two lines around it. There is one screen and one thing on the front of it, so
/// this is shared the way <see cref="Controllers.ControllerProfiles"/> is: an instance rather
/// than a static, handed to whoever makes a chain view and to the thing that resolves a link, so
/// both halves can be put a question to without a window.
/// </remarks>
public sealed class SoundEffectInFront : ISoundEffectInFront
{
    /// <inheritdoc/>
    public ISoundEffectShown? Shown { get; private set; }

    /// <inheritdoc/>
    public void InFront(ISoundEffectShown? shown) => Shown = shown;

    /// <inheritdoc/>
    public void Gone(ISoundEffectShown? shown)
    {
        if (ReferenceEquals(Shown, shown)) Shown = null;
    }
}
