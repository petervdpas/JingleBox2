using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;

namespace JingleBox2.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// How loud one note of a preset actually gets, by playing it rather than by reading its numbers.
/// </summary>
/// <remarks>
/// A level is not a loudness. The level knob is one term in a chain that also has a drive, a
/// resonance and an envelope in it, and each of those can add several decibels without saying so:
/// a saw driven hard into a resonant filter leaves at full scale from a level knob sitting at
/// nought. So the only honest answer is the one the engine gives, which is what this asks for.
///
/// Rendered rather than played. Nothing here opens a sound card, so it can be asked in a test, on
/// a build server and while somebody is typing a number into the designer.
///
/// Several notes rather than one, because the answer moves with pitch. A filter at a fixed
/// frequency is wide open under a low note and shut over a high one, and a pitch envelope lands
/// somewhere different at each end, so a preset measured at middle C alone is a preset measured
/// where it happens to be quietest.
/// </remarks>
public interface IPresetLoudness
{
    /// <summary>The notes a preset is tried at, spread over the range somebody would play it in.</summary>
    IReadOnlyList<Note> Notes { get; }

    /// <summary>
    /// The loudest sample one note of this preset reaches, as an amplitude where one is full scale.
    /// </summary>
    /// <remarks>
    /// Nothing when the level is not the machine's to answer. A sampler, a kit and a recording
    /// play audio somebody else made and are as loud as that recording, and a plugin is another
    /// program entirely; in all four the number would be a fact about somebody's take rather than
    /// about the preset, and reporting it as the preset's would send whoever is reading it to
    /// change the wrong knob.
    /// </remarks>
    /// <param name="sound">The preset, as the instrument it was read into.</param>
    double? Peak(TrackerInstrument? sound);
}
