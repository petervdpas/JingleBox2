using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JingleBox2.Rack.SoundDevices.Faces.Records;
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

        _meters = new Avalonia.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(50) };
        _meters.Tick += (_, _) => Read();

        AttachedToVisualTree += (_, _) => _meters.Start();
        DetachedFromVisualTree += (_, _) => _meters.Stop();
    }

    /// <summary>
    /// What reads the three strips that are not the song's.
    /// </summary>
    /// <remarks>
    /// The page's own rather than the tracker's, because the tracker polls while the mix is
    /// sounding and these three are not the mix: a pad is fired and a take is auditioned with the
    /// transport stopped, and an input meter shows what is arriving whether anything is playing at
    /// all. It runs only while this page is on screen, which is what the two visual-tree events
    /// are for, so a mixer nobody is looking at costs nothing.
    /// </remarks>
    private readonly Avalonia.Threading.DispatcherTimer _meters;

    /// <summary>Reads the three meters again, on the drawing thread.</summary>
    /// <remarks>
    /// Through the strips themselves rather than through the page's own context: they are handed
    /// in from outside and the page's context is the song, which knows nothing about them.
    /// </remarks>
    private void Read()
    {
        (RecorderInput as ViewModels.SourceStripViewModel)?.ReadMeter();
        (RecorderPlay as ViewModels.SourceStripViewModel)?.ReadMeter();
        (PadsStrip as ViewModels.SourceStripViewModel)?.ReadMeter();
    }

    /// <summary>
    /// The recording input's strip, handed in rather than read off the data context.
    /// </summary>
    /// <remarks>
    /// This page is bound to the song, because almost everything on it is the song's: the tracks,
    /// the master, the chains, the lanes. These three are not. They belong to the application,
    /// which is what makes them the reason the page moved out of the tracker in the first place.
    ///
    /// Handed in through a property rather than by re-pointing the page at the application and
    /// prefixing forty bindings with the song, which is the same shape TrackerView already uses
    /// for the rack it is given: the page keeps its own context and is told the few things that
    /// come from further out.
    /// </remarks>
    public static readonly StyledProperty<object?> RecorderInputProperty =
        AvaloniaProperty.Register<MixerView, object?>(nameof(RecorderInput));

    /// <inheritdoc cref="RecorderInputProperty"/>
    public object? RecorderInput
    {
        get => GetValue(RecorderInputProperty);
        set => SetValue(RecorderInputProperty, value);
    }

    /// <summary>The take being auditioned, as a strip. See <see cref="RecorderInputProperty"/>.</summary>
    public static readonly StyledProperty<object?> RecorderPlayProperty =
        AvaloniaProperty.Register<MixerView, object?>(nameof(RecorderPlay));

    /// <inheritdoc cref="RecorderPlayProperty"/>
    public object? RecorderPlay
    {
        get => GetValue(RecorderPlayProperty);
        set => SetValue(RecorderPlayProperty, value);
    }

    /// <summary>The pads, as one strip. See <see cref="RecorderInputProperty"/>.</summary>
    public static readonly StyledProperty<object?> PadsStripProperty =
        AvaloniaProperty.Register<MixerView, object?>(nameof(PadsStrip));

    /// <inheritdoc cref="PadsStripProperty"/>
    public object? PadsStrip
    {
        get => GetValue(PadsStripProperty);
        set => SetValue(PadsStripProperty, value);
    }

    /// <summary>
    /// Takes the page out into a window, or brings that window forward.
    /// </summary>
    /// <remarks>
    /// Nothing here puts it back: closing the window does that, which is the one way back and is
    /// the one a window's own frame already offers. A second button that said dock would be a
    /// second way to do what the cross in the corner does.
    /// </remarks>
    /// <param name="sender">Unused.</param>
    /// <param name="e">Unused.</param>
    private void Detach_Pressed(object? sender, RoutedEventArgs e) =>
        this.FindAncestorOfType<MainWindow>()?.DetachMixer();

    /// <summary>What a machine offers, drawn as menu items. The same rule a machine's face uses.</summary>
    private readonly Rack.Controls.Interfaces.IMenuLines _lines = new Rack.Controls.MenuLines();

    /// <summary>
    /// Shows what the hardware on this desk does to the mixer.
    /// </summary>
    /// <remarks>
    /// The same button a machine's face carries and the same lines behind it. It is here in the
    /// card's header rather than a part dropped on a panel because the mixer is drawn by this
    /// program: there is no description to put a part into.
    ///
    /// Read when it is pressed rather than held, since what it answers moves under it: a knob
    /// pointed at a strip a moment ago should be in the list the next time it opens.
    /// </remarks>
    /// <param name="sender">The button, which the menu opens under.</param>
    /// <param name="e">Unused.</param>
    private void MixerMenu_Pressed(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TrackerViewModel tracker) return;
        if (sender is not Control under) return;

        var offers = new List<PanelMenuItem> { Window() };

        offers.AddRange(tracker.MixerMenu.Read());

        new MenuFlyout { ItemsSource = _lines.Listed(offers) }.ShowAt(under);
    }

    /// <summary>
    /// The line that takes this page into a window of its own, or puts it back.
    /// </summary>
    /// <remarks>
    /// Which of the two it says is answered by where this page is standing rather than by a flag
    /// kept beside it. Detached, the page is inside a <see cref="DetachedWindow"/> and there is no
    /// <see cref="MainWindow"/> above it at all, so the same walk answers both questions and the
    /// two can never disagree.
    ///
    /// Always offered, and it is what made the button worth pressing. The menu used to return
    /// early when nothing was pointed at the mixer, so on a desk with no controller learned the
    /// hamburger did nothing whatever and said nothing about why.
    /// </remarks>
    private PanelMenuItem Window()
    {
        if (this.FindAncestorOfType<DetachedWindow>() is { } detached)
        {
            return new PanelMenuItem("Put the mixer back")
            {
                Tip = "Closes this window and puts the mixer back on the tracker's own page.",
                Chosen = detached.Close,
            };
        }

        var main = this.FindAncestorOfType<MainWindow>();

        return new PanelMenuItem("Open in a window")
        {
            Tip = "Takes the mixer out onto a window of its own, so the desk and whatever else you are doing can be seen at once.",
            Live = main != null,
            Chosen = main == null ? null : main.DetachMixer,
        };
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
