using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.Rack.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Rack.Controls.Enums;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// A working example of one kind of panel part, small enough to stand on a chip in the
/// designer's library.
/// </summary>
/// <remarks>
/// A library that lists its parts by name asks you to remember what the names mean. The parts
/// of a front panel are the one set of things that never needed naming in the first place: a
/// knob looks like a knob from across the room, which is the whole reason machines are built
/// out of them. So the chip carries the part itself, turned part way and lit, rather than the
/// word for it.
///
/// A <see cref="Decorator"/> because that is exactly what this is: one built example held and
/// handed on, the same shape <see cref="PanelGroup"/> takes for the same reason. What it holds
/// is the real control off this folder's shelf, not a drawing of one, so a knob that changes
/// how it is moulded changes on the chips as well and nobody has to remember to redraw them.
///
/// Nothing inside can be pressed. The library starts a drag from the press on the chip, and a
/// live knob catching that press would turn instead of picking anything up, so the sample is
/// deaf to the pointer and skipped by tabbing. The examples are still the real controls: they
/// simply have nothing to say to either.
///
/// The containers have no face of their own, since what a row looks like is whatever is put in
/// it. Those get a diagram instead: blank boxes laid out the way that container lays things
/// out, drawn in the theme's own border colour so the diagram is as much part of the theme as
/// the controls beside it.
/// </remarks>
public sealed class PartSample : Decorator
{
    /// <summary>
    /// The kinds of part this can draw, one literal each.
    /// </summary>
    /// <remarks>
    /// Written out rather than assembled from anything, so every kind stays greppable: a name
    /// built from a variable never appears in the source as a string, and nothing that goes
    /// looking for which kinds the library covers, or which of them a machine's file names,
    /// would find it. These have to match what a panel description calls its elements.
    /// </remarks>
    private const string GridKind = "Grid";
    /// <inheritdoc cref="GridKind"/>
    private const string GroupKind = "Group";
    /// <inheritdoc cref="GridKind"/>
    private const string RowKind = "Row";
    /// <inheritdoc cref="GridKind"/>
    private const string ColumnKind = "Column";
    /// <inheritdoc cref="GridKind"/>
    private const string StripKind = "Strip";
    /// <inheritdoc cref="GridKind"/>
    private const string KnobKind = "Knob";
    /// <inheritdoc cref="GridKind"/>
    private const string FaderKind = "Fader";
    /// <inheritdoc cref="GridKind"/>
    private const string SwitchKind = "Switch";
    /// <inheritdoc cref="GridKind"/>
    private const string NumberKind = "Number";
    /// <inheritdoc cref="GridKind"/>
    private const string ButtonKind = "Button";
    /// <inheritdoc cref="GridKind"/>
    private const string LabelKind = "Label";
    /// <inheritdoc cref="GridKind"/>
    private const string SpacerKind = "Spacer";
    /// <inheritdoc cref="GridKind"/>
    private const string LedKind = "Led";
    /// <inheritdoc cref="GridKind"/>
    private const string MeterKind = "Meter";
    /// <inheritdoc cref="GridKind"/>
    private const string KeysKind = "Keys";
    /// <inheritdoc cref="GridKind"/>
    private const string LocationKind = "Location";
    /// <inheritdoc cref="GridKind"/>
    private const string WaveKind = "Wave";
    /// <inheritdoc cref="GridKind"/>
    private const string EnvelopeKind = "Envelope";
    /// <inheritdoc cref="GridKind"/>
    private const string ScopeKind = "Scope";
    /// <inheritdoc cref="GridKind"/>
    private const string ImageKind = "Image";
    /// <inheritdoc cref="GridKind"/>
    private const string ChoiceKind = "Choice";
    /// <inheritdoc cref="GridKind"/>
    private const string TakeKind = "Take";
    /// <inheritdoc cref="GridKind"/>
    private const string PresetKind = "Preset";
    /// <inheritdoc cref="GridKind"/>
    private const string PadsKind = "Pads";
    /// <inheritdoc cref="GridKind"/>
    private const string PadKind = "Pad";
    /// <inheritdoc cref="GridKind"/>
    private const string SlicesKind = "Slices";
    /// <inheritdoc cref="GridKind"/>
    private const string PadPickerKind = "PadPicker";
    /// <inheritdoc cref="GridKind"/>
    private const string ZonesKind = "Zones";
    /// <inheritdoc cref="GridKind"/>
    private const string ZonePickerKind = "ZonePicker";
    /// <inheritdoc cref="GridKind"/>
    private const string TextKind = "Text";
    /// <inheritdoc cref="GridKind"/>
    private const string MenuKind = "Menu";
    /// <inheritdoc cref="GridKind"/>
    private const string InstrumentNameKind = "InstrumentName";

