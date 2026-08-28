namespace JingleBox2.Machines;

/// <summary>
/// Walking a shelf of presets: which way, and where it stops.
/// </summary>
/// <remarks>
/// Out here rather than inside the picker or the panel because both of them ask, and because
/// what it decides can be put a question to without a window: which of the two steps the pointer
/// is offering, and where along the list a step lands.
/// </remarks>
public static class PresetStep
{
    /// <summary>
    /// Where a step from here lands, or the same place when there is nowhere to go.
    /// </summary>
    /// <remarks>
    /// Stopping at the ends rather than coming round. A list of presets has a first and a last,
    /// and a button held down that wrapped would carry you past the one you were looking for
    /// without a pause to notice it.
    /// </remarks>
    public static int Moved(int picked, int count, int by)
    {
        if (count <= 0) return picked;

        // Nothing picked yet, so a step in either direction means the first of them.
        if (picked < 0) return 0;

        int wanted = picked + by;

        return wanted < 0 ? 0 : wanted >= count ? count - 1 : wanted;
    }

    /// <summary>
    /// Which of the two steps a place on the picker is offering.
    /// </summary>
    /// <remarks>
    /// The left half offers the one before and the right half the one after, because that is
    /// where the picker's own two arrows are. Nobody has to be told that; it is where their hand
    /// was going anyway.
    /// </remarks>
    public static string Side(double x, double middle) =>
        x < middle ? MachineActions.PresetPrevious : MachineActions.PresetNext;
}
