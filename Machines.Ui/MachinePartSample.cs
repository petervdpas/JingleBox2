using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace JingleBox2.Machines.Ui;

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
public sealed class MachinePartSample : Decorator
{
    // Declared rather than built from a variable, so every kind this can draw is greppable and
    // the set stays visible to anything that goes looking for it.
    private const string GridKind = "Grid";
    private const string GroupKind = "Group";
    private const string RowKind = "Row";
    private const string ColumnKind = "Column";
    private const string StripKind = "Strip";
    private const string KnobKind = "Knob";
    private const string FaderKind = "Fader";
    private const string SwitchKind = "Switch";
    private const string NumberKind = "Number";
    private const string ButtonKind = "Button";
    private const string LabelKind = "Label";
    private const string SpacerKind = "Spacer";
    private const string LedKind = "Led";
    private const string MeterKind = "Meter";
    private const string KeysKind = "Keys";
    private const string WaveKind = "Wave";
    private const string EnvelopeKind = "Envelope";
    private const string ImageKind = "Image";
    private const string ChoiceKind = "Choice";
    private const string TakeKind = "Take";
    private const string PresetKind = "Preset";

    /// <summary>Which part to show. Anything this version has never heard of shows nothing.</summary>
    /// <remarks>
    /// A plain string rather than an enumeration, because the panel description names its
    /// elements in strings and a library built from that description would otherwise have to
    /// translate every name twice, once on the way in and once on the way back out.
    /// </remarks>
    public static readonly StyledProperty<string?> KindProperty =
        AvaloniaProperty.Register<MachinePartSample, string?>(nameof(Kind));

    public string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public MachinePartSample()
    {
        // One flag at the top rather than one per control: hit testing stops at the first thing
        // that says it is not there, so this takes the whole example with it and the press lands
        // on the chip underneath, which is what starts the drag.
        IsHitTestVisible = false;

        // The same again for the keyboard, which does not care about hit testing. Without it a
        // library of twenty chips is twenty extra stops on the way to the panel.
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.None);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == KindProperty) Rebuild();
    }

    /// <summary>Throws the old example away and builds the one the kind now names.</summary>
    private void Rebuild()
    {
        var built = Build(Kind ?? "");

        if (built is null)
        {
            Child = null;
            return;
        }

        // A control that thinks it can be focused still can be, reached from a child of it.
        built.Focusable = false;

        // Shrunk to whatever room the chip has, never stretched past its own size. A part drawn
        // at half scale still reads as itself; one with its corner cut off does not.
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
        WaveKind => new PartSketch(SketchShape.Wave) { Width = 74, Height = 34 },
        EnvelopeKind => BuildEnvelope(),
        ImageKind => new PartSketch(SketchShape.Picture) { Width = 60, Height = 40 },
        ChoiceKind => BuildChoice(),
        TakeKind => BuildTake(),
        PresetKind => BuildPreset(),
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

    private static Control BuildKnob() => new Knob
    {
        Label = "TONE",
        DialSize = 26,
        Minimum = 0,
        Maximum = 1,
        Value = 0.66,
        // The way a machine prints it, which is what the panel does with a knob of its own.
        LabelAbove = true,
    };

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

    private static Control BuildNumber() => new NumberField
    {
        Width = 64,
        Minimum = 0,
        Maximum = 16,
        Format = "0",
        Value = 8,
    };

    /// <summary>A latching button, held down, with its lamp lit to say so.</summary>
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
    /// The button that fetches a recording off the shelf.
    /// </summary>
    /// <remarks>
    /// An ordinary button rather than the app's own take picker, which lives in the
    /// application and cannot be reached from here. What the library has to show is the shape
    /// of the thing on the panel, and on the panel it is a button with the take's name on it.
    /// </remarks>
    private static Control BuildTake() => new Button
    {
        Content = "kick.wav",
        FontSize = 10,
        Padding = new Thickness(8, 3),
        IsHitTestVisible = false,
    };

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

        public PartSketch(SketchShape shape) => Shape = shape;

        public SketchShape Shape { get; }

        public override void Render(DrawingContext context)
        {
            double width = Bounds.Width;
            double height = Bounds.Height;

            if (width <= 1 || height <= 1) return;

            var palette = ThemePalette.From(this);

            // Outlined in the muted text colour and filled with the surface, not drawn in the
            // border colour. A border is meant to be barely there, which is right for a border
            // and wrong for the only thing on the chip: a container drawn in it reads as an
            // empty chip, and the library then has five entries nobody can tell apart.
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

        private static void DrawRow(DrawingContext context, IPen pen, IBrush fill, double width, double height)
        {
            double cellWidth = (width - Gap * 2) / 3;

            for (int i = 0; i < 3; i++)
                Box(context, pen, fill, new Rect(i * (cellWidth + Gap) + 0.5, 0.5, cellWidth - 1, height - 1));
        }

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

        private static void Box(DrawingContext context, IPen pen, IBrush? fill, Rect area)
        {
            if (area.Width <= 0 || area.Height <= 0) return;

            context.DrawRectangle(fill, pen, new RoundedRect(area, Corner));
        }
    }
}
