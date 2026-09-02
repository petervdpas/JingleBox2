namespace JingleBox2.Rack.SoundDevices.Faces;

/// <summary>
/// Where a sound device's panel says its picker gets its list.
/// </summary>
/// <remarks>
/// Almost every sound device is started from one of its own presets. One is not: a sound device
/// whose whole sound is a recording of yours has no settings worth shipping, so the picker at the
/// top of its panel offers your shelf of takes instead.
///
/// The sound device says which, rather than anything working it out from what the sound device
/// looks like. The rack asks so it can put the right list behind the picker; the designer asks so
/// the picker is laid out against the list it will really have, which is the difference between a
/// control 258 wide and the same control with a category dropdown in front of it.
///
/// Written out as constants and matched by name, so a sound device naming a source this version has
/// never heard of falls back to its own presets rather than failing to open.
/// </remarks>
public static class PanelStarts
{
    /// <summary>
    /// The presets the sound device ships with, which is what nearly every sound device means.
    /// </summary>
    public const string Presets = "presets";

    /// <summary>Your recordings, for the sound device that is nothing but the one on it.</summary>
    public const string Takes = "takes";
}
