using Avalonia.Controls;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// One strip's automation: a track's under the pattern, the master's under the mixer.
/// </summary>
/// <remarks>
/// Shows whichever <see cref="AutomationViewModel"/> it is given rather than reaching through
/// to the tracker for one, which is what makes the same strip serve both. The only behaviour
/// here is carrying the picture's two announcements, which a binding cannot do: an edit on a
/// drawn control is an event and not a property.
///
/// <c>Editing</c> is raised before the gesture changes anything, so what the history keeps is
/// the state being left. That is the rule every history hook in this application follows.
/// </remarks>
public partial class AutomationStrip : UserControl
{
    /// <summary>
    /// Builds the strip and wires the curve's two announcements to the lane view model.
    /// </summary>
    /// <remarks>
    /// The picture is a drawn control, so what it has to say arrives as events rather than as
    /// properties, and a binding cannot carry either of them.
    /// </remarks>
    public AutomationStrip()
    {
        InitializeComponent();

        var curve = this.FindControl<AutomationCurve>("Curve");
        if (curve is null) return;

        curve.Editing += what =>
        {
            if (DataContext is AutomationViewModel lanes) lanes.Editing(what);
        };

        curve.Edited += () =>
        {
            if (DataContext is AutomationViewModel lanes) lanes.Edited();
        };
    }
}
