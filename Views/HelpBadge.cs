using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Help;
using System.Globalization;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Help.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// A small question mark beside a title. Hovering it says what the panel is for in a line;
/// clicking it opens the help window on that subject.
/// </summary>
/// <remarks>
/// The text is not here. A badge carries the id of a topic, and the topics all live in one
/// file, so a panel says which explanation belongs to it rather than carrying a paragraph of
/// prose in its layout.
///
/// Drawn rather than assembled from a border and a label, for the same reason the knobs are:
/// it always looks the same, it follows the theme, and there is no chance of a transparent
/// background making it something the pointer cannot find.
/// </remarks>
public class HelpBadge : ThemedControl
{
    /// <summary>Everything the app explains about itself, looked up by id.</summary>
    private readonly IHelpText _help = new HelpText();

    /// <summary>
    /// Which explanation this badge is about, as one of the ids declared in HelpText. An id
    /// nothing has been written for says so rather than opening an empty window.
    /// </summary>
    public static readonly StyledProperty<string> TopicProperty =
        AvaloniaProperty.Register<HelpBadge, string>(nameof(Topic), "");

    /// <summary>
    /// A line of its own, for a badge with something to say that is not worth a topic. The
    /// topic's own summary is used when this is empty.
    /// </summary>
    public static readonly StyledProperty<string> TipProperty =
        AvaloniaProperty.Register<HelpBadge, string>(nameof(Tip), "");

    /// <summary>How big the circle is, which is also the whole size the badge asks for.</summary>
    public static readonly StyledProperty<double> DiameterProperty =
        AvaloniaProperty.Register<HelpBadge, double>(nameof(Diameter), 16);

    /// <summary>The size changes what is asked for as well as what is drawn.</summary>
    static HelpBadge()
    {
        AffectsRender<HelpBadge>(TipProperty, TopicProperty, DiameterProperty);
        AffectsMeasure<HelpBadge>(DiameterProperty);
    }

    /// <summary>Sits on the middle of the line it is on, and says it can be clicked.</summary>
    public HelpBadge()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <inheritdoc cref="TopicProperty"/>
    public string Topic
    {
        get => GetValue(TopicProperty);
        set => SetValue(TopicProperty, value);
    }

    /// <inheritdoc cref="TipProperty"/>
    public string Tip
    {
        get => GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    /// <inheritdoc cref="DiameterProperty"/>
    public double Diameter
    {
        get => GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    /// <summary>Square and exactly <see cref="Diameter"/>, whatever room it is offered.</summary>
    protected override Size MeasureOverride(Size availableSize) => new(Diameter, Diameter);

    /// <summary>Rewrites the hover line when either half of what it says has changed.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TipProperty || change.Property == TopicProperty) UpdateTip();
    }

    /// <summary>
    /// What hovering says: the badge's own line if it has one, otherwise the topic's summary,
    /// with an invitation to click for the rest of it.
    /// </summary>
    private void UpdateTip()
    {
        var topic = _help.Find(Topic);

        string line = !string.IsNullOrWhiteSpace(Tip)
            ? Tip
            : topic?.Summary ?? "";

        if (topic != null) line = string.IsNullOrWhiteSpace(line) ? "Click for help" : line + "\n\nClick for more.";
        else if (string.IsNullOrWhiteSpace(line)) line = "No help has been written for this yet.";

        ToolTip.SetTip(this, line);
    }

    /// <summary>Opens the help window on this badge's topic, over the window the badge is in.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        HelpWindow.Show(Topic, owner);
        e.Handled = true;
    }

    /// <summary>
    /// A question mark in a circle, brighter while the pointer is on it.
    /// </summary>
    /// <remarks>
    /// Filled as well as outlined: a ring on its own disappears against a busy panel, and this
    /// has to read as a thing to point at rather than as punctuation after the title.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        double size = System.Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 2) return;

        var palette = ThemePalette.From(this);
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double radius = size / 2 - 0.5;

        context.DrawEllipse(
            new SolidColorBrush(palette.Accent, IsPointerOver ? 0.35 : 0.18),
            new Pen(new SolidColorBrush(palette.Accent, IsPointerOver ? 1 : 0.75), 1),
            centre,
            radius,
            radius);

        var mark = new FormattedText(
            "?",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            size * 0.68,
            new SolidColorBrush(palette.Text, 0.9));

        context.DrawText(mark, new Point(centre.X - mark.Width / 2, centre.Y - mark.Height / 2));
    }

    /// <summary>Redraws brighter, since the lit state is worked out in <see cref="Render"/>.</summary>
    protected override void OnPointerEntered(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        InvalidateVisual();
    }

    /// <summary>And back down again when the pointer leaves.</summary>
    protected override void OnPointerExited(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerExited(e);
        InvalidateVisual();
    }
}
