using JingleBox2.Machines;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// The machine's own presets, offered to the panel being laid out.
/// </summary>
/// <remarks>
/// Names and nothing else. A picker on a panel being designed is there to be sized and placed,
/// and what it needs for that is the list it will really have: five presets and no category
/// dropdown looks nothing like a shelf of a hundred takes with one in front of it.
///
/// Picking one does nothing. What a preset does to a machine happens where the machine is
/// played, and the panel on the bench has no instrument behind it to do it to.
/// </remarks>
public sealed class MachinePresetNames : IMachinePresets
{
    private readonly MachinePresetDesk _desk;

    public MachinePresetNames(MachinePresetDesk desk) => _desk = desk;

    public IReadOnlyList<string> Names => _desk.Presets.Select(one => one.Name).ToList();

    /// <summary>Which one is showing, which on a panel being laid out is none of them.</summary>
    public int Picked { get; set; } = -1;

    public string Caption => "Preset";
}
