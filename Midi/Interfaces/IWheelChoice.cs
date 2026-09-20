using JingleBox2.SoundDevices.SoundMachines;
using JingleBox2.Tracker;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Which of a machine's controls the modulation wheel turns.
/// </summary>
/// <remarks>
/// **Three layers, each narrower than the one under it, each with one storage.** The machine
/// names a control in its own manifest, which is what makes a wheel arrive working on somebody
/// else's computer; the instrument may name another for this part, which travels with its preset
/// and with the song; and a link somebody pointed at the wheel beats both. The last of those is
/// not decided here, because it is not decided per machine: <c>MidiControlRouter</c> answers
/// whether anything was pointed at the control and the wheels are read only where nothing was.
///
/// A rule of its own rather than three lines inside whatever wanted them, because it is the
/// whole of a design that is otherwise only visible by reading two classes, and because it can
/// then be put a question to without a song, a window or a machine on a disc.
///
/// **It answers a key and never a control**, deliberately. Whether the machine still has that
/// control, what its range is and what writing to it does are settled where every machine
/// parameter is settled, which is one place; asked here they would be settled twice.
/// </remarks>
public interface IWheelChoice
{
    /// <summary>What the wheel turns, or nothing where neither has said.</summary>
    /// <param name="instrument">The instrument being played, or nothing.</param>
    /// <param name="machine">The machine it is on, or nothing where this installation has not got it.</param>
    string Turns(TrackerInstrument? instrument, SoundMachineProject? machine);
}
