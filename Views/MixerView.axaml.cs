using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>The song's channel strips. Shares the tracker's view model, and its song.</summary>
public partial class MixerView : UserControl
{
    public MixerView()
    {
        InitializeComponent();

        // Tunnelled and on the whole page rather than on each strip. A fader takes hold of the
        // pointer to be dragged and marks the press handled, so a handler waiting for it to come
        // back up would never hear about the one press that matters most: the one that grabbed a
        // fader. Coming down instead, the strip is picked on the way past and the fader still
        // moves.
        AddHandler(PointerPressedEvent, Touched, RoutingStrategies.Tunnel);

        // One of the views a controller may be laid out from. The list of those is the list of
        // views that say so, which is why it is a call and not a control being pointable: a knob
        // on a page nobody meant to lay out is still a knob. See LinkKey.
        LinkKey.Watch(this);
    }

    /// <summary>
    /// Touching a strip anywhere picks its track.
    /// </summary>
    /// <remarks>
    /// Which strip is picked is the whole of what makes one link on the mixer enough: everything
    /// pointed at a strip follows the track you are on, so a knob pointed at Level once is the
    /// level of whichever strip you last touched.
    /// </remarks>
    private void Touched(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TrackerViewModel tracker) return;

        for (var at = e.Source as Visual; at is not null; at = at.GetVisualParent())
        {
            if (at is not Control { DataContext: TrackStripViewModel strip }) continue;

            tracker.PickTrack(strip.Track);

            return;
        }
    }
}
