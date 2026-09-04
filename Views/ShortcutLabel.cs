using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// A page's name along the top, with the letter its shortcut uses underlined.
/// </summary>
/// <remarks>
/// Which is how every application that has ever had a menu bar tells you the key without
/// spending a line on it, and the only place that information can be where somebody is looking
/// when they want it. A page on no key is drawn plain, which is every page on a fresh
/// installation.
///
/// It reads the map itself rather than being handed a string, and follows it: the strip is drawn
/// once when the window opens and a key set afterwards would leave it marking a letter nobody
/// uses any more. What it listens to is the map's own <c>Changed</c>, because the map is what
/// knows.
///
/// Which letter is <see cref="IShortcutLetter"/> and has no control in it. This half is only the
/// drawing.
/// </remarks>
public sealed class ShortcutLabel : TextBlock
{
    /// <summary>Which letter of the word is the shortcut's.</summary>
    private readonly IShortcutLetter _letter = new ShortcutLetter();

    /// <summary>What the map was when this subscribed, so it can let go of the same one.</summary>
    private IShortcutMap? _heard;

    /// <inheritdoc cref="Word"/>
    public static readonly StyledProperty<string?> WordProperty =
        AvaloniaProperty.Register<ShortcutLabel, string?>(nameof(Word));

    /// <inheritdoc cref="For"/>
    public static readonly StyledProperty<ShortcutAction> ForProperty =
        AvaloniaProperty.Register<ShortcutLabel, ShortcutAction>(nameof(For));

    /// <summary>The page's name, as the tab strip spells it.</summary>
    /// <remarks>
    /// Its own property rather than <c>Text</c>, since what is drawn is inline runs and setting
    /// the text of a TextBlock that has inlines is the two of them arguing.
    /// </remarks>
    public string? Word
    {
        get => GetValue(WordProperty);
        set => SetValue(WordProperty, value);
    }

    /// <summary>Which page it is, which is what its shortcut is looked up by.</summary>
    public ShortcutAction For
    {
        get => GetValue(ForProperty);
        set => SetValue(ForProperty, value);
    }

    /// <summary>Redraws when either of the two things it is built from changes.</summary>
    /// <param name="change">What moved.</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WordProperty || change.Property == ForProperty) Draw();
    }

    /// <summary>Starts following the map, and draws what it says now.</summary>
    /// <param name="e">Ignored: what it listens to is the application's map rather than a parent.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _heard = ShortcutKeys.Map;
        _heard.Changed += OnKeysMoved;

        Draw();
    }

    /// <summary>Stops following it, so a strip that is gone is not still being told.</summary>
    /// <param name="e">Ignored, as above.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_heard is { } map) map.Changed -= OnKeysMoved;

        _heard = null;
    }

    /// <summary>A key moved somewhere, so this word may have gained or lost its mark.</summary>
    /// <param name="sender">The map. Not read: there is one.</param>
    /// <param name="e">Nothing.</param>
    private void OnKeysMoved(object? sender, System.EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(Draw);

    /// <summary>The word, with one letter underlined or none.</summary>
    private void Draw()
    {
        string word = Word ?? "";

        Inlines?.Clear();

        if (Inlines is null) return;

        int at = _letter.In(word, ShortcutKeys.Map.Said(For));

        if (at < 0)
        {
            Inlines.Add(new Run(word));

            return;
        }

        if (at > 0) Inlines.Add(new Run(word[..at]));

        Inlines.Add(new Run(word[at].ToString())
        {
            TextDecorations = Avalonia.Media.TextDecorations.Underline
        });

        if (at + 1 < word.Length) Inlines.Add(new Run(word[(at + 1)..]));
    }
}
