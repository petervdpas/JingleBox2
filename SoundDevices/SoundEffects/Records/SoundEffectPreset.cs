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
    /// <summary>Its name, so a preset can be dropped straight into a picker.</summary>
    public override string ToString() => Name;
}
