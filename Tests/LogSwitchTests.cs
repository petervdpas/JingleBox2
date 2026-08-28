using System;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Diagnostics.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which areas the log writes, and what each one is called.
/// </summary>
/// <remarks>
/// The environment variable is the half that has to be got right blind: it exists for the run
/// that will not start far enough to reach its settings, so a build where it is misread is a
/// build nobody can get an account out of, and there is nowhere for it to say so.
/// </remarks>
public class LogSwitchTests
{
    private readonly ILogAreas _areas = new LogAreas();

    /// <summary>Not set at all, and set to nothing, both ask for nothing.</summary>
    [Fact]
    public void A_variable_that_says_nothing_asks_for_nothing()
    {
        Assert.Equal(LogArea.None, _areas.Asked(null));
        Assert.Equal(LogArea.None, _areas.Asked(""));
        Assert.Equal(LogArea.None, _areas.Asked("   "));
        Assert.Equal(LogArea.None, _areas.Asked("\t\n"));
    }

    /// <summary>"1" and "all" are everything, for the hands that have been typing one of them.</summary>
    [Fact]
    public void One_and_all_are_everything()
    {
        Assert.Equal(LogArea.Everything, _areas.Asked("1"));
        Assert.Equal(LogArea.Everything, _areas.Asked("all"));
        Assert.Equal(LogArea.Everything, _areas.Asked("ALL"));
        Assert.Equal(LogArea.Everything, _areas.Asked("  all  "));
    }

    /// <summary>"0" asks for nothing, which is not the same as not asking.</summary>
    /// <remarks>
    /// Both come back as no areas, and what differs is what the setting is then allowed to do:
    /// a variable asking for nothing steps aside and the setting decides, which is what makes
    /// "0" mean "never mind me" rather than "be quiet".
    /// </remarks>
    [Fact]
    public void Nought_asks_for_nothing()
    {
        Assert.Equal(LogArea.None, _areas.Asked("0"));
        Assert.Equal(LogArea.Midi, _areas.Wanted(true, LogArea.Midi, "0"));
    }

    /// <summary>Each area can be had by name, on its own.</summary>
    [Fact]
    public void An_area_can_be_asked_for_by_name()
    {
        Assert.Equal(LogArea.App, _areas.Asked("app"));
        Assert.Equal(LogArea.Audio, _areas.Asked("audio"));
        Assert.Equal(LogArea.Plugins, _areas.Asked("plugin"));
        Assert.Equal(LogArea.Tracker, _areas.Asked("tracker"));
        Assert.Equal(LogArea.Midi, _areas.Asked("midi"));
        Assert.Equal(LogArea.Machines, _areas.Asked("machines"));
    }

    /// <summary>Every name in the table works, whichever way it is typed.</summary>
    /// <remarks>
    /// Walked rather than listed, so an area added to the table is covered by this without
    /// anybody remembering to come back here. That is the whole point of the table being one
    /// list for three jobs.
    /// </remarks>
    [Fact]
    public void Every_name_in_the_table_is_understood()
    {
        foreach (var (area, called) in _areas.Everywhere)
        {
            Assert.Equal(area, _areas.Asked(called));
            Assert.Equal(area, _areas.Asked(called.ToUpperInvariant()));
            Assert.Equal(area, _areas.Asked("  " + called + "  "));
            Assert.Equal(called, _areas.Short(area));
        }
    }

    /// <summary>Several areas at once, separated by any of the three separators.</summary>
    [Fact]
    public void Areas_can_be_listed_three_ways()
    {
        var both = LogArea.Midi | LogArea.Plugins;

        Assert.Equal(both, _areas.Asked("midi,plugin"));
        Assert.Equal(both, _areas.Asked("midi plugin"));
        Assert.Equal(both, _areas.Asked("midi;plugin"));
        Assert.Equal(both, _areas.Asked("midi, plugin"));
        Assert.Equal(both, _areas.Asked(",midi,,plugin,"));
    }

    /// <summary>A name this build does not know is passed over, and the rest still count.</summary>
    /// <remarks>
    /// Deliberate. A variable left set from a later version of the application still starts
    /// this one, and still asks for whatever it named that this build does have.
    /// </remarks>
    [Fact]
    public void A_name_nobody_knows_is_passed_over()
    {
        Assert.Equal(LogArea.None, _areas.Asked("quantum"));
        Assert.Equal(LogArea.Midi, _areas.Asked("quantum,midi"));
        Assert.Equal(LogArea.Midi | LogArea.App, _areas.Asked("midi,nonsense,app"));
    }

    /// <summary>The same area twice is that area once, since it is a set of flags.</summary>
    [Fact]
    public void The_same_area_twice_is_that_area()
    {
        Assert.Equal(LogArea.Midi, _areas.Asked("midi,midi,MIDI"));
    }

    /// <summary>An area nobody has named reads as the plain word rather than throwing.</summary>
    [Fact]
    public void An_area_with_no_name_reads_plainly()
    {
        Assert.Equal("log", _areas.Short(LogArea.None));
        Assert.Equal("log", _areas.Short(LogArea.Everything));
        Assert.Equal("log", _areas.Short(LogArea.Midi | LogArea.App));
    }

    /// <summary>The variable beats the setting, on and off.</summary>
    [Fact]
    public void The_variable_beats_the_setting()
    {
        Assert.Equal(LogArea.Midi, _areas.Wanted(false, LogArea.Everything, "midi"));
        Assert.Equal(LogArea.Midi, _areas.Wanted(true, LogArea.Everything, "midi"));
    }

    /// <summary>Without a variable, the setting decides both whether and which.</summary>
    [Fact]
    public void Without_a_variable_the_setting_decides()
    {
        Assert.Equal(LogArea.None, _areas.Wanted(false, LogArea.Everything, null));
        Assert.Equal(LogArea.Everything, _areas.Wanted(true, LogArea.Everything, null));
        Assert.Equal(LogArea.Tracker, _areas.Wanted(true, LogArea.Tracker, null));
        Assert.Equal(LogArea.None, _areas.Wanted(true, LogArea.None, null));
    }

    /// <summary>Every area in the enum has a name, which the settings page is built from.</summary>
    /// <remarks>
    /// An area with no name would be a switch missing from SETTINGS and a word that cannot be
    /// typed into the variable, and nothing would say so: the page is built from this table.
    /// </remarks>
    [Fact]
    public void Every_area_has_a_name()
    {
        foreach (LogArea area in Enum.GetValues<LogArea>())
        {
            if (area is LogArea.None or LogArea.Everything) continue;

            Assert.True(_areas.Everywhere.ContainsKey(area), $"{area} has no name");
        }
    }
}
