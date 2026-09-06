using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.Views;

/// <summary>
/// The surface the blocks stand on: it places them, draws the cables between them, and answers
/// the hand that drags a cable from one connection point to another.
/// </summary>
/// <remarks>
/// **The blocks are controls and the cables are drawn.** A block is a thing a hand takes hold
/// of, so it is a control with its own hit testing; a cable is a curve between two points on
/// two different controls, which no single control can own, so it is painted here on a layer
/// under them. The same arrangement the pattern's playing line and the automation's playhead
/// already use, and for the same reason: what moves often is drawn separately from what is
/// expensive to draw.
///
/// It says what was plugged and unplugged rather than doing it. What a cable means is somebody
/// else's business, and on this machine it means rewiring the sound server, so the decision
/// belongs to whoever knows that rather than to a picture.
/// </remarks>
public sealed class PatchbayView : Panel
{
    /// <summary>Where a block's parts sit, which is also where the cable's bend comes from.</summary>
    private static readonly IPatchGeometry Shape = new PatchGeometry();

    /// <summary>What may be joined to what, and how the channels line up.</summary>
    private static readonly IPatchWiring Wiring = new PatchWiring();

    /// <summary>What the two kinds of wire are painted in.</summary>
    private static readonly Interfaces.IPatchColours Colours = new PatchColours();

    /// <summary>
    /// What a press means before anything on the page is asked about it.
    /// </summary>
    /// <remarks>
    /// The waveform's own rule rather than a second one written here, which is what makes the
    /// gesture that moves a picture the same gesture everywhere in this application: the middle
    /// button, or Ctrl, or Shift. Two spellings of it would drift, and the way that fails is a
    /// drag that pans in one editor and drags a block in the next.
    /// </remarks>
    private static readonly Rack.Controls.Interfaces.IWaveformPress Press = new Rack.Controls.WaveformPress();

    /// <summary>The blocks to draw.</summary>
    public static readonly StyledProperty<IReadOnlyList<PatchNode>> NodesProperty =
        AvaloniaProperty.Register<PatchbayView, IReadOnlyList<PatchNode>>(
            nameof(Nodes), Array.Empty<PatchNode>());

    /// <summary>The cables between them.</summary>
    public static readonly StyledProperty<IReadOnlyList<PatchLink>> LinksProperty =
        AvaloniaProperty.Register<PatchbayView, IReadOnlyList<PatchLink>>(
            nameof(Links), Array.Empty<PatchLink>());

    /// <summary>Which of those are carrying audio at this moment.</summary>
    /// <remarks>
    /// A live cable is drawn solid and a quiet one dashed, so the page says what the application
    /// is doing rather than only how it is wired. Its own list, since this changes many times a
    /// second and the cables do not.
    /// </remarks>
    public static readonly StyledProperty<IReadOnlyList<PatchLink>> LiveProperty =
        AvaloniaProperty.Register<PatchbayView, IReadOnlyList<PatchLink>>(
            nameof(Live), Array.Empty<PatchLink>());

    /// <inheritdoc cref="NodesProperty"/>
    public IReadOnlyList<PatchNode> Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    /// <inheritdoc cref="LinksProperty"/>
    public IReadOnlyList<PatchLink> Links
    {
        get => GetValue(LinksProperty);
        set => SetValue(LinksProperty, value);
    }

    /// <inheritdoc cref="LiveProperty"/>
    public IReadOnlyList<PatchLink> Live
    {
        get => GetValue(LiveProperty);
        set => SetValue(LiveProperty, value);
    }

