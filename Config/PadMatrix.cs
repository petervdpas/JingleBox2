namespace JingleBox2.Config;

/// <summary>
/// How many pads there may be.
/// </summary>
/// <remarks>
/// Written down once, because four places have to agree about it: the settings file as it is
/// read, the fields you type a size into, the check that lights the error under them, and the
/// window that grows to fit the grid. They disagreed before, which is how a config file could
/// hold a size the settings page refused to show.
/// </remarks>
public static class PadMatrix
{
    /// <summary>Fewer than this is not a pad launcher.</summary>
    public const int Least = 4;

    /// <summary>What fits on a laptop, and what a hand finds without looking.</summary>
    public const int Usual = 16;

    /// <summary>With the extended switch on, for a desk with the screen for it.</summary>
    public const int Most = 32;
}
