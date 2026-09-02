using System;
using System.Linq;
using JingleBox2.Rack.Faces;
using JingleBox2.Rack.Faces.Interfaces;
using JingleBox2.Rack.Faces.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which options a machine's Menu part carries, read off what its file says.
/// </summary>
/// <remarks>
/// The rule on its own, with no window and no machine, which is why it is a class rather than a
/// few lines inside the drawing. Almost none of what is asked here is the happy path: it is a
/// string out of somebody's file, so what matters is what it does with a word it has never heard
/// of, a list with holes in it, and a machine that says nothing at all.
/// </remarks>
public class MenuOptionsTests
{
    /// <summary>The rule.</summary>
    private static readonly IMenuOptions Options = new MenuOptions();

    /// <summary>
    /// A machine that says nothing about its options carries all of them.
    /// </summary>
    /// <remarks>
    /// Which is what a Menu dropped on a panel and left alone should do, and what every machine
    /// written before an option existed will keep doing when one is added.
    /// </remarks>
    [Fact]
    public void A_menu_that_says_nothing_carries_everything()
    {
        Assert.Equal(MenuOptionWords.All, Options.Named(null));
    }

    /// <summary>
    /// And one that says so and names none carries none.
    /// </summary>
    /// <remarks>
    /// Not the same answer as saying nothing, and it must not be: somebody has taken every option
    /// off, which is a menu that drops down nothing and is a state the designer allows.
    /// </remarks>
    [Fact]
    public void A_menu_that_names_none_carries_none()
    {
        Assert.Empty(Options.Named(""));
        Assert.Empty(Options.Named("   "));
        Assert.Empty(Options.Named(",,,"));
    }

    /// <summary>What it names, however untidily the file writes it.</summary>
    /// <remarks>
    /// A machine's file is meant to be opened and corrected by hand, so spaces around a word and
    /// a trailing separator are what it will really look like.
    /// </remarks>
    [Theory]
    [InlineData("surfaces,learn")]
    [InlineData(" surfaces , learn ")]
    [InlineData("surfaces,learn,")]
    [InlineData("SURFACES,Learn")]
    public void It_reads_what_the_file_names_however_it_is_written(string said)
    {
        var named = Options.Named(said);

        Assert.True(Options.Carries(named, MenuOptionWords.Surfaces));
        Assert.True(Options.Carries(named, MenuOptionWords.Learn));
    }

    /// <summary>The same word twice is one option and not two.</summary>
    [Fact]
    public void The_same_option_twice_is_one_option()
    {
        Assert.Single(Options.Named("learn,learn,LEARN"));
    }

    /// <summary>
    /// A word this build has never heard of is carried nowhere and takes nothing with it.
    /// </summary>
    /// <remarks>
    /// Which is what a machine from a later version looks like. Refusing the part would throw
    /// away the options that do work, and the useful answer is the ones that are understood.
    /// </remarks>
    [Fact]
    public void An_option_this_build_does_not_know_leaves_the_rest_alone()
    {
        var named = Options.Named("surfaces,somethingelse");

        Assert.True(Options.Carries(named, MenuOptionWords.Surfaces));
        Assert.False(Options.Carries(named, MenuOptionWords.Learn));
        Assert.True(Options.Carries(named, "somethingelse"));
    }

    /// <summary>An option named on its own leaves the others off.</summary>
    [Fact]
    public void Naming_one_option_leaves_the_others_off()
    {
        var named = Options.Named(MenuOptionWords.Learn);

        Assert.False(Options.Carries(named, MenuOptionWords.Surfaces));
        Assert.True(Options.Carries(named, MenuOptionWords.Learn));
    }

    /// <summary>
    /// A line belonging to no option is always carried.
    /// </summary>
    /// <remarks>
    /// It is something the Menu always says rather than part of an option anybody chose, so it
    /// survives a machine that has taken every option off.
    /// </remarks>
    [Fact]
    public void A_line_belonging_to_no_option_is_always_carried()
    {
        Assert.True(Options.Carries(Options.Named(""), ""));
        Assert.True(Options.Carries(Options.Named(""), null));
        Assert.True(Options.Carries(Options.Named("learn"), ""));
    }

