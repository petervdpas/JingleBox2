using JingleBox2.Rack.Controls;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Where a Menu draws a divider: between its parts, and nowhere else.
/// </summary>
public class MenuSectionsTests
{
    private readonly MenuLines _lines = new();

    /// <summary>A line with no part of its own is in its option's part.</summary>
    [Fact]
    public void A_line_is_in_its_options_part_unless_it_says_otherwise()
    {
        Assert.Equal(MenuOptionWords.Help, new PanelMenuItem("Help") { Option = MenuOptionWords.Help }.Section);
        Assert.Equal(MenuOptionWords.Surfaces,
            new PanelMenuItem("Learn a control") { Option = MenuOptionWords.Learn, Section = MenuOptionWords.Surfaces }.Section);
        Assert.Equal("", new PanelMenuItem("Open in a window").Section);
    }

    /// <summary>
    /// Help, the presets and the hardware are three parts, and learning sits with the controllers.
    /// </summary>
    [Fact]
    public void Dividers_fall_between_the_parts()
    {
        var help = new PanelMenuItem("Help") { Option = MenuOptionWords.Help };
        var save = new PanelMenuItem("Save as preset...") { Option = MenuOptionWords.Presets };
        var delete = new PanelMenuItem("Delete this preset") { Option = MenuOptionWords.Presets };
        var desk = new PanelMenuItem("MiniLab 3") { Option = MenuOptionWords.Surfaces };
        var learn = new PanelMenuItem("Learn a control") { Option = MenuOptionWords.Learn, Section = MenuOptionWords.Surfaces };

        Assert.False(_lines.Divides(null, help));
        Assert.True(_lines.Divides(help, save));
        Assert.False(_lines.Divides(save, delete));
        Assert.True(_lines.Divides(delete, desk));
        Assert.False(_lines.Divides(desk, learn));
        Assert.True(_lines.Divides(help, learn));
    }
}
