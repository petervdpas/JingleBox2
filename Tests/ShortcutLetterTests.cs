using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using Avalonia.Input;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which letter of a page's name the tab strip underlines.
/// </summary>
/// <remarks>
/// The mark is how every application with a menu bar has ever told you the key, and it is the
/// only place that can say it where somebody is looking when they want it. What it must never do
/// is mark the wrong letter, which would be worse than marking none: a word with a letter
/// underlined is a promise about a keystroke.
/// </remarks>
public class ShortcutLetterTests
{
    /// <summary>The rule under test.</summary>
    private readonly IShortcutLetter _letter = new ShortcutLetter();

    /// <summary>The letter is found in the word, whatever case either is in.</summary>
    [Theory]
    [InlineData("MIXER", "Ctrl+Alt+M", 0)]
    [InlineData("RECORD", "Ctrl+Alt+R", 0)]
    [InlineData("TRACKER", "Ctrl+Alt+T", 0)]
    [InlineData("MIDI CC", "Ctrl+Alt+C", 5)]
    [InlineData("SETTINGS", "Ctrl+Alt+E", 1)]
    public void The_letter_is_marked_where_it_is(string word, string keys, int at)
    {
        Assert.Equal(at, _letter.In(word, keys));
    }

    /// <summary>The first one, where the word has the letter twice.</summary>
    /// <remarks>
    /// Which is the one an eye lands on, and it is also what every menu bar has always done.
    /// </remarks>
    [Fact]
    public void The_first_of_two()
    {
        Assert.Equal(2, _letter.In("RECORD", "Ctrl+Alt+C"));
    }

    /// <summary>
    /// A letter that is not in the word marks nothing rather than guessing.
    /// </summary>
    /// <remarks>
    /// Ctrl+Alt+Q on MIXER is a perfectly good shortcut and there is nothing in MIXER to
    /// underline. The tab is drawn plain and the page in SETTINGS is where it is read.
    /// </remarks>
    [Fact]
    public void A_letter_that_is_not_there_marks_nothing()
    {
        Assert.Equal(-1, _letter.In("MIXER", "Ctrl+Alt+Q"));
    }

    /// <summary>A page on no key is drawn plain, which is every page on a fresh installation.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void A_page_on_no_key_marks_nothing(string? keys)
    {
        Assert.Equal(-1, _letter.In("PADS", keys));
    }

    /// <summary>
    /// Nothing but Ctrl+Alt and one letter is taken apart at all.
    /// </summary>
    /// <remarks>
    /// A page shortcut can be nothing else, so a keystroke of another shape here means somebody
    /// asked about a key that is not one of these, and answering with an index would put a mark
    /// under a letter for a key that does not go to that page.
    /// </remarks>
    [Theory]
    [InlineData("Ctrl+S")]
    [InlineData("Ctrl+Shift+M")]
    [InlineData("Space")]
    [InlineData("Ctrl+Alt+F5")]
    [InlineData("Ctrl+Alt+")]
    [InlineData("ctrl+alt+M")]
    public void Nothing_else_is_read_as_a_page_key(string keys)
    {
        Assert.Equal(-1, _letter.In("MIXER", keys));
    }

    /// <summary>An empty word marks nothing rather than throwing.</summary>
    [Fact]
    public void An_empty_word_marks_nothing()
    {
        Assert.Equal(-1, _letter.In("", "Ctrl+Alt+M"));
    }

    /// <summary>
    /// The map says when something moved, which is what lets the strip follow it.
    /// </summary>
    /// <remarks>
    /// The strip is drawn once when the window opens, so without this it would go on marking the
    /// letter of a key somebody had just moved.
    /// </remarks>
    [Fact]
    public void The_map_says_when_a_key_moves()
    {
        IShortcutMap map = new ShortcutMap();

        int said = 0;
        map.Changed += (_, _) => said++;

        map.Set(ShortcutAction.Tracker, new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Alt));

        Assert.Equal(1, said);
    }

    /// <summary>And says nothing when the same key is written back over itself.</summary>
    /// <remarks>
    /// Or every tab would be redrawn for a change that is not one.
    /// </remarks>
    [Fact]
    public void And_nothing_when_nothing_moved()
    {
        IShortcutMap map = new ShortcutMap();

        map.Set(ShortcutAction.Pads, new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Alt));

        int said = 0;
        map.Changed += (_, _) => said++;

        map.Set(ShortcutAction.Pads, new KeyGesture(Key.P, KeyModifiers.Control | KeyModifiers.Alt));
        map.Set(ShortcutAction.Save, new KeyGesture(Key.F4));

        Assert.Equal(0, said);
    }
}