    /// <summary>Every option there is has a word, and no two of them share one.</summary>
    /// <remarks>
    /// The list is what the designer builds its ticks from and what a machine with no options
    /// named carries, so a duplicate in it would be a tick that turned two things on.
    /// </remarks>
    [Fact]
    public void Every_option_has_a_word_of_its_own()
    {
        Assert.NotEmpty(MenuOptionWords.All);
        Assert.All(MenuOptionWords.All, one => Assert.False(string.IsNullOrWhiteSpace(one)));
        Assert.Equal(MenuOptionWords.All.Count,
            MenuOptionWords.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>A line says nothing about which option it is on unless it is told.</summary>
    /// <remarks>
    /// The default matters: a host writing a line and forgetting to say which option it belongs
    /// to gets a line that is always shown, which is visible, rather than one that is never
    /// shown, which is not.
    /// </remarks>
    [Fact]
    public void A_line_belongs_to_no_option_until_it_is_told()
    {
        var line = new PanelMenuItem("Something");

        Assert.Equal("", line.Option);
        Assert.True(line.Live);
        Assert.Equal("", line.Tip);
        Assert.Null(line.Chosen);
    }

    /// <summary>The designer's ticks are the options there are, so a new one turns up on its own.</summary>
    [Fact]
    public void The_designer_offers_a_tick_for_every_option_there_is()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element);

        Assert.True(picked.IsMenu);
        Assert.Equal(MenuOptionWords.All.Count, picked.Options.Count);
        Assert.All(picked.Options, one => Assert.True(one.On));
    }

