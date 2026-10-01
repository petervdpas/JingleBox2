using System.Linq;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Enums;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The command popup: a command chosen by name and set by its ruler, a chord or a slider, handing
/// back exactly what typing would have written.
/// </summary>
public class CommandEditorTests
{
    /// <summary>Opens the popup on a cell holding that command.</summary>
    private static CommandEditorViewModel On(char letter, int parameter) => new(new TrackerCommand(letter, parameter));

    /// <summary>The popup on the given command, switched to the given kind.</summary>
    private static CommandEditorViewModel Choosing(CommandKind kind)
    {
        var editor = new CommandEditorViewModel(TrackerCommand.None);
        editor.Chosen = editor.Choices.Single(one => one.Kind == kind);
        return editor;
    }

    /// <summary>Every command the popup can read, it hands back unchanged when nothing is touched.</summary>
    [Theory]
    [InlineData('Q', 0x06)]
    [InlineData('Q', 0x00)]
    [InlineData('C', 0x0B)]
    [InlineData('R', 0x84)]
    [InlineData('R', 0x01)]
    [InlineData('A', 0x37)]
    [InlineData('A', 0xC0)]
    [InlineData('V', 0x80)]
    [InlineData('V', 0x00)]
    [InlineData('P', 0x10)]
    [InlineData('G', 0x00)]
    [InlineData('G', 0x18)]
    [InlineData('S', 0x80)]
    [InlineData('S', 0xFF)]
    [InlineData('T', 0x5A)]
    [InlineData('T', 0x14)]
    public void Opening_and_closing_changes_nothing(char letter, int parameter)
    {
        Assert.Equal(new TrackerCommand(letter, parameter), On(letter, parameter).Command);
    }

    /// <summary>An empty cell opens on a delay of half a line.</summary>
    [Fact]
    public void An_empty_cell_opens_on_a_delay()
    {
        var editor = new CommandEditorViewModel(TrackerCommand.None);

        Assert.Equal(CommandKind.Delay, editor.Kind);
        Assert.Equal("Q06", editor.Written);
    }

    /// <summary>A command the popup does not offer opens it on a delay rather than losing its way.</summary>
    [Fact]
    public void A_command_it_does_not_offer_opens_on_a_delay()
    {
        Assert.Equal(CommandKind.Delay, On('Z', 0x12).Kind);
    }

    /// <summary>A delay past the line read back is held to the line's last tick.</summary>
    [Fact]
    public void A_delay_past_the_line_is_held()
    {
        Assert.Equal(new TrackerCommand('Q', 11), On('Q', 0xFF).Command);
    }

    /// <summary>A retrigger of nought ticks reads back as every tick, since nought is no retrigger at all.</summary>
    [Fact]
    public void A_retrigger_of_nought_reads_as_every_tick()
    {
        Assert.Equal(new TrackerCommand('R', 0x31), On('R', 0x30).Command);
    }

    /// <summary>Clicking a tick on the ruler is a delay's amount.</summary>
    [Fact]
    public void Clicking_a_tick_sets_the_delay()
    {
        var editor = Choosing(CommandKind.Delay);

        editor.PickTickCommand.Execute(3);

        Assert.Equal("Q03", editor.Written);
        Assert.Equal(new[] { 3 }, editor.Ruler.Where(mark => mark.On).Select(mark => mark.Index));
        Assert.Contains("a quarter of a line", editor.Explanation);
    }

    /// <summary>A tick off the ruler is held to it.</summary>
    [Theory]
    [InlineData(-4, 0)]
    [InlineData(40, 11)]
    public void A_tick_off_the_ruler_is_held(int clicked, int tick)
    {
        var editor = Choosing(CommandKind.Cut);

        editor.PickTickCommand.Execute(clicked);

        Assert.Equal(new TrackerCommand('C', tick), editor.Command);
    }

    /// <summary>Clicking the ruler does nothing for a command it only shows.</summary>
    [Fact]
    public void Clicking_does_nothing_for_a_retrigger()
    {
        var editor = Choosing(CommandKind.Retrigger);
        var before = editor.Command;

        editor.PickTickCommand.Execute(2);

        Assert.Equal(before, editor.Command);
    }

    /// <summary>The retrigger's ruler shows the ticks the note plays on, and its words count them.</summary>
    [Fact]
    public void The_retrigger_ruler_shows_its_hits()
    {
        var editor = Choosing(CommandKind.Retrigger);
        editor.Every = 4;
        editor.Fall = 8;

        Assert.Equal("R84", editor.Written);
        Assert.Equal(new[] { 0, 4, 8 }, editor.Ruler.Where(mark => mark.On).Select(mark => mark.Index));
        Assert.Contains("3 times", editor.Explanation);
        Assert.Contains("50%", editor.Explanation);
    }

