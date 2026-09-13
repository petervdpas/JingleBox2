namespace JingleBox2.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// A soundmachine written into a zip with the recordings its presets name brought along.
/// </summary>
/// <remarks>
/// **A preset can name a recording that is not in the machine's folder**, and a kit or a map
/// kept from the Menu always does: a take you chopped is on your recordings shelf, so the
/// preset holds its full path. A zip of the folder as it is on disc carries none of those files,
/// and on another computer every pad naming one is silent with nothing to say why.
///
/// So each preset is read on the way into the zip, every recording it names from outside the
/// folder is put in the zip in a folder named after the preset, beside it, and the preset in the
/// zip names it there relative to the presets folder: <c>Kraftwerk/Kick.wav</c>. That is exactly
/// how a preset that ships keeps its sounds, Chopper's Energy Beat among them. A recording several
/// presets use goes in once, beside the first of them, and the others name it there. Two different
/// recordings with one file name go in as two, the second numbered.
///
/// **Only the zip changes.** The folder on disc and the presets in it are left exactly as they
/// were, so exporting is not a way of moving anybody's recordings about. A path that names nothing
/// on this disc is left as it was written, since there is nothing to carry and rewriting it would
/// only lose where it pointed; so is anything that is not a preset file or will not read as JSON.
/// </remarks>
public interface ISoundMachinePack
{
    /// <summary>Writes the machine's folder into that zip, carrying what its presets name.</summary>
    /// <param name="folder">The machine's folder.</param>
    /// <param name="zipPath">The zip to write, which must not exist yet.</param>
    void Write(string folder, string zipPath);
}
