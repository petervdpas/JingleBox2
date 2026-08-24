namespace JingleBox2.Machines;

/// <summary>
/// Where a panel reads and writes the values its controls stand for.
/// </summary>
/// <remarks>
/// A panel is a picture of somebody's settings, and whose settings they are depends on where
/// the panel is standing: in a song it is the instrument's, in the machine editor it is a
/// preview nobody keeps. Neither is any business of the drawing, so the panel is handed this
/// and asks it.
///
/// Values are doubles throughout, switches included, so that a machine's settings are one kind
/// of thing however they are shown.
/// </remarks>
public interface IMachineValues
{
    /// <summary>What that parameter is set to, or its default when nothing has set it.</summary>
    double Get(string key);

    /// <summary>Sets it, because somebody turned the control that stands for it.</summary>
    void Set(string key, double value);
}
