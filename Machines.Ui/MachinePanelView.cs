using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A machine's front panel, drawn from what the machine says it looks like.
/// </summary>
/// <remarks>
/// The other half of <see cref="MachinePanel"/>. The machine describes a tree of elements and
/// this turns that tree into knobs, faders and frames, so a machine can change its face without
/// anything here being recompiled and a machine written by somebody else has a face at all.
///
/// Two jobs in one control, which is why <see cref="Designing"/> exists. Playing a machine and
/// laying one out want the same picture and opposite pointers: a knob you can turn cannot also
/// be a knob you can pick up, so in designing mode the controls stop listening to the pointer
/// and a transparent skin over each element takes the press instead. The alternative, a second
/// control that draws a preview of the panel, means two drawings to keep in step and a designer
/// that lies about what the panel will look like.
///
/// Anything it does not understand it leaves out. An element whose kind this version has never
/// heard of, or one wired to a parameter the machine does not have, draws nothing and does not
/// complain, so a panel saved by a later designer still opens here with the parts that do exist.
/// </remarks>
public class MachinePanelView : Decorator
{
    /// <summary>
    /// The machine being drawn: its panel, its parameters and where it is kept, as one thing.
    /// </summary>
    /// <remarks>
    /// One property rather than three, because they are one machine. Handing them over
    /// separately meant every host had to remember all of them, and a host that passed the panel
    /// and forgot the folder drew a machine whose pictures could not be found.
    /// </remarks>
    public static readonly StyledProperty<MachineFace?> FaceProperty =
        AvaloniaProperty.Register<MachinePanelView, MachineFace?>(nameof(Face));

    /// <summary>Whose settings these controls stand for, read on the way in and written on every move.</summary>
    public static readonly StyledProperty<IMachineValues?> ValuesProperty =
        AvaloniaProperty.Register<MachinePanelView, IMachineValues?>(nameof(Values));

    /// <summary>
    /// Where the recordings a machine names are looked up, for the controls that show one.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Values"/> because it answers a different question. The settings
    /// say which take, by name; this says what that name is worth: its shape and its wording.
    /// Nothing here is a shelf of takes to choose from, since choosing is <see cref="TakeWanted"/>
    /// and belongs to whoever put the panel on screen.
    /// </remarks>
    public static readonly StyledProperty<IMachineTakes?> TakesProperty =
        AvaloniaProperty.Register<MachinePanelView, IMachineTakes?>(nameof(Takes));

    /// <summary>
    /// Where the machine can be started from, for the picker at the top of the panel.
    /// </summary>
    /// <remarks>
    /// Beside the settings rather than in them, because a preset is not a setting: it is a way
    /// of writing all of them at once, and what a machine offers to start from is the host's
    /// shelf and not the machine's state. Which is why nothing here is read back on the way
    /// out either. The panel writes which one was asked for and stops.
    /// </remarks>
    public static readonly StyledProperty<IMachinePresets?> PresetsProperty =
        AvaloniaProperty.Register<MachinePanelView, IMachinePresets?>(nameof(Presets));

    /// <summary>
    /// The machine's own folder, which is what the pictures on its panel are named against.
    /// </summary>
    /// <remarks>
    /// A machine travels as a folder, so a picture on its face is a file in that folder and the
    /// description says no more than the name of it. Where the folder is is the host's to know
    /// and nobody else's: the same machine sits somewhere different on every disc it is ever
    /// copied to, and a path written into machine.json would be wrong for all of them.
    ///
    /// Nothing here means no pictures, which is the state a panel is in when whatever put it on
    /// screen has no folder to offer. A picture then draws its frame and says so, rather than
    /// going looking for a file relative to wherever the program happens to have been started.
    /// </remarks>
    public static readonly StyledProperty<string?> AssetsProperty =
        AvaloniaProperty.Register<MachinePanelView, string?>(nameof(Assets));

    /// <summary>
    /// The element the designer is working on, outlined on the panel.
    /// </summary>
    /// <remarks>
    /// Two way by default. A press on the panel is one way somebody picks an element and a list
    /// beside the panel is another, and both have to end up pointing at the same thing without
    /// either having to know about the other.
    /// </remarks>
    public static readonly StyledProperty<MachineElement?> SelectedProperty =
        AvaloniaProperty.Register<MachinePanelView, MachineElement?>(
            nameof(Selected), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Whether the panel is being laid out rather than played.
    /// </summary>
    /// <remarks>
    /// On, every element can be picked and none of them can be turned. Off, the panel is an
    /// ordinary panel and nothing is selectable, so the same control can be dropped into a song
    /// without the risk of somebody selecting a knob when they meant to turn it.
    /// </remarks>
    /// <summary>
    /// What a part being dragged would go inside, outlined while the drag is over the panel.
    /// </summary>
    /// <remarks>
    /// A drop lands somewhere whether or not anybody said so, and a designer that does not show
    /// where is a designer you aim with by guessing. Told rather than worked out here, because
    /// what a drop means is the editor's rule and not the drawing's: a part let go over a knob
    /// goes in the knob's container, and this draws the answer rather than deciding it.
    /// </remarks>
    public static readonly StyledProperty<MachineElement?> MarkedProperty =
        AvaloniaProperty.Register<MachinePanelView, MachineElement?>(nameof(Marked));

    public static readonly StyledProperty<bool> DesigningProperty =
        AvaloniaProperty.Register<MachinePanelView, bool>(nameof(Designing));

    /// <summary>
    /// How far through the recording the machine has got, or -1 while nothing is sounding.
    /// </summary>
    /// <remarks>
    /// Not a setting, which is why it is here and not among the values: it is what the machine
    /// is doing this instant, and a song saved in the middle of a note should not remember where
    /// the note had got to. The panel passes it to whatever draws a recording, so a machine with
    /// two pictures on it gets a playhead on both without describing one twice.
    /// </remarks>
    public static readonly StyledProperty<double> PlayheadProperty =
        AvaloniaProperty.Register<MachinePanelView, double>(nameof(Playhead), -1);

    /// <summary>Bumped every time a note is played, which is what starts the curves moving.</summary>
    /// <remarks>
    /// A count rather than an event, so the host can hand it over as a plain binding and the
    /// panel needs nothing wiring up or taking down again.
    /// </remarks>
    public static readonly StyledProperty<int> TriggerProperty =
        AvaloniaProperty.Register<MachinePanelView, int>(nameof(Trigger));

    /// <summary>How long a note played by hand is held down for, which the envelope is drawn to.</summary>
    public static readonly StyledProperty<double> HoldSecondsProperty =
        AvaloniaProperty.Register<MachinePanelView, double>(nameof(HoldSeconds), 0.4);

    /// <summary>How far apart things sit inside a row, a column or a group.</summary>
    private const double Gap = 6;

    /// <summary>
    /// Which frame stands for which element, so the outline can move without the panel being
    /// built again.
    /// </summary>
    /// <remarks>
    /// Selection changes as fast as somebody can click, and rebuilding the tree for each one
    /// would throw away every control's state and blink the whole panel.
    /// </remarks>
    /// <summary>
    /// Who wants telling when a parameter moves, by the key of the parameter it is about.
    /// </summary>
    /// <remarks>
    /// Thrown away with the rest of the panel when it is rebuilt, since what is in here points
    /// at controls that no longer exist the moment it is.
    /// </remarks>
    private readonly Dictionary<string, List<Action>> _watchers = new(StringComparer.Ordinal);

    /// <summary>
    /// The pictures already read off the disc, by the full path each came from.
    /// </summary>
    /// <remarks>
    /// A panel showing the same badge in two places decodes it once, and one that could not be
    /// read is remembered as one that could not, so a broken name does not send the panel back
    /// to the disc every time it is drawn. Emptied with the rest of the panel when it is built
    /// again, which is also how a picture replaced on disc becomes the new one.
    ///
    /// A picture and not a bitmap, because a drawing is neither decoded nor sampled and is still
    /// something an element hangs on the panel. Past this point the two are the same thing, which
    /// is the whole reason the difference is settled where the file is opened and nowhere else.
    /// </remarks>
    private readonly Dictionary<string, IImage?> _pictures = new(StringComparer.Ordinal);

    /// <summary>
    /// The right press that has already been answered, so the frames around it leave it alone.
    /// </summary>
    /// <remarks>
    /// The press itself, not a copy of it: one press is one object however many frames it
    /// travels through, which is exactly the question being asked.
    /// </remarks>
    private object? _claimed;

    /// <summary>
    /// The handles on the element being worked on, drawn over the panel rather than in it.
    /// </summary>
    /// <remarks>
    /// Over, because a handle that took part in the layout would change the size of the thing it
    /// is measuring, and the panel would grow every time somebody picked something. It is deaf
    /// to the pointer for the same reason the selection outline is: the press that grabs a
    /// handle is caught on the way down, before the knob underneath ever sees it.
    /// </remarks>
    private readonly Handles _handles = new() { IsHitTestVisible = false };

    /// <summary>What is being sized, or nothing when nobody is dragging a handle.</summary>
    private Sizing? _sizing;

    private readonly Dictionary<MachineElement, Border> _frames = new();

    /// <summary>
    /// The same pairing the other way about, for asking what is under the pointer.
    /// </summary>
    /// <remarks>
    /// Kept rather than searched because dropping something asks this on every mouse move, and
    /// the frames are already being built anyway.
    /// </remarks>
    private readonly Dictionary<Border, MachineElement> _elements = new();

    static MachinePanelView()
    {
        AffectsMeasure<MachinePanelView>(FaceProperty, DesigningProperty);
    }

    /// <summary>Raised whenever the selection lands somewhere, for code that would rather not bind.</summary>
    /// <remarks>
    /// Raised from the property rather than from the press, so a selection made from a list
    /// beside the panel is announced the same way as one made by clicking the panel itself.
    /// </remarks>
    public event EventHandler<MachineElement>? SelectionChanged;

    /// <summary>
    /// Somebody pressed a take control, naming the text setting they want a recording put into.
    /// </summary>
    /// <remarks>
    /// The key rather than the recording, because the panel has nothing to offer: picking one
    /// means a list of what has been recorded, and that list is the host's. So the panel asks,
    /// the host puts an answer in the settings, and the panel is drawn again with it.
    /// </remarks>
    public event EventHandler<string>? TakeWanted;

    public MachineFace? Face
    {
        get => GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    /// <summary>What the machine looks like, or nothing when there is no machine.</summary>
    private MachinePanel? Panel => Face?.Panel;

    /// <summary>What its controls stand for.</summary>
    private IReadOnlyList<MachineParameter>? Parameters => Face?.Parameters;

    /// <summary>Where it is kept, which is what its pictures are named against.</summary>
    private string? Assets => Face?.Folder;

    public IMachineValues? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IMachineTakes? Takes
    {
        get => GetValue(TakesProperty);
        set => SetValue(TakesProperty, value);
    }

    public IMachinePresets? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    /// <summary>Where the machine keeps its own files, for the elements that name one.</summary>
    public MachineElement? Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    public bool Designing
    {
        get => GetValue(DesigningProperty);
        set => SetValue(DesigningProperty, value);
    }

    /// <summary>
    /// Where the thing being carried would land, drawn as a line between two elements.
    /// </summary>
    /// <remarks>
    /// Set by whoever is doing the carrying, from <see cref="Caret"/>, and cleared when the hand
    /// lets go. The panel works out where the line goes and draws it; deciding what a drop means
    /// stays with the editor, the same way the outline does.
    /// </remarks>
    public void Landing(Point? at) => _handles.At(at is { } point ? Caret(point)?.Where : null);

    /// <summary>What would take the part that is being dragged over the panel.</summary>
    public MachineElement? Marked
    {
        get => GetValue(MarkedProperty);
        set => SetValue(MarkedProperty, value);
    }

    /// <summary>Where the sound has got to, for every picture on the panel.</summary>
    public double Playhead
    {
        get => GetValue(PlayheadProperty);
        set => SetValue(PlayheadProperty, value);
    }

    /// <summary>Bumped by whoever plays a note, for every curve that comes alive when one is played.</summary>
    public int Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <summary>How long that note is held.</summary>
    public double HoldSeconds
    {
        get => GetValue(HoldSecondsProperty);
        set => SetValue(HoldSecondsProperty, value);
    }

    /// <summary>
    /// Builds the panel again when what it is a picture of has changed, and moves the outline
    /// when only the selection has.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Values is in here with the rest because the controls hold the object they write to.
        // Handed a different one, every control on the panel is still writing to the old one.
        if (change.Property == FaceProperty ||
            change.Property == ValuesProperty ||
            change.Property == TakesProperty ||
            change.Property == PresetsProperty ||
            change.Property == AssetsProperty ||
            change.Property == DesigningProperty)
        {
            Rebuild();
        }
        else if (change.Property == MarkedProperty)
        {
            ShowSelection();
        }
        else if (change.Property == SelectedProperty)
        {
            ShowSelection();

            if (change.GetNewValue<MachineElement?>() is { } picked)
                SelectionChanged?.Invoke(this, picked);
        }
    }

    /// <summary>Repaints the outline when the theme moves, since its colour was read once.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        ActualThemeVariantChanged += OnThemeChanged;
    }

