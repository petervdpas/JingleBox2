using System.Linq;
using Avalonia.Input;
using JingleBox2.Help;
using JingleBox2.Help.Enums;
using JingleBox2.Help.Interfaces;
using JingleBox2.Shortcuts;
using JingleBox2.Shortcuts.Enums;
using JingleBox2.Shortcuts.Interfaces;
using JingleBox2.Views;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The key that opens the help, and the page it opens on.
/// </summary>
/// <remarks>
/// The page is worth a test rather than a read, because it is the one piece of prose in this
/// application that can go quietly wrong while nobody is looking: four of the keys on it are a
/// setting, so a page that spelled them out would be right on the day it was written and wrong
/// for anybody who had ever changed one. What is pinned here is that it says what the map says.
/// </remarks>
public class HelpKeyTests
{
    /// <summary>Everything the app explains about itself.</summary>
    private readonly IHelpText _help = new HelpText();

    /// <summary>Ctrl+H and nothing else.</summary>
    [Fact]
    public void Control_h_asks_for_the_help()
    {
        Assert.True(HelpKey.Wants(Key.H, KeyModifiers.Control));
    }

    /// <summary>The modifiers agree exactly, which is the rule the shortcut map keeps too.</summary>
    [Theory]
    [InlineData(Key.H, KeyModifiers.None)]
    [InlineData(Key.H, KeyModifiers.Shift)]
    [InlineData(Key.H, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.H, KeyModifiers.Alt)]
    [InlineData(Key.G, KeyModifiers.Control)]
    public void And_nothing_else_does(Key key, KeyModifiers modifiers)
    {
        Assert.False(HelpKey.Wants(key, modifiers));
    }

    /// <summary>The topic the key opens on is one that exists.</summary>
    /// <remarks>
    /// A key pointed at an id nobody wrote opens the help on whatever happens to sort first,
    /// which reads as the key having gone to the wrong page rather than as a missing topic.
    /// </remarks>
    [Fact]
    public void The_page_it_opens_is_there()
    {
        var topic = _help.Find(HelpText.AppShortcuts);

        Assert.NotNull(topic);
        Assert.Equal("Keyboard shortcuts", topic!.Title);
    }

    /// <summary>And it is in the list down the side, so it can be browsed to as well.</summary>
    [Fact]
    public void And_is_in_the_list()
    {
        Assert.Contains(_help.All, topic => topic.Id == HelpText.AppShortcuts);
    }

    /// <summary>The keys that are written into the application are on the page.</summary>
    /// <remarks>
    /// Not every key, which would be a copy of the page in the test and would pass by agreeing
    /// with itself. These four are the ones somebody would open this page to find, and each is
    /// answered by a door of its own that a page could be written without knowing about.
    /// </remarks>
    [Theory]
    [InlineData("Space")]
    [InlineData("Ctrl+R")]
    [InlineData("Ctrl+Shift+M")]
    [InlineData("Ctrl+H")]
    public void The_fixed_keys_are_on_it(string key)
    {
        Assert.Contains(key, Page());
    }

    /// <summary>
    /// A key somebody puts on a page turns up on the page that lists the keys.
    /// </summary>
    /// <remarks>
    /// This is what the whole live half is for. A page of literals would be right on the day it
    /// was written and wrong for anybody who had ever set one.
    /// </remarks>
    [Fact]
    public void A_key_somebody_set_is_on_the_page()
    {
        IShortcutMap map = new ShortcutMap();
        map.Set(ShortcutAction.Tracker, new KeyGesture(Key.F2));

        Assert.Contains("`F2` goes to TRACKER.", Page(map));
    }

    /// <summary>A page nobody has put a key on says so rather than naming a key it is not on.</summary>
    [Fact]
    public void A_page_with_no_key_says_so()
    {
        Assert.Contains("FIRE is on no key.", Page(new ShortcutMap()));
    }

    /// <summary>Every action there is gets a line, including one added after this was written.</summary>
    /// <remarks>
    /// The page walks the actions rather than naming four, which is what makes it keep up. The
    /// test walks them the same way, so it cannot be satisfied by a page that lists exactly the
    /// four that existed the day it was written.
    /// </remarks>
    [Fact]
    public void Every_editable_shortcut_is_on_the_page()
    {
        IShortcutActions actions = new ShortcutActions();
        IShortcutMap map = new ShortcutMap();

        string body = new HelpText(new ShortcutSheet(actions, map)).Find(HelpText.AppShortcuts)!.Body;

        foreach (var one in actions.Everything)
            Assert.Contains(map.Said(one.Action), body);
    }

    /// <summary>The page is not empty, which is the one way all of the above could pass.</summary>
    [Fact]
    public void The_page_says_something()
    {
        Assert.True(Page().Split('\n').Length > 20);
    }

    /// <summary>
    /// Every section on the page is a heading rather than a shouty line of capitals.
    /// </summary>
    /// <remarks>
    /// It was capitals for about an hour, because the body was one TextBlock and a TextBlock is
    /// one size and one weight. Pinned so that a section added later is written the same way.
    /// Anything with a space in it is counted, since a single letter is its own capital and `Z`
    /// on the note keyboard is one.
    /// </remarks>
    [Fact]
    public void The_sections_are_headings()
    {
        var blocks = new Markdown().Read(Page());

        Assert.Contains(blocks, block => block.Kind == MarkdownKind.Heading);

        foreach (var block in blocks)
        {
            string said = string.Concat(block.Spans.Select(span => span.Text));

            if (!said.Contains(' ')) continue;

            Assert.NotEqual(said, said.ToUpperInvariant());
        }
    }

    /// <summary>Every key on the page is written as one, so it is drawn as one.</summary>
    /// <remarks>
    /// The code marks are what puts a key name in the pattern's own face. A key written as bare
    /// words reads as prose, which is exactly what somebody scanning for it cannot pick out.
    /// </remarks>
    [Fact]
    public void The_keys_are_marked_as_keys()
    {
        var blocks = new Markdown().Read(Page());

        foreach (string key in new[] { "Space", "Ctrl+R", "Ctrl+Shift+M", "Ctrl+H" })
            Assert.Contains(blocks, block =>
                block.Spans.Any(span => span.Code && span.Text == key));
    }

    /// <summary>
    /// The keyboard page as somebody reads it: the file, with its live half filled in.
    /// </summary>
    /// <remarks>
    /// Through <see cref="HelpText"/> rather than off the sheet, since the sheet is only the
    /// four lines that are a setting and the rest of the page is a file that ships. What is
    /// being asked about here is the page.
    /// </remarks>
    /// <param name="map">Which keys the editable shortcuts are on, or nothing for what ships.</param>
    private static string Page(IShortcutMap? map = null) =>
        new HelpText(new ShortcutSheet(map: map)).Find(HelpText.AppShortcuts)!.Body;
}
