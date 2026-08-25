using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Globalization;

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
    /// <summary>What to draw. Nothing here means nothing on screen.</summary>
    public static readonly StyledProperty<MachinePanel?> PanelProperty =
        AvaloniaProperty.Register<MachinePanelView, MachinePanel?>(nameof(Panel));

    /// <summary>
    /// The machine's parameters, which is how an element's <c>Parameter</c> becomes a range,
    /// a default and a unit.
    /// </summary>
    /// <remarks>
    /// Passed in beside the panel rather than read out of it, because the panel says where the
    /// knobs go and the parameter list says what they are worth. A panel that named a parameter
    /// twice would otherwise have to describe it twice, and the two descriptions could differ.
    /// </remarks>
    public static readonly StyledProperty<IReadOnlyList<MachineParameter>?> ParametersProperty =
        AvaloniaProperty.Register<MachinePanelView, IReadOnlyList<MachineParameter>?>(nameof(Parameters));

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
    public static readonly StyledProperty<bool> DesigningProperty =
        AvaloniaProperty.Register<MachinePanelView, bool>(nameof(Designing));

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
        AffectsMeasure<MachinePanelView>(PanelProperty, ParametersProperty, DesigningProperty);
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

    public MachinePanel? Panel
    {
        get => GetValue(PanelProperty);
        set => SetValue(PanelProperty, value);
    }

    public IReadOnlyList<MachineParameter>? Parameters
    {
        get => GetValue(ParametersProperty);
        set => SetValue(ParametersProperty, value);
    }

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
    /// Builds the panel again when what it is a picture of has changed, and moves the outline
    /// when only the selection has.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Values is in here with the rest because the controls hold the object they write to.
        // Handed a different one, every control on the panel is still writing to the old one.
        if (change.Property == PanelProperty ||
            change.Property == ParametersProperty ||
            change.Property == ValuesProperty ||
            change.Property == TakesProperty ||
            change.Property == DesigningProperty)
        {
            Rebuild();
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

        Child = Build(panel.Root, parameters);

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
            MachineElementKinds.Take => BuildTake(element),
            MachineElementKinds.Label => BuildLabel(element),
            MachineElementKinds.Spacer => BuildSpacer(element),
            _ => null,
        };

        if (built is null) return null;

        Sized(element, built);

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
        if (Measurement(element, "width") is { } width) control.Width = width;
        if (Measurement(element, "height") is { } height) control.Height = height;
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

        Handle(element, "start", parameters, wave, WaveformView.StartProperty);
        Handle(element, "end", parameters, wave, WaveformView.EndProperty);
        Handle(element, "loopStart", parameters, wave, WaveformView.LoopStartProperty);
        Handle(element, "loopEnd", parameters, wave, WaveformView.LoopEndProperty);

        return wave;
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

    /// <summary>Outlines whatever is selected now and clears the outline off everything else.</summary>
    private void ShowSelection()
    {
        if (_frames.Count == 0) return;

        var accent = new SolidColorBrush(ThemePalette.From(this).Accent);
        var selected = Selected;

        foreach (var (element, frame) in _frames)
            frame.BorderBrush = ReferenceEquals(element, selected) ? accent : Brushes.Transparent;
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

    private void Write(string key, double value) => Values?.Set(key, value);

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

    /// <summary>A size in the description, or nothing when it says none, so the default stands.</summary>
    private static double? Measurement(MachineElement element, string key) =>
        element.Properties.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
