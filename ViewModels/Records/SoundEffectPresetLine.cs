using System.Collections.Generic;

namespace JingleBox2.ViewModels.Records;

/// <summary>
/// One preset as the picker on a face holds it: its name, and what it sets.
/// </summary>
/// <remarks>
/// The same two things <c>JingleBox2.SoundDevices.SoundEffects.Records.SoundEffectPreset</c>
/// carries, and deliberately a second type rather than a reference to that one. The picker lives
/// in the view models and the shelf lives beside the disc, and the panel library between them
/// knows about neither: a face is handed names and hands back the one that was picked.
/// </remarks>
/// <param name="Name">What the picker shows.</param>
/// <param name="Settings">Where each control goes when it is picked, by the parameter's key.</param>
public sealed record SoundEffectPresetLine(string Name, IReadOnlyDictionary<string, double> Settings);
