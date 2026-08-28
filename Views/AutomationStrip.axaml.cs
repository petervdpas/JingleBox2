using Avalonia.Controls;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// One track's automation, under the pattern.
/// </summary>
/// <remarks>
/// The only behaviour here is carrying the picture's two announcements to the view model, which
/// a binding cannot do: an edit on a drawn control is an event and not a property.
/// </remarks>
public partial class AutomationStrip : UserControl
{
    public AutomationStrip()
    {
        InitializeComponent();

        var curve = this.FindControl<AutomationCurve>("Curve");
        if (curve is null) return;

        // Before, so what is kept is the state being left. The same rule every history hook in
        // this application follows.
        curve.Editing += what =>
        {
            if (DataContext is TrackerViewModel tracker && tracker.CurrentPattern is { } pattern)
                tracker.History.Taking(pattern, what);
        };

        curve.Edited += () =>
        {
            if (DataContext is TrackerViewModel tracker)
            {
                tracker.CurrentPattern?.LaneChanged();
                tracker.Lanes?.Touched();
            }
        };
    }
}
