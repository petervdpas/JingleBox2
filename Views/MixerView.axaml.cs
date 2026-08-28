using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>The song's channel strips. Shares the tracker's view model, and its song.</summary>
/// <remarks>
/// The master is on it as a strip without being a track, and its effect chain and its
/// automation fold away underneath, so the mixer is the one page where a chain is shown that
/// the pattern's cursor cannot reach.
/// </remarks>
public partial class MixerView : UserControl
{
    /// <summary>
    /// Builds the page, and says it is somewhere a hardware knob can be pointed.
    /// </summary>
    /// <remarks>
    /// The press handler is tunnelled and on the whole page rather than on each strip. A fader
    /// takes hold of the pointer to be dragged and marks the press handled, so a handler
    /// waiting for it to come back up would never hear about the one press that matters most:
    /// the one that grabbed a fader. Coming down instead, the strip is picked on the way past
    /// and the fader still moves.
    ///
    /// <see cref="LinkKey"/>.Watch puts the page in the tally of places worth entering the
    /// other mouse mode for. That is a call rather than a control saying it is pointable,
    /// because a knob on a page nobody meant to lay out is still a knob.
    /// </remarks>
    public MixerView()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, Touched, RoutingStrategies.Tunnel);

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
