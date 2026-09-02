using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.ComponentModel;
using JingleBox2.Rack.Machines.Interfaces;

namespace JingleBox2.Rack.Ui;

/// <summary>
/// The recording a machine is holding, cut into pieces: the picture, the boundaries, and what
/// it takes to make more or fewer of them.
/// </summary>
/// <remarks>
/// It has no way of loading anything, on purpose. The recording is whichever one the machine
/// already plays, so there is one place to put a sample and this is not it. What a piece becomes
/// afterwards, a stretch of keyboard on one machine and one key on another, is settled by
/// whoever supplied the <see cref="IMachineSlices"/>, so nothing here has to know which machine
/// it is sitting on.
///
/// It lives here rather than in the program because a machine carries it on its own face. A
/// control the app owned would be a part of the panel that could not travel in the zip, and a
/// machine somebody else built could never put one on itself.
/// </remarks>
public class ChopEditor : Decorator
{
    /// <summary>Backs <see cref="Slices"/>: the recording being cut and where its boundaries are.</summary>
    public static readonly StyledProperty<IMachineSlices?> SlicesProperty =
        AvaloniaProperty.Register<ChopEditor, IMachineSlices?>(nameof(Slices));

    /// <summary>
    /// Backs <see cref="PictureHeight"/>. The rest of the control is one row of fields, so this
    /// is very nearly the whole height of it.
    /// </summary>
    public static readonly StyledProperty<double> PictureHeightProperty =
        AvaloniaProperty.Register<ChopEditor, double>(nameof(PictureHeight), 90);

    /// <summary>Which recording is on the machine, trimmed when the name is longer than the room.</summary>
    private readonly TextBlock _take = new() { VerticalAlignment = VerticalAlignment.Center };

    /// <summary>How many pieces there are now, hidden while there is no recording to count.</summary>
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The take with every boundary on it, which is where the cutting is actually done.</summary>
    private readonly WaveformView _picture = new();

    /// <summary>How many pieces the cutting should aim for, which is a wish rather than a promise.</summary>
    private readonly NumberField _pieces = new();

    /// <summary>Which way the cutting looks for its boundaries.</summary>
    private readonly ComboBox _cutBy = new();

    /// <summary>Whether the piece in hand repeats, and how.</summary>
    private readonly ComboBox _loop = new();

    /// <summary>Cuts the recording again, throwing away wherever it is cut now.</summary>
    private readonly Button _chop = new();

    /// <summary>
    /// The machine's slices being listened to, and the handler doing it.
    /// </summary>
    /// <remarks>
    /// Both are kept so the subscription can be taken off again. The handler is a closure rather
    /// than a method, so it is not the same delegate twice and cannot be unsubscribed without
    /// having been held on to; and this control is handed a different machine every time the
    /// panel above it changes what it is showing.
    /// </remarks>
    private IMachineSlices? _watching;

    /// <inheritdoc cref="_watching"/>
    private PropertyChangedEventHandler? _listening;

    /// <summary>
    /// Builds the whole control once, and lets go of the machine when it leaves the tree.
    /// </summary>
    /// <remarks>
    /// Built in code rather than from a template because it goes on a machine's own face, and a
    /// machine that somebody else built has no access to this application's XAML.
    /// </remarks>
    public ChopEditor()
    {
        Build();

        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    /// <inheritdoc cref="SlicesProperty"/>
    public IMachineSlices? Slices
    {
        get => GetValue(SlicesProperty);
        set => SetValue(SlicesProperty, value);
    }

    /// <inheritdoc cref="PictureHeightProperty"/>
    public double PictureHeight
    {
        get => GetValue(PictureHeightProperty);
        set => SetValue(PictureHeightProperty, value);
    }

    /// <summary>
    /// Moves the listening to whichever machine has just arrived, and reads it once.
    /// </summary>
    /// <remarks>
    /// The old one is let go of first. A control handed two machines in a row would otherwise go
    /// on refreshing itself from the first for as long as anything else held it.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SlicesProperty)
        {
            Unwatch();
            Watch();
            Refresh();
        }
        else if (change.Property == PictureHeightProperty)
        {
            _picture.Height = PictureHeight;
        }
    }

    /// <summary>Starts listening to the machine now in hand, and points the picture at its list.</summary>
    private void Watch()
    {
        if (Slices is not { } slices) return;

        _watching = slices;
        _listening = (_, _) => Refresh();

        slices.PropertyChanged += _listening;

        _picture.SlicePoints = slices.Points;
    }