    /// <summary>A named chord sets both intervals, and the ruler shows the steps.</summary>
    [Fact]
    public void A_named_chord_sets_the_arpeggio()
    {
        var editor = Choosing(CommandKind.Arpeggio);

        editor.PickChordCommand.Execute(editor.Chords.Single(chord => chord.Name == "Minor"));

        Assert.Equal("A37", editor.Written);
        Assert.Equal("Minor", editor.ChordName);
        Assert.Equal(new[] { "0", "+3", "+7", "0", "+3", "+7" }, editor.Ruler.Take(6).Select(mark => mark.Text));
    }

    /// <summary>Intervals that make no named chord have no name.</summary>
    [Fact]
    public void Odd_intervals_have_no_name()
    {
        var editor = Choosing(CommandKind.Arpeggio);
        editor.Up = 1;
        editor.Up2 = 9;

        Assert.Equal("", editor.ChordName);
        Assert.Equal("A19", editor.Written);
    }

    /// <summary>Sliders that hand back fractions or go past their ends are held to what a command can say.</summary>
    [Fact]
    public void Slider_values_are_held_to_what_fits()
    {
        var editor = Choosing(CommandKind.Arpeggio);
        editor.Up = 3.4;
        editor.Up2 = 99;

        Assert.Equal("A3F", editor.Written);

        editor.Chosen = editor.Choices.Single(one => one.Kind == CommandKind.Volume);
        editor.Level = -5;

        Assert.Equal("V00", editor.Written);
    }

    /// <summary>The pan says where the note sits.</summary>
    [Theory]
    [InlineData(0x40, "centre")]
    [InlineData(0x00, "100% to the left")]
    [InlineData(0x60, "50% to the right")]
    public void The_pan_says_where(int place, string words)
    {
        var editor = Choosing(CommandKind.Pan);
        editor.Place = place;

        Assert.Contains(words, editor.Explanation);
    }

    /// <summary>Volume and pan have nothing to show on the ruler.</summary>
    [Theory]
    [InlineData(CommandKind.Volume, false)]
    [InlineData(CommandKind.Pan, false)]
    [InlineData(CommandKind.Delay, true)]
    [InlineData(CommandKind.Arpeggio, true)]
    public void Only_timed_commands_show_the_ruler(CommandKind kind, bool shows)
    {
        Assert.Equal(shows, Choosing(kind).ShowsRuler);
    }

    /// <summary>A glide of nought takes one line, and the ruler lights the whole of it.</summary>
    [Fact]
    public void A_glide_of_nought_is_one_line()
    {
        var editor = Choosing(CommandKind.Glide);

        Assert.Equal("G00", editor.Written);
        Assert.All(editor.Ruler, mark => Assert.True(mark.On));
        Assert.Contains("one line", editor.Explanation);
    }

    /// <summary>A short glide lights its ticks and says how much of a line that is.</summary>
    [Fact]
    public void A_short_glide_lights_its_ticks()
    {
        var editor = Choosing(CommandKind.Glide);
        editor.Slide = 6;

        Assert.Equal("G06", editor.Written);
        Assert.Equal(Enumerable.Range(0, 6), editor.Ruler.Where(mark => mark.On).Select(mark => mark.Index));
        Assert.Contains("half a line", editor.Explanation);
    }

    /// <summary>A glide longer than a line says how many lines.</summary>
    [Fact]
    public void A_long_glide_says_its_lines()
    {
        var editor = Choosing(CommandKind.Glide);
        editor.Slide = 24;

        Assert.Contains("2 lines", editor.Explanation);
    }

    /// <summary>An offset says how far in, and has no ruler.</summary>
    [Fact]
    public void An_offset_says_how_far_in()
    {
        var editor = Choosing(CommandKind.Offset);
        editor.Skip = 0x40;

        Assert.Equal("S40", editor.Written);
        Assert.Contains("25%", editor.Explanation);
        Assert.False(editor.ShowsRuler);
    }

    /// <summary>A tempo below twenty read back is held to twenty, the slowest a song allows.</summary>
    [Fact]
    public void A_tempo_below_twenty_is_held()
    {
        Assert.Equal(new TrackerCommand('T', 0x14), On('T', 0x05).Command);
    }

    /// <summary>A tempo says its beats a minute and where it goes, and has no ruler.</summary>
    [Fact]
    public void A_tempo_says_its_beats_and_where_it_goes()
    {
        var editor = Choosing(CommandKind.Tempo);
        editor.Beats = 90;

        Assert.Equal("T5A", editor.Written);
        Assert.Contains("90 beats a minute", editor.Explanation);
        Assert.Contains("tempo lane", editor.Explanation);
        Assert.False(editor.ShowsRuler);
    }
}
