using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Windows.Input;
using System.Globalization;
using JingleBox2.Rack.Controls;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Views;

/// <summary>
/// The line along the bottom of a window: a lamp saying what kind of thing it is, and the thing.
/// </summary>
/// <remarks>
/// Drawn rather than a border round a text block, because the lamp is the point. Where you are
/// and what has just gone wrong are the same line of text in the same place, and without
/// something that changes colour they read the same as each other.
///
/// It knows nothing about pages. Whatever fills it decides what it says.
/// </remarks>
public class StatusBar : ThemedControl
{
    /// <summary>What the line says. Empty draws the lamp alone.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<StatusBar, string>(nameof(Text), "");

    /// <summary>What kind of thing it is, which is the lamp's colour and the ink's weight.</summary>
    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<StatusBar, StatusKind>(nameof(Kind), StatusKind.Context);

    /// <summary>Its own rather than inherited, since the bar is drawn and not a text block.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(FontSize), 12);

    /// <summary>How tall the bar is, which is the same whatever it says.</summary>
    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(BarHeight), 26);

    /// <inheritdoc cref="InputLevel"/>
    public static readonly StyledProperty<double> InputLevelProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(InputLevel));

    /// <inheritdoc cref="OutputLevel"/>
    public static readonly StyledProperty<double> OutputLevelProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(OutputLevel));

    /// <summary>Draws the two meters at the right end. Off for a bar in a dialog.</summary>
    public static readonly StyledProperty<bool> ShowLevelsProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(ShowLevels));

    /// <summary>The lamp at the near end, big enough to read a colour off and no bigger.</summary>
    private const double LampSize = 8;

    /// <summary>The meters: two thin columns, tall enough to read and no taller.</summary>
    private const double MeterWidth = 4;

    /// <summary>Between the two columns, so they read as two meters rather than one wide one.</summary>
    private const double MeterGap = 3;

    /// <summary>Where the scale stops being green. Above this it is amber, then red.</summary>
    private const double Warm = 0.72;

    /// <summary>And where it stops being amber. Above this the level is being clipped.</summary>
    private const double Hot = 0.92;

    /// <summary>How far in from either end anything is drawn.</summary>
    private const double Inset = 10;

    /// <summary>Between the lamp and the words.</summary>
    private const double Gap = 8;

    /// <summary>The meters' colours: amber where a level is warm and red where it is clipped.</summary>
    private static readonly Color Amber = Color.FromRgb(0xF5, 0xA6, 0x23);

    /// <inheritdoc cref="Amber"/>
    private static readonly Color Red = Color.FromRgb(0xE5, 0x39, 0x35);

    /// <summary>And green, a meter with room left in it.</summary>
    private static readonly Color Green = Color.FromRgb(0x4C, 0xAF, 0x50);

    /// <summary>Everything drawn is a render; only the height is a measurement.</summary>
    static StatusBar()
    {
        AffectsRender<StatusBar>(TextProperty, KindProperty, FontSizeProperty, BarHeightProperty,
                                 InputLevelProperty, OutputLevelProperty, ShowLevelsProperty,
                                 CpuLoadProperty, MemoryLoadProperty, ShowLoadProperty);
        AffectsMeasure<StatusBar>(BarHeightProperty);
    }

    /// <inheritdoc cref="TextProperty"/>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="KindProperty"/>
    public StatusKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <inheritdoc cref="BarHeightProperty"/>
    public double BarHeight
    {
        get => GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    /// <summary>What is coming in, 0 to 1. The recorder's input, whether or not it is recording.</summary>
    public double InputLevel
    {
        get => GetValue(InputLevelProperty);
        set => SetValue(InputLevelProperty, value);
    }

    /// <summary>What is going out, 0 to 1. Everything the app is playing, pads and tracker both.</summary>
    public double OutputLevel
    {
        get => GetValue(OutputLevelProperty);
        set => SetValue(OutputLevelProperty, value);
    }

    /// <inheritdoc cref="ShowLevelsProperty"/>
    public bool ShowLevels
    {
        get => GetValue(ShowLevelsProperty);
        set => SetValue(ShowLevelsProperty, value);
    }

    /// <summary>How busy the computer's processors are, nought to one, drawn beside the levels.</summary>
    public static readonly StyledProperty<double> CpuLoadProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(CpuLoad));

    /// <summary>How full the computer's memory is, nought to one, drawn beside the processors.</summary>
    public static readonly StyledProperty<double> MemoryLoadProperty =
        AvaloniaProperty.Register<StatusBar, double>(nameof(MemoryLoad));

    /// <summary>Whether the processor and memory meters are drawn.</summary>
    public static readonly StyledProperty<bool> ShowLoadProperty =
        AvaloniaProperty.Register<StatusBar, bool>(nameof(ShowLoad));

    /// <inheritdoc cref="ShowLoadProperty"/>
    public bool ShowLoad
    {
        get => GetValue(ShowLoadProperty);
        set => SetValue(ShowLoadProperty, value);
    }

    /// <summary>What clicking the level meters does, or nothing.</summary>
    public static readonly StyledProperty<ICommand?> LevelsCommandProperty =
        AvaloniaProperty.Register<StatusBar, ICommand?>(nameof(LevelsCommand));

    /// <summary>What clicking the load meters does, or nothing.</summary>
    public static readonly StyledProperty<ICommand?> LoadCommandProperty =
        AvaloniaProperty.Register<StatusBar, ICommand?>(nameof(LoadCommand));

    /// <inheritdoc cref="CpuLoadProperty"/>
    public double CpuLoad
    {
        get => GetValue(CpuLoadProperty);
        set => SetValue(CpuLoadProperty, value);
    }

    /// <inheritdoc cref="MemoryLoadProperty"/>
    public double MemoryLoad
    {
        get => GetValue(MemoryLoadProperty);
        set => SetValue(MemoryLoadProperty, value);
    }

    /// <inheritdoc cref="LevelsCommandProperty"/>
    public ICommand? LevelsCommand
    {
        get => GetValue(LevelsCommandProperty);
        set => SetValue(LevelsCommandProperty, value);
    }

    /// <inheritdoc cref="LoadCommandProperty"/>
    public ICommand? LoadCommand
    {
        get => GetValue(LoadCommandProperty);
        set => SetValue(LoadCommandProperty, value);
    }

    /// <summary>Where the level meters and the load meters were last drawn, for a click to find them.</summary>
    private Rect _levelsArea, _loadArea;

    /// <summary>Which of the two a point is on: the command to run, or nothing.</summary>
    /// <param name="at">The point, in this control's own space.</param>
    private ICommand? CommandAt(Point at) =>
        _levelsArea.Contains(at) ? LevelsCommand
        : _loadArea.Contains(at) ? LoadCommand
        : null;

    /// <summary>A hand over either set of meters, since both can be clicked.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Cursor = CommandAt(e.GetPosition(this)) != null ? HandCursor : Cursor.Default;
    }

    /// <summary>Runs whichever of the two commands the click landed on.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var command = CommandAt(e.GetPosition(this));

        if (command == null || !command.CanExecute(null)) return;

        command.Execute(null);
        e.Handled = true;
    }

    /// <summary>The hand, made once.</summary>
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    /// <summary>
    /// As wide as it is offered and exactly <see cref="BarHeight"/> tall, so the line along the
    /// bottom of a window does not move as its wording changes.
    /// </summary>
    /// <remarks>
    /// Offered an unbounded width, which is what a stack panel does, it asks for a plain 200
    /// rather than infinity, since a bar that asks for everything there is cannot be arranged.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, BarHeight);

    /// <summary>
    /// The bar, the lamp, the words trimmed to what is left, and the meters at the far end.
    /// </summary>
    /// <remarks>
    /// The context kind is the resting state and is drawn in the muted ink, so something that has
    /// just happened reads as louder than where you happen to be standing.
    ///
    /// The room the meters take is subtracted from the words rather than the words being drawn
    /// under them: a line trimmed with an ellipsis says it was too long, and a line running
    /// behind a meter looks like a fault in the drawing.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 1 || height <= 1) return;

        var palette = ThemePalette.From(this);
        var area = new Rect(0, 0, width, height);

        context.DrawRectangle(
            new SolidColorBrush(palette.Surface, 0.55),
            new Pen(new SolidColorBrush(palette.Border, 0.8), 1),
            new RoundedRect(new Rect(0.5, 0.5, width - 1, height - 1), 4));

        var lamp = Lamp(palette);

        Led.DrawLamp(context, new Point(Inset + LampSize / 2, height / 2), LampSize / 2, lamp,
                     Kind != StatusKind.Context);

        DrawMeters(context, palette, area);

        if (Text.Length == 0) return;

        var ink = Kind == StatusKind.Context
            ? new SolidColorBrush(palette.Muted)
            : new SolidColorBrush(palette.Text);

        double meters = (ShowLevels ? MeterWidth * 2 + MeterGap + Inset + LabelRoom : 0)
                        + (ShowLoad ? LoadRoom + (ShowLevels ? 0 : Inset) : 0);

        var text = new FormattedText(
            Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            FontSize,
            ink)
        {
            MaxTextWidth = Math.Max(0, width - Inset * 2 - LampSize - Gap - meters),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

        context.DrawText(text, new Point(Inset + LampSize + Gap, (height - text.Height) / 2));
    }

    /// <summary>How much room the two little letters beside the meters take.</summary>
    private const double LabelRoom = 16;

    /// <summary>How much room the processor and memory meters take, their words and gaps included.</summary>
    private const double LoadRoom = 84;

    /// <summary>Between a load meter's word and the meter itself.</summary>
    private const double WordGap = 5;

    /// <summary>Between the processor meter and the memory meter's word.</summary>
    private const double PairGap = 12;

    /// <summary>Between the load meters and the levels.</summary>
    private const double GroupGap = 16;

    /// <summary>
    /// The main input and the main output as two thin columns at the far end, and the computer's
    /// processors and memory as two more to the left of them, each pair where it is switched on.
    /// </summary>
    /// <remarks>
    /// Peak rather than average, and coloured by where the peak is rather than by a line drawn
    /// across it: the whole use of a meter this size is to be read without being looked at, and
    /// a colour is the only thing that can be.
    /// </remarks>
    private void DrawMeters(DrawingContext context, ThemePalette palette, Rect area)
    {
        _levelsArea = default;
        _loadArea = default;

        double top = 4;
        double bottom = area.Height - 4;
        double tall = bottom - top;

        if (tall <= 2) return;

        double right = area.Width - Inset;

        if (ShowLevels)
        {
            double outputX = right - MeterWidth;
            double inputX = outputX - MeterGap - MeterWidth;

            Draw(inputX, InputLevel);
            Draw(outputX, OutputLevel);

            Word("io", inputX - LabelRoom + 2);

            _levelsArea = new Rect(inputX - LabelRoom, 0, right - inputX + LabelRoom, area.Height);

            right = inputX - LabelRoom - GroupGap;
        }

        if (!ShowLoad) return;

        /* The computer's load, the same thin columns to the left of the levels, each with its own
           word since they are two different things rather than a pair. */
        double memoryX = right - MeterWidth;
        Draw(memoryX, MemoryLoad);

        double memoryWord = memoryX - WordGap - Words("mem").Width;
        Word("mem", memoryWord);

        double cpuX = memoryWord - PairGap - MeterWidth;
        Draw(cpuX, CpuLoad);

        double cpuWord = cpuX - WordGap - Words("cpu").Width;
        Word("cpu", cpuWord);

        _loadArea = new Rect(cpuWord - 2, 0, memoryX + MeterWidth - cpuWord + 4, area.Height);

        FormattedText Words(string word) => new(
            word,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            9,
            new SolidColorBrush(palette.Muted, 0.8));

        void Word(string word, double x)
        {
            var letters = Words(word);

            context.DrawText(letters, new Point(x, (area.Height - letters.Height) / 2));
        }

        void Draw(double x, double level)
        {
            level = double.IsNaN(level) ? 0 : Math.Clamp(level, 0, 1);

            context.FillRectangle(
                new SolidColorBrush(palette.Border, 0.7),
                new Rect(x, top, MeterWidth, tall));

            if (level <= 0) return;

            double lit = tall * level;

            var colour = level >= Hot ? Red : level >= Warm ? Amber : Green;

            context.FillRectangle(
                new SolidColorBrush(colour, 0.95),
                new Rect(x, bottom - lit, MeterWidth, lit));
        }
    }

    /// <summary>Which colour a kind of message is lit in, the same rule a toast asks.</summary>
    private static readonly IStatusLamp Lamps = new StatusLamp();

    /// <summary>The lamp's colour for the kind, and the theme's muted for the resting state.</summary>
    private Color Lamp(ThemePalette palette) => Lamps.For(Kind, palette.Accent, palette.Muted);
}
