using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Naming a take, which has to be right before the recording starts rather than after it.
/// </summary>
/// <remarks>
/// Saving overwrites without asking, so a clash caught late is a take somebody has already made
/// landing on a take they already had.
/// </remarks>
public class RecordingNameTests
{
    private readonly IRecordingNames _names = new RecordingNames();

    private static readonly string[] Nothing = Array.Empty<string>();

    /// <summary>A blank name, however it is blank, is refused for the same reason.</summary>
    [Fact]
    public void A_blank_name_is_refused()
    {
        Assert.Equal(_names.EmptyMessage, _names.Validate(null, Nothing));
        Assert.Equal(_names.EmptyMessage, _names.Validate("", Nothing));
        Assert.Equal(_names.EmptyMessage, _names.Validate("   ", Nothing));
        Assert.Equal(_names.EmptyMessage, _names.Validate("\t \n", Nothing));
    }

    /// <summary>An ordinary name is allowed, and says nothing back.</summary>
    [Fact]
    public void An_ordinary_name_is_allowed()
    {
        Assert.Null(_names.Validate("jingle", Nothing));
        Assert.Null(_names.Validate("  jingle  ", Nothing));
        Assert.Null(_names.Validate("jingle-001", Nothing));
    }

    /// <summary>Every character a file name may not hold is refused.</summary>
    /// <remarks>
    /// Walked rather than listed, because the set differs between the two systems this runs on
    /// and a list written here would be right on one of them.
    /// </remarks>
    [Fact]
    public void A_name_holding_a_forbidden_character_is_refused()
    {
        foreach (char bad in Path.GetInvalidFileNameChars())
        {
            if (char.IsWhiteSpace(bad)) continue;

            Assert.Equal(_names.InvalidCharsMessage, _names.Validate("take" + bad, Nothing));
        }
    }

    /// <summary>A name somebody's take already has is refused, whatever its case or padding.</summary>
    [Fact]
    public void A_name_already_taken_is_refused()
    {
        var taken = new[] { "Jingle" };

        Assert.Equal(_names.InUseMessage, _names.Validate("Jingle", taken));
        Assert.Equal(_names.InUseMessage, _names.Validate("jingle", taken));
        Assert.Equal(_names.InUseMessage, _names.Validate("JINGLE", taken));
        Assert.Equal(_names.InUseMessage, _names.Validate("  jingle  ", taken));
    }

    /// <summary>
    /// A difference of case is a clash on both systems, deliberately.
    /// </summary>
    /// <remarks>
    /// File names are case insensitive on Windows and case sensitive on Linux. Allowing the
    /// pair on Linux would let somebody make a shelf that cannot be opened on Windows, where
    /// the second take would land on the first.
    /// </remarks>
    [Fact]
    public void Case_alone_is_a_clash_on_both_systems()
    {
        Assert.Equal(_names.InUseMessage, _names.Validate("TAKE", new[] { "take" }));
    }

    /// <summary>A blank name is refused before a clash is even looked for.</summary>
    [Fact]
    public void Blankness_is_reported_before_a_clash()
    {
        Assert.Equal(_names.EmptyMessage, _names.Validate("", new[] { "" }));
    }

    /// <summary>The series a name belongs to, with any number taken off and lowercased.</summary>
    [Fact]
    public void A_name_says_which_series_it_is_in()
    {
        Assert.Equal("jingle", _names.BaseNameOf("jingle"));
        Assert.Equal("jingle", _names.BaseNameOf("Jingle"));
        Assert.Equal("jingle", _names.BaseNameOf("jingle-004"));
        Assert.Equal("jingle", _names.BaseNameOf("Jingle-9999"));
        Assert.Equal("jingle", _names.BaseNameOf("  jingle-004  "));
    }

    /// <summary>A name that says nothing about a series falls into the default one.</summary>
    [Fact]
    public void A_name_that_says_nothing_falls_into_the_default_series()
    {
        Assert.Equal(_names.DefaultBaseName, _names.BaseNameOf(null));
        Assert.Equal(_names.DefaultBaseName, _names.BaseNameOf(""));
        Assert.Equal(_names.DefaultBaseName, _names.BaseNameOf("   "));
        Assert.Equal(_names.DefaultBaseName, _names.BaseNameOf("-004"));
    }