    /// <summary>
    /// Takes the press that grabs a handle before anything under it can.
    /// </summary>
    /// <remarks>
    /// Tunnelling, which is the whole point: the handles lie over the machine, and a press that
    /// reached the knob first would turn it instead of sizing what holds it.
    /// </remarks>
    public MachinePanelView()
    {
        AddHandler(PointerPressedEvent, Grabbed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, Sizes, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, Sized, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ActualThemeVariantChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ShowSelection();

    /// <summary>Throws the old panel away and draws the description from the top.</summary>
    private void Rebuild()
    {
        _frames.Clear();
        _elements.Clear();
        _watchers.Clear();
        _pictures.Clear();

        var panel = Panel;

        if (panel is null)
        {
            Child = null;
            return;
        }

        var parameters = new Dictionary<string, MachineParameter>(StringComparer.Ordinal);

        foreach (var parameter in Parameters ?? Array.Empty<MachineParameter>())
        {
            if (parameter.Key.Length > 0)
                parameters[parameter.Key] = parameter;
        }

        var built = Build(panel.Root, parameters);

        // The layer outlives the panel it was over, so it has to be taken off the old one before
        // it can go on the new one. A control has one parent, and putting it on a second throws.
        if (_handles.Parent is Panel was) was.Children.Remove(_handles);

        // The layer only exists while the panel is being laid out. Off, the panel measures to
        // exactly what it draws, which is what a machine standing in a song has to do.
        Child = Designing && built != null
            ? new Panel { Children = { built, _handles } }
            : built;

        ShowSelection();
    }

    /// <summary>
    /// One element and everything under it, or nothing at all when this version cannot draw it.
    /// </summary>
    private Control? Build(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        Control? built = element.Element switch
        {
            MachineElementKinds.Grid => BuildGrid(element, parameters),
            MachineElementKinds.Group => BuildGroup(element, parameters),
            MachineElementKinds.Row => Fill(
                new WrapPanel { Orientation = Orientation.Horizontal }, element, parameters, Orientation.Horizontal),
            MachineElementKinds.Column => Fill(
                new StackPanel { Orientation = Orientation.Vertical }, element, parameters, Orientation.Vertical),
            MachineElementKinds.Strip => BuildStrip(element, parameters),
            MachineElementKinds.Knob => BuildKnob(element, parameters),
            MachineElementKinds.Fader => BuildFader(element, parameters),
            MachineElementKinds.Switch => BuildSwitch(element, parameters),
            MachineElementKinds.Number => BuildNumber(element, parameters),
            MachineElementKinds.Button => BuildButton(element, parameters),
            MachineElementKinds.Led => BuildLed(element, parameters),
            MachineElementKinds.Meter => BuildMeter(element, parameters),
            MachineElementKinds.Choice => BuildChoice(element, parameters),
            MachineElementKinds.Keys => BuildKeys(element, parameters),
            MachineElementKinds.Wave => BuildWave(element, parameters),
            MachineElementKinds.Envelope => BuildEnvelope(element, parameters),
            MachineElementKinds.Image => BuildImage(element),
            MachineElementKinds.Take => BuildTake(element),
            MachineElementKinds.Preset => BuildPreset(element, parameters),
            MachineElementKinds.Label => BuildLabel(element),
            MachineElementKinds.Spacer => BuildSpacer(element),
            _ => null,
        };

        // A control that cannot be drawn yet. While the panel is being laid out that is an
        // ordinary state and not a fault: a knob dropped on a machine names no parameter for as
        // long as it takes to pick one, and a part that disappears the moment it lands is a part
        // nobody can place. Off the bench it draws nothing, as it always has, because a machine
        // in a song must not show its own loose ends.
        built ??= Designing ? Waiting(element) : null;

        if (built is null) return null;

        Sized(element, built);
        Tipped(element, built);

        return Apart(element, Skin(element, built, Holds(element.Element)));
    }

    /// <summary>
    /// The size the description asks for, on the element itself rather than on the skin round it.
    /// </summary>
    /// <remarks>
    /// Every kind takes these, since how much room a thing takes is a question about the panel
    /// and not about what the thing is. The controls that already have a size of their own set
    /// theirs first and this writes over it, which is the way round it wants to be: what the
    /// panel says beats what the control would have chosen.
    /// </remarks>
    private static void Sized(MachineElement element, Control control)
    {
        // Pushed to the corner as well as sized. A control given a width inside a container that
        // stretches is centred in what is left over, which is Avalonia being helpful and is
        // never what a front panel wants: a picker 150 wide on a panel 1500 wide belongs at the
        // left hand end, where the machine's own panel puts it, not in the middle of the room.
        // The preset picker is the one control whose width is not the width of the control. It
        // spends what it is given on the name, because the arrows, the count and the category in
        // front of it are fixed, and BuildPreset has already done that. Setting it here as well
        // would be the same number said twice in two different senses, and the outer one wins:
        // the picker would be drawn 258 wide inside a box 258 wide that also has to hold a
        // category list, and what does not fit is drawn over whatever stands beside it.
        if (element.Element != MachineElementKinds.Preset &&
            Measurement(element, "width") is { } width)
        {
            control.Width = width;
            control.HorizontalAlignment = HorizontalAlignment.Left;
        }

        if (Measurement(element, "height") is { } height)
        {
            control.Height = height;
            control.VerticalAlignment = VerticalAlignment.Top;
        }
    }

    /// <summary>
    /// A stand-in for a part that has nothing to draw yet, with what it is waiting for on it.
    /// </summary>
    /// <remarks>
    /// The wording is the question: a knob with no parameter says so, and one naming a parameter
    /// the machine has not got says that instead, which is the difference between not finished
    /// and wrong. It takes the room the real control would take, so a panel laid out around it
    /// does not jump when the parameter is picked.
    /// </remarks>
    private Control Waiting(MachineElement element)
    {
        var palette = ThemePalette.From(this);

        string said = element.Parameter.Length > 0
            ? element.Element + ": no " + element.Parameter
            : element.Element + ": pick a parameter";

        return new Border
        {
            BorderBrush = new SolidColorBrush(palette.Muted, 0.7),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(palette.Muted, 0.10),
            Padding = new Thickness(8, 6),
            MinWidth = 60,
            MinHeight = 34,
            Child = new TextBlock
            {
                Text = said,
                FontSize = 10,
                Foreground = new SolidColorBrush(palette.Muted),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
        };
    }

    /// <summary>
    /// What the element says about itself when the pointer rests on it.
    /// </summary>
    /// <remarks>
    /// Every kind takes one, since a machine explaining a control is a machine explaining a
    /// control whatever the control turns out to be. It goes on the element rather than on the
    /// skin, so that the wording follows the thing it is about when the panel is picked apart in
    /// the designer.
    ///
    /// The wording is the machine's, not ours. A knob whose name says everything needs no help
    /// text, and a machine that has thought about what its knob does can say so in one line
    /// that nobody has to open a manual for.
    /// </remarks>
    private static void Tipped(MachineElement element, Control control)
    {
        if (Text(element, "tip") is { Length: > 0 } said) ToolTip.SetTip(control, said);
    }

    /// <summary>
    /// The margin the description asks for, or none, which leaves the container to space it.
    /// </summary>
    /// <remarks>
    /// On the outside of the skin, so that while designing the outline hugs the element and the
    /// margin is the gap between one outline and the next. Inside it, the outline would be drawn
    /// round the empty space as well and two elements side by side would look joined.
    /// </remarks>
    private static Control Apart(MachineElement element, Control control)
    {
        if (Edges(element, "margin") is { } margin) control.Margin = margin;

        return control;
    }

    /// <summary>
    /// Whether this kind of element has other elements inside it.
    /// </summary>
    /// <remarks>
    /// Asked because of the pointer, not the drawing. A container that was made deaf to the
    /// pointer would take its children with it, since hit testing stops at the first thing that
    /// says it is not there, and then nothing inside a group could be picked.
    /// </remarks>
    private static bool Holds(string kind) =>
        kind is MachineElementKinds.Grid
             or MachineElementKinds.Group
             or MachineElementKinds.Row
             or MachineElementKinds.Column
             or MachineElementKinds.Strip;

    private Control BuildGrid(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        var grid = new Grid();

        if (Text(element, "columns") is { Length: > 0 } columns)
        {
            // A grid whose definitions do not parse is a grid with one column, not a crash. The
            // description came off disk and may have been written by hand.
            try { grid.ColumnDefinitions = ColumnDefinitions.Parse(columns); }
            catch (Exception) { }
        }

        if (Text(element, "rows") is { Length: > 0 } rows)
        {
            try { grid.RowDefinitions = RowDefinitions.Parse(rows); }
            catch (Exception) { }
        }

        foreach (var child in element.Children)
        {
            if (Build(child, parameters) is not { } control) continue;

            Grid.SetColumn(control, Number(child, "column", 0));
            Grid.SetRow(control, Number(child, "row", 0));
            Grid.SetColumnSpan(control, Math.Max(1, Number(child, "span", 1)));

            grid.Children.Add(control);
        }

        return grid;
    }

    /// <summary>
    /// A framed part of the panel, with what it holds laid out inside the frame.
    /// </summary>
    /// <remarks>
    /// Clipped to itself. A group is a box drawn round a set of controls, and a control drawn
    /// outside the box it is supposed to be in is worse than a control cut off by it: cut off,
    /// the panel says the group is too small, which it is. That only happens to a group given a
    /// size, since one left to itself is as big as what it holds.
    /// </remarks>
    private Control BuildGroup(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        var caption = Text(element, "caption");

        return new PanelGroup
        {
            Caption = caption.Length > 0 ? caption : element.Label,
            Child = Fill(
                new WrapPanel { Orientation = Orientation.Horizontal }, element, parameters, Orientation.Horizontal),
            ClipToBounds = true,
        };
    }

    /// <summary>
    /// A run of cells, with each child standing on as many of them as it asks for.
    /// </summary>
    /// <remarks>
    /// The span goes on whatever <see cref="Build"/> handed back rather than on the control
    /// itself, because while designing that is the skin around it and the strip measures the
    /// skin. The same reason the grid sets its column on what it is given.
    /// </remarks>
    private Control BuildStrip(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        var strip = new PanelStrip
        {
            Orientation = Way(element, Orientation.Horizontal),
            Columns = Text(element, "columns"),
        };

        if (Measurement(element, "cell") is { } cell) strip.CellSize = cell;

        // A strip has a gap of its own, on top of the cells, so its children are spaced by the
        // strip rather than by a margin apiece. The default is the panel's, so that a strip and
        // a row put things the same distance apart unless somebody says otherwise.
        strip.Gap = Measurement(element, "gap") ?? Gap;

        foreach (var child in element.Children)
        {
            if (Build(child, parameters) is not { } control) continue;

            PanelStrip.SetSpan(control, Math.Max(1, Number(child, "span", 1)));

            strip.Children.Add(control);
        }

        return strip;
    }

    /// <summary>
    /// Puts an element's children into a container, spaced out, skipping the ones that draw
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The gap is a margin on each child rather than a spacing on the container, because the
    /// container that wraps has no spacing to set and a panel where a row and a wrapping row put
    /// things different distances apart is a panel laid out by accident. A child that names its
    /// own margin keeps it: one element standing apart from the rest is the reason to write one.
    ///
    /// Whether the children are all as tall as the tallest is <c>equal</c>. Left to itself a
    /// child is as tall as it needs to be and sits at the top of the line, which is what a knob
    /// beside a fader should do. Set, they all take the height of the line, which is what a row
    /// of framed sections should do: sections of different heights are a ragged edge across the
    /// panel, and the frames are what the eye follows.
    /// </remarks>
    private T Fill<T>(
        T container,
        MachineElement element,
        Dictionary<string, MachineParameter> parameters,
        Orientation flow)
        where T : Panel
    {
        double gap = Measurement(element, "gap") ?? Gap;
        bool equal = Flag(element, "equal");

        var built = new List<(Control Control, MachineElement Element)>();

        foreach (var child in element.Children)
        {
            if (Build(child, parameters) is { } control) built.Add((control, child));
        }

        for (int i = 0; i < built.Count; i++)
        {
            var (control, child) = built[i];

            if (!Has(child, "margin"))
            {
                // Down a column the last child needs no gap under it, since nothing follows it.
                // Across a row it does, because a row wraps and what follows may be underneath.
                control.Margin = flow == Orientation.Horizontal
                    ? new Thickness(0, 0, gap, gap)
                    : new Thickness(0, 0, 0, i == built.Count - 1 ? 0 : gap);
            }

            if (!equal) control.VerticalAlignment = VerticalAlignment.Top;

            container.Children.Add(control);
        }

        return container;
    }

    private Control? BuildKnob(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var knob = new Knob
        {
            Label = Caption(element, parameter),
            Unit = parameter.Unit,
            Minimum = parameter.Min,
            Maximum = parameter.Max,
            SmallStep = parameter.Step,
            LargeStep = parameter.Step * 10,
            DefaultValue = parameter.Default,
            Format = Format(parameter),
            // A machine prints a control's name above it, which is what Knob's own remarks say
            // this switch is for.
            LabelAbove = true,
            Value = Start(parameter),
        };

        // Subscribed after the starting value is in, or opening a panel would write every
        // parameter back over itself.
        knob.PropertyChanged += (_, e) =>
        {
            if (e.Property == Knob.ValueProperty) Write(parameter.Key, knob.Value);
        };

        // What the knob writes under itself, where the number it turns is not the number anybody
        // wants to read. A filter's dial is the case: it turns a position and it says hertz, and
        // only the machine knows how one becomes the other, so the machine is asked for the
        // wording rather than the panel working it out.
        if (Text(element, "display") is { Length: > 0 } readout)
        {
            knob.Display = Setting(readout);

            Watch(parameter.Key, () => knob.Display = Setting(readout));
        }

        return knob;
    }

    private Control? BuildFader(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var fader = new Fader
        {
            Label = Caption(element, parameter),
            Unit = parameter.Unit,
            Minimum = parameter.Min,
            Maximum = parameter.Max,
            SmallStep = parameter.Step,
            LargeStep = parameter.Step * 10,
            DefaultValue = parameter.Default,
            Format = Format(parameter),
            Ticks = Text(element, "ticks"),
            Value = Start(parameter),
        };

        fader.PropertyChanged += (_, e) =>
        {
            if (e.Property == Fader.ValueProperty) Write(parameter.Key, fader.Value);
        };

        return fader;
    }

    private Control? BuildSwitch(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        // Everything a machine is set to is a double, switches included, so a switch is a
        // parameter read as one of two ends: anything past halfway is on, and off is the bottom
        // of the range rather than zero, since a range need not include zero.
        double middle = (parameter.Min + parameter.Max) / 2;

        var toggle = new Switch
        {
            Label = Caption(element, parameter),
            IsChecked = Start(parameter) > middle,
        };

        // Only when the panel says so: the switch words itself on and off, and a machine that
        // has nothing better to call the two ends is better off with those than with blanks.
        if (Text(element, "on") is { Length: > 0 } on) toggle.OnLabel = on;
        if (Text(element, "off") is { Length: > 0 } off) toggle.OffLabel = off;

        toggle.PropertyChanged += (_, e) =>
        {
            if (e.Property == Switch.IsCheckedProperty)
                Write(parameter.Key, toggle.IsChecked ? parameter.Max : parameter.Min);
        };

        return toggle;
    }

    private Control? BuildNumber(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var field = new NumberField
        {
            Minimum = parameter.Min,
            Maximum = parameter.Max,
            SmallStep = parameter.Step,
            LargeStep = parameter.Step * 10,
            Format = Format(parameter),
            Value = Start(parameter),
        };

        field.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberField.ValueProperty) Write(parameter.Key, field.Value);
        };

        return Captioned(Caption(element, parameter), field);
    }

    /// <summary>
    /// A momentary button, held down for as long as the pointer or the key is.
    /// </summary>
    /// <remarks>
    /// The press and the release are taken off the pointer rather than off the button's own
    /// Pressed event, which fires once, on release, and so could only ever say that something
    /// happened and not for how long. The handlers ask to hear about events already marked
    /// handled, since the button handles both of them itself.
    ///
    /// The lamp, where the button has one, follows the press rather than being read back out
    /// of the value, because nothing tells this panel that a value has moved since it was
    /// drawn. It is the same thing either way while a person is doing the pressing.
    /// </remarks>
    private Control? BuildButton(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var cap = Text(element, "cap");

        var button = new PushButton
        {
            Label = Caption(element, parameter),
            CapText = cap.Length > 0 ? cap : null,
            HasLamp = Flag(element, "lamp"),
            Lit = Start(parameter) > Middle(parameter),
        };

        bool held = false;

        void Hold(bool down)
        {
            // A release nobody pressed is not a release: focus can arrive on a button with a
            // key already on its way up, and that must not write the bottom of the range.
            if (down == held) return;

            held = down;
            button.Lit = down;

            Write(parameter.Key, down ? parameter.Max : parameter.Min);
        }

        button.AddHandler(
            PointerPressedEvent,
            (_, e) =>
            {
                if (!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;

                // Taking the pointer is what makes the release arrive here even when it lands
                // somewhere else. Without it, sliding off the cap before letting go leaves the
                // button held down for ever and the parameter stuck at its top.
                e.Pointer.Capture(button);

                Hold(true);
            },
            RoutingStrategies.Bubble, handledEventsToo: true);

        button.AddHandler(
            PointerReleasedEvent, (_, _) => Hold(false),
            RoutingStrategies.Bubble, handledEventsToo: true);

        button.AddHandler(
            PointerCaptureLostEvent, (_, _) => Hold(false),
            RoutingStrategies.Bubble, handledEventsToo: true);

        button.AddHandler(
            KeyDownEvent, (_, e) => { if (Presses(e.Key)) Hold(true); },
            RoutingStrategies.Bubble, handledEventsToo: true);

        button.AddHandler(
            KeyUpEvent, (_, e) => { if (Presses(e.Key)) Hold(false); },
            RoutingStrategies.Bubble, handledEventsToo: true);

        return button;
    }

    /// <summary>Which keys work a button that has the focus, which is what the button itself takes.</summary>
    private static bool Presses(Key key) => key is Key.Space or Key.Enter;

    /// <summary>
    /// A lamp, lit by the top half of the parameter's range.
    /// </summary>
    /// <remarks>
    /// What is written under it is the element's own wording and nothing when it has none,
    /// rather than falling back to the parameter's name the way a knob does. A lamp is a dot,
    /// and a dot with a sentence under it is as wide as the sentence.
    ///
    /// Read only, and it reads once: the value is fetched as the panel is drawn and nothing
    /// says when it has moved since. A lamp that has to follow the sound is not this element.
    /// </remarks>
    private Control? BuildLed(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var lamp = new Led
        {
            Label = element.Label.Length > 0 ? element.Label : null,
            IsLit = Start(parameter) > Middle(parameter),
        };

        if (Measurement(element, "size") is { } size) lamp.Size = size;
        if (Colour(element, "colour") is { } colour) lamp.Colour = colour;

        return lamp;
    }

    /// <summary>
    /// A level, from where the parameter sits in its range.
    /// </summary>
    /// <remarks>
    /// The meter is on a decibel scale and wants an amplitude, not a setting, so the value is
    /// handed over as its share of the range: the bottom is silence and the top is full scale
    /// whatever the numbers either end of it are.
    ///
    /// It is given a size, since a meter draws itself and asks for no room of its own, and one
    /// left to measure itself would come out as nothing at all. Read only, and read once, for
    /// the same reason the lamp is.
    /// </remarks>
    private Control? BuildMeter(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var way = Way(element, Orientation.Vertical);
        double level = Fraction(parameter, Start(parameter));

        return new LevelMeter
        {
            Orientation = way,
            Stereo = Flag(element, "stereo"),
            Left = level,
            Right = level,
            Width = Measurement(element, "width") ?? (way == Orientation.Vertical ? 18 : 90),
            Height = Measurement(element, "height") ?? (way == Orientation.Vertical ? 90 : 18),
        };
    }

    /// <summary>
    /// One of a list of words, stored as which one.
    /// </summary>
    /// <remarks>
    /// The words are the element's, not the parameter's: the parameter is a number with a
    /// range like any other, and a machine that renames its waveforms does not thereby change
    /// what any song is set to. The chosen index is clamped into the range on the way in and
    /// on the way out, so a list longer than the parameter allows cannot write past its top.
    ///
    /// A list with nothing in it still draws its box. In the designer the options are typed
    /// after the element is dropped, and a control that appeared only once it was finished
    /// would look like one that had failed.
    /// </remarks>
    private Control? BuildChoice(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Parameter(element, parameters) is not { } parameter) return null;

        var options = Text(element, "options")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var box = new ComboBox
        {
            ItemsSource = options,
            SelectedIndex = Chosen(parameter, Start(parameter), options.Length),
        };

        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0) return;

            Write(parameter.Key, Math.Clamp(box.SelectedIndex, parameter.Min, parameter.Max));
        };

