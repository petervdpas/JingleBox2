using System;
using System.Linq;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which theme a name out of a settings file really means.
/// </summary>
/// <remarks>
/// This is the half of the theme machinery that goes wrong silently. Putting a theme on either
/// works or is obvious; resolving a name wrongly turns somebody's chosen theme back into the
/// default the next time they open the application, and nothing anywhere says why.
/// </remarks>
public class ThemeCatalogueTests
{
    private readonly IThemeCatalogue _themes = new ThemeCatalogue();

    /// <summary>Nothing at all reads as the default rather than throwing.</summary>
    [Fact]
    public void A_name_that_says_nothing_is_the_default()
    {
        Assert.Equal(_themes.Default, _themes.Resolve(null));
        Assert.Equal(_themes.Default, _themes.Resolve(""));
        Assert.Equal(_themes.Default, _themes.Resolve("   "));
    }

    /// <summary>A name nobody has is the default rather than a crash on a missing sheet.</summary>
    [Fact]
    public void A_name_nobody_has_is_the_default()
    {
        Assert.Equal(_themes.Default, _themes.Resolve("Chartreuse"));
        Assert.Equal(_themes.Default, _themes.Resolve("Dark Neon"));
    }

    /// <summary>The default is itself a theme, which is the thing that stops the loop.</summary>
    [Fact]
    public void The_default_is_one_of_the_themes()
    {
        Assert.Contains(_themes.Default, _themes.Names);
        Assert.Equal(_themes.Default, _themes.Resolve(_themes.Default));
    }

    /// <summary>Every theme resolves to itself, whatever case or padding it arrives in.</summary>
    [Fact]
    public void Every_theme_resolves_to_itself()
    {
        foreach (string name in _themes.Names)
        {
            Assert.Equal(name, _themes.Resolve(name));
            Assert.Equal(name, _themes.Resolve(name.ToUpperInvariant()));
            Assert.Equal(name, _themes.Resolve(name.ToLowerInvariant()));
            Assert.Equal(name, _themes.Resolve("  " + name + "  "));
        }
    }

    /// <summary>
    /// A name saved before the coloured themes gained a light half follows to the dark one.
    /// </summary>
    /// <remarks>
    /// Every one of these was the whole family before it was the dark half of it, so somebody
    /// who chose Neon a year ago gets Neon Dark rather than the default.
    /// </remarks>
    [Fact]
    public void An_old_name_follows_to_what_it_is_called_now()
    {
        Assert.Equal("Neon Dark", _themes.Resolve("Neon"));
        Assert.Equal("Industrial Dark", _themes.Resolve("Industrial"));
        Assert.Equal("Orchid Dark", _themes.Resolve("Orchid"));
        Assert.Equal("Citrus Dark", _themes.Resolve("Citrus"));
        Assert.Equal("Ember Dark", _themes.Resolve("Ember"));

        Assert.Equal("Neon Dark", _themes.Resolve("neon"));
        Assert.Equal("Neon Dark", _themes.Resolve("  NEON  "));
    }

    /// <summary>Every theme has a sheet, and no two share one.</summary>
    /// <remarks>
    /// Two themes on one sheet would be two rows in the picker that look identical, which reads
    /// as one of them being broken.
    /// </remarks>
    [Fact]
    public void Every_theme_has_a_sheet_of_its_own()
    {
        var sheets = _themes.Names.Select(_themes.SheetFor).ToList();

        Assert.Equal(sheets.Count, sheets.Distinct(StringComparer.Ordinal).Count());
        Assert.All(sheets, sheet => Assert.StartsWith("avares://JingleBox2/Themes/", sheet, StringComparison.Ordinal));
        Assert.All(sheets, sheet => Assert.EndsWith(".axaml", sheet, StringComparison.Ordinal));
    }

    /// <summary>The base sheet is not one of the themes: it is what they all sit on.</summary>
    [Fact]
    public void The_base_sheet_is_not_a_theme()
    {
        Assert.DoesNotContain(_themes.BaseSheet, _themes.Names.Select(_themes.SheetFor));
        Assert.EndsWith("Base.axaml", _themes.BaseSheet, StringComparison.Ordinal);
    }

    /// <summary>A name nobody has still answers with a sheet, which is the default's.</summary>
    [Fact]
    public void A_name_nobody_has_still_answers_with_a_sheet()
    {
        Assert.Equal(_themes.SheetFor(_themes.Default), _themes.SheetFor("Chartreuse"));
        Assert.Equal(_themes.SheetFor(_themes.Default), _themes.SheetFor(null));
    }

    /// <summary>
    /// The themes come in pairs, and each half says which room it is for.
    /// </summary>
    /// <remarks>
    /// Whether a theme is lit is what Fluent draws its own popups, scrollbars and text boxes
    /// from, so a pale sheet marked dark gets dark popups over a white page. Read off the names
    /// rather than listed here, so a pair added later is covered without anybody remembering.
    /// </remarks>
    [Fact]
    public void Each_half_of_a_pair_says_which_room_it_is_for()
    {
        foreach (string name in _themes.Names)
        {
            if (name.EndsWith("Light", StringComparison.Ordinal))
                Assert.True(_themes.IsLit(name), name + " is a light theme and says it is not");
            else
                Assert.False(_themes.IsLit(name), name + " is a dark theme and says it is lit");
        }
    }

    /// <summary>Every coloured family has both halves.</summary>
    [Fact]
    public void Every_coloured_family_has_both_halves()
    {
        foreach (string family in new[] { "Neon", "Industrial", "Orchid", "Citrus", "Ember" })
        {
            Assert.Contains(family + " Dark", _themes.Names);
            Assert.Contains(family + " Light", _themes.Names);
        }

        Assert.Contains("Dark", _themes.Names);
        Assert.Contains("Light", _themes.Names);
    }

    /// <summary>No theme is listed twice, since the picker is built straight off this list.</summary>
    [Fact]
    public void No_theme_is_listed_twice()
    {
        Assert.Equal(_themes.Names.Count, _themes.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// The plain pair comes first, since it is the one with no colour to be named after.
    /// </summary>
    /// <remarks>
    /// The order is the picker's order, and the picker is read top down by somebody who knows
    /// the colours they want and then which room they are sitting in.
    /// </remarks>
    [Fact]
    public void The_plain_pair_comes_first()
    {
        Assert.Equal("Dark", _themes.Names[0]);
        Assert.Equal("Light", _themes.Names[1]);
    }
}