    /// <summary>Which part to show. Anything this version has never heard of shows nothing.</summary>
    /// <remarks>
    /// A plain string rather than an enumeration, because the panel description names its
    /// elements in strings and a library built from that description would otherwise have to
    /// translate every name twice, once on the way in and once on the way back out.
    /// </remarks>
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<PartSample, string?>(nameof(Kind));

    /// <inheritdoc cref="KindProperty"/>
    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Makes the whole example deaf to the pointer and invisible to tabbing.
    /// </summary>
    /// <remarks>
    /// One flag at the top rather than one per control: hit testing stops at the first thing
    /// that says it is not there, so this takes the whole example with it and the press lands on
    /// the chip underneath, which is what starts the drag. A live knob catching that press would
    /// turn instead of picking anything up.
    ///
    /// Tab navigation is the same again for the keyboard, which does not care about hit testing.
    /// Without it a library of twenty chips is twenty extra stops on the way to the panel.
    /// </remarks>
    public PartSample()
    {
        IsHitTestVisible = false;

        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
    }

    /// <summary>Builds a different example whenever the kind moves.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty) Rebuild();
    }

    /// <summary>
    /// Throws the old example away and builds the one the kind now names.
    /// </summary>
    /// <remarks>
    /// The built control is unfocusable as well as the sample being untabbable: a control that
    /// thinks it can be focused still can be, reached from a child of it.
    ///
    /// It goes inside a viewbox that shrinks and never grows. A part drawn at half scale still
    /// reads as itself; one with its corner cut off does not, and one blown up past its own size
    /// stops looking like the control it is standing for.
    /// </remarks>
    private void Rebuild()
    {
        var built = Build(Kind ?? "");

        if (built is null)
        {
            Child = null;
            return;
        }

        built.Focusable = false;

        Child = new Viewbox
        {
            Child = built,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>
    /// One small example, or nothing when the kind is not one of ours.
    /// </summary>
    /// <remarks>
    /// The values are set on purpose and none of them is zero. A knob at the bottom of its
    /// travel, a meter reading silence and an unlit lamp all look like the same thing, which is
    /// a part that is not working, so each one is left somewhere it is plainly alive.
    /// </remarks>
    private static Control? Build(string kind) => kind switch
    {
        GridKind => new PartSketch(SketchShape.Grid) { Width = 56, Height = 40 },
        GroupKind => BuildGroup(),
        RowKind => new PartSketch(SketchShape.Row) { Width = 62, Height = 24 },
        ColumnKind => new PartSketch(SketchShape.Column) { Width = 32, Height = 46 },
        StripKind => new PartSketch(SketchShape.Strip) { Width = 66, Height = 32 },
        KnobKind => BuildKnob(),
        FaderKind => BuildFader(),
        SwitchKind => BuildSwitch(),
        NumberKind => BuildNumber(),
        ButtonKind => BuildButton(),
        LabelKind => BuildLabel(),
        SpacerKind => new PartSketch(SketchShape.Spacer) { Width = 46, Height = 28 },
        LedKind => BuildLeds(),
        MeterKind => BuildMeter(),
        KeysKind => BuildKeys(),
        LocationKind => BuildLocation(),
        WaveKind => new PartSketch(SketchShape.Wave) { Width = 74, Height = 34 },
        EnvelopeKind => BuildEnvelope(),
        ScopeKind => BuildScope(),
        ImageKind => new PartSketch(SketchShape.Picture) { Width = 60, Height = 40 },
        ChoiceKind => BuildChoice(),
        TakeKind => BuildTake(),
        PresetKind => BuildPreset(),
        PadsKind => BuildPads(),
        PadKind => new PushButton { CapWidth = 46, CapHeight = 26, FontSize = 9, HasLamp = true, LampBelow = false },
        SlicesKind => new PartSketch(SketchShape.Wave) { Width = 78, Height = 40 },
        PadPickerKind => BuildSlotPicker("Kick", "Snare"),
        ZonesKind => BuildZones(),
        ZonePickerKind => BuildSlotPicker("Low", "High"),
        TextKind => BuildTextBox(),
        MenuKind => BuildMenu(),
        InstrumentNameKind => BuildInstrumentNameBadge(),
        _ => null,
    };

    /// <summary>
    /// The real curve, drawn from an envelope somebody might plausibly dial in.
    /// </summary>
    /// <remarks>
    /// A quick attack and a long release, so the sample shows both ends of the shape. A flat
    /// envelope would draw a straight line and tell nobody what the part is.
    /// </remarks>
    private static Control BuildEnvelope() => new EnvelopeScope
    {
        Width = 74,
        Height = 34,
        AttackMs = 60,
        DecayMs = 220,
        Sustain = 0.55,
        ReleaseMs = 400,
    };

    /// <summary>The real frame, with a blank box standing in for whatever would go inside it.</summary>
    private static Control BuildGroup() => new PanelGroup
    {
        Caption = "GROUP",
        CaptionSize = 9,
        Inset = 5,
        Child = new PartSketch(SketchShape.Blank) { Width = 44, Height = 18 },
    };

    /// <summary>
    /// A dial turned two thirds of the way round, named the way a machine names one.
    /// </summary>
    /// <remarks>
    /// The name goes above the dial, which is what a machine's own panel does with a knob and so
    /// what the chip has to show. Everywhere else in the application it sits underneath.
    /// </remarks>
    private static Control BuildKnob() => new Knob
    {
        Label = "TONE",
        DialSize = 26,
        Minimum = 0,
        Maximum = 1,
        Value = 0.66,
        LabelAbove = true,
    };

    /// <summary>
    /// A fader a little under two thirds up, on a throw short enough for a chip.
    /// </summary>
    /// <remarks>
    /// Not at unity and not at an end: a cap resting against either end of its travel is what a
    /// fader that has not been wired up looks like.
    /// </remarks>
    private static Control BuildFader() => new Fader
    {
        Label = "LEVEL",
        TrackLength = 26,
        Minimum = 0,
        Maximum = 1,
        Value = 0.62,
    };

    /// <summary>
    /// Two positions, thrown to the upper one.
    /// </summary>
    /// <remarks>
    /// The recess and the wording are both smaller than a panel would use them. A switch is
    /// mostly the two words beside it, and at panel size those two words are taller than the
    /// chip they have to fit on.
    /// </remarks>
    private static Control BuildSwitch() => new Switch
    {
        SlotWidth = 19,
        SlotHeight = 15,
        FontSize = 8,
        OnLabel = "ON",
        OffLabel = "OFF",
        IsChecked = true,
    };

    /// <summary>A field with a number in it, since an empty one would look like a text box.</summary>
    private static Control BuildNumber() => new NumberField
    {
        Width = 64,
        Minimum = 0,
        Maximum = 16,
        Format = "0",
        Value = 8,
    };

    /// <summary>
    /// Where a machine is started from: the arrows, the name, and how far through the list you are.
    /// </summary>
    /// <remarks>
    /// The real control, narrowed to fit a chip. Its count is left on, since a picker that says
    /// how long the list is, is half of what makes it that picker rather than a dropdown.
    /// </remarks>
    private static Control BuildPreset() => new Chooser
    {
        ItemsSource = new[] { "Kick", "Snare", "Hat" },
        SelectedItem = "Snare",
        FieldWidth = 54,
        FontSize = 9,
    };

    /// <summary>
    /// Four pads, not sixteen: the chip is an inch across and what it has to say is "a grid of
    /// pads", which four of them say as plainly as sixteen would and legibly at this size.
    /// </summary>
    private static Control BuildPads()
    {
        var grid = new Grid();

        for (int at = 0; at < 2; at++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (int at = 0; at < 4; at++)
        {
            var cap = new PushButton
            {
                CapWidth = 34,
                CapHeight = 20,
                FontSize = 8,
                HasLamp = true,
                LampBelow = false,
                Margin = new Thickness(0, 0, at % 2 == 0 ? 3 : 0, at < 2 ? 3 : 0),
            };

            Grid.SetColumn(cap, at % 2);
            Grid.SetRow(cap, at / 2);

            grid.Children.Add(cap);
        }

        return grid;
    }

    /// <summary>A box with a word in it, since an empty one is a rectangle and says nothing.</summary>
    private static Control BuildTextBox() => new TextBox
    {
        Text = "Name",
        Width = 66,
        FontSize = 11,
    };

    /// <summary>
    /// The button that fetches a recording off the shelf.
    /// </summary>
    /// <remarks>
    /// An ordinary button rather than the app's own take picker, which lives in the application
    /// and cannot be reached from here. What the library has to show is the shape of the thing
    /// on the panel, and on the panel it is a button with the take's name on it.
    /// </remarks>
    private static Control BuildTake() => new Button
    {
        Content = "kick.wav",
        FontSize = 10,
        Padding = new Thickness(8, 3),
        IsHitTestVisible = false,
    };

    /// <summary>
    /// The three bars, on the same cap every other button on a machine wears.
    /// </summary>
    /// <remarks>
    /// Not lit and not held. The others in the library are left somewhere plainly alive because
    /// a knob at the bottom of its travel looks broken; this one has no travel and nothing to
    /// report, and a lamp on it would be saying something it never says.
    /// </remarks>
    private static Control BuildMenu() => new PushButton
    {
        Mark = CapMark.Bars,
        CapWidth = 30,
        CapHeight = 20,
    };

    /// <summary>
    /// The badge, with a name on it somebody might plausibly have given an instrument.
    /// </summary>
    /// <remarks>
    /// A name and not the word Name, since the chip is what the part is and a chip with its own
    /// label on it would read as a control called Name rather than as somewhere a name goes.
    /// </remarks>
    private static Control BuildInstrumentNameBadge() => new Border
    {
        Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(5, 1),
        Child = new TextBlock
        {
            Text = "Bassline",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            FontFamily = new FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,monospace"),
        },
    };

    /// <summary>A latching button, held down, with its lamp lit to say so.</summary>
    private static Control BuildButton() => new PushButton
    {
        CapText = "RUN",
        CapWidth = 46,
        CapHeight = 20,
        FontSize = 9,
        HasLamp = true,
        IsLatching = true,
        IsChecked = true,
    };

    /// <summary>
    /// A line of text, and nothing else, since that is all the element is.
    /// </summary>
    /// <remarks>
    /// No colour set. Foreground is inherited, so the sample follows a theme swap without
    /// having to hear about one, the same as the label the panel itself builds.
    /// </remarks>
    private static Control BuildLabel() => new TextBlock
    {
        Text = "Label",
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>
    /// Three lamps with the first one burning.
    /// </summary>
    /// <remarks>
    /// A row rather than a single lamp, because one dot on a chip reads as a mark on the screen
    /// and three in a line read as a panel counting something, which is what they are for.
    /// </remarks>
    private static Control BuildLeds()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };

        for (int i = 0; i < 3; i++)
            row.Children.Add(new Led { Size = 13, IsLit = i == 0 });

        return row;
    }

    /// <summary>
    /// Both bars, at two clearly different levels, so it reads as a meter rather than as a bar.
    /// </summary>
    /// <remarks>
    /// The two levels are further apart than they look. The bar is on a decibel scale, so a
    /// quarter of full scale is already four fifths of the way up, and anything above half is
    /// into the amber. These two are picked to leave the meter reading loud and unclipped, with
    /// one channel plainly under the other.
    /// </remarks>
    private static Control BuildMeter() => new LevelMeter
    {
        Width = 18,
        Height = 44,
        Stereo = true,
        Left = 0.28,
        Right = 0.09,
    };

    /// <summary>
    /// An octave and a bit, with one key sounding.
    /// </summary>
    /// <remarks>
    /// The smallest a keyboard goes and still be one. The keys shrink freely, but the head above
    /// them does not: the octave lamps carry their numbers underneath and a number has a size of
    /// its own, so the wording is taken down to where the head is about as tall as it can be
    /// asked to be. The octave marks along the bottom go, since at this width they would be the
    /// widest thing on the chip.
    /// </remarks>
    private static Control BuildKeys() => new Clavier
    {
        Octave = 0,
        OctaveCount = 2,
        KeyCount = 13,
        KeyWidth = 9,
        KeyHeight = 28,
        LampSize = 5,
        LampGap = 6,
        FontSize = 6,
        Caption = null,
        MarksOctaves = false,
        Lit = new[] { 4 },
    };

    /// <summary>
    /// The lamps and one page of buttons, with the playhead a third of the way through.
    /// </summary>
    /// <remarks>
    /// Two pages rather than the eight a full pattern has, because the chip is a chip: what it
    /// has to say is that this part is a row of buttons over a row of lamps, and a picture of
    /// the whole pattern's worth would say it at a quarter of the size.
    /// </remarks>
    private static Control BuildLocation() => new LocationView
    {
        Location = new SampleRun(),
        LampSize = 7,
        Gap = 6,
    };

    /// <summary>
    /// The map, drawn as a map: three stretches of keyboard, one of them in hand.
    /// </summary>
    /// <remarks>
    /// The real control against a stand-in, for the reason every sample here is the real control:
    /// a chip that showed a rectangle would tell nobody that this element is the picture of a
    /// keyboard shared out. Three zones rather than one, because the thing worth seeing is that
    /// there is more than one of them and that they lie side by side.
    /// </remarks>
    private static Control BuildZones() => new ZoneMapView
    {
        Zones = new SampleMap(),
        Width = 108,
        Height = 34,
        LaneHeight = 14,
        FontSize = 7,
    };

    /// <summary>
    /// Which one of a machine's things the controls beside it are about.
    /// </summary>
    /// <remarks>
    /// The pads of a kit and the zones of a map are different things, and their pickers are the
    /// same control showing a different list, which is all a chip in the library can show. So it
    /// is one drawing given the two words, rather than two drawings that would have to be kept
    /// looking alike by hand.
    /// </remarks>
    private static Control BuildSlotPicker(string first, string second) => new ComboBox
    {
        Width = 78,
        FontSize = 11,
        ItemsSource = new[] { first, second },
        SelectedIndex = 0,
    };

    /// <summary>A track a third of the way through the first of two pages.</summary>
    private sealed class SampleRun : IMachineLocation
    {
        /// <summary>
        /// Two pages rather than the eight a full pattern has.
        /// </summary>
        /// <remarks>
        /// Written out and shared, since nothing about a chip changes and building the list per
        /// instance would be building the same two strings for every chip in the library.
        /// </remarks>
        private static readonly string[] Runs = { "0-7", "8-15" };

        /// <inheritdoc/>
        /// <remarks>Always, so the chip shows the part working rather than greyed out.</remarks>
        public bool Live => true;

        /// <inheritdoc/>
        public int Lamps => 8;

        /// <inheritdoc/>
        /// <remarks>A third of the way along, so the playhead is plainly somewhere rather than parked.</remarks>
        public int Lit => 2;

        /// <inheritdoc/>
        public int FirstNumber => 0;

        /// <inheritdoc/>
        public System.Collections.Generic.IReadOnlyList<string> Pages => Runs;

        /// <inheritdoc/>
        public int Page => 0;

        /// <inheritdoc/>
        /// <remarks>Nothing to do: a chip is not hit testable, so no press ever reaches this.</remarks>
        public void Show(int page) { }

        /// <summary>Nowhere to subscribe, because a chip does not play.</summary>
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    /// <summary>
    /// A map for the chip to draw: three zones across the keyboard, playing nothing.
    /// </summary>
    /// <remarks>
    /// The same reason the pads on a chip are real buttons. Nothing here can be edited: the chip
    /// is not hit testable, so the drag that moves an edge never reaches it.
    /// </remarks>
    private sealed class SampleMap : IMachineZones
    {
        /// <summary>
        /// Three stretches of keyboard, laid end to end with no gap between them.
        /// </summary>
        /// <remarks>
        /// Three rather than one, because what is worth seeing on the chip is that there is more
        /// than one of them and that they lie side by side.
        /// </remarks>
        private static readonly (int Low, int High)[] Laid =
        {
            (0, 39), (40, 79), (80, 119),
        };

        /// <inheritdoc/>
        public int Count => Laid.Length;

        /// <inheritdoc/>
        /// <remarks>Unnamed: at chip size a caption would be the widest thing on the drawing.</remarks>
        public string Cap(int at) => "";

        /// <inheritdoc/>
        public int Low(int at) => Laid[at].Low;

        /// <inheritdoc/>
        public int High(int at) => Laid[at].High;

        /// <inheritdoc/>
        /// <remarks>The middle of the zone, which is where a root note sits when nobody has moved it.</remarks>
        public int Root(int at) => (Laid[at].Low + Laid[at].High) / 2;

        /// <inheritdoc/>
        /// <remarks>All of them, or the chip would show a map that is half empty.</remarks>
        public bool Filled(int at) => true;

        /// <summary>The middle one, so the chip shows both a picked zone and an unpicked one.</summary>
        public int Picked { get; set; } = 1;

        /// <inheritdoc/>
        /// <remarks>Nothing to do: the chip is not hit testable, so there is no drag to answer.</remarks>
        public void Move(int at, int low, int high, int root) { }

        /// <summary>
        /// Nowhere to subscribe, because none of this moves.
        /// </summary>
        /// <remarks>
        /// Taken and dropped rather than kept. A stand-in for a chip in the library never changes:
        /// the zones are three fixed stretches of keyboard and the chip is not hit testable, so
        /// there is no drag to move one. Holding the handlers would be holding a list that is
        /// never read.
        /// </remarks>
        event EventHandler? IMachineZones.Changed
        {
            add { }
            remove { }
        }
    }

    /// <summary>
    /// The wave, drawn as a wave: the real control against a stand-in that makes a sawtooth.
    /// </summary>
    private static Control BuildScope() => new ScopeView
    {
        Scope = new SampleWave(),
        Width = 74,
        Height = 34,
        Cycles = 2,
    };

    /// <summary>A wave for the chip to draw, which no machine is making.</summary>
    private sealed class SampleWave : IPanelScope
    {
        /// <inheritdoc/>
        /// <remarks>
        /// A sawtooth, because it is the one wave that cannot be mistaken for anything else at
        /// this size: a sine and a triangle are hard to tell apart across an inch. The time and
        /// the running flag are ignored, since a chip does not animate.
        /// </remarks>
        public void Trace(double[] into, double cycles, double seconds, bool running)
        {
            for (int at = 0; at < into.Length; at++)
            {
                double across = into.Length == 1 ? 0 : at / (into.Length - 1.0);

                into[at] = across * cycles % 1.0 * 2.0 - 1.0;
            }
        }

        /// <summary>Nowhere to subscribe: a chip is not hit testable and nothing here plays.</summary>
        event EventHandler? IPanelScope.Changed
        {
            add { }
            remove { }
        }
    }

    /// <summary>The ordinary drop down, with something picked, since an empty one says nothing.</summary>
    private static Control BuildChoice() => new ComboBox
    {
        Width = 78,
        FontSize = 11,
        ItemsSource = new[] { "Sine", "Square" },
        SelectedIndex = 0,
    };

    /// <summary>What a diagram is a diagram of.</summary>
    private enum SketchShape
    {
        /// <summary>Four boxes in two rows: things placed by row and column.</summary>
        Grid,

        /// <summary>Three boxes side by side.</summary>
        Row,

        /// <summary>Three boxes one under the other.</summary>
        Column,

        /// <summary>Cells of different heights standing on one line.</summary>
        Strip,

        /// <summary>An outline round nothing.</summary>
        Spacer,

        /// <summary>A recording's peaks about their middle line.</summary>
        Wave,

        /// <summary>A hill and a sun in a frame: the mark everybody draws for a picture.</summary>
        Picture,

        /// <summary>One box, for standing inside a frame that has a face of its own.</summary>
        Blank,
    }

    /// <summary>
    /// A small drawing of a shape, for the parts that have nothing of their own to show.
    /// </summary>
    /// <remarks>
    /// A <see cref="ThemedControl"/> and not a handful of borders, for the reason that class
    /// exists: the colour is read at paint time out of the theme, so a theme swap lands on the
    /// diagram at the same moment it lands on the knob next to it.
    ///
    /// The shape is fixed when the sketch is made rather than being a styled property. Nothing
    /// ever changes a diagram into a different diagram: the sample throws the whole example away
    /// and builds another one when its kind changes.
    /// </remarks>
    private sealed class PartSketch : ThemedControl
    {
        /// <summary>Between one box and the next.</summary>
        private const double Gap = 4;

        /// <summary>How much each box's corners are rounded, matching the controls beside it.</summary>
        private const double Corner = 3;

        /// <summary>How tall each cell of a strip stands, as a share of the room above the line.</summary>
        private static readonly double[] Standing = { 0.55, 1.0, 0.72, 0.9 };

        /// <summary>
        /// One take's peaks, as a share of the loudest.
        /// </summary>
        /// <remarks>
        /// Written out rather than worked out. A curve off a formula looks like a curve off a
        /// formula, and what this has to look like is a recording of something being hit.
        /// </remarks>
        private static readonly double[] Take =
        {
            0.14, 0.58, 0.96, 0.78, 0.9, 0.54, 0.68, 0.42,
            0.56, 0.31, 0.45, 0.25, 0.35, 0.19, 0.27, 0.12,
        };

        /// <summary>Fixes what this one is a drawing of, for the life of the control.</summary>
        public PartSketch(SketchShape shape) => Shape = shape;

        /// <summary>Which drawing this is, settled when it was made and never moved.</summary>
        public SketchShape Shape { get; }

        /// <summary>
        /// Paints whichever diagram this sketch was made for.
        /// </summary>
        /// <remarks>
        /// Outlined in the muted text colour and filled with the surface, not drawn in the
        /// border colour. A border is meant to be barely there, which is right for a border and
        /// wrong for the only thing on the chip: a container drawn in it read as an empty chip,
        /// and the library then had five entries nobody could tell apart.
        ///
        /// Everything is laid out on half pixels so the one pixel outlines land on a pixel
        /// rather than straddling two and coming out grey and two wide.
        /// </remarks>
        public override void Render(DrawingContext context)
        {
            double width = Bounds.Width;
            double height = Bounds.Height;

            if (width <= 1 || height <= 1) return;

            var palette = ThemePalette.From(this);

            var pen = new Pen(palette.MutedBrush, 1);
            var fill = palette.SurfaceBrush;

            switch (Shape)
            {
                case SketchShape.Grid: DrawGrid(context, pen, fill, width, height); break;
                case SketchShape.Row: DrawRow(context, pen, fill, width, height); break;
                case SketchShape.Column: DrawColumn(context, pen, fill, width, height); break;
                case SketchShape.Strip: DrawStrip(context, pen, fill, palette, width, height); break;
                case SketchShape.Spacer: DrawSpacer(context, palette, width, height); break;
                case SketchShape.Wave: DrawWave(context, palette, width, height); break;
                case SketchShape.Picture: DrawPicture(context, pen, fill, palette, width, height); break;
                case SketchShape.Blank: Box(context, pen, fill, new Rect(0.5, 0.5, width - 1, height - 1)); break;
            }
        }

        /// <summary>Four boxes in two rows, which is what placing things by row and column looks like.</summary>
        private static void DrawGrid(DrawingContext context, IPen pen, IBrush fill, double width, double height)
        {
            double cellWidth = (width - Gap) / 2;
            double cellHeight = (height - Gap) / 2;

            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    Box(context, pen, fill, new Rect(
                        column * (cellWidth + Gap) + 0.5,
                        row * (cellHeight + Gap) + 0.5,
                        cellWidth - 1,
                        cellHeight - 1));
                }
            }
        }

        /// <summary>Three boxes side by side, all the same height.</summary>
        private static void DrawRow(DrawingContext context, IPen pen, IBrush fill, double width, double height)
        {
            double cellWidth = (width - Gap * 2) / 3;

            for (int i = 0; i < 3; i++)
                Box(context, pen, fill, new Rect(i * (cellWidth + Gap) + 0.5, 0.5, cellWidth - 1, height - 1));
        }

        /// <summary>The same three, stacked instead.</summary>
        private static void DrawColumn(DrawingContext context, IPen pen, IBrush fill, double width, double height)
        {
            double cellHeight = (height - Gap * 2) / 3;

            for (int i = 0; i < 3; i++)
                Box(context, pen, fill, new Rect(0.5, i * (cellHeight + Gap) + 0.5, width - 1, cellHeight - 1));
        }

        /// <summary>
        /// Cells of different heights, all of them standing on the same line.
        /// </summary>
        /// <remarks>
        /// The line is the point of a strip and the boxes are only there to sit on it, which is
        /// why they are drawn at four different heights: what a strip is for is putting things
        /// that are not the same size on one scribe line.
        /// </remarks>
        private static void DrawStrip(DrawingContext context, IPen pen, IBrush fill, ThemePalette palette, double width, double height)
        {
            double cellWidth = (width - Gap * 3) / Standing.Length;
            double baseline = height - 2.5;
            double room = baseline - 1;

            for (int i = 0; i < Standing.Length; i++)
            {
                double tall = Math.Max(4, room * Standing[i]);

                Box(context, pen, fill, new Rect(
                    i * (cellWidth + Gap) + 0.5,
                    baseline - tall,
                    cellWidth - 1,
                    tall));
            }

            context.DrawLine(
                new Pen(palette.MutedBrush, 1),
                new Point(0, baseline + 0.5),
                new Point(width, baseline + 0.5));
        }

        /// <summary>Room held open and nothing in it, which is what a spacer is.</summary>
        private static void DrawSpacer(DrawingContext context, ThemePalette palette, double width, double height)
        {
            var pen = new Pen(palette.MutedBrush, 1) { DashStyle = DashStyle.Dash };

            Box(context, pen, null, new Rect(1.5, 1.5, width - 3, height - 3));
        }

        /// <summary>
        /// A block of peaks about their middle line, the way a take is drawn.
        /// </summary>
        /// <remarks>
        /// Drawn here rather than by borrowing the application's waveform view, which lives on
        /// the far side of a wall this assembly is not allowed through. A chip does not need the
        /// real one: it needs something anyone who has seen a recording will recognise.
        /// </remarks>
        private static void DrawWave(DrawingContext context, ThemePalette palette, double width, double height)
        {
            var brush = palette.AccentBrush;

            double pitch = width / Take.Length;
            double bar = Math.Max(1.5, pitch * 0.55);
            double middle = height / 2;
            double reach = height / 2 - 1;

            for (int i = 0; i < Take.Length; i++)
            {
                double half = Math.Max(1, reach * Take[i]);
                double left = i * pitch + (pitch - bar) / 2;

                context.DrawRectangle(
                    brush,
                    null,
                    new RoundedRect(new Rect(left, middle - half, bar, half * 2), bar / 2));
            }

            context.DrawLine(
                new Pen(palette.AccentTint(90), 1),
                new Point(0, middle + 0.5),
                new Point(width, middle + 0.5));
        }

        /// <summary>
        /// A frame with a hill and a sun in it.
        /// </summary>
        /// <remarks>
        /// A drawing and not a picture off the disc, unlike every other chip in the library,
        /// which holds the real control. There is no real one to hold: what the element draws is
        /// whatever picture a machine happens to carry, and the library is showing the kind of
        /// thing rather than one machine's badge. This is the mark that has meant picture since
        /// before anybody had a screen to put one on.
        /// </remarks>
        private static void DrawPicture(
            DrawingContext context, IPen pen, IBrush fill, ThemePalette palette, double width, double height)
        {
            var frame = new Rect(0.5, 0.5, width - 1, height - 1);

            Box(context, pen, fill, frame);

            var ink = palette.MutedBrush;

            context.DrawEllipse(
                palette.AccentBrush,
                null,
                new Point(frame.Right - width * 0.24, frame.Y + height * 0.28),
                height * 0.1,
                height * 0.1);

            double bottom = frame.Bottom - 2;

            context.DrawGeometry(ink, null, Hill(frame.X + 2, bottom, width * 0.62));
            context.DrawGeometry(ink, null, Hill(frame.X + width * 0.42, bottom, width * 0.5));
        }

        /// <summary>One hill, standing on that point and as wide as it is asked to be.</summary>
        private static Geometry Hill(double left, double bottom, double width)
        {
            var geometry = new StreamGeometry();

            using (var draw = geometry.Open())
            {
                draw.BeginFigure(new Point(left, bottom), isFilled: true);
                draw.LineTo(new Point(left + width / 2, bottom - width * 0.6));
                draw.LineTo(new Point(left + width, bottom));
                draw.EndFigure(isClosed: true);
            }

            return geometry;
        }

        /// <summary>
        /// One rounded box, or nothing at all when it has been squeezed to no size.
        /// </summary>
        /// <remarks>
        /// The empty case is not defensive tidiness: a chip is measured before it is given any
        /// room, so a diagram of three boxes across an inch really does get asked to draw at
        /// negative widths on the first pass.
        /// </remarks>
        private static void Box(DrawingContext context, IPen pen, IBrush? fill, Rect area)
        {
            if (area.Width <= 0 || area.Height <= 0) return;

            context.DrawRectangle(fill, pen, new RoundedRect(area, Corner));
        }
    }
}
