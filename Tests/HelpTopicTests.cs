using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JingleBox2.Help;
using JingleBox2.Help.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The topics that ship, read off disc the way the application reads them.
/// </summary>
/// <remarks>
/// This is content rather than code, and content goes wrong the way content goes wrong: a file
/// renamed and a badge left pointing at the old name, a page with no heading, a topic nobody can
/// reach. None of those is a crash and every one of them is a badge that opens the wrong page or
/// no page at all, which from a chair reads as the help being broken.
///
/// It says out loud when it cannot find the folder, since a test that quietly passes where its
/// subject is missing reports nothing for the rest of its life.
/// </remarks>
public class HelpTopicTests
{
    /// <summary>
    /// How a badge is found in a layout: the part before the topic it names.
    /// </summary>
    /// <remarks>
    /// Written once and used by both directions, since two spellings of one pattern would
    /// eventually disagree about what counts as a badge, and the way that fails is a test that
    /// quietly stops seeing half of them.
    /// </remarks>
    private const string Badge = @"HelpBadge[^>]*?Topic=""([^""]+)""";

    /// <summary>Everything the app explains about itself, as it ships.</summary>
    private readonly IHelpText _help = new HelpText();

    /// <summary>The folder is where it says it is.</summary>
    [Fact]
    public void The_topics_are_beside_the_program()
    {
        Assert.True(Directory.Exists(HelpText.Folder), "no help folder at " + HelpText.Folder);
        Assert.NotEmpty(Directory.GetFiles(HelpText.Folder, "*" + HelpTopics.Extension));
    }

