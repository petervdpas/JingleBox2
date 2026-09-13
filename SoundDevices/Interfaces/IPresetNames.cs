using System.Collections.Generic;

namespace JingleBox2.SoundDevices.Interfaces;

/// <summary>
/// Which names a preset of your own may have, on a soundmachine or an effect.
/// </summary>
/// <remarks>
/// One rule for both worlds, since a preset of yours is a file in a device's own presets folder
/// either way. The name is the file's name, so it has to be one on every system this runs on; and
/// it may not be one of the device's own, since two presets called Init with one of them yours is a
/// picker nobody can trust. A name of yours is allowed, since keeping under it is replacing it.
/// </remarks>
public interface IPresetNames
{
    /// <summary>
    /// Why that name cannot be used, or nothing when it can.
    /// </summary>
    /// <param name="name">What somebody typed.</param>
    /// <param name="device">What the device is called, for the sentence.</param>
    /// <param name="theirs">The device's own presets: the name shown and the file each was read from.</param>
    string Refusal(string name, string device, IEnumerable<(string Name, string File)> theirs);

    /// <summary>The file a preset of yours with that name is written to, without its folder.</summary>
    /// <param name="name">A name that was not refused.</param>
    string FileFor(string name);
}
