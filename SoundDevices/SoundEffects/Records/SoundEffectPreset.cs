using System.Collections.Generic;

namespace JingleBox2.SoundDevices.SoundEffects.Records;

/// <summary>
/// One place a sound effect can be started from: a name, and where its controls stand.
/// </summary>
/// <remarks>
/// Nothing like a soundmachine's preset, which is a whole instrument file because a soundmachine
/// can hold a kit, a keyboard map and a folder of recordings. An effect holds none of that. It is
/// a handful of parameters and nothing else, so a preset is a handful of numbers and nothing
/// else, which is also why it is a record here rather than a class with a reader inside it.
///
/// Keyed by the parameter's own key rather than by its place in the list, so a preset written
/// today still means the same thing after somebody adds a knob in the middle of the face.
/// </remarks>
/// <param name="Name">
/// What it is called in the picker, which is the name inside the file rather than the file's own.
/// A filename starts with a number only to hold the order they are offered in.
/// </param>
/// <param name="Settings">
/// Where each control stands, by the parameter's key. A key the effect no longer has is dropped
/// as the file is read, and a parameter the file says nothing about is left where it was.
/// </param>
public sealed record SoundEffectPreset(string Name, IReadOnlyDictionary<string, double> Settings)
{
    /// <summary>What is written in front of a preset of yours in a picker.</summary>
    public const string YoursMark = "\u2605 ";

    /// <summary>The file it was read from, or nothing for one that was never on disc.</summary>
    public string File { get; init; } = "";

    /// <summary>
    /// True for a preset you kept yourself, false for one the effect ships with.
    /// </summary>
    /// <remarks>
    /// Only yours can be replaced or taken off from a face. One the effect ships with would come
    /// straight back the next time the effect is brought up to date.
    /// </remarks>
    public bool Yours { get; init; }

    /// <summary>Its name as a picker shows it, marked where it is yours.</summary>
    public string Shown => Yours ? YoursMark + Name : Name;

    /// <summary>Its name as a picker shows it, so a preset can be dropped straight into one.</summary>
    public override string ToString() => Shown;
}
