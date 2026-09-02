using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using JingleBox2.Machines;
using JingleBox2.Machines.Interfaces;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The two parts a machine carries for this program rather than for itself.
/// </summary>
/// <remarks>
/// A Menu, which is what your controllers do to the machine, and an InstrumentName, which is what
/// the thing on the panel is called in the song. Neither is the machine's own, and both are parts
/// rather than something drawn over the panel from code, because nothing is added to a machine's
/// face from code: the machine asks for them and says where they go.
///
/// The machines that ship are read off the disc rather than described here, since what is worth
/// checking is the files people actually get.
/// </remarks>
public class MachinePartsTests
{
    /// <summary>Where the machines that ship are, beside the program.</summary>
    /// <remarks>
    /// The test run's own output folder, which the build copies them into. Read rather than
    /// listed, so a machine added later is checked without anybody remembering to add it here.
    /// </remarks>
    private static IEnumerable<(string Name, MachinePanel Panel)> Shipped()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "machines");

        Assert.True(Directory.Exists(folder), "the machines that ship are not beside the program");

        foreach (string each in Directory.GetDirectories(folder).OrderBy(one => one, StringComparer.Ordinal))
        {
            string file = Path.Combine(each, "machine.json");

            if (!File.Exists(file)) continue;

            using var stream = File.OpenRead(file);

            var read = JsonSerializer.Deserialize<Described>(stream, Layout);

            if (read?.Panel is { } panel) yield return (Path.GetFileName(each), panel);
        }
    }

    /// <summary>Only the half of a machine's file this is about.</summary>
    /// <param name="Panel">What it looks like.</param>
    private sealed record Described(MachinePanel? Panel);

    /// <summary>How a machine's file is written, which is how it has to be read.</summary>
    private static readonly JsonSerializerOptions Layout = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Every element in that panel, however deep.</summary>
    /// <param name="element">Where to start.</param>
    private static IEnumerable<MachineElement> Below(MachineElement element)
    {
        yield return element;

        foreach (var child in element.Children)
            foreach (var one in Below(child))
                yield return one;
    }

    /// <summary>Every machine that ships carries a badge saying what it is called in the song.</summary>
    /// <remarks>
    /// It used to be drawn over every panel from code, which is exactly what a machine's face
    /// may not have done to it. Now it is asked for, so a machine that stops asking loses it,
    /// and this says the ones we ship have not stopped by accident.
    /// </remarks>
    [Fact]
    public void Every_machine_that_ships_carries_a_name_badge()
    {
        var seen = 0;

        foreach (var (name, panel) in Shipped())
        {
            seen++;

            Assert.True(
                Below(panel.Root).Any(one => one.Element == MachineElementKinds.InstrumentName),
                name + " has no InstrumentName on its face");
        }

        Assert.Equal(5, seen);
    }

    /// <summary>And one of it, since two would say the same name twice.</summary>
    [Fact]
    public void No_machine_that_ships_carries_two_of_them()
    {
        foreach (var (name, panel) in Shipped())
        {
            Assert.Equal(1, Below(panel.Root).Count(one => one.Element == MachineElementKinds.InstrumentName));

            Assert.True(
                Below(panel.Root).Count(one => one.Element == MachineElementKinds.Menu) <= 1,
                name + " has more than one Menu on its face");
        }
    }

    /// <summary>Neither part turns a parameter, and neither ever will.</summary>
    /// <remarks>
    /// What they show is not a setting: a name belongs to the song and what a controller is
    /// pointed at belongs to the room. A machine naming a parameter on one of them would be
    /// saying something that cannot be true, and the shipped files are where that would first
    /// show up.
    /// </remarks>
    [Fact]
    public void Neither_part_turns_a_parameter()
    {
        foreach (var (name, panel) in Shipped())
            foreach (var one in Below(panel.Root))
            {
                if (one.Element is not (MachineElementKinds.InstrumentName or MachineElementKinds.Menu)) continue;

                Assert.True(one.Parameter.Length == 0, name + " points " + one.Element + " at a parameter");
            }
    }

    /// <summary>Both are in the designer's library, or a machine could not be given one.</summary>
    [Fact]
    public void Both_parts_can_be_dropped_on_a_panel()
    {
        var library = new MachineEditorViewModel().Library;

        Assert.Contains(MachineElementKinds.Menu, library);
        Assert.Contains(MachineElementKinds.InstrumentName, library);
    }

    /// <summary>
    /// The stand-in the designer shows says a name and refuses to be renamed.
    /// </summary>
    /// <remarks>
    /// A badge laid out around an empty one is laid out around the wrong width, and there is no
    /// instrument on the bench to rename, so it says the same thing however hard anybody types.
    /// </remarks>
    [Fact]
    public void The_designers_stand_in_name_says_something_and_cannot_be_renamed()
    {
        IInstrumentName preview = new MachinePreviewName();

        Assert.NotEqual("", preview.Said);
        Assert.True(preview.Fixed);
    }

    /// <summary>The word the file uses is the word the code uses, spelled exactly.</summary>
    /// <remarks>
    /// A machine names its parts in strings, so a rename in code that misses the shipped files
    /// leaves five machines describing a part nothing draws, and nothing would say so: an
    /// element of an unknown kind draws nothing and carries on.
    /// </remarks>
    [Fact]
    public void The_shipped_files_name_the_parts_the_way_the_code_spells_them()
    {
        Assert.Equal("InstrumentName", MachineElementKinds.InstrumentName);
        Assert.Equal("Menu", MachineElementKinds.Menu);
    }
}
