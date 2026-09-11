namespace JingleBox2.Audio.Enums;

/// <summary>
/// One of the things that can be done to a take in the editor.
/// </summary>
/// <remarks>
/// A closed list because it is what a step in the history is made of: a step has to be worth
/// writing down, doing again on a fresh copy of the take, and saying in a word on the screen, and
/// all three want a name rather than a method. Adding one here is adding a tool.
///
/// Nothing in it is a fact about a file, so a history can be walked, wound back and read without
/// any of it happening.
/// </remarks>
public enum TakeEditKind
{
    /// <summary>Keep the selection and throw the rest of the take away.</summary>
    Trim,

    /// <summary>Empty the selection and leave the length alone.</summary>
    Silence,

    /// <summary>Turn the selection back to front.</summary>
    Reverse,

    /// <summary>Bring the selection up from silence.</summary>
    FadeIn,

    /// <summary>Take it down to silence.</summary>
    FadeOut,

    /// <summary>Lift the whole take so its loudest moment sits on a peak.</summary>
    Normalize
}
