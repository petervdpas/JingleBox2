using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using JingleBox2.Help;
using JingleBox2.Help.Enums;
using JingleBox2.Help.Interfaces;
using JingleBox2.Help.Records;
using JingleBox2.Rack.Controls;

namespace JingleBox2.Views;

/// <summary>
/// A help topic drawn from the markdown it is written in.
/// </summary>
/// <remarks>
/// Built out of the toolkit's own text controls rather than drawn, unlike the knobs and the
/// pattern, because everything a paragraph needs is already there: wrapping, selection, the
/// theme's own colours, and inline runs for the words that are bold or are a key name. What is
/// ours is which blocks there are and what each looks like.
///
/// What it is made of is <see cref="IMarkdown"/>, which has no control in it, so what a piece of
/// text means can be put a question to without a window. This half is only the look.
///
/// One TextBlock per block rather than one for the lot, which is the whole reason this exists: a
/// TextBlock is one size and one weight, so a heading inside one had to be shouty capitals.
/// </remarks>
public sealed class MarkdownView : ContentControl
{
    /// <summary>What the words mean, as opposed to what they look like.</summary>
    private readonly IMarkdown _markdown = new Markdown();

    /// <inheritdoc cref="Markdown"/>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    /// <summary>How far a heading stands off whatever is above it.</summary>
    /// <remarks>
    /// Room above and almost none below, which is what makes a heading read as belonging to what
    /// follows rather than floating between two sections. The first block gets none, since a gap
    /// at the very top is a gap against the window's own edge.
    /// </remarks>
    private const double SectionGap = 18;

    /// <summary>How far apart two paragraphs stand.</summary>
    private const double ParagraphGap = 10;

    /// <summary>How far a list line is indented, and how wide the mark before it is.</summary>
    private const double BulletColumn = 16;

    /// <summary>The words this is drawn from, as they were written.</summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Redraws whenever the words change, which is every time a topic is picked.</summary>
    /// <param name="change">What moved.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty) Content = Build();
    }

    /// <summary>The whole topic, block by block.</summary>
    private Control Build()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical };

        var blocks = _markdown.Read(Markdown);

        for (int at = 0; at < blocks.Count; at++)
            stack.Children.Add(Draw(blocks[at], first: at == 0));

        return stack;
    }

    /// <summary>One block, in the shape its kind asks for.</summary>
    /// <param name="block">What to draw.</param>
    /// <param name="first">Whether it is at the very top, where a gap above would be a hole.</param>
    private Control Draw(MarkdownBlock block, bool first) => block.Kind switch
    {
        MarkdownKind.Heading => Heading(block, first),
        MarkdownKind.Bullet => Bullet(block),
        _ => Words(block, new Thickness(0, first ? 0 : ParagraphGap, 0, 0))
    };

    /// <summary>
    /// A section title: bigger, heavier, and standing off what is above it.
    /// </summary>
    /// <remarks>
    /// Two sizes and no more, however many hashes are written. The help is a page rather than a
    /// document, and a third level of heading on a page this size is a distinction nobody
    /// reading it can use.
    /// </remarks>
    /// <param name="block">The heading.</param>
    /// <param name="first">Whether it is at the very top.</param>
    private Control Heading(MarkdownBlock block, bool first)
    {
        var text = Words(block, new Thickness(0, first ? 0 : SectionGap, 0, 4));

        text.FontWeight = FontWeight.SemiBold;
        text.FontSize = block.Level <= 1 ? 16 : 14;

        return text;
    }

    /// <summary>
    /// One line of a list, with its mark in a column of its own.
    /// </summary>
    /// <remarks>
    /// A grid rather than a bullet character pushed into the words, so a line that wraps lines
    /// up under itself rather than under the mark.
    /// </remarks>
    /// <param name="block">The list line.</param>
    private Control Bullet(MarkdownBlock block)
    {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 0) };

        row.ColumnDefinitions.Add(new ColumnDefinition(BulletColumn, GridUnitType.Pixel));
        row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        var mark = new TextBlock { Text = "•", Opacity = 0.7 };

        var words = Words(block, default);

        Grid.SetColumn(words, 1);

        row.Children.Add(mark);
        row.Children.Add(words);

        return row;
    }

    /// <summary>The words of a block, with each stretch drawn as it asks to be.</summary>
    /// <param name="block">The block being drawn.</param>
    /// <param name="margin">What room to leave around it.</param>
    private TextBlock Words(MarkdownBlock block, Thickness margin)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19,
            Margin = margin
        };

        foreach (var span in block.Spans) text.Inlines?.Add(Run(span));

        return text;
    }

    /// <summary>
    /// One stretch of words, in the face and weight its marks asked for.
    /// </summary>
    /// <remarks>
    /// A key name is drawn in the same monospaced face the pattern uses, at a hair under the
    /// surrounding size, since a monospaced face at the same nominal size reads larger than the
    /// prose around it. That is the whole of what the code marks are for here: everything in
    /// them is something somebody presses.
    /// </remarks>
    /// <param name="span">The stretch to draw.</param>
    private static Run Run(MarkdownSpan span)
    {
        var run = new Run(span.Text);

        if (span.Strong) run.FontWeight = FontWeight.SemiBold;

        if (span.Code)
        {
            run.FontFamily = PatternFont.Family;
            run.FontSize = 12.5;
        }

        return run;
    }
}
