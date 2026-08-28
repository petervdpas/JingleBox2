using System;
using System.Collections.Generic;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The note a run leaves saying it is under way, and what a note still lying there means.
/// </summary>
/// <remarks>
/// The two directions are a pair and the pair is what is tested: a marker written under one
/// word and read under another always says it does not know when the run began, and a report
/// with no start time dates the crash by the moment it was found out, which is the next run
/// starting up. Nothing would say so.
/// </remarks>
public class RunMarkerTests
{
    private readonly IRunMarker _marks = new RunMarker();

    /// <summary>What is written is what is read back.</summary>
    [Fact]
    public void A_marker_reads_back_the_time_it_was_written_with()
    {
        var began = new DateTime(2026, 8, 27, 14, 30, 15);

        string marker = _marks.Compose(began, "1.2.3.4");

        Assert.Equal(began, _marks.StartedFrom(marker.Split('\n')));
    }

    /// <summary>The build is in there too, since a report from a version nobody can name says little.</summary>
    [Fact]
    public void The_marker_names_the_build()
    {
        Assert.Contains("1.2.3.4", _marks.Compose(DateTime.Now, "1.2.3.4"), StringComparison.Ordinal);
    }

    /// <summary>A build that will not say is written as a question mark rather than as nothing.</summary>
    [Fact]
    public void A_build_that_will_not_say_is_still_written()
    {
        foreach (string? said in new[] { null, "", "   " })
            Assert.Contains("version ?", _marks.Compose(DateTime.Now, said!), StringComparison.Ordinal);
    }

    /// <summary>Seconds survive, since a run that lasted four of them is worth knowing about.</summary>
    [Fact]
    public void The_time_survives_to_the_second()
    {
        var began = new DateTime(2026, 1, 2, 3, 4, 5);

        Assert.Equal(began, _marks.StartedFrom(_marks.Compose(began, "1").Split('\n')));
    }

    /// <summary>A marker saying nothing about a start answers with nothing rather than a guess.</summary>
    /// <remarks>
    /// A report that says it does not know when the run began is true; one dated by the moment
    /// the marker was found says the crash happened at startup, which sends somebody looking in
    /// the wrong place.
    /// </remarks>
    [Fact]
    public void A_marker_that_says_nothing_answers_with_nothing()
    {
        Assert.Null(_marks.StartedFrom(null!));
        Assert.Null(_marks.StartedFrom(Array.Empty<string>()));
        Assert.Null(_marks.StartedFrom(new[] { "version 1.0" }));
        Assert.Null(_marks.StartedFrom(new[] { "", "   ", "nonsense" }));
    }

    /// <summary>A start time that will not parse answers with nothing rather than throwing.</summary>
    [Fact]
    public void A_time_that_will_not_parse_answers_with_nothing()
    {
        Assert.Null(_marks.StartedFrom(new[] { "started yesterday" }));
        Assert.Null(_marks.StartedFrom(new[] { "started " }));
        Assert.Null(_marks.StartedFrom(new[] { "started 2026-13-45 99:99:99" }));
    }

    /// <summary>A null line among the rest is passed over rather than throwing.</summary>
    [Fact]
    public void A_missing_line_is_passed_over()
    {
        var began = new DateTime(2026, 5, 5, 5, 5, 5);

        Assert.Equal(began, _marks.StartedFrom(new[] { null!, _marks.Compose(began, "1").Split('\n')[0] }));
    }

    /// <summary>The time is read whatever order the lines arrive in.</summary>
    [Fact]
    public void The_order_of_the_lines_does_not_matter()
    {
        var began = new DateTime(2026, 3, 3, 3, 3, 3);
        string line = _marks.Compose(began, "1").Split('\n')[0];

        Assert.Equal(began, _marks.StartedFrom(new[] { "version 1", line }));
        Assert.Equal(began, _marks.StartedFrom(new[] { line, "version 1" }));
    }

    /// <summary>A crash from this run is reported, and one from before it is not.</summary>
    /// <remarks>
    /// The blocked list is kept across runs, so without the time a report would name every
    /// plugin that has ever fallen over rather than the one that just did.
    /// </remarks>
    [Fact]
    public void Only_this_runs_crashes_are_reported()
    {
        var began = new DateTime(2026, 8, 27, 12, 0, 0);

        var blocked = new List<PluginCrash>
        {
            Crash("last week", began.AddDays(-7)),
            Crash("just now", began.AddMinutes(3)),
            Crash("on the line", began)
        };

        var held = _marks.Since(blocked, began);

        Assert.Equal(2, held.Count);
        Assert.DoesNotContain(held, one => one.Name == "last week");
    }

    /// <summary>Nothing blocked is nothing to report, and no list at all does not throw.</summary>
    [Fact]
    public void Nothing_blocked_is_nothing_to_report()
    {
        Assert.Empty(_marks.Since(Array.Empty<PluginCrash>(), DateTime.Now));
        Assert.Empty(_marks.Since(null!, DateTime.Now));
    }

    /// <summary>A crash at the very instant the run began belongs to that run.</summary>
    /// <remarks>
    /// The boundary is inclusive on purpose: a plugin that takes the application down while it
    /// is still starting up is exactly the case this whole mechanism was written for, and it is
    /// the case where the two times are closest together.
    /// </remarks>
    [Fact]
    public void A_crash_on_the_boundary_belongs_to_the_run()
    {
        var began = new DateTime(2026, 8, 27, 12, 0, 0);

        Assert.Single(_marks.Since(new[] { Crash("at the start", began) }, began));
        Assert.Empty(_marks.Since(new[] { Crash("a tick before", began.AddTicks(-1)) }, began));
    }

    private static PluginCrash Crash(string name, DateTime when) =>
        new() { Name = name, Path = "/tmp/" + name, Stage = PluginStage.Load, When = when };
}
