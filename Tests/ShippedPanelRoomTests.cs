using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.SoundDevices.SoundEffects;
using JingleBox2.SoundDevices.SoundMachines;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Every shipped panel gives each of its controls somewhere to stand.
/// </summary>
/// <remarks>
/// Where a control sits is written by hand in a device's own file, in two forms that are easy to
/// get wrong and impossible to see wrong from the file. A grid child says its row and column, and
/// one that says neither lands on the first cell: a control added to a grid without being told
/// where to go is drawn on top of whatever was already there, and what shows is the two of them
/// overlapping. A strip says its widths positionally, one number per child, so a control inserted
/// in the middle without its width being inserted with it takes its neighbour's width and every
/// control after it wears somebody else's.
///
/// Both have happened here, to the same control, a fortnight apart. Neither failed anything: the
/// panel drew, the parameter turned, and the only sign was a dropdown reading "1," with a knob's
/// caption printed through it.
/// </remarks>
public class ShippedPanelRoomTests
{
    /// <summary>The rack folder beside the program, found by walking up from the test's output.</summary>
    private static string Shipped(string world)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !Directory.Exists(Path.Combine(at.FullName, "rack", world))) at = at.Parent;

        return at is null ? "" : Path.Combine(at.FullName, "rack", world);
    }

    /// <summary>Every shipped panel, by the name of the device it belongs to.</summary>
    private static IEnumerable<(string Named, Panel Panel)> Panels()
    {
        foreach (string one in Directory.EnumerateDirectories(Shipped("machines")))
            if (SoundMachineProject.Open(one) is { } project)
                yield return (project.Name, project.Panel);

        foreach (string one in Directory.EnumerateDirectories(Shipped("effects")))
            if (SoundEffectProject.Open(one) is { } project)
                yield return (project.Name, project.Panel);
    }

    /// <summary>Everything on a panel, container and control alike.</summary>
    private static IEnumerable<PanelElement> Everything(PanelElement at)
    {
        yield return at;

        foreach (var child in at.Children)
            foreach (var inside in Everything(child))
                yield return inside;
    }

    /// <summary>A property read as a whole number, or the answer for one that is not there.</summary>
    private static int Number(PanelElement element, string named, int spare) =>
        element.Properties.TryGetValue(named, out string? text) && int.TryParse(text, out int value)
            ? value
            : spare;

    /// <summary>A property read as a measurement, or the answer for one that is not there.</summary>
    private static double Size(PanelElement element, string named, double spare) =>
        element.Properties.TryGetValue(named, out string? text)
        && double.TryParse(text, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out double value)
            ? value
            : spare;

    /// <summary>How many rows or columns a definition comes to.</summary>
    private static int Defined(PanelElement element, string named) =>
        element.Properties.TryGetValue(named, out string? text) && text.Length > 0
            ? text.Split(',', StringSplitOptions.RemoveEmptyEntries).Length
            : 0;

    /// <summary>The widths a strip declares, which are read one per child in order.</summary>
    private static int[] Widths(PanelElement strip) =>
        strip.Properties.TryGetValue("columns", out string? text) && text.Length > 0
            ? text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                  .Select(part => int.TryParse(part, out int cells) ? Math.Max(1, cells) : 1)
                  .ToArray()
            : Array.Empty<int>();

    /// <summary>
    /// No two things on a grid stand on the same cell.
    /// </summary>
    /// <remarks>
    /// Which is the whole of the first fault: a child with no row and no column of its own is at
    /// nought and nought, along with everything else that forgot to say.
    /// </remarks>
    [Fact]
    public void Nothing_on_a_grid_stands_on_anything_else()
    {
        foreach (var (named, panel) in Panels())
        {
            foreach (var grid in Everything(panel.Root).Where(one => one.Element == ElementKinds.Grid))
            {
                var taken = new Dictionary<(int Row, int Column), string>();

                foreach (var child in grid.Children)
                {
                    int row = Number(child, "row", 0);
                    int column = Number(child, "column", 0);
                    int span = Math.Max(1, Number(child, "span", 1));
                    string what = child.Parameter.Length > 0 ? child.Parameter : child.Element;

                    for (int on = column; on < column + span; on++)
                    {
                        Assert.False(taken.TryGetValue((row, on), out string? already),
                            named + ": " + what + " stands on " + already + " at row " + row
                            + ", column " + on);

                        taken[(row, on)] = what;
                    }
                }
            }
        }
    }

    /// <summary>And nothing stands outside the grid it is in.</summary>
    /// <remarks>
    /// A row added without the grid being told it has one is the same mistake seen from the other
    /// side, and draws the same way.
    /// </remarks>
    [Fact]
    public void Nothing_on_a_grid_stands_outside_it()
    {
        foreach (var (named, panel) in Panels())
        {
            foreach (var grid in Everything(panel.Root).Where(one => one.Element == ElementKinds.Grid))
            {
                int rows = Defined(grid, "rows");
                int columns = Defined(grid, "columns");

                foreach (var child in grid.Children)
                {
                    string what = child.Parameter.Length > 0 ? child.Parameter : child.Element;
                    int row = Number(child, "row", 0);
                    int column = Number(child, "column", 0);
                    int span = Math.Max(1, Number(child, "span", 1));

                    if (rows > 0)
                        Assert.True(row < rows,
                            named + ": " + what + " is on row " + row + " of a grid with " + rows);

                    if (columns > 0)
                        Assert.True(column + span <= columns,
                            named + ": " + what + " reaches column " + (column + span - 1)
                            + " of a grid with " + columns);
                }
            }
        }
    }

    /// <summary>
    /// A strip that declares its widths declares no more of them than it has children.
    /// </summary>
    /// <remarks>
    /// Fewer is allowed and means something: the extras take the last width, which is how a row
    /// of like things is written without counting them. More cannot mean anything, and is what a
    /// width appended to the end rather than inserted at the control looks like afterwards.
    /// </remarks>
    [Fact]
    public void A_strip_declares_no_more_widths_than_it_has_children()
    {
        foreach (var (named, panel) in Panels())
        {
            foreach (var strip in Everything(panel.Root).Where(one => one.Element == ElementKinds.Strip))
            {
                int widths = Widths(strip).Length;

                if (widths == 0) continue;

                Assert.True(widths <= strip.Children.Count,
                    named + ": a strip declares " + widths + " widths for "
                    + strip.Children.Count + " children");
            }
        }
    }

    /// <summary>
    /// Every dropdown is wide enough for the longest word in it.
    /// </summary>
    /// <remarks>
    /// The fault a width in the wrong place actually shows up as. A control given a lamp's width
    /// still works, still turns its parameter and still saves, and reads "1," where it should say
    /// "1/16T", so the only thing that ever says it is wrong is somebody looking at it.
    ///
    /// The room a word needs is estimated rather than measured, because measuring it means a font
    /// and a text layout and neither belongs in a test of a file on disc. Seven points a letter
    /// and a bit for the arrow is under what the panel's own font takes, so this fails on a
    /// control that is definitely too small rather than on one that is merely tight.
    /// </remarks>
    [Fact]
    public void Every_dropdown_has_room_for_its_longest_word()
    {
        foreach (var (named, panel) in Panels())
        {
            foreach (var strip in Everything(panel.Root).Where(one => one.Element == ElementKinds.Strip))
            {
                var widths = Widths(strip);
                double cell = Size(strip, "cell", 24);

                for (int at = 0; at < strip.Children.Count; at++)
                {
                    var child = strip.Children[at];

                    if (child.Element != ElementKinds.Choice) continue;

                    int cells = widths.Length > 0
                        ? (at < widths.Length ? widths[at] : widths[^1])
                        : Math.Max(1, Number(child, "span", 1));

                    int longest = (child.Properties.TryGetValue("options", out string? words) ? words : "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(one => one.Length)
                        .DefaultIfEmpty(0)
                        .Max();

                    double wanted = 7.0 * longest + 36.0;

                    Assert.True(cells * cell >= wanted,
                        named + ": " + child.Parameter + " is " + cells + " cells of " + cell
                        + " and wants " + wanted + " for its longest word");
                }
            }
        }
    }
}
