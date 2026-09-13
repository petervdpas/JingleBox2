using System.Collections.Generic;
using JingleBox2.SoundDevices.SoundEffects.Records;

namespace JingleBox2.SoundDevices.SoundEffects.Interfaces;

/// <summary>
/// The presets a sound effect ships with: a folder of files, one preset to a file.
/// </summary>
/// <remarks>
/// Files rather than code, the same as a soundmachine's, so a preset can be added, edited or
/// taken out without a build and so one can travel inside the effect's zip. They live in
/// <c>presets</c> inside the effect's own folder, which is where the effect already keeps
/// everything else it ships.
///
/// Read fresh each time rather than remembered. A soundmachine's library caches, because a
/// machine's presets are read while a panel is being drawn and a folder that never changes is
/// worth walking once; this is asked when somebody opens the presets page or drops down the
/// picker, which is rare and is exactly when the folder may have just changed underneath.
///
/// Nothing here throws for a folder that is not there or a file that will not parse. A preset
/// that cannot be read is one preset, not the whole shelf, which is the same rule the
/// soundmachine's shelf keeps and for the same reason: an effect with one bad file should open
/// with the rest of its presets rather than none.
/// </remarks>
public interface ISoundEffectPresets
{
    /// <summary>
    /// What that effect offers: its own presets, then yours, each in the order its filenames put them.
    /// </summary>
    /// <remarks>
    /// A value naming a parameter the effect has not got is dropped, and one outside that
    /// parameter's range is brought inside it, so what comes back can always be applied.
    /// </remarks>
    /// <param name="effect">The effect to look in. Nothing offers nothing.</param>
    IReadOnlyList<SoundEffectPreset> For(SoundEffectProject? effect);

    /// <summary>
    /// Writes one, keeping the order it is in and giving it a filename from its place.
    /// </summary>
    /// <remarks>
    /// Written whole through <c>JingleBox2.Files.SafeFile</c>, so a preset that fails half way
    /// leaves the one that was there. The name inside the file is what the picker shows; the
    /// number a filename starts with is only there to hold the order.
    /// </remarks>
    /// <param name="effect">The effect to write into. Nothing writes nothing.</param>
    /// <param name="preset">What to write.</param>
    /// <param name="at">Where in the order it goes, counting from nought.</param>
    bool Write(SoundEffectProject? effect, SoundEffectPreset preset, int at);

    /// <summary>Takes one off the shelf, by the name inside it.</summary>
    /// <param name="effect">The effect to take it from. Nothing removes nothing.</param>
    /// <param name="name">The preset's own name, compared the way a person would.</param>
    bool Remove(SoundEffectProject? effect, string? name);

    /// <summary>
    /// Why that name cannot be used for a preset of yours on that effect, or nothing when it can.
    /// </summary>
    /// <remarks>
    /// A preset of yours is a file in the effect's own presets folder, the installed one, beside
    /// the ones it ships with: kept file by file when the effect is updated and never deleted from,
    /// and carried in its zip. <see cref="JingleBox2.SoundDevices.Interfaces.IPresetNames"/> is the
    /// rule, and a soundmachine keeps the same one.
    /// </remarks>
    /// <param name="effect">The effect the preset is for.</param>
    /// <param name="name">What somebody typed.</param>
    string Refusal(SoundEffectProject? effect, string name);

    /// <summary>The preset of yours on that effect called that, or nothing.</summary>
    /// <param name="effect">The effect to look on.</param>
    /// <param name="name">The name, compared without regard to case.</param>
    SoundEffectPreset? Yours(SoundEffectProject? effect, string name);

    /// <summary>
    /// Keeps where the controls stand as a preset of yours, replacing one of yours called that.
    /// </summary>
    /// <param name="effect">The effect it is a preset of.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="settings">Where each control stands, by the parameter's key.</param>
    /// <returns>The preset as it is now listed, or nothing when the name was refused or the file could not be written.</returns>
    SoundEffectPreset? Keep(SoundEffectProject? effect, string name, IReadOnlyDictionary<string, double> settings);

    /// <summary>
    /// Takes a preset of yours off, and never one the effect ships with.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="Remove"/>, which the presets page in DESIGNER uses on any preset of the
    /// effect being built; this is what a face asks, where the effect's own are not yours to delete.
    /// </remarks>
    /// <param name="effect">The effect it is on.</param>
    /// <param name="preset">The preset, as this shelf listed it.</param>
    /// <returns>Whether it was taken off.</returns>
    bool RemoveYours(SoundEffectProject? effect, SoundEffectPreset? preset);
}
