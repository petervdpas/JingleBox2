using System.Collections.Generic;

namespace JingleBox2.Machines;

/// <summary>
/// Where a machine can be started from.
/// </summary>
/// <remarks>
/// Every machine's panel opens with the same question: which of these do you want to begin
/// with. On most machines the answer is one of the presets in the machine's own folder; on the
/// Recording machine it is one of your takes, because a machine whose whole sound is the
/// recording on it has no settings worth shipping. The panel does not care which, so it is
/// handed a list of names and told what the machine calls the list.
///
/// Names and a number rather than a list of presets, for the same reason
/// <see cref="IMachineTakes"/> hands over peaks rather than a recording. What a preset is, where
/// it was read from and what happens to the settings when one is picked all belong to whoever
/// owns the shelf. The panel's whole part in it is drawing the name that is showing and saying
/// when somebody asked for a different one.
///
/// Picking is <see cref="Picked"/> rather than an event, unlike the recording a
/// <see cref="IMachineTakes"/> panel asks for, because the list is already here. There is
/// nothing for the host to go and fetch: the panel can say which one, and which one is a number.
/// </remarks>
public interface IMachinePresets
{
    /// <summary>What is offered, in the order it is offered.</summary>
    IReadOnlyList<string> Names { get; }

    /// <summary>Which one is showing, or -1 for none.</summary>
    /// <remarks>
    /// Written when somebody works the picker, which is the whole of what the panel does to
    /// this. Whether that loads anything is the implementer's business.
    /// </remarks>
    int Picked { get; set; }

    /// <summary>What this machine calls the list: "Preset", or "Take".</summary>
    string Caption { get; }
}
