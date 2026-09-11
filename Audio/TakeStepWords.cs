using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class TakeStepWords : ITakeStepWords
{
    /// <inheritdoc/>
    /// <remarks>
    /// Written out one by one rather than worked out from the name of the value, so a value
    /// renamed in the code does not quietly rename the thing on somebody's screen, and so the
    /// two-word ones read as two words.
    /// </remarks>
    public string For(TakeEditKind kind) => kind switch
    {
        TakeEditKind.Trim => "Trim",
        TakeEditKind.Silence => "Silence",
        TakeEditKind.Reverse => "Reverse",
        TakeEditKind.FadeIn => "Fade in",
        TakeEditKind.FadeOut => "Fade out",
        TakeEditKind.Normalize => "Normalize",
        _ => "Edit"
    };
}
