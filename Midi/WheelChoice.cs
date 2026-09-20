using JingleBox2.Midi.Interfaces;
using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;

namespace JingleBox2.Midi;

/// <inheritdoc/>
/// <remarks>
/// The instrument first and the machine after it, which is the whole rule: what is declared is
/// the default and what an instrument says is the exception. Empty either way means the layer
/// under it answers, so an instrument that has never been asked about its wheel reads back as
/// what it always was.
/// </remarks>
public sealed class WheelChoice : IWheelChoice
{
    /// <inheritdoc/>
    public string Turns(TrackerInstrument? instrument, SoundMachineProject? machine) =>
        instrument?.WheelKey is { Length: > 0 } own ? own : machine?.Wheel ?? "";
}
