using System.Collections.Generic;
using System.Linq;
using JingleBox2.Machines.Interfaces;

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
    /// <summary>The bench the presets are being written on, asked afresh rather than copied.</summary>
    /// <remarks>
    /// Held rather than the list itself, because a preset added or renamed while the panel is on
    /// screen has to show up in the picker without anybody wiring the two together.
    /// </remarks>
    private readonly MachinePresetDesk _desk;

    /// <summary>Offers the desk's presets to a panel being laid out.</summary>
    public MachinePresetNames(MachinePresetDesk desk) => _desk = desk;

    /// <inheritdoc/>
    public IReadOnlyList<string> Names => _desk.Presets.Select(one => one.Name).ToList();

    /// <inheritdoc/>
    /// <remarks>Which one is showing, which on a panel being laid out is none of them.</remarks>
    public int Picked { get; set; } = -1;

    /// <inheritdoc/>
    public string Caption => "Preset";
}
