using JingleBox2.Rack.Faces.Interfaces;

namespace JingleBox2.Rack.Faces;

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
        x < middle ? PanelActions.PresetPrevious : PanelActions.PresetNext;
}