    /// <summary>Stops listening, so nothing here keeps a machine alive after it has been put down.</summary>
    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.PropertyChanged -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>
    /// Lays the whole thing out: the heading, the picture, and the row of fields under it.
    /// </summary>
    /// <remarks>
    /// The picture carries the whole take with every boundary on it, and dragging a boundary is
    /// truncating the two pieces it lies between, which is why there is no separate place to set
    /// where a piece starts.
    ///
    /// The row underneath is plain controls rather than the panel-style ones, because it stands
    /// next to the panel's other plain rows. A panel switch is a thing you reach for while
    /// playing; these are things you set once and leave.
    /// </remarks>
    private void Build()
    {
        _take.Classes.Add("cardHint");
        _take.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        _take.MaxWidth = 200;

        _count.Classes.Add("cardHint");

        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        var title = new TextBlock { Text = "Chop", VerticalAlignment = VerticalAlignment.Center };

        title.Classes.Add("section");

        heading.Children.Add(title);
        heading.Children.Add(_take);
        heading.Children.Add(_count);

        _picture.Height = PictureHeight;
        _picture.ShowMarkers = false;
        _picture.Placeholder = "Put a recording on this machine to chop it.";

        ToolTip.SetTip(
            _picture,
            "Drag a boundary to move it. Double-click the wave to add one, double-click a boundary "
            + "to take it away. Click a piece to work on it.");

        _picture.PropertyChanged += (_, e) =>
        {
            if (Slices is not { } slices) return;

            if (e.Property == WaveformView.SelectedSliceProperty) slices.SelectedSlice = _picture.SelectedSlice;
            else if (e.Property == WaveformView.LoopStartProperty) slices.LoopStart = _picture.LoopStart;
            else if (e.Property == WaveformView.LoopEndProperty) slices.LoopEnd = _picture.LoopEnd;
        };

        _pieces.Width = 62;
        _pieces.Format = "0";
        _pieces.Minimum = 2;
        _pieces.SmallStep = 1;
        _pieces.LargeStep = 4;
        _pieces.VerticalAlignment = VerticalAlignment.Center;

        ToolTip.SetTip(
            _pieces,
            "How many pieces to aim for. Fewer come back when the take has fewer attacks than that.");

        _pieces.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberField.ValueProperty && Slices is { } slices)
                slices.Pieces = _pieces.Value;
        };

        _cutBy.Width = 80;
        _cutBy.VerticalAlignment = VerticalAlignment.Center;

        ToolTip.SetTip(
            _cutBy,
            "Hits looks for attacks and cuts just before each one: right for drums. Gaps looks for "
            + "the silences between things and cuts where the next one starts: right for speech and "
            + "for played phrases. Even divides the take into equal pieces.");

        _cutBy.SelectionChanged += (_, _) =>
        {
            if (_cutBy.SelectedItem is string chosen && Slices is { } slices) slices.CutBy = chosen;
        };

        _chop.Content = "Chop";

        ToolTip.SetTip(
            _chop, "Cuts the recording on this machine into pieces, throwing away where it is cut now.");

        _chop.Click += (_, _) => Slices?.Chop();

        _loop.Width = 82;
        _loop.VerticalAlignment = VerticalAlignment.Center;

        ToolTip.SetTip(
            _loop,
            "Whether the piece in hand repeats. The dashed handles on the picture say where, and "
            + "stay inside the piece they belong to.");

        _loop.SelectionChanged += (_, _) =>
        {
            if (_loop.SelectedItem is string chosen && Slices is { } slices) slices.LoopName = chosen;
        };

        var fields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        fields.Children.Add(Field("Pieces"));
        fields.Children.Add(_pieces);
        fields.Children.Add(Field("Cut at", 6));
        fields.Children.Add(_cutBy);
        fields.Children.Add(_chop);
        fields.Children.Add(Field("Loop piece", 10));
        fields.Children.Add(_loop);

        var body = new StackPanel { Spacing = 6 };

        body.Children.Add(heading);
        body.Children.Add(_picture);
        body.Children.Add(fields);

        Child = body;
    }

    /// <summary>The wording in front of one of the fields, with room to its left where asked for.</summary>
    private static TextBlock Field(string said, double left = 0)
    {
        var text = new TextBlock
        {
            Text = said,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(left, 0, 0, 0),
        };

        text.Classes.Add("field");

        return text;
    }

    /// <summary>Says again what everything on it should be showing.</summary>
    /// <remarks>
    /// Everything at once and every time, rather than watching for which property moved. There
    /// are a dozen of them, they move together whenever the machine is pointed at a different
    /// recording, and setting a control to what it already says costs nothing.
    /// </remarks>
    private void Refresh()
    {
        if (Slices is not { } slices)
        {
            _picture.Peaks = null;

            return;
        }

        _take.Text = slices.TakeText;
        _count.Text = slices.CountText;
        _count.IsVisible = slices.IsOpen;

        _picture.Peaks = slices.Peaks;
        _picture.SlicePoints = slices.Points;
        _picture.MaxSlices = slices.MaxSlices;
        _picture.SelectedSlice = slices.SelectedSlice;
        _picture.ShowLoop = slices.Looping;
        _picture.Playhead = slices.Playhead;
        _picture.LoopStart = slices.LoopStart;
        _picture.LoopEnd = slices.LoopEnd;

        _pieces.Maximum = slices.MaxSlices;
        _pieces.Value = slices.Pieces;

        _cutBy.ItemsSource = slices.CutOptions;
        _cutBy.SelectedItem = slices.CutBy;

        _loop.ItemsSource = slices.LoopNames;
        _loop.SelectedItem = slices.LoopName;

        _chop.IsEnabled = slices.IsOpen;
    }
}
