using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
using Avalonia.VisualTree;
using JingleBox2.UI.Interfaces;
using JingleBox2.ViewModels;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The plugin chain control. Its behaviour is in PluginChainViewModel, so the same strip works
/// for a pad, a tracker track, or anything else that grows one later. Opening a window is the
/// one thing that has to happen here: windows are a view's business.
/// </summary>
public partial class PluginStrip : UserControl
{
    /// <summary>
    /// The chain currently shown, kept only so its announcement can be let go of when the strip
    /// is pointed at another one. A strip is reused as the cursor moves between tracks, so
    /// without this it would be subscribed to every chain it had ever shown.
    /// </summary>
    private PluginChainViewModel? _chain;

    /// <summary>
    /// Builds the strip and keeps the plugin windows in step with what is on the chain.
    /// </summary>
    /// <remarks>
    /// A device that leaves the chain takes its window with it, wherever the removal came from:
    /// the strip's menu, a song being opened, or a pad profile changing. A window left open
    /// over a disposed plugin draws into nothing, which is a crash inside the plugin's own
    /// toolkit rather than an exception anything here could catch.
    /// </remarks>
    public PluginStrip()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_chain != null) _chain.DeviceClosing -= Closing;

            _chain = DataContext as PluginChainViewModel;

            if (_chain != null) _chain.DeviceClosing += Closing;
        };

        _ghost = new DragGhost(GhostLayer);

        Slots.AddHandler(PointerPressedEvent, OnSlotPressed);

        // The whole row takes the drop rather than the devices alone, so letting go over the
        // instrument means in front of the first effect. The instrument itself is not in the
        // row of devices and is never picked up, so first stays the only place it has.
        DragDrop.SetAllowDrop(Row, true);
        Row.AddHandler(DragDrop.DragOverEvent, OnSlotDragOver);
        Row.AddHandler(DragDrop.DropEvent, OnSlotDrop);
        Row.AddHandler(DragDrop.DragLeaveEvent, (_, _) => ShowLanding(-1, false));
    }

    /// <summary>What a chain carries while one of its devices is being moved.</summary>
    private static readonly ISlotDrag Carried = new SlotDrag();

    /// <summary>The picture in the hand while a device is being carried.</summary>
    private readonly DragGhost _ghost;

    /// <summary>
    /// A press on a device picks it up.
    /// </summary>
    /// <remarks>
    /// **Bubbling rather than tunnelling, which is the whole of how this stays out of the way of
    /// the buttons on a block.** A button answers a press and marks it handled, and a handled
    /// event is not delivered here, so the name, the power switch and the cross work exactly as
    /// they did and a press anywhere else on the block is a grab.
    ///
    /// Letting go without moving ends the drag with no effect, so this does not get in the way
    /// of a plain click on the body either. The one thing it must not swallow is the right
    /// button, which is the context menu the move commands already live on.
    /// </remarks>
    private async void OnSlotPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Slots).Properties.IsLeftButtonPressed) return;
        if (_chain is not { } chain) return;

        int slot = SlotAt(e.GetPosition(Slots));

        if (slot < 0) return;

        try
        {
            await DragDrop.DoDragDropAsync(e, Carried.For(chain, slot), DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            // Said rather than swallowed, and caught rather than left: a drag that falls over
            // on the way through the toolkit would otherwise take the application down from an
            // async handler, which on a show is the worst answer there is to a gesture.
            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.App,
                () => "chain: the drag fell over, " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            _ghost.Hide();
            ShowLanding(-1, false);
        }
    }

    /// <summary>
    /// The card drawn in the hand while a device is carried, or nothing where it has gone.
    /// </summary>
    /// <remarks>
    /// **Built rather than the block itself**, which is not a nicety: a control has one parent,
    /// so putting the live block in the hand takes it out of the row it is being dragged along
    /// and the toolkit refuses it outright. The ghost's layer is a canvas over the strip and
    /// takes a control rather than a data context, which is why this is written out here the
    /// way the tracker's is.
    ///
    /// The name and what it is, which is what a block says at a glance: enough to read as the
    /// thing being moved rather than as a label about it.
    /// </remarks>
    /// <param name="slot">Which device is being carried.</param>
    private Control? Picture(int slot)
    {
        if (_chain is not { } chain || slot < 0 || slot >= chain.Devices.Count) return null;

        var device = chain.Devices[slot];

        var lines = new StackPanel { Spacing = 1 };

        lines.Children.Add(new TextBlock
        {
            Text = device.Name,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
        });

        if (!string.IsNullOrEmpty(device.Format))
            lines.Children.Add(new TextBlock
            {
                Text = device.Format,
                FontSize = 11,
                Opacity = 0.75,
            });

        return lines;
    }

    /// <summary>Which device is under a point in the row, or -1 for the gaps between them.</summary>
    /// <remarks>
    /// Asked of the containers rather than worked out from a width, since the devices are not
    /// all the same width: what a block is as wide as is the readings on it.
    /// </remarks>
    private int SlotAt(Point point)
    {
        var edges = Edges();

        for (int i = 0; i < edges.Count; i++)
            if (point.X >= edges[i].Left && point.X <= edges[i].Right) return i;

        return -1;
    }

    /// <summary>Where a drop lands, which is arithmetic and is kept out here.</summary>
    private static readonly IChainDrop Landing = new UI.ChainDrop();

    /// <summary>Where a point in the row means, as the gap a device would go in at.</summary>
    /// <param name="point">Where the hand is, in the row's own space.</param>
    private int PlaceAt(Point point) => Landing.Place(Edges(), point.X);

    /// <summary>Where each block starts and ends across the row, in the order they play.</summary>
    /// <remarks>
    /// Read off the containers rather than worked out from a width, since the blocks are not all
    /// the same width: what one is as wide as is the readings on it.
    /// </remarks>
    private List<(double Left, double Right)> Edges()
    {
        var edges = new List<(double Left, double Right)>();

        for (int i = 0; i < Slots.ItemCount; i++)
        {
            if (Slots.ContainerFromIndex(i) is not Control container) continue;

            // Asked in the row's own space rather than the container's, since a container's
            // bounds are against whatever panel the items are laid out in and the two agree
            // only while nothing has any padding.
            if (container.TranslatePoint(new Point(0, 0), Slots) is not { } at) continue;

            edges.Add((at.X, at.X + container.Bounds.Width));
        }

        return edges;
    }

    /// <summary>
    /// Marks the edge the device in the hand would go in at, or takes every mark off.
    /// </summary>
    /// <remarks>
    /// A class on the block beside the gap rather than a line of its own, since a chain is a row
    /// of blocks with gaps between them and a mark drawn in a gap has nothing to hold on to. The
    /// place past the last block is drawn on the last block's far edge for the same reason.
    /// </remarks>
    /// <param name="place">Where it would land, or -1 for nowhere.</param>
    /// <param name="show">Whether to draw anything at all.</param>
    private void ShowLanding(int place, bool show)
    {
        for (int i = 0; i < Slots.ItemCount; i++)
        {
            if (Slots.ContainerFromIndex(i) is not Control container) continue;

            var block = container as Border ?? FirstBorder(container);

            if (block == null) continue;

            bool before = show && place == i;
            bool after = show && place == Slots.ItemCount && i == Slots.ItemCount - 1;

            Mark(block, "before", before);
            Mark(block, "after", after);
        }
    }

    /// <summary>Puts a class on a block or takes it off, without asking twice.</summary>
    /// <param name="block">The block being marked.</param>
    /// <param name="name">Which mark.</param>
    /// <param name="wanted">Whether it should be there.</param>
    private static void Mark(Border block, string name, bool wanted)
    {
        bool has = block.Classes.Contains(name);

        if (wanted == has) return;

        if (wanted) block.Classes.Add(name);
        else block.Classes.Remove(name);
    }

    /// <summary>The block inside a container, since a template puts one there.</summary>
    /// <param name="container">The row's own control for that device.</param>
    private static Border? FirstBorder(Control container)
    {
        foreach (var child in container.GetVisualDescendants())
            if (child is Border border && border.Classes.Contains("device")) return border;

        return null;
    }

    /// <summary>
    /// The hand is over the row, holding something.
    /// </summary>
    /// <remarks>
    /// It takes a device off this chain and nothing else: a device from another strip is refused
    /// rather than carried across, since a chain holds loaded plugins and moving one between
    /// chains would mean loading it somewhere else.
    /// </remarks>
    private void OnSlotDragOver(object? sender, DragEventArgs e)
    {
        int moving = _chain is { } chain ? Carried.IndexFrom(e.DataTransfer, chain) : -1;

        if (moving < 0)
        {
            _ghost.Refused = true;
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;

            return;
        }

        if (!_ghost.IsShowing && Picture(moving) is { } picture) _ghost.Show(picture);

        _ghost.Refused = false;
        _ghost.MoveTo(e.GetPosition(this));

        ShowLanding(PlaceAt(e.GetPosition(Slots)), true);

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>Let go over the row, which puts the device where the mark was.</summary>
    /// <remarks>
    /// The gap is counted with the device still in the row and the chain counts without it, which
    /// is <see cref="IChainDrop.Landing"/>: that difference is the one place this could be wrong
    /// by one, and it is not decided here.
    /// </remarks>
    private void OnSlotDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        ShowLanding(-1, false);

        if (_chain is not { } chain) return;

        int moving = Carried.IndexFrom(e.DataTransfer, chain);

        if (moving < 0 || moving >= chain.Devices.Count) return;

        int place = Landing.Landing(moving, PlaceAt(e.GetPosition(Slots)));

        chain.MoveTo(chain.Devices[moving], place);
    }

    /// <summary>
    /// Opens the plugin the track plays, in the same kind of window an effect gets.
    /// </summary>
    /// <remarks>
    /// The plugin is loaded here rather than when the track was picked: a track selection
    /// should not cost the time a big synth takes to come up. It is the one the notes go to,
    /// so a knob turned in it changes what is actually heard.
    ///
    /// An instrument of ours opens the designer's panel; a plugin opens its own interface. The
    /// window is the same window either way, because to the track they are the same thing.
    /// </remarks>
    private void OnOpenInstrument(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PluginChainViewModel chain) return;

        var instrument = chain.Instrument;
        if (instrument == null) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        if (instrument.MachineMissing)
        {
            _ = Missing(instrument);
            return;
        }

        if (!instrument.IsPlugin)
        {
            var designer = instrument.Designer;
            if (designer == null) return;

            instrument.IsOpen = true;

            InstrumentWindow.Show(instrument, designer, owner, () => instrument.IsOpen = false);
            return;
        }

        var panel = instrument.Prepare();
        if (panel == null) return;

        instrument.IsOpen = true;

        PluginWindow.Show(instrument, panel, instrument.Title, owner, () => instrument.Close());
    }

    /// <summary>
    /// Says why an instrument will not open, which is that its machine is not here.
    /// </summary>
    /// <remarks>
    /// Said when it is asked for and nowhere else. An instrument whose machine is missing is a
    /// row in a song like any other until somebody tries to use it, and that is the moment the
    /// answer is wanted: told on the way in, while opening a song, it is a dialog about
    /// something nobody had asked about yet and is gone by the time it matters.
    ///
    /// The window does not open behind it. There is no panel to draw, so what would open is an
    /// empty frame with a keyboard that cannot sound a note, which reads as a machine that is
    /// broken rather than one that is absent.
    ///
    /// The machine is labelled in the heading because an instrument takes its machine's name
    /// unless somebody renames it, so the two are the same word more often than not and
    /// "Ouroboros is not registered" leaves somebody wondering which of the two is meant. The
    /// body then names the instrument and says "on it", which needs no second label.
    ///
    /// It names the machine and what that costs, and stops. Where to go and put it right used to
    /// be on the end of it and is not: somebody with a song full of machines knows this
    /// application, and a sentence sending them to a page they already know is a sentence they
    /// read every time to learn nothing.
    ///
    /// Not awaited by the caller: it is an event handler, and the dialog owns itself once it is
    /// up. What it is waiting on is somebody pressing OK.
    /// </remarks>
    /// <param name="instrument">The instrument whose machine has gone.</param>
    private static System.Threading.Tasks.Task Missing(PluginInstrumentViewModel instrument) =>
        MissingSoundMachineDialog.ShowAsync(instrument.Missing, instrument.Instrument.Name);

    /// <summary>
    /// Opens the effect whose block was pressed.
    /// </summary>
    /// <remarks>
    /// Read off the button's own row rather than off anything the strip has picked, since a
    /// chain has no selection: a press on a block is about that block.
    /// </remarks>
    private void OnOpenDevice(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        if (control.DataContext is PluginSlotViewModel plugin)
        {
            PluginWindow.Show(plugin, owner);

            return;
        }

        if (control.DataContext is SoundEffectViewModel ours) SoundEffectWindow.Show(ours, owner);
    }

    /// <summary>
    /// Shuts whatever window a box has, for the box going out of the chain.
    /// </summary>
    /// <remarks>
    /// Either kind: a plugin's window is somebody else's interface embedded in ours and one of
    /// ours is a panel, and both have to go before the thing behind them does.
    /// </remarks>
    /// <param name="device">The box on its way out.</param>
    private static void Closing(ViewModels.Interfaces.IChainSlot device)
    {
        PluginWindow.CloseFor(device);

        SoundEffectWindow.CloseFor(device);
    }
}