    /// <summary>Which block is picked out, whose details the sidebar beside this shows.</summary>
    /// <remarks>
    /// The node rather than its id, since what a sidebar wants is everything about the block and
    /// looking it up again from an id would be the list walked twice. Two way: the surface sets
    /// it when a block is touched, and clearing it from outside puts the sidebar back to saying
    /// nothing is picked.
    /// </remarks>
    public static readonly StyledProperty<PatchNode?> SelectedProperty =
        AvaloniaProperty.Register<PatchbayView, PatchNode?>(
            nameof(Selected), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <inheritdoc cref="SelectedProperty"/>
    public PatchNode? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    /// <summary>Raised when a cable has been dropped on a connection point that will take it.</summary>
    public event Action<PatchLink>? Wired;

    /// <summary>Raised when a cable has been pulled out and dropped on nothing.</summary>
    public event Action<PatchLink>? Unwired;

    /// <summary>Raised once a block has been dragged and let go of, with where it was left.</summary>
    /// <remarks>
    /// Where a block sits is the surface's own answer while the page is open, and somebody
    /// else's business between one run and the next: this is how it leaves.
    /// </remarks>
    public event Action<string, double, double>? Moved;

    /// <summary>The block for each node, by id, so a list read again reuses what is on screen.</summary>
    private readonly Dictionary<string, PatchBlock> _blocks = new(StringComparer.Ordinal);

    /// <summary>Where each block has been put, by id, which outlives the list being read again.</summary>
    private readonly Dictionary<string, Point> _places = new(StringComparer.Ordinal);

    /// <summary>The layer the cables are painted on, under every block.</summary>
    private readonly PatchCables _cables;

    /// <summary>How far the whole surface has been pushed about, in its own coordinates.</summary>
    /// <remarks>
    /// The blocks keep their own places and the page moves under them, so panning changes
    /// nothing anybody has arranged: what is stored is where a block was put, and this is only
    /// where the window is looking.
    /// </remarks>
    private Point _pan;

    /// <summary>Where the pointer was when the surface was taken hold of, or nothing.</summary>
    private Point? _panning;

    /// <summary>The end of the cable in the hand that is not moving, or nothing.</summary>
    private PatchPort? _anchor;

    /// <summary>The cable being re-attached, or nothing when a fresh one is being drawn.</summary>
    private PatchLink? _moving;

    /// <summary>Where the loose end of that cable is right now.</summary>
    private Point _loose;

    /// <summary>Builds the surface with its cable layer underneath.</summary>
    /// <remarks>
    /// The layer is added first so it is painted first, which puts every cable behind every
    /// block: a wire that ran over the face of a block would read as being plugged into the
    /// middle of it. It takes no clicks, for the reason the pattern's own layers take none.
    /// </remarks>
    public PatchbayView()
    {
        ClipToBounds = true;

        // A panel with no background is invisible to the pointer, so every press on the empty
        // part of the surface went to whatever was behind it and the page could not be taken
        // hold of at all. Transparent rather than a colour: the card underneath paints the
        // ground, and painting a second one over it would be a plate on a plate.
        Background = Brushes.Transparent;

        _cables = new PatchCables(this) { IsHitTestVisible = false };

        Children.Add(_cables);

        // On the way down rather than on the way up, because a block answers a press by taking
        // hold of itself and a dot answers by starting a cable: coming up, the two keys would
        // only work over the parts of the page where nothing is, which is the opposite of what
        // they are for.
        AddHandler(PointerPressedEvent, Grabbed, RoutingStrategies.Tunnel);
    }

    /// <summary>Rebuilds the blocks whenever the list of them changes.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == NodesProperty) Rebuild();
        if (change.Property == LinksProperty) _cables.InvalidateVisual();
        if (change.Property == LiveProperty) _cables.InvalidateVisual();
        if (change.Property == SelectedProperty) Mark();
    }