        return Captioned(Caption(element, parameter), box);
    }

    /// <summary>
    /// The keyboard, showing the octave the parameter is set to.
    /// </summary>
    /// <remarks>
    /// The octave is all of it that a machine can be wired to, and that is not a shortcoming
    /// of the wiring. The keyboard's other two ends are which notes are sounding and what to
    /// do when one is pressed, and neither is a setting: a note is an event with a beginning
    /// and an end, and this panel knows about values and nothing else. So the keys draw, the
    /// octave lamps follow the parameter, and pressing a key here sounds nothing until a
    /// machine is handed something better than <c>Get</c> and <c>Set</c> to hear it through.
    ///
    /// The parameter is optional, unlike every other control here, because a keyboard with no
    /// octave to show is still a keyboard. Naming one the machine does not have is still
    /// wrong, though, and still draws nothing.
    /// </remarks>
    private Control? BuildKeys(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Missing(element, parameters)) return null;

        var keys = new Clavier();

        if (Number(element, "keys", 0) is var count and > 0) keys.KeyCount = count;
        if (Text(element, "caption") is { Length: > 0 } caption) keys.Caption = caption;

        if (Parameter(element, parameters) is { } parameter)
        {
            keys.Octave = (int)Math.Round(Math.Clamp(Start(parameter), parameter.Min, parameter.Max));

            keys.PropertyChanged += (_, e) =>
            {
                if (e.Property == Clavier.OctaveProperty) Write(parameter.Key, keys.Octave);
            };
        }

        return keys;
    }

    /// <summary>
    /// The recording's picture, with what plays of it marked on the picture and draggable there.
    /// </summary>
    /// <remarks>
    /// The element's parameter is the text setting naming the take, not a value, so the picture
    /// is of whatever the machine is set to play. The shape comes from <see cref="Takes"/>: a
    /// panel is handed settings and a recording is not one, and where the recordings are kept is
    /// the host's business rather than the drawing's.
    ///
    /// The handles are values, and they are the only part of this that is. Each of the four names
    /// a parameter holding a fraction of the file, wired both ways, so dragging a handle writes
    /// the same setting a knob would have written and the two cannot disagree. A handle naming a
    /// parameter the machine does not have is left where it started rather than being wired to
    /// nothing.
    ///
    /// With no take, and only while designing, it is filled with a made up waveform. Somebody
    /// laying out a panel is deciding how much room the picture takes and what it sits beside,
    /// and an empty box tells them neither. The shape is worked out rather than random, so the
    /// panel looks the same every time it is drawn.
    /// </remarks>
    private Control BuildWave(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        var placeholder = Text(element, "placeholder");
        var take = Setting(element.Parameter);

        var wave = new WaveformView
        {
            Width = Measurement(element, "width") ?? 240,
            Height = Measurement(element, "height") ?? 90,
            Placeholder = placeholder.Length > 0 ? placeholder : element.Label,
            ShowMarkers = Flag(element, "showMarkers"),
            ShowLoop = Flag(element, "showLoop"),
            Peaks = take.Length > 0
                ? Takes?.Peaks(take)
                : Designing ? Demonstration() : null,
        };

        // Where the sound has got to, which the panel is told and the description is not. Bound
        // rather than set, because it moves forty times a second and the panel is built once.
        wave.Bind(WaveformView.PlayheadProperty, this.GetObservable(PlayheadProperty));

        Handle(element, "start", parameters, wave, WaveformView.StartProperty);
        Handle(element, "end", parameters, wave, WaveformView.EndProperty);
        Handle(element, "loopStart", parameters, wave, WaveformView.LoopStartProperty);
        Handle(element, "loopEnd", parameters, wave, WaveformView.LoopEndProperty);

        return wave;
    }

    /// <summary>
    /// The envelope drawn as the shape it is, from whichever four parameters hold it.
    /// </summary>
    /// <remarks>
    /// It reads the four and never writes them. The faders beside it are what somebody moves,
    /// and this is the picture of the result, so a machine with an envelope on it explains the
    /// four numbers at once in the way four numbers on their own never do.
    ///
    /// The values are read once and then followed, because a curve that only changed when the
    /// panel was rebuilt would sit still while somebody dragged a fader, which is exactly the
    /// moment they are looking at it. Whoever supplies the settings tells the panel what the
    /// parameter is worth; the panel does not poll them.
    /// </remarks>
    private Control BuildEnvelope(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        var scope = new EnvelopeScope
        {
            Width = Measurement(element, "width") ?? 150,
            Height = Measurement(element, "height") ?? 58,
            VerticalAlignment = VerticalAlignment.Top,
        };

        Segment(element, "attack", parameters, scope, EnvelopeScope.AttackMsProperty);
        Segment(element, "decay", parameters, scope, EnvelopeScope.DecayMsProperty);
        Segment(element, "sustain", parameters, scope, EnvelopeScope.SustainProperty);
        Segment(element, "release", parameters, scope, EnvelopeScope.ReleaseMsProperty);

        // What the machine is doing rather than what it is set to, so it comes off the panel.
        scope.Bind(EnvelopeScope.HoldSecondsProperty, this.GetObservable(HoldSecondsProperty));
        scope.Bind(ScopeControl.TriggerProperty, this.GetObservable(TriggerProperty));

        return scope;
    }

    /// <summary>
    /// Points one part of the curve at the parameter that holds it, and keeps it pointed there.
    /// </summary>
    /// <remarks>
    /// One way, unlike the handles on a picture of a recording: an envelope is drawn from the
    /// faders and has nothing on it to take hold of. It is told when the value moves rather than
    /// asking, so a curve on a panel costs nothing while nobody is touching the panel.
    /// </remarks>
    private void Segment(
        MachineElement element,
        string key,
        Dictionary<string, MachineParameter> parameters,
        EnvelopeScope scope,
        StyledProperty<double> property)
    {
        if (Text(element, key) is not { Length: > 0 } named) return;
        if (!parameters.TryGetValue(named, out var parameter)) return;

        scope.SetValue(property, Start(parameter));

        Watch(parameter.Key, () => scope.SetValue(property, Start(parameter)));
    }

    /// <summary>
    /// Asks to be told when that parameter moves, for a control that shows one it does not turn.
    /// </summary>
    /// <remarks>
    /// The panel is the only thing that hears about every move, since every control writes
    /// through it, so this is where one control watching another belongs. A curve drawn from
    /// four faders is the case it exists for, and it would be a poor panel that only redrew the
    /// curve once you let go.
    ///
    /// It does not hear about a value written anywhere but here, a preset being loaded most of
    /// all. That is the host's to say, by handing the panel its settings again, which draws
    /// everything from the top.
    /// </remarks>
    private void Watch(string key, Action told)
    {
        if (!_watchers.TryGetValue(key, out var list)) _watchers[key] = list = new List<Action>();

        list.Add(told);
    }

    /// <summary>
    /// Ties one handle on the picture to the parameter the description says it stands for.
    /// </summary>
    /// <remarks>
    /// The property is passed in rather than switched on, because the four handles differ in
    /// nothing but which property they are: a start and a loop start are the same fraction of
    /// the same file read off the same drag.
    /// </remarks>
    private void Handle(
        MachineElement element,
        string key,
        Dictionary<string, MachineParameter> parameters,
        WaveformView wave,
        StyledProperty<double> property)
    {
        if (Text(element, key) is not { Length: > 0 } named) return;
        if (!parameters.TryGetValue(named, out var parameter)) return;

        wave.SetValue(property, Math.Clamp(Start(parameter), parameter.Min, parameter.Max));

        // Subscribed after the starting value is in, for the reason every other control here is.
        wave.PropertyChanged += (_, e) =>
        {
            if (e.Property == property) Write(parameter.Key, wave.GetValue(property));
        };
    }

    /// <summary>
    /// Which recording the machine plays, and the way of choosing another.
    /// </summary>
    /// <remarks>
    /// What is written on it is what <see cref="Takes"/> makes of the name, since the name itself
    /// is usually a file and reads as one. With nothing set it asks to be filled, because an
    /// empty button on a panel otherwise looks like one that has failed.
    ///
    /// It names its setting when it asks, rather than being asked for a recording in general, so
    /// a machine with two of them gets its answer put in the right one.
    /// </remarks>
    private Control BuildTake(MachineElement element)
    {
        var caption = Text(element, "caption");
        var take = Setting(element.Parameter);

        var button = new PushButton
        {
            Label = caption.Length > 0 ? caption : element.Label.Length > 0 ? element.Label : null,
            CapText = Describe(take),
        };

        button.Pressed += (_, _) => TakeWanted?.Invoke(this, element.Parameter);

        return button;
    }

    /// <summary>What to write on a control standing for that take, or the invitation to pick one.</summary>
    /// <remarks>
    /// The name itself when nothing will describe it, since a name badly shown is still better
    /// than a blank: it is what the machine is playing, and somebody has to be able to tell.
    /// </remarks>
    private string Describe(string take)
    {
        if (take.Length == 0) return "Pick a recording...";

        return Takes?.Describe(take) is { Length: > 0 } described ? described : take;
    }

    /// <summary>
    /// One of what is on offer, kept with the place it came from in the list.
    /// </summary>
    /// <remarks>
    /// The picker deals in items and the shelf deals in numbers, so something has to carry the
    /// one to the other. Two presets can be called the same thing, and matching by name would
    /// then pick the first of them whichever was showing, which is why the number is carried
    /// rather than looked up again afterwards.
    /// </remarks>
    private sealed class PresetOffer(int at, string name)
    {
        /// <summary>Where in the list it was offered.</summary>
        public int At { get; } = at;

        /// <summary>What the picker writes in the field, which is all it does with an item.</summary>
        public override string ToString() => name;
    }

    /// <summary>
    /// A shelf nobody filled, for laying a panel out before there is a host to fill it.
    /// </summary>
    /// <remarks>
    /// Names of the length real ones are, and enough of them for the count beside the arrows to
    /// read as a count. The same reason the picture draws a waveform nobody recorded: somebody
    /// deciding what the picker sits beside needs to see how much room it takes, and an empty
    /// box tells them nothing.
    /// </remarks>
    private static readonly string[] Imagined =
        ["Init", "Bright Lead", "Deep Bass", "Slow Sweep", "Short Pluck"];

    /// <summary>
    /// Where the machine is started from: the presets it ships with, or your own recordings.
    /// </summary>
    /// <remarks>
    /// The list is the host's, through <see cref="Presets"/>, and so is what happens when one is
    /// chosen. All this does is show which is showing and write down that somebody asked for a
    /// different one, which is a number, so unlike the recording picker there is nothing to go
    /// and fetch and nothing to raise an event about.
    ///
    /// With no host and while designing it offers made up names, so a panel can be laid out
    /// before it is attached to anything. With no host and not designing it draws nothing: a
    /// picker offering a list that does not exist is worse than no picker.
    ///
    /// <c>width</c> is the width of the whole control, as it is on every other element, and is
    /// spent on the name, since the arrows and the count do not change size.
    /// </remarks>
    private Control? BuildPreset(MachineElement element, Dictionary<string, MachineParameter> parameters)
    {
        if (Missing(element, parameters)) return null;

        var shelf = Presets;
        var names = shelf?.Names ?? (Designing ? Imagined : null);

        if (names is null) return null;

        var chooser = new Chooser { Placeholder = "Start from..." };

        Offer(chooser, names, shelf?.Picked ?? 0);

        // Worked back from the whole width rather than set straight, because the arrows and the
        // count are a fixed part of the control and only the name can give.
        if (Measurement(element, "width") is { } width)
            chooser.FieldWidth = Math.Max(40, width - chooser.Chrome);

        // Subscribed after the starting one is in, for the reason every other control here is.
        if (shelf is not null)
        {
            chooser.PropertyChanged += (_, e) =>
            {
                if (e.Property == Chooser.SelectedItemProperty)
                    shelf.Picked = chooser.SelectedItem is PresetOffer one ? one.At : -1;
            };
        }

        // Nothing to narrow on a machine that ships five presets, and everything to narrow on a
        // shelf holding every recording you have ever made. So the categories are the shelf's to
        // offer, and where it offers none this is one control wide, as it always was.
        var narrowing = shelf?.Filters ?? Array.Empty<string>();

        if (narrowing.Count == 0) return Captioned(Heading(element, shelf), chooser);

        var filter = new ComboBox
        {
            ItemsSource = narrowing,
            SelectedItem = shelf!.Filter is { Length: > 0 } held && narrowing.Contains(held)
                ? held
                : narrowing[0],
            VerticalAlignment = VerticalAlignment.Center,
            Width = 132,
        };

        // The list under the picker is a different list once a category is chosen, so the names
        // are read again rather than filtered here. Which one is showing is read again with
        // them: the shelf may well have moved to the first of the narrowed list, and if it has
        // not, the picker should be showing nothing rather than the last one by its number.
        filter.SelectionChanged += (_, _) =>
        {
            if (filter.SelectedItem is not string chosen) return;

            shelf.Filter = chosen;

            Offer(chooser, shelf.Names, shelf.Picked);
        };

        var side = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        side.Children.Add(filter);
        side.Children.Add(chooser);

        return Captioned(Heading(element, shelf), side);
    }

    /// <summary>Fills the picker with what is on offer now, and shows whichever one is picked.</summary>
    private static void Offer(Chooser chooser, IReadOnlyList<string> names, int picked)
    {
        var offered = new List<PresetOffer>(names.Count);

        for (int i = 0; i < names.Count; i++) offered.Add(new PresetOffer(i, names[i]));

        chooser.ItemsSource = offered;
        chooser.SelectedItem = picked >= 0 && picked < offered.Count ? offered[picked] : null;
    }

    /// <summary>
    /// What the picker is called: the panel's word for it, or the machine's own.
    /// </summary>
    /// <remarks>
    /// The panel wins, since a machine offering presets that are really something else is
    /// exactly the case the property is there for. Failing both, "Preset", which is what the
    /// list is on every machine but one.
    /// </remarks>
    private static string Heading(MachineElement element, IMachinePresets? shelf)
    {
        if (Text(element, "caption") is { Length: > 0 } said) return said;
        if (element.Label.Length > 0) return element.Label;

        return shelf?.Caption is { Length: > 0 } called ? called : "Preset";
    }

    /// <summary>A waveform nobody recorded: four hits, each falling away from its attack.</summary>
    private static float[] Demonstration()
    {
        const int Points = 512;

        var peaks = new float[Points];

        for (int i = 0; i < Points; i++)
        {
            double at = (double)i / Points;
            double since = at * 4 % 1;

            // The fall is what makes it read as a recording; the ripple is what stops the fall
            // reading as a drawn curve.
            double fall = Math.Exp(-6 * since);
            double ripple = 0.55 + 0.45 * Math.Sin(at * Math.PI * 37);

            peaks[i] = (float)(fall * ripple * 0.92);
        }

        return peaks;
    }

    /// <summary>
    /// A line of text on the panel: its own wording, or what a text setting says.
    /// </summary>
    /// <remarks>
    /// Naming a setting is how a panel writes down something it was told rather than something
    /// it was built with, which is a recording's name and little else today. Its own wording
    /// when the setting is empty, so a label wired to a machine that has not been pointed at
    /// anything yet still says what it is for.
    ///
    /// No colour set on purpose. Foreground is inherited, so the text follows a theme swap
    /// without this control having to hear about one.
    /// </remarks>
    private Control BuildLabel(MachineElement element)
    {
        var said = Setting(element.Parameter);

        return new TextBlock
        {
            Text = said.Length > 0 ? said : element.Label,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Control BuildSpacer(MachineElement element)
    {
        var spacer = new Control();

        if (Measurement(element, "width") is { } width) spacer.Width = width;
        if (Measurement(element, "height") is { } height) spacer.Height = height;

        return spacer;
    }

    /// <summary>
    /// A picture the machine carries, drawn in the room the panel gives it.
    /// </summary>
    /// <remarks>
    /// The one element that shows nothing about the machine and turns nothing on it. A logo or a
    /// badge is neither a setting nor a reading, and a machine that wants a picture to change
    /// with what it is doing wants a control instead.
    ///
    /// What it names is a file inside the machine's own folder. That name arrived in a
    /// description written by whoever built the machine, the same way a zip entry does, so it is
    /// a claim and is checked as one before anything is opened. A name that will not resolve, a
    /// file that will not open, and no folder to look in all end the same way: the frame, and a
    /// word in it. Never an exception, because a bad file has to cost a picture rather than the
    /// machine it is on.
    ///
    /// It can be a drawing as easily as a photograph, and everything below this line is written
    /// as though it made no difference, because to the element it makes none. The width, the
    /// height and the fit are the same question either way; all that changes is who opens the
    /// file.
    /// </remarks>
    private Control BuildImage(MachineElement element)
    {
        double width = Measurement(element, "width") ?? 120;
        double height = Measurement(element, "height") ?? 60;

        var file = Text(element, "file");

        if (Locate(file) is { } path && Decoded(path) is { } picture)
        {
            return new Image
            {
                Source = picture,
                Stretch = Fitted(element),
                Width = width,
                Height = height,
                // Drawn at its own size, a picture is as big as it was made and would otherwise
                // spill over whatever stands beside it. What the panel says is how much room it
                // has, whichever way it is fitted into that room.
                ClipToBounds = true,
            };
        }

        return new PicturePlaceholder
        {
            Word = Wanting(element, file),
            Width = width,
            Height = height,
        };
    }

    /// <summary>
    /// What the empty frame says.
    /// </summary>
    /// <remarks>
    /// An element dragged onto a panel has no file yet, and that is the state every picture on
    /// every panel starts in, so while one is being laid out the frame asks for a picture rather
    /// than reporting one missing. Named and not found, it says the name: whoever typed it is
    /// the only person who can tell what became of it, and the name is what they need to read.
    /// </remarks>
    private string Wanting(MachineElement element, string file)
    {
        if (file.Length > 0) return file;

        if (Designing) return "Choose a picture...";

        return element.Label;
    }

    /// <summary>
    /// Where that name really is, when it is inside the machine's folder, and nowhere when it
    /// is not.
    /// </summary>
    /// <remarks>
    /// The same measuring an arriving zip gets, for the same reason. A file named in a
    /// description is a claim about somebody else's disc made on this one, and "../../../etc/passwd"
    /// is a machine asking to have a panel read a file that is none of its business. So the name
    /// is resolved and then held against the folder, rather than being searched for the ways it
    /// might climb out: those cannot all be listed, and the resolved path can.
    /// </remarks>
    private string? Locate(string file)
    {
        if (file.Length == 0) return null;

        if (Assets is not { Length: > 0 } folder) return null;

        try
        {
            string root = Path.GetFullPath(folder);

            if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;

            string full = Path.GetFullPath(Path.Combine(root, file));

            if (!full.StartsWith(root, StringComparison.Ordinal)) return null;

            return File.Exists(full) ? full : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>That file as a picture, or nothing when it is not one.</summary>
    /// <remarks>
    /// A file that would not open is written down as one that would not, so the answer is the
    /// same the second time it is asked for and the disc is not touched again for it. A drawing
    /// that will not parse is a file that would not open, and lands in the same place: an empty
    /// frame, never an exception thrown through the middle of a panel being built.
    ///
    /// Which reader by the name, since the name is what a picture format announces itself by
    /// everywhere else a file is passed around, and reading the first few bytes to decide would
    /// be a second answer to a question already answered. A machine that lies about which it has
    /// gets an empty frame, which is what a machine that lies about a picture deserves.
    /// </remarks>
    private IImage? Decoded(string path)
    {
        if (_pictures.TryGetValue(path, out var held)) return held;

        IImage? picture = null;

        try
        {
            picture = Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase)
                ? Drawing(path)
                : new Bitmap(path);
        }
        catch (Exception)
        {
        }

        _pictures[path] = picture;

        return picture;
    }

    /// <summary>The drawing in that file, or nothing when there is nothing in it to draw.</summary>
    /// <remarks>
    /// Read as a stream rather than by its path, so the file is shut the moment it has been
    /// parsed. A badge is read once and drawn for as long as the panel is open, and a machine's
    /// folder somebody is still editing should not have a handle held open across all of it.
    ///
    /// What comes back is the shape and not a picture of the shape, which is the point of the
    /// whole exercise: the element is given a width and a height by the panel, and the drawing
    /// is laid down at that size instead of being stretched up from the size somebody saved it
    /// at. Nothing here is cached; <see cref="Decoded"/> does that for both kinds at once.
    /// </remarks>
    private static IImage? Drawing(string path)
    {
        using var file = File.OpenRead(path);

        return SvgSource.Load(file) is { } drawing ? new SvgImage { Source = drawing } : null;
    }

    /// <summary>How the picture takes the room it is given, its shape kept unless it says not.</summary>
    private static Stretch Fitted(MachineElement element) =>
        Text(element, "fit").ToLowerInvariant() switch
        {
            "fill" => Stretch.Fill,
            "none" => Stretch.None,
            _ => Stretch.Uniform,
        };

    /// <summary>
    /// The frame a picture leaves behind when there is no picture.
    /// </summary>
    /// <remarks>
    /// Drawn rather than built out of a border and a line of text so that it follows a theme
    /// swap the moment one happens, which is what <see cref="ThemedControl"/> is for. The little
    /// hill and sun are there because the word alone reads as an error: a panel being laid out
    /// is full of empty frames, and this one has to say which of them is waiting for a picture.
    /// </remarks>
    private sealed class PicturePlaceholder : ThemedControl
    {
        /// <summary>What is written in the frame, or nothing, which leaves the frame empty.</summary>
        /// <remarks>
        /// A plain property and not a styled one. Nothing ever changes what an empty frame says:
        /// the panel is thrown away and drawn again when the description under it moves.
        /// </remarks>
        public string Word { get; init; } = "";

        public override void Render(DrawingContext context)
        {
            double width = Bounds.Width;
            double height = Bounds.Height;

            if (width <= 1 || height <= 1) return;

            var palette = ThemePalette.From(this);
            var area = new Rect(0, 0, width, height);

            context.DrawRectangle(
                new SolidColorBrush(palette.Background),
                new Pen(new SolidColorBrush(palette.Border), 1),
                new RoundedRect(area, 4));

            var text = Wording(palette);

            // The sketch takes what is left over the wording, and goes when there is not enough
            // of that to draw anything anybody would recognise.
            double spare = height - (text?.Height ?? 0) - 10;

            if (spare > 14) Sketch(context, palette, area, spare);

            if (text == null) return;

            context.DrawText(text, new Point(
                Math.Max(4, (width - text.Width) / 2),
                Math.Max(2, height - text.Height - 5)));
        }

        /// <summary>The word, laid out once, since where it goes depends on how big it came out.</summary>
        private FormattedText? Wording(ThemePalette palette)
        {
            if (Word.Length == 0) return null;

            return new FormattedText(
                Word,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                new SolidColorBrush(palette.Muted));
        }

        /// <summary>A hill, a smaller hill and a sun: what everybody draws when they mean picture.</summary>
        private static void Sketch(DrawingContext context, ThemePalette palette, Rect area, double spare)
        {
            double size = Math.Min(Math.Min(area.Width - 12, spare), 46);

            if (size < 14) return;

            double left = area.X + (area.Width - size) / 2;
            double top = area.Y + 5 + (spare - size) / 2;

            var brush = new SolidColorBrush(palette.Muted, 0.55);

            context.DrawEllipse(brush, null, new Point(left + size * 0.74, top + size * 0.26), size * 0.1, size * 0.1);

            context.DrawGeometry(brush, null, Hill(left + size * 0.04, top + size * 0.92, size * 0.62));
            context.DrawGeometry(brush, null, Hill(left + size * 0.5, top + size * 0.92, size * 0.46));

            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(palette.Muted, 0.55), 1),
                new RoundedRect(new Rect(left, top, size, size * 0.94), 3));
        }

        /// <summary>One hill, standing on that point and as wide as it is asked to be.</summary>
        private static Geometry Hill(double left, double bottom, double width)
        {
            var geometry = new StreamGeometry();

            using (var draw = geometry.Open())
            {
                draw.BeginFigure(new Point(left, bottom), isFilled: true);
                draw.LineTo(new Point(left + width / 2, bottom - width * 0.62));
                draw.LineTo(new Point(left + width, bottom));
                draw.EndFigure(isClosed: true);
            }

            return geometry;
        }
    }

    /// <summary>
    /// Wraps a built element in the skin that makes it selectable, but only while designing.
    /// </summary>
    /// <remarks>
    /// The frame carries a transparent background so a press anywhere over the element lands on
    /// it rather than falling through the gaps between controls, and a control with nothing
    /// inside it is made deaf to the pointer so a click cannot turn what it is trying to pick
    /// up. The press is marked handled on the way out, since a knob inside a group inside a grid
    /// would otherwise select all three and the outermost would win.
    ///
    /// Nothing at all when not designing: an extra element around every control would change
    /// what the panel measures to, and a panel that is a slightly different size depending on
    /// which mode it is in is a panel the designer lies about.
    /// </remarks>
    private Control Skin(MachineElement element, Control built, bool holdsOthers)
    {
        if (!Designing) return built;

        if (!holdsOthers) built.IsHitTestVisible = false;

        var frame = new Border
        {
            Child = built,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
        };

        frame.PointerPressed += (_, e) =>
        {
            // A right press picks the element up as a left one does, and then gets out of the
            // way: the menu belongs to whoever put the panel on screen, and marking the press
            // handled here would stop it opening. The innermost frame answers and the ones
            // around it stand off, which the flag does in place of the handled flag, since the
            // same press bubbles outwards through all of them.
            if (e.GetCurrentPoint(frame).Properties.IsRightButtonPressed)
            {
                if (ReferenceEquals(_claimed, e)) return;

                _claimed = e;

                Selected = element;

                return;
            }

            Selected = element;
            e.Handled = true;
        };

        _frames[element] = frame;
        _elements[frame] = element;

        return frame;
    }

    /// <summary>
    /// Which element that thing on screen belongs to, or nothing when it belongs to none.
    /// </summary>
    /// <remarks>
    /// For a drop, which arrives naming the control it landed on and not the element behind it.
    /// The walk upwards is the point: a press lands on whatever is innermost, and the answer
    /// wanted is the nearest element containing it rather than the exact control.
    ///
    /// Nothing at all when not designing, since there are no frames then and a panel being
    /// played is not a panel anything is dropped on.
    /// </remarks>
    public MachineElement? ElementAt(object? source)
    {
        for (var at = source as Visual; at != null; at = at.GetVisualParent())
        {
            if (at is Border frame && _elements.TryGetValue(frame, out var element)) return element;
        }

        return null;
    }

    /// <summary>
    /// Outlines what is picked and what a drop would land in, and clears both off everything
    /// else.
    /// </summary>
    /// <remarks>
    /// Both in the machine's own accent, told apart by weight: what you are working on has a
    /// thin line round it, and what a part would fall into has a thick one and a wash of the
    /// same colour inside it. A second hue would mean a panel wearing an accent that is not the
    /// machine's, and the wash is what makes a container read as open rather than merely outlined.
    ///
    /// The wash is barely there on purpose. It sits over the machine's own face while somebody
    /// is dragging, and a drop target that hides what it is about to change is no help.
    /// </remarks>
    private void ShowSelection()
    {
        if (_frames.Count == 0) return;

        var colour = ThemePalette.From(this).Accent;

        var accent = new SolidColorBrush(colour);
        var wash = new SolidColorBrush(colour, 0.16);

        var selected = Selected;
        var marked = Marked;

        Rect? around = null;

        foreach (var (element, frame) in _frames)
        {
            bool wanted = marked != null && ReferenceEquals(element, marked);

            if (ReferenceEquals(element, selected)) around = Bounds(frame);

            frame.BorderBrush = wanted || ReferenceEquals(element, selected) ? accent : Brushes.Transparent;
            frame.BorderThickness = new Thickness(wanted ? 2 : 1);

            // Transparent rather than none, so a press in the gap between two controls still
            // lands on the element that holds them.
            frame.Background = wanted ? wash : Brushes.Transparent;
        }

        // Only what is being worked on gets handles, and only while it is being laid out. A
        // machine in a song is a machine, and its knobs are for turning.
        _handles.Around(Designing ? around : null, colour);
    }

    /// <summary>
    /// The parameter an element turns, or nothing when it names one the machine does not have.
    /// </summary>
    private static MachineParameter? Parameter(
        MachineElement element, Dictionary<string, MachineParameter> parameters) =>
        parameters.TryGetValue(element.Parameter, out var parameter) ? parameter : null;

    /// <summary>
    /// Whether an element names a parameter the machine does not have.
    /// </summary>
    /// <remarks>
    /// For the elements that can do without one. Naming nothing is allowed; naming something
    /// that is not there is the mistake, and the two have to be told apart.
    /// </remarks>
    private static bool Missing(
        MachineElement element, Dictionary<string, MachineParameter> parameters) =>
        element.Parameter.Length > 0 && !parameters.ContainsKey(element.Parameter);

    /// <summary>What the control says: the element's own wording, or the parameter's name.</summary>
    private static string Caption(MachineElement element, MachineParameter parameter) =>
        element.Label.Length > 0 ? element.Label : parameter.Name;

    /// <summary>
    /// Writes a name over a control that has none of its own, and hands back the pair as one.
    /// </summary>
    /// <remarks>
    /// A knob and a fader draw their own name; a number field and a dropdown do not. Without
    /// this they would be the only things on a panel nobody could label.
    /// </remarks>
    private static Control Captioned(string caption, Control control)
    {
        if (caption.Length == 0) return control;

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };

        stack.Children.Add(new TextBlock { Text = caption, FontSize = 11 });
        stack.Children.Add(control);

        return stack;
    }

    /// <summary>
    /// Halfway up a parameter, which is where off becomes on.
    /// </summary>
    /// <remarks>
    /// The middle of the range rather than zero, since a range need not include zero. The same
    /// rule the switch has always used, said once now that the button and the lamp want it too.
    /// </remarks>
    private static double Middle(MachineParameter parameter) => (parameter.Min + parameter.Max) / 2;

    /// <summary>How far up its range a value sits, from nothing to all of it.</summary>
    /// <remarks>
    /// A parameter with no range at all is a fixed number, and a fixed number is at the bottom
    /// rather than being a division by nothing.
    /// </remarks>
    private static double Fraction(MachineParameter parameter, double value)
    {
        double span = parameter.Max - parameter.Min;

        return span <= 0 ? 0 : Math.Clamp((value - parameter.Min) / span, 0, 1);
    }

    /// <summary>Which of a list of options a value picks out, or none when the list is empty.</summary>
    /// <remarks>
    /// Clamped twice: into the parameter's range, because that is what the value may be, and
    /// into the list, because a machine may offer fewer words than its range has room for.
    /// </remarks>
    private static int Chosen(MachineParameter parameter, double value, int options)
    {
        if (options <= 0) return -1;

        double within = Math.Clamp(value, parameter.Min, parameter.Max);

        return (int)Math.Clamp(Math.Round(within), 0, options - 1);
    }

    /// <summary>Which way round an element lies, or the way its kind lies by default.</summary>
    private static Orientation Way(MachineElement element, Orientation fallback) =>
        Text(element, "orientation").ToLowerInvariant() switch
        {
            "vertical" or "down" => Orientation.Vertical,
            "horizontal" or "across" => Orientation.Horizontal,
            _ => fallback,
        };

    /// <summary>A yes or no in the description. Anything it does not recognise is no.</summary>
    private static bool Flag(MachineElement element, string key) =>
        Text(element, key).ToLowerInvariant() is "true" or "yes" or "1";

    /// <summary>A colour in the description, or nothing when it says none or says nonsense.</summary>
    private static Color? Colour(MachineElement element, string key) =>
        Color.TryParse(Text(element, key), out var colour) ? colour : null;

    /// <summary>Where the control starts, which is whatever the settings say it is.</summary>
    private double Start(MachineParameter parameter) =>
        Values is { } values ? values.Get(parameter.Key) : parameter.Default;

    private void Write(string key, double value)
    {
        Values?.Set(key, value);

        if (!_watchers.TryGetValue(key, out var told)) return;

        foreach (var one in told) one();
    }

    /// <summary>
    /// What a text setting says, or nothing when it names none and nothing when there is nobody
    /// to ask.
    /// </summary>
    /// <remarks>
    /// A text setting is not in the parameter list and cannot be: the list is ranges and steps,
    /// and a name has neither. So an element naming one that is not there is not a mistake this
    /// can see, and an empty answer is the same answer as never having been set, which is what a
    /// panel showing an unfilled setting should look like either way.
    /// </remarks>
    private string Setting(string key) =>
        key.Length > 0 && Values is { } values ? values.GetText(key) : "";

    /// <summary>
    /// How many decimals to write the value with, taken from how far one notch moves it.
    /// </summary>
    /// <remarks>
    /// A parameter that steps in whole numbers is a count of something and reads wrong as
    /// "3.00". The step is the only thing a machine says about how fine its parameter is, so it
    /// is what this has to go on.
    /// </remarks>
    private static string Format(MachineParameter parameter) =>
        parameter.Step >= 1 ? "0" : parameter.Step >= 0.1 ? "0.0" : "0.00";

    private static string Text(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var value) ? value : "";

    /// <summary>Whether the description says anything at all about that, blank not counting.</summary>
    private static bool Has(MachineElement element, string key) => Text(element, key).Length > 0;

    /// <summary>
    /// A margin in the description: one number for all four sides, or two, or four.
    /// </summary>
    /// <remarks>
    /// Whatever a Thickness can be written as, since that is what somebody typing a panel by
    /// hand will already know. Nonsense is nothing rather than a crash, the same as every other
    /// property here, because the description came off disk.
    /// </remarks>
    private static Thickness? Edges(MachineElement element, string key)
    {
        if (!Has(element, key)) return null;

        try { return Thickness.Parse(Text(element, key)); }
        catch (Exception) { return null; }
    }

    private static int Number(MachineElement element, string key, int fallback) =>
        element.Properties.TryGetValue(key, out var value) &&
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;


    /// <summary>How big a handle is, and how far from an edge a press still counts as one.</summary>
    private const double HandleSize = 9;

    /// <summary>Nothing may be sized smaller than this, or it cannot be got hold of again.</summary>
    private const double SmallestSize = 12;

    /// <summary>Which handle is being dragged, which is also which way the size may move.</summary>
    private enum Grip
    {
        Right,
        Bottom,
        Corner,
    }

    /// <summary>A size being dragged out: what is being sized, from what, and by which handle.</summary>
    private sealed record Sizing(MachineElement Element, Control Control, Grip Grip, Point From, Size Was);

    /// <summary>
    /// The handles, drawn over the panel on whatever is being worked on.
    /// </summary>
    /// <remarks>
    /// A control of its own rather than a few rectangles drawn by the panel, because the panel
    /// is a Decorator and what a Decorator draws goes under its child. Handles under the machine
    /// would be handles nobody can see.
    /// </remarks>
    private sealed class Handles : Control
    {
        private Rect? _around;
        private Rect? _caret;
        private Color _colour = Colors.White;

        /// <summary>Puts the handles round that rectangle, or takes them off the panel.</summary>
        public void Around(Rect? area, Color colour)
        {
            _around = area;
            _colour = colour;

            InvalidateVisual();
        }

        /// <summary>Puts the line where the thing being carried would land, or takes it away.</summary>
        public void At(Rect? line)
        {
            if (_caret == line) return;

            _caret = line;

            InvalidateVisual();
        }

        /// <summary>Where the three handles sit for a rectangle, in the order they are drawn.</summary>
        public static IEnumerable<(Grip Grip, Rect Where)> For(Rect area)
        {
            double half = HandleSize / 2;

            yield return (Grip.Right, new Rect(area.Right - half, area.Center.Y - half, HandleSize, HandleSize));
            yield return (Grip.Bottom, new Rect(area.Center.X - half, area.Bottom - half, HandleSize, HandleSize));
            yield return (Grip.Corner, new Rect(area.Right - half, area.Bottom - half, HandleSize, HandleSize));
        }

        public override void Render(DrawingContext context)
        {
            var fill = new SolidColorBrush(_colour);

            // The line first, so a handle sitting on top of it is still visible.
            if (_caret is { } line) context.DrawRectangle(fill, null, line, 1, 1);

            if (_around is not { } area) return;

            var edge = new Pen(new SolidColorBrush(Colors.Black, 0.55), 1);

            foreach (var (_, where) in For(area)) context.DrawRectangle(fill, edge, where, 2, 2);
        }
    }

    /// <summary>
    /// Catches a press on a handle before the machine underneath hears about it.
    /// </summary>
    /// <remarks>
    /// On the way down rather than on the way up, because a handle sits over a knob and the knob
    /// would otherwise take the press and start turning. The panel is not a control anybody
    /// turns while it is being laid out, so taking the press here costs nothing.
    /// </remarks>
    private void Grabbed(object? sender, PointerPressedEventArgs e)
    {
        if (!Designing || Selected is not { } element) return;

        if (!_frames.TryGetValue(element, out var frame) || frame.Child is not { } control) return;

        if (Bounds(frame) is not { } area) return;

        var at = e.GetPosition(this);

        foreach (var (grip, where) in Handles.For(area))
        {
            if (!where.Contains(at)) continue;

            _sizing = new Sizing(element, control, grip, at, control.Bounds.Size);

            e.Pointer.Capture(this);
            e.Handled = true;

            return;
        }
    }

    /// <summary>
    /// The size as the hand moves, put on the control and nowhere else yet.
    /// </summary>
    /// <remarks>
    /// The description is written when the hand lets go. Writing it on every move would rebuild
    /// the panel forty times a second, and the control being dragged would be thrown away and
    /// made again under the pointer, which is how a drag turns into a fight.
    /// </remarks>
    private void Sizes(object? sender, PointerEventArgs e)
    {
        if (_sizing is not { } sizing) return;

        var at = e.GetPosition(this);

        double width = sizing.Was.Width + (at.X - sizing.From.X);
        double height = sizing.Was.Height + (at.Y - sizing.From.Y);

        if (sizing.Grip != Grip.Bottom) sizing.Control.Width = Math.Max(SmallestSize, Math.Round(width));
        if (sizing.Grip != Grip.Right) sizing.Control.Height = Math.Max(SmallestSize, Math.Round(height));

        // The handles follow the size while it is being dragged, or they would sit where the
        // element used to end.
        ShowSelection();
    }

    /// <summary>Writes the size the hand left it at into the machine's own description.</summary>
    /// <remarks>
    /// Only what was dragged: a handle pulled sideways writes a width and leaves the height
    /// alone, so an element that was happy deciding its own height goes on deciding it.
    /// </remarks>
    private void Sized(object? sender, PointerReleasedEventArgs e)
    {
        if (_sizing is not { } sizing) return;

        _sizing = null;

        e.Pointer.Capture(null);

        if (sizing.Grip != Grip.Bottom) Written(sizing.Element, "width", sizing.Control.Width);
        if (sizing.Grip != Grip.Right) Written(sizing.Element, "height", sizing.Control.Height);

        Resized?.Invoke(this, sizing.Element);
    }

    private static void Written(MachineElement element, string key, double value)
    {
        if (double.IsNaN(value) || value <= 0) return;

        element.Properties[key] = ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Said when a handle has been let go and the element's size has changed with it.</summary>
    /// <remarks>
    /// For whoever is showing the panel, since the size is now part of the machine and the
    /// project has something new to save. The panel has already drawn it.
    /// </remarks>
    public event EventHandler<MachineElement>? Resized;

    /// <summary>
    /// Where something let go at that point would go: inside what, and in which place.
    /// </summary>
    /// <remarks>
    /// The place is the whole point. Dropping into a container has always put the thing at the
    /// end, which is fine for the first part and no use at all for the fourth: putting a picture
    /// after the picker means saying after, and the only way to say it is where the hand is.
    ///
    /// Over the first half of something, along the way its container runs, means before it; over
    /// the second half means after it. Over a container itself, or over nothing in particular,
    /// means the end of it, which is what dropping into open space has always meant.
    /// </remarks>
    public (MachineElement? Into, int At) Where(Point at)
    {
        var over = ElementAt(this.InputHitTest(at) as Visual);

        if (over == null || Panel?.Root is not { } root) return (null, -1);

        // A container is asked to hold the thing, not to stand aside for it.
        if (Holds(over.Element)) return (over, -1);

        var parent = Parent(root, over);

        if (parent == null) return (over, -1);

        int index = parent.Children.IndexOf(over);

        if (index < 0) return (parent, -1);

        if (!_frames.TryGetValue(over, out var frame) || Bounds(frame) is not { } area) return (parent, -1);

        bool after = Down(parent)
            ? at.Y > area.Center.Y
            : at.X > area.Center.X;

        return (parent, after ? index + 1 : index);
    }

    /// <summary>The line to draw where something would land, or nothing when it would go at the end.</summary>
    /// <remarks>
    /// A line between two things rather than a box round one, because what is being said is
    /// "here", and here is a gap. It lies across the way the container runs: down the side of a
    /// row, along the top of a column.
    /// </remarks>
    public (Rect Where, bool Down)? Caret(Point at)
    {
        var (into, index) = Where(at);

        if (into == null || index < 0) return null;

        bool down = Down(into);

        var children = into.Children;

        // The gap is drawn against whichever element is beside it: the one being pushed along,
        // or the last one when the drop is at the end.
        var beside = index < children.Count ? children[index] : children.Count > 0 ? children[^1] : null;

        if (beside == null || !_frames.TryGetValue(beside, out var frame) || Bounds(frame) is not { } area)
            return null;

        bool before = index < children.Count;

        const double Thickness = 3;

        var line = down
            ? new Rect(area.X, (before ? area.Top : area.Bottom) - Thickness / 2, area.Width, Thickness)
            : new Rect((before ? area.Left : area.Right) - Thickness / 2, area.Y, Thickness, area.Height);

        return (line, down);
    }

    /// <summary>Whether a container runs down the panel rather than across it.</summary>
    private static bool Down(MachineElement container) =>
        container.Element == MachineElementKinds.Column;

    /// <summary>The same question asked of the kind alone.</summary>
    private static bool DownKind(string kind) => kind == MachineElementKinds.Column;

    /// <summary>What holds that element, or nothing when it is the outermost one.</summary>
    private static MachineElement? Parent(MachineElement at, MachineElement wanted)
    {
        foreach (var child in at.Children)
        {
            if (ReferenceEquals(child, wanted)) return at;

            if (Parent(child, wanted) is { } found) return found;
        }

        return null;
    }

    /// <summary>Where an element's frame sits on this panel, or nothing when it is not on it.</summary>
    private Rect? Bounds(Visual frame)
    {
        if (frame.TranslatePoint(new Point(0, 0), this) is not { } corner) return null;

        return new Rect(corner, frame.Bounds.Size);
    }

    /// <summary>A size in the description, or nothing when it says none, so the default stands.</summary>
    private static double? Measurement(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
