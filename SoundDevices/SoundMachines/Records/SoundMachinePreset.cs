
using JingleBox2.Tracker;

namespace JingleBox2.SoundDevices.SoundMachines.Records;

/// <summary>
/// One place a machine can start from, shipped with the machine as a file.
/// </summary>
/// <remarks>
/// A preset is not an instrument. It belongs to the machine, arrives with the program, and is
/// never on your shelf: picking one writes its settings into whatever instrument you are
/// editing and then has nothing more to do with it. What you keep afterwards is yours, called
/// what you called it, and changing it changes nothing here.
///
/// That is the difference the shelf could not express. An instrument seeded into the rack
/// is one more thing to scroll past, to rename by accident, and to wonder whether you made.
/// </remarks>
/// <param name="Name">
/// What it is called in the picker, which is the name inside the file rather than the file's
/// own. A filename starts with a number only to hold the order they are offered in.
/// </param>
/// <param name="Sound">
/// The settings, as a whole instrument, because a preset file is an instrument file: the same
/// shape, read by the same reader, so one saved off the rack can be dropped straight in.
/// </param>
public sealed record SoundMachinePreset(string Name, TrackerInstrument Sound)
{
    /// <summary>Its name, so a preset can be dropped straight into a picker.</summary>
    public override string ToString() => Name;
}