    /// <summary>
    /// Taking one off writes the rest down, and taking the last off leaves a menu saying nothing.
    /// </summary>
    /// <remarks>
    /// Nothing is written until somebody takes one off, so a machine that has never been near
    /// this page keeps a file with no options line in it and goes on carrying everything.
    /// </remarks>
    [Fact]
    public void Taking_an_option_off_writes_the_rest_down()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element);

        Assert.False(element.Properties.ContainsKey(MenuOptionWords.Property));

        picked.Options.First(one => one.Said.Contains("Learn", StringComparison.Ordinal)).On = false;

        Assert.Equal(MenuOptionWords.Surfaces, element.Properties[MenuOptionWords.Property]);

        foreach (var one in picked.Options) one.On = false;

        Assert.Equal("", element.Properties[MenuOptionWords.Property]);
        Assert.Empty(Options.Named(element.Properties[MenuOptionWords.Property]));
    }

    /// <summary>And putting one back on says so again.</summary>
    [Fact]
    public void Putting_an_option_back_on_says_so_again()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        element.Properties[MenuOptionWords.Property] = "";

        var picked = new ViewModels.PanelElementViewModel(element);

        Assert.All(picked.Options, one => Assert.False(one.On));

        picked.Options[0].On = true;

        Assert.True(picked.Options[0].On);
        Assert.True(Options.Carries(
            Options.Named(element.Properties[MenuOptionWords.Property]),
            MenuOptionWords.All[0]));
    }

    /// <summary>An option a machine names that this build has no word for is still shown as itself.</summary>
    /// <remarks>
    /// A machine from a later version should be legible in the designer rather than quietly
    /// missing a line, which is the same rule the drawing keeps for an element of an unknown kind.
    /// </remarks>
    [Fact]
    public void An_option_with_no_wording_here_is_shown_as_the_file_spells_it()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        element.Properties[MenuOptionWords.Property] = "surfaces,somethingelse";

        var picked = new ViewModels.PanelElementViewModel(element);

        Assert.All(picked.Options, one => Assert.NotEqual("", one.Said));
        Assert.True(picked.Options.First().On);
    }

    /// <summary>A menu sits in the upper right unless somebody says otherwise.</summary>
    /// <remarks>
    /// Where every program puts this button and therefore the first place a hand looks. A machine
    /// that has never been near the corner picker keeps a file with no corner in it.
    /// </remarks>
    [Fact]
    public void A_menu_sits_in_the_upper_right_until_it_is_told_otherwise()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element);

        Assert.Equal(MenuCorners.All.Count, picked.Corners.Count);
        Assert.Equal(picked.Corners[0], picked.Corner);
        Assert.False(element.Properties.ContainsKey(MenuCorners.Property));
    }

    /// <summary>Choosing the other one writes the word the file uses.</summary>
    [Fact]
    public void Choosing_the_other_corner_writes_the_word_the_file_uses()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element);

        picked.Corner = picked.Corners[1];

        Assert.Equal(MenuCorners.TopLeft, element.Properties[MenuCorners.Property]);

        picked.Corner = picked.Corners[0];

        Assert.Equal(MenuCorners.TopRight, element.Properties[MenuCorners.Property]);
    }

    /// <summary>
    /// A corner this build has never heard of reads as the upper right rather than as nothing.
    /// </summary>
    /// <remarks>
    /// Which is what a machine from a later version, or a hand-written file with a typo in it,
    /// looks like. A menu drawn nowhere would be a part that had silently vanished.
    ///
    /// The two lower corners are in that list on purpose. They were offered for a while and had
    /// to go: a panel taller than its window scrolls, so the bottom of it is below the fold and
    /// the button was really there with nobody able to see it. A machine saved while they existed
    /// still opens, and its menu comes back to the corner a hand looks in.
    /// </remarks>
    [Theory]
    [InlineData("bottomLeft")]
    [InlineData("bottomRight")]
    [InlineData("somewhere")]
    [InlineData("")]
    public void A_corner_this_build_does_not_know_reads_as_the_upper_right(string said)
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        element.Properties[MenuCorners.Property] = said;

        Assert.Equal(new ViewModels.PanelElementViewModel(element).Corners[0],
            new ViewModels.PanelElementViewModel(element).Corner);
    }

    /// <summary>The corner is spelled however a hand wrote it and still reads.</summary>
    [Fact]
    public void The_corner_is_read_however_it_is_spelled()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        element.Properties[MenuCorners.Property] = "  TOPLEFT ";

        Assert.Equal(new ViewModels.PanelElementViewModel(element).Corners[1],
            new ViewModels.PanelElementViewModel(element).Corner);
    }

    /// <summary>
    /// Neither of a menu's own two settings turns up in the list of properties to be typed.
    /// </summary>
    /// <remarks>
    /// They are set where they are acted on, the same as a grid's rows and a picker's source. A
    /// box you could type into beside a picker that already says it is two ways of saying one
    /// thing, and they would eventually disagree.
    /// </remarks>
    [Fact]
    public void A_menus_own_settings_are_not_offered_as_properties_to_type()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        element.Properties[MenuCorners.Property] = MenuCorners.TopLeft;
        element.Properties[MenuOptionWords.Property] = MenuOptionWords.Learn;
        element.Properties["margin"] = "4";

        var picked = new ViewModels.PanelElementViewModel(element);

        Assert.Equal("margin", Assert.Single(picked.Properties).Key);
    }

    /// <summary>All four corners are offered, and each has a word of its own.</summary>
    [Fact]
    public void Every_corner_is_offered_and_each_has_a_word_of_its_own()
    {
        var picked = new ViewModels.PanelElementViewModel(new PanelElement { Element = ElementKinds.Menu });

        Assert.Equal(MenuCorners.All.Count, picked.Corners.Count);
        Assert.Equal(picked.Corners.Count, picked.Corners.Distinct(StringComparer.Ordinal).Count());

        foreach (string corner in picked.Corners)
        {
            picked.Corner = corner;

            Assert.Equal(corner, picked.Corner);
        }
    }

    /// <summary>
    /// Each one writes a word that is one of the four the file allows.
    /// </summary>
    /// <remarks>
    /// The default is left out of the loop on purpose: a menu that has never been moved says
    /// nothing about its corner, so choosing the corner it already sits in writes nothing at all.
    /// It is picked up again at the end, from a menu that does name one.
    /// </remarks>
    [Fact]
    public void Every_corner_writes_a_word_the_file_allows()
    {
        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element);

        foreach (string corner in picked.Corners.Skip(1))
        {
            picked.Corner = corner;

            Assert.Contains(element.Properties[MenuCorners.Property], MenuCorners.All);
        }

        picked.Corner = picked.Corners[0];

        Assert.Equal(MenuCorners.TopRight, element.Properties[MenuCorners.Property]);
    }

    /// <summary>
    /// A machine may have one menu, and turning something else into a second one is refused.
    /// </summary>
    /// <remarks>
    /// A second menu is either in the same corner drawing over the first or in another corner
    /// offering the same lines twice. Neither is a thing anybody meant, and both are the kind of
    /// mistake you only notice with the designer shut.
    /// </remarks>
    [Fact]
    public void A_machine_may_have_one_menu()
    {
        var root = new PanelElement { Element = ElementKinds.Column };

        root.Children.Add(new PanelElement { Element = ElementKinds.Menu });
        root.Children.Add(new PanelElement { Element = ElementKinds.Knob });

        var picked = new ViewModels.PanelElementViewModel(root);

        var knob = picked.Children[1];

        knob.Kind = ElementKinds.Menu;

        Assert.Equal(ElementKinds.Knob, knob.Kind);
    }

    /// <summary>And the one that is there may still be turned into something else and back.</summary>
    /// <remarks>
    /// The rule is about a second menu and not about the menu, so it must not fence the only one
    /// in: an element already a menu stays settable, or somebody who changed their mind could
    /// never get it back.
    /// </remarks>
    [Fact]
    public void The_only_menu_may_still_be_turned_into_something_else_and_back()
    {
        var root = new PanelElement { Element = ElementKinds.Column };

        root.Children.Add(new PanelElement { Element = ElementKinds.Menu });

        var only = new ViewModels.PanelElementViewModel(root).Children[0];

        only.Kind = ElementKinds.Knob;

        Assert.Equal(ElementKinds.Knob, only.Kind);

        only.Kind = ElementKinds.Menu;

        Assert.Equal(ElementKinds.Menu, only.Kind);
    }

    /// <summary>
    /// A combo box binding itself to the corner it already has changes nothing.
    /// </summary>
    /// <remarks>
    /// This is what broke saving. A list bound two ways writes the value back as soon as it is
    /// shown, so merely picking the menu wrote a property nobody had asked for; the history was
    /// never told, its idea of what is on screen stayed where it was, and the difference between
    /// that and what the save wrote was permanent. From the outside the Save button went green
    /// and never went back.
    /// </remarks>
    [Fact]
    public void Showing_the_corner_it_already_has_changes_nothing()
    {
        int edits = 0;

        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element, edited: () => edits++);

        picked.Corner = picked.Corner;

        Assert.False(element.Properties.ContainsKey(MenuCorners.Property));
        Assert.Equal(0, edits);
    }

    /// <summary>And choosing the one it already has written down changes nothing either.</summary>
    [Fact]
    public void Choosing_the_corner_it_already_names_changes_nothing()
    {
        int edits = 0;

        var element = new PanelElement { Element = ElementKinds.Menu };

        element.Properties[MenuCorners.Property] = MenuCorners.TopLeft;

        var picked = new ViewModels.PanelElementViewModel(element, edited: () => edits++);

        picked.Corner = picked.Corner;

        Assert.Equal(MenuCorners.TopLeft, element.Properties[MenuCorners.Property]);
        Assert.Equal(0, edits);
    }

    /// <summary>
    /// Everything here that writes into the element says so, so the machine counts as changed.
    /// </summary>
    /// <remarks>
    /// The elements are plain data and say nothing when they are edited, so an edit that does not
    /// say so reaches the machine and nothing else. Listed one by one rather than trusted,
    /// because the one that is forgotten is the one that breaks saving and it will not be the
    /// same one twice.
    /// </remarks>
    [Fact]
    public void Everything_that_writes_into_the_element_says_so()
    {
        int edits = 0;

        var element = new PanelElement { Element = ElementKinds.Menu };

        var picked = new ViewModels.PanelElementViewModel(element, edited: () => edits++);

        picked.Corner = picked.Corners[1];
        Assert.Equal(1, edits);

        picked.Options[0].On = false;
        Assert.Equal(2, edits);

        picked.Label = "Setup";
        Assert.Equal(3, edits);

        picked.Parameter = "cutoff";
        Assert.Equal(4, edits);

        picked.Kind = ElementKinds.Knob;
        Assert.Equal(5, edits);
    }

    /// <summary>And so does something dropped onto it afterwards, without being wired again.</summary>
    /// <remarks>
    /// A child takes the hook from whatever holds it, so one at the root hears the whole panel
    /// however deep it goes and however late a part arrives.
    /// </remarks>
    [Fact]
    public void A_part_added_later_says_it_too()
    {
        int edits = 0;

        var root = new PanelElement { Element = ElementKinds.Column };

        var picked = new ViewModels.PanelElementViewModel(root, edited: () => edits++);

        var added = picked.Add(new PanelElement { Element = ElementKinds.Knob });

        added.Label = "Cutoff";

        Assert.Equal(1, edits);
    }

    /// <summary>A wrapper nobody wired says nothing and does not fall over saying it.</summary>
    [Fact]
    public void A_wrapper_with_no_hook_writes_without_complaining()
    {
        var picked = new ViewModels.PanelElementViewModel(new PanelElement { Element = ElementKinds.Menu });

        picked.Corner = picked.Corners[1];

        Assert.Equal(picked.Corners[1], picked.Corner);
    }

    /// <summary>Anything that is not a menu has no options to offer.</summary>
    [Fact]
    public void Anything_that_is_not_a_menu_is_not_asked_about_options()
    {
        Assert.False(new ViewModels.PanelElementViewModel(
            new PanelElement { Element = ElementKinds.Knob }).IsMenu);
    }
}
