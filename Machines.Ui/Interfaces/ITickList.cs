
namespace JingleBox2.Machines.Ui.Interfaces;

/// <summary>
/// The scale marks beside a fader, written the way they read: "6,0,-6,-12".
/// </summary>
/// <remarks>
/// Junk is skipped rather than throwing, since these come from markup and a typo should cost a
/// mark rather than a page. There is nowhere for a parse failure to be reported to: the marks
/// are an attribute on a control, read while the panel is being built.
/// </remarks>
internal interface ITickList
{
    /// <summary>Reads the marks out of the written form, dropping anything that is not a number.</summary>
    double[] Parse(string? text);
}