    /// <summary>
    /// Puts a block on the surface for every node, and takes away the ones that have gone.
    /// </summary>
    /// <remarks>
    /// A block already on screen is kept and told what it now says rather than being replaced,
    /// which is what lets a source come and go in the list without the block under somebody's
    /// pointer being swapped for another one halfway through a drag.
    /// </remarks>
    private void Rebuild()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in Nodes)
        {
            seen.Add(node.Id);

            if (!_blocks.TryGetValue(node.Id, out var block))
            {
                block = new PatchBlock { Node = node.Id };

                block.PortPressed += (port, e) => Grab(port, e);
                block.Dragged += moved => Move(node.Id, moved);
                block.Touched += () => Selected = Find(block.Node);
                block.Settled += () => Left(node.Id);

                _blocks[node.Id] = block;
                Children.Add(block);
            }

            block.Title = node.Title;
            block.Ins = node.Ins;
            block.Outs = node.Outs;
            block.IsOurs = node.IsOurs;

            if (!_places.ContainsKey(node.Id)) _places[node.Id] = new Point(node.X, node.Y);
        }

        foreach (string gone in new List<string>(_blocks.Keys))
        {
            if (seen.Contains(gone)) continue;

            Children.Remove(_blocks[gone]);
            _blocks.Remove(gone);
            _places.Remove(gone);
        }

        if (Selected is { } picked && !seen.Contains(picked.Id)) Selected = null;

        Mark();
        InvalidateArrange();
        _cables.InvalidateVisual();
    }

    /// <summary>Says where a block was left, once the hand has finished with it.</summary>
    private void Left(string id)
    {
        if (!_places.TryGetValue(id, out var at)) return;

        Moved?.Invoke(id, at.X, at.Y);
    }

    /// <summary>Tells each block whether it is the one picked out.</summary>
    private void Mark()
    {
        foreach (var (id, block) in _blocks)
            block.IsSelected = Selected is { } picked && string.Equals(picked.Id, id, StringComparison.Ordinal);
    }

    /// <summary>The node with that id, or nothing where the list no longer holds one.</summary>
    private PatchNode? Find(string id)
    {
        foreach (var node in Nodes)
            if (string.Equals(node.Id, id, StringComparison.Ordinal)) return node;

        return null;
    }

    /// <summary>Places the cable layer over the whole surface and each block where it is.</summary>
    protected override Size ArrangeOverride(Size size)
    {
        _cables.Arrange(new Rect(size));

        foreach (var (id, block) in _blocks)
        {
            var at = _places.TryGetValue(id, out var place) ? place : default;

            block.Measure(size);
            block.Arrange(new Rect(at + _pan, block.DesiredSize));
        }

        return size;
    }

    /// <summary>Moves one block, held inside the surface so it cannot be dragged out of sight.</summary>
    private void Move(string id, Vector by)
    {
        if (!_places.TryGetValue(id, out var at)) return;

        _places[id] = new Point(at.X + by.X, at.Y + by.Y);

        InvalidateArrange();
        _cables.InvalidateVisual();
    }

    /// <summary>
    /// Takes hold of a cable: the one already on this point, or a new one from it.
    /// </summary>
    /// <remarks>
    /// **A point that already has a cable hands that cable over rather than starting a second
    /// one**, which is what makes a cable re-attachable: the far end stays where it is and the
    /// end you took moves with the pointer, exactly as a real one does. Dropped on nothing it is
    /// out, which is the only way to unplug something and needs no second gesture to learn.
    /// </remarks>
    private void Grab(PatchPort port, PointerPressedEventArgs e)
    {
        if (port.Fixed) return;

        _moving = null;
        _anchor = port;

        foreach (var link in Links)
        {
            if (link.From == port)
            {
                _moving = link;
                _anchor = link.To;
                break;
            }

            if (link.To == port)
            {
                _moving = link;
                _anchor = link.From;
                break;
            }
        }

        _loose = e.GetPosition(this);

        e.Pointer.Capture(this);
        e.Handled = true;

        _cables.InvalidateVisual();
    }

    /// <summary>
    /// A held modifier or the middle button takes hold of the whole surface, which is then
    /// dragged about.
    /// </summary>
    /// <remarks>
    /// The waveform's own gesture, through the waveform's own rule: every plain press on this
    /// page already means something, since a block is dragged and a dot starts a cable, so the
    /// one that moves the page has to be a press that cannot be made by accident. It works
    /// anywhere, blocks included, because the whole point of it is to reach a part of the picture
    /// that is off the page.
    /// </remarks>
    private void Grabbed(object? sender, PointerPressedEventArgs e)
    {
        var pressed = e.GetCurrentPoint(this).Properties;

        if (!Press.MeansPan(pressed.IsMiddleButtonPressed, e.KeyModifiers)) return;
        if (!pressed.IsLeftButtonPressed && !pressed.IsMiddleButtonPressed) return;

        _panning = e.GetPosition(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_panning is { } from)
        {
            var now = e.GetPosition(this);

            _pan += now - from;
            _panning = now;

            InvalidateArrange();
            _cables.InvalidateVisual();

            return;
        }

        if (_anchor is null) return;

        _loose = e.GetPosition(this);

        _cables.InvalidateVisual();
    }



    /// <summary>
    /// Lets go of the cable: onto a point that will take it, or out.
    /// </summary>
    /// <remarks>
    /// A cable put back where it came from says nothing happened rather than reporting an unplug
    /// and a plug, since a hand that changed its mind halfway through a drag has changed nothing.
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        e.Pointer.Capture(null);

        if (_panning != null)
        {
            _panning = null;
            Cursor = Cursor.Default;

            return;
        }

        if (_anchor is not { } anchor) return;

        var landed = PortAt(e.GetPosition(this));
        var moved = _moving;

        _anchor = null;
        _moving = null;

        try
        {
            if (landed is not { } port)
            {
                if (moved is { } pulled) Unwired?.Invoke(pulled);
                return;
            }

            if (!Wiring.Allowed(anchor, port)) return;

            var link = anchor.Side == PatchSide.Out
                ? new PatchLink(anchor, port)
                : new PatchLink(port, anchor);

            if (moved is { } was)
            {
                if (was == link) return;

                Unwired?.Invoke(was);
            }

            Wired?.Invoke(link);
        }
        finally
        {
            _cables.InvalidateVisual();
        }
    }

    /// <summary>Which block's port a place on the surface is on, or nothing.</summary>
    private PatchPort? PortAt(Point at)
    {
        foreach (var (id, block) in _blocks)
        {
            if (!_places.TryGetValue(id, out var place)) continue;

            var inside = at - place - _pan;

            if (inside.X < -Shape.GrabRadius || inside.Y < -Shape.GrabRadius) continue;
            if (inside.X > block.Bounds.Width + Shape.GrabRadius) continue;
            if (inside.Y > block.Bounds.Height + Shape.GrabRadius) continue;

            if (block.PortAt(new Point(inside.X, inside.Y)) is { } port) return port;
        }

        return null;
    }

    /// <summary>
    /// Whether a cable runs between two of this application's own blocks.
    /// </summary>
    /// <remarks>
    /// Drawn in another colour, because the two are different kinds of fact. **A cable to
    /// something on the machine is a patch somebody made and can move**; one between our own
    /// blocks is how this program is built, and it is on the picture so that the whole path can
    /// be read at once rather than to be pulled apart. Worked out from the blocks rather than
    /// written on the cable, so there is one answer to what is ours.
    /// </remarks>
    /// <param name="link">The cable in question.</param>
    private bool Inside(PatchLink link) => IsOurs(link.From.Node) && IsOurs(link.To.Node);

    /// <summary>Whether a cable is carrying audio at this moment.</summary>
    /// <remarks>
    /// Read off the list rather than asked of anything, since what is live is worked out where
    /// the meters are and handed over: a picture that measured audio would be a second set of
    /// meters on a page that is not about levels.
    /// </remarks>
    /// <param name="link">The cable in question.</param>
    private bool Carrying(PatchLink link)
    {
        foreach (var one in Live)
            if (one == link) return true;

        return false;
    }

    /// <summary>Whether a block is one of ours, by its id.</summary>
    private bool IsOurs(string node)
    {
        foreach (var one in Nodes)
            if (string.Equals(one.Id, node, StringComparison.Ordinal)) return one.IsOurs;

        return false;
    }

    /// <summary>Where one end of a cable is on the surface, or nothing when its block has gone.</summary>
    private Point? Dot(PatchPort port, int channel)
    {
        if (!_blocks.TryGetValue(port.Node, out var block)) return null;
        if (!_places.TryGetValue(port.Node, out var place)) return null;

        return place + _pan + block.Dot(port, channel);
    }

    /// <summary>
    /// The cables, painted under the blocks.
    /// </summary>
    /// <remarks>
    /// A layer of its own rather than the surface painting them, because a Panel paints itself
    /// before its children and there is no way to ask it to paint between two of them. It reads
    /// the surface it was made with rather than being told the cables, since what it draws is
    /// where the blocks have got to and that is the surface's answer.
    /// </remarks>
    private sealed class PatchCables : Control
    {
        /// <summary>The surface whose blocks and cables this is drawing.</summary>
        private readonly PatchbayView _bay;

        /// <summary>Takes the surface it belongs to.</summary>
        public PatchCables(PatchbayView bay) => _bay = bay;

        /// <inheritdoc/>
        /// <remarks>
        /// One curve per wire rather than per cable, so a stereo pair is two lines and a mono
        /// source into a stereo input is one line that fans into both: the picture says exactly
        /// what the machine underneath is being asked to do, which is the whole reason the
        /// channel pairing is one rule.
        /// </remarks>
        public override void Render(DrawingContext context)
        {
            var palette = ThemePalette.From(this);

            var dashes = new DashStyle(new double[] { 5, 4 }, 0);

            var patched = ThemePalette.Alpha(palette.Accent, 0xC0);
            var inside = ThemePalette.Alpha(Colours.Counter(palette.Accent), 0xC0);

            var pens = new Dictionary<(bool Inside, bool Live), IPen>
            {
                [(false, true)] = new Pen(new SolidColorBrush(patched), 2),
                [(false, false)] = new Pen(new SolidColorBrush(patched), 2, dashes),
                [(true, true)] = new Pen(new SolidColorBrush(inside), 2),
                [(true, false)] = new Pen(new SolidColorBrush(inside), 2, dashes)
            };

            var hand = new Pen(new SolidColorBrush(palette.Text), 2, new DashStyle(new double[] { 3, 3 }, 0));

            foreach (var link in _bay.Links)
            {
                var wire = pens[(_bay.Inside(link), _bay.Carrying(link))];

                foreach (var (from, to) in Wiring.Pairs(link.From.Channels, link.To.Channels))
                {
                    var start = _bay.Dot(link.From, from);
                    var end = _bay.Dot(link.To, to);

                    if (start is { } a && end is { } b) Draw(context, wire, a, b);
                }
            }

            if (_bay._anchor is not { } anchor) return;

            var held = _bay.Dot(anchor, 0);

            if (held is { } at) Draw(context, hand, at, _bay._loose);
        }

        /// <summary>Draws one wire, bent so it leaves and arrives horizontally.</summary>
        private static void Draw(DrawingContext context, IPen pen, Point from, Point to)
        {
            var (x1, y1, x2, y2) = Shape.Curve(from.X, from.Y, to.X, to.Y);

            var line = new StreamGeometry();

            using (var draw = line.Open())
            {
                draw.BeginFigure(from, false);
                draw.CubicBezierTo(new Point(x1, y1), new Point(x2, y2), to);
                draw.EndFigure(false);
            }

            context.DrawGeometry(null, pen, line);
        }
    }
}