    /// <summary>A hyphen followed by something that is not a number is part of the name.</summary>
    [Fact]
    public void A_hyphen_that_is_not_a_number_stays_in_the_name()
    {
        Assert.Equal("post-roll", _names.BaseNameOf("post-roll"));
        Assert.Equal("take-a", _names.BaseNameOf("take-a"));
    }

    /// <summary>Only the last number is the suffix, so a name may hold others.</summary>
    [Fact]
    public void Only_the_last_number_is_the_suffix()
    {
        Assert.Equal("jingle-2024", _names.BaseNameOf("jingle-2024-003"));
    }

    /// <summary>An empty shelf starts at one, padded.</summary>
    [Fact]
    public void An_empty_shelf_starts_at_one()
    {
        Assert.Equal("jingle-001", _names.NextName("jingle", Nothing));
        Assert.Equal(_names.DefaultBaseName + "-001", _names.NextName(null, Nothing));
    }

    /// <summary>The next name is one past the highest already taken, not one past the count.</summary>
    /// <remarks>
    /// Numbers are not reused after a delete, so a name never points at two different takes
    /// over a session. Two and five taken means the next is six rather than three.
    /// </remarks>
    [Fact]
    public void The_next_name_is_one_past_the_highest()
    {
        var taken = new[] { "jingle-002", "jingle-005" };

        Assert.Equal("jingle-006", _names.NextName("jingle", taken));
    }

    /// <summary>A name in the series with no number still occupies its place.</summary>
    [Fact]
    public void A_name_with_no_number_still_occupies_a_place()
    {
        Assert.Equal("jingle-002", _names.NextName("jingle", new[] { "jingle-001" }));
        Assert.Equal("jingle-001", _names.NextName("jingle", new[] { "jingle" }));
    }

    /// <summary>The search walks up past whatever is already answered to.</summary>
    [Fact]
    public void The_search_walks_past_what_is_taken()
    {
        var taken = new[] { "jingle-001", "jingle-002", "jingle-003" };

        Assert.Equal("jingle-004", _names.NextName("jingle", taken));
    }

    /// <summary>Another series' takes do not push this one along.</summary>
    [Fact]
    public void Another_series_does_not_count()
    {
        var taken = new[] { "sting-009", "bed-100" };

        Assert.Equal("jingle-001", _names.NextName("jingle", taken));
    }

    /// <summary>Any name in a series names the series, numbered or not.</summary>
    [Fact]
    public void Any_name_in_the_series_will_do()
    {
        var taken = new[] { "jingle-004" };

        Assert.Equal("jingle-005", _names.NextName("jingle-002", taken));
        Assert.Equal("jingle-005", _names.NextName("JINGLE", taken));
    }

    /// <summary>Numbers past the padded width simply grow past it.</summary>
    [Fact]
    public void Numbers_grow_past_the_padding()
    {
        Assert.Equal("jingle-1000", _names.NextName("jingle", new[] { "jingle-0999" }));
    }

    /// <summary>A suffix that is not a number this build can read is passed over.</summary>
    [Fact]
    public void A_number_too_big_to_read_is_passed_over()
    {
        Assert.Equal("jingle-001", _names.NextName("jingle", new[] { "jingle-99999999999999999999" }));
    }

    /// <summary>What it suggests is always something it would itself allow.</summary>
    /// <remarks>
    /// The two halves are used together, so a suggestion the check refuses would be a page
    /// showing an error against a name nobody typed.
    /// </remarks>
    [Fact]
    public void What_it_suggests_is_always_allowed()
    {
        var taken = new List<string>();

        for (int i = 0; i < 40; i++)
        {
            string next = _names.NextName("jingle", taken);

            Assert.Null(_names.Validate(next, taken));
            taken.Add(next);
        }

        Assert.Equal(40, taken.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
