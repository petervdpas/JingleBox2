using JingleBox2.Machines.Interfaces;

namespace JingleBox2.Machines;

/// <inheritdoc/>
public sealed class PresetStep : IPresetStep
{
    /// <inheritdoc/>
    public int Moved(int picked, int count, int by)
    {
        if (count <= 0) return picked;

        if (picked < 0) return 0;

        int wanted = picked + by;

        return wanted < 0 ? 0 : wanted >= count ? count - 1 : wanted;
    }

    /// <inheritdoc/>
    public string Side(double x, double middle) =>
        x < middle ? MachineActions.PresetPrevious : MachineActions.PresetNext;
}