    /// <summary>Every file is a topic, and every topic has a title and something to say.</summary>
    [Fact]
    public void Every_file_reads_as_a_topic()
    {
        var topics = _help.All;

        Assert.Equal(Directory.GetFiles(HelpText.Folder, "*" + HelpTopics.Extension).Length, topics.Count);

        foreach (var topic in topics)
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title), topic.Id + " has no title");
            Assert.False(string.IsNullOrWhiteSpace(topic.Summary), topic.Id + " has no summary");
            Assert.False(string.IsNullOrWhiteSpace(topic.Body), topic.Id + " has no page");
        }
    }

    /// <summary>
    /// Every id declared in code has a file, and every file has an id declared in code.
    /// </summary>
    /// <remarks>
    /// Both directions, because they are two different faults. A constant with no file is a
    /// badge that opens nothing; a file with no constant is a page nobody can reach from
    /// anywhere but the list, which is a page somebody wrote and quietly lost.
    /// </remarks>
    [Fact]
    public void The_declared_ids_and_the_files_are_the_same_set()
    {
        var declared = typeof(HelpText).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(id => id != HelpText.SystemKeysMark && id != HelpText.MenuKeysMark)
            .ToHashSet(StringComparer.Ordinal);

        var onDisc = _help.All.Select(topic => topic.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(declared.OrderBy(id => id), onDisc.OrderBy(id => id));
    }

    /// <summary>
    /// Every topic a layout asks for by name is one that exists.
    /// </summary>
    /// <remarks>
    /// The badges name their topic as a literal in XAML rather than through the constants, since
    /// XAML cannot reach a const. So the compiler has nothing to say about a badge pointing at a
    /// topic that was renamed, and this is the only thing that would.
    /// </remarks>
    [Fact]
    public void Every_badge_in_a_layout_points_at_a_topic()
    {
        string views = Path.Combine(Sources(), "Views");

        Assert.True(Directory.Exists(views), "no Views folder at " + views);

        var asked = Directory.GetFiles(views, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), Badge, RegexOptions.Singleline)
                .Select(found => (File: Path.GetFileName(file), Id: found.Groups[1].Value)))
            .ToList();

        Assert.NotEmpty(asked);

        foreach (var one in asked)
            Assert.True(_help.Find(one.Id) != null, one.File + " asks for '" + one.Id + "', which is not a topic");
    }

    /// <summary>
    /// Every page along the top has a badge somewhere on it.
    /// </summary>
    /// <remarks>
    /// The other direction from the test above, and it is the one that was missing. That one
    /// catches a badge pointing at nothing; this one catches a page pointing at nothing, which
    /// is quieter: the help is complete, the topic exists, and there is no way to it from the
    /// page it is about. Two pages had gone that way before this test existed, FIRE and the
    /// rack, and both were found by reading the layouts by hand rather than by anything failing.
    ///
    /// The pages rather than every view, since a strip, a dialog and a window are reached from a
    /// page that has one. Named here rather than read off the tab strip, because what counts as
    /// a page is a decision, and a list somebody has to edit is exactly the reminder wanted: a
    /// page added along the top has to be added here, and the way that fails is this going red
    /// rather than a page shipping with no way into the help.
    ///
    /// SETTINGS is left out and is the one exception. It is a page of cards and every card
    /// carries its own badge, so a rule of one per page would be met by a page answering for a
    /// tenth of itself.
    /// </remarks>
    [Fact]
    public void Every_page_has_a_way_into_the_help()
    {
        string views = Path.Combine(Sources(), "Views");

        string[] pages =
        {
            "MixerView.axaml",
            "RecordView.axaml",
            "PadsView.axaml",
            "UseView.axaml",
            "TrackerView.axaml",
            "RackView.axaml",
            "DesignerView.axaml",
            "ControlLinksView.axaml"
        };

        foreach (string page in pages)
        {
            string file = Path.Combine(views, page);

            Assert.True(File.Exists(file), page + " is not in " + views + " any more");

            var badges = Regex.Matches(File.ReadAllText(file), Badge, RegexOptions.Singleline);

            Assert.True(badges.Count > 0,
                page + " has no help badge on it, so the topic it is about cannot be reached from it");

            foreach (Match badge in badges)
                Assert.True(_help.Find(badge.Groups[1].Value) != null,
                    page + " asks for '" + badge.Groups[1].Value + "', which is not a topic");
        }
    }

    /// <summary>The keyboard page really does have the two holes its live half goes into.</summary>
    /// <remarks>
    /// Without a mark the page reads perfectly and simply never mentions the keys, which is the
    /// quietest way this could fail.
    /// </remarks>
    [Fact]
    public void The_keyboard_page_has_its_holes()
    {
        string file = Path.Combine(HelpText.Folder, HelpText.AppShortcuts + HelpTopics.Extension);
        string text = File.ReadAllText(file);

        Assert.Contains(HelpText.SystemKeysMark, text);
        Assert.Contains(HelpText.MenuKeysMark, text);
    }

    /// <summary>And both are filled by the time anybody reads the page.</summary>
    [Fact]
    public void And_the_holes_are_filled_in()
    {
        var topic = _help.Find(HelpText.AppShortcuts);

        Assert.NotNull(topic);
        Assert.DoesNotContain(HelpText.SystemKeysMark, topic!.Body);
        Assert.DoesNotContain(HelpText.MenuKeysMark, topic.Body);
        Assert.Contains("Ctrl+S", topic.Body);
        Assert.Contains("TRACKER", topic.Body);
    }

    /// <summary>A folder that is not there is no topics rather than a start that fails.</summary>
    [Fact]
    public void A_missing_folder_is_empty_rather_than_fatal()
    {
        var help = new HelpText(folder: Path.Combine(Path.GetTempPath(), "jinglebox-no-help-" + Guid.NewGuid()));

        Assert.Empty(help.All);
        Assert.Null(help.Find(HelpText.SettingsEngine));
    }

    /// <summary>Where the repository is, walked up from wherever the tests are running.</summary>
    /// <param name="from">Where to start looking, or nothing for the test's own folder.</param>
    private static string Sources(string? from = null)
    {
        var at = new DirectoryInfo(from ?? AppContext.BaseDirectory);

        while (at != null && !File.Exists(Path.Combine(at.FullName, "JingleBox2.csproj")))
            at = at.Parent;

        return at?.FullName ?? "";
    }
}
