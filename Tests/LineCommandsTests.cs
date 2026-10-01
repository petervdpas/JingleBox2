using System.Linq;
using JingleBox2.Tracker.Commands;
using JingleBox2.Tracker.Commands.Interfaces;
using JingleBox2.Tracker.Commands.Records;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The pattern commands that work inside a line: where each thing a line asks for lands among its
/// twelve ticks, and what delay, cut, retrigger and arpeggio add.
/// </summary>
/// <remarks>
/// Asked of the module on its own, with no player, clock or sound, since what a line comes to is
/// arithmetic. Most of these are the ways a command can be handed nonsense, because a value in a
/// cell is whatever somebody typed.
/// </remarks>
public class LineCommandsTests
{
    /// <summary>The module, with its four commands and twelve ticks.</summary>
    private readonly ILineCommands _commands = new LineCommands();

    /// <summary>A note starting on track nought, column nought, carrying the command given.</summary>
    private static TrackerEvent Note(char letter, int parameter, int semitone = 48, float gain = 1f) =>
        new(0, 0, TrackerEventKind.Trigger, new Note(semitone), 0, gain, new TrackerCommand(letter, parameter));

    /// <summary>A line with no note in it on track nought, carrying the command given.</summary>
    private static TrackerEvent Bare(char letter, int parameter) =>
        new(0, 0, TrackerEventKind.Adjust, Tracker.Records.Note.Empty, 0, 1f, new TrackerCommand(letter, parameter));

    /// <summary>A plain note with no command.</summary>
    private static TrackerEvent Plain(int track = 0, int semitone = 48) =>
        new(track, 0, TrackerEventKind.Trigger, new Note(semitone), 0, 1f, TrackerCommand.None);

    /// <summary>The ticks of one line.</summary>
    private System.Collections.Generic.IReadOnlyList<TickEvent> Line(params TrackerEvent[] events) => _commands.Ticks(events);

    /// <summary>A line is twelve ticks.</summary>
    [Fact]
    public void A_line_is_twelve_ticks() => Assert.Equal(12, _commands.TicksPerLine);

    /// <summary>A line with no command is its own events, all on tick nought, in the order written.</summary>
    [Fact]
    public void A_line_without_commands_is_unchanged()
    {
        var line = new[] { Plain(0), Plain(1, 50), TrackerEvent.Stop(2) };

        var ticks = Line(line);

        Assert.Equal(line, ticks.Select(one => one.Event));
        Assert.All(ticks, one => Assert.Equal(0, one.Tick));
    }

    /// <summary>A command this module does not know is left on tick nought, as written.</summary>
    [Theory]
    [InlineData('V', 0x40)]
    [InlineData('P', 0x10)]
    [InlineData('Z', 0xFF)]
    public void A_command_it_does_not_know_is_left_alone(char letter, int parameter)
    {
        var note = Note(letter, parameter);

        Assert.Equal(new TickEvent(0, note), Assert.Single(Line(note)));
    }

    /// <summary>A delay moves the note to its tick.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 6)]
    [InlineData(11, 11)]
    public void A_delay_moves_the_note(int parameter, int tick)
    {
        var note = Note('Q', parameter);

        Assert.Equal(new TickEvent(tick, note), Assert.Single(Line(note)));
    }

    /// <summary>A delay past the end of the line is held to its last tick rather than leaking into the next.</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(0xFF)]
    public void A_delay_past_the_line_is_held_to_its_last_tick(int parameter)
    {
        Assert.Equal(11, Assert.Single(Line(Note('Q', parameter))).Tick);
    }

    /// <summary>A delayed note comes after the undelayed notes beside it.</summary>
    [Fact]
    public void A_delayed_note_comes_after_the_others()
    {
        var late = Note('Q', 3);
        var plain = Plain(1);

        var ticks = Line(late, plain);

        Assert.Equal(new[] { plain, late }, ticks.Select(one => one.Event));
    }

    /// <summary>A delayed OFF lets go late.</summary>
    [Fact]
    public void A_delayed_off_lets_go_late()
    {
        var off = TrackerEvent.Stop(0) with { Effect = new TrackerCommand('Q', 4) };

        var only = Assert.Single(Line(off));

        Assert.Equal(4, only.Tick);
        Assert.Equal(TrackerEventKind.Stop, only.Event.Kind);
    }

    /// <summary>A cut plays the note and silences its column at the tick.</summary>
    [Fact]
    public void A_cut_silences_the_column_at_its_tick()
    {
        var ticks = Line(Note('C', 3));

        Assert.Equal(2, ticks.Count);
        Assert.Equal((0, TrackerEventKind.Trigger), (ticks[0].Tick, ticks[0].Event.Kind));
        Assert.Equal((3, TrackerEventKind.Cut), (ticks[1].Tick, ticks[1].Event.Kind));
        Assert.Equal((0, 0), (ticks[1].Event.Track, ticks[1].Event.Column));
    }

    /// <summary>A cut at tick nought still comes after the note it cuts.</summary>
    [Fact]
    public void A_cut_at_nought_comes_after_its_note()
    {
        var ticks = Line(Note('C', 0));

        Assert.Equal(new[] { TrackerEventKind.Trigger, TrackerEventKind.Cut }, ticks.Select(one => one.Event.Kind));
    }

    /// <summary>A cut past the end of the line is no cut.</summary>
    [Theory]
    [InlineData(12)]
    [InlineData(0x80)]
    public void A_cut_past_the_line_is_no_cut(int parameter)
    {
        Assert.Single(Line(Note('C', parameter)));
    }

    /// <summary>A cut on a line with no note cuts what is ringing.</summary>
    [Fact]
    public void A_cut_on_a_bare_line_cuts_what_rings()
    {
        Line(Plain());

        var ticks = Line(Bare('C', 5));

        Assert.Contains(ticks, one => one.Tick == 5 && one.Event.Kind == TrackerEventKind.Cut);
    }

    /// <summary>A retrigger plays the note again every so many ticks from the start of the line.</summary>
    [Fact]
    public void A_retrigger_plays_again_every_so_many_ticks()
    {
        var ticks = Line(Note('R', 0x04));

        Assert.Equal(new[] { 0, 4, 8 }, ticks.Select(one => one.Tick));
        Assert.All(ticks, one => Assert.Equal(TrackerEventKind.Trigger, one.Event.Kind));
        Assert.All(ticks, one => Assert.Equal(48, one.Event.Note.Semitone));
        Assert.All(ticks, one => Assert.Equal(1f, one.Event.Gain));
    }

    /// <summary>The high digit makes each repeat quieter than the one before.</summary>
    [Fact]
    public void A_retrigger_falls_by_its_high_digit()
    {
        var ticks = Line(Note('R', 0x86, gain: 0.8f));

        Assert.Equal(new[] { 0.8f, 0.4f }, ticks.Select(one => one.Event.Gain!.Value));
    }

    /// <summary>A retrigger of nought ticks is the note once.</summary>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0xF0)]
    public void A_retrigger_of_nought_ticks_is_the_note_once(int parameter)
    {
        Assert.Single(Line(Note('R', parameter)));
    }

    /// <summary>A retrigger every tick plays on all twelve.</summary>
    [Fact]
    public void A_retrigger_every_tick_plays_on_all_twelve()
    {
        Assert.Equal(Enumerable.Range(0, 12), Line(Note('R', 0x01)).Select(one => one.Tick));
    }

    /// <summary>A retrigger on a line with no note plays the one still ringing again.</summary>
    [Fact]
    public void A_retrigger_on_a_bare_line_plays_what_rings()
    {
        Line(Plain(semitone: 55));

        var again = Line(Bare('R', 0x06)).Where(one => one.Event.Kind == TrackerEventKind.Trigger).ToArray();

        Assert.Equal(6, Assert.Single(again).Tick);
        Assert.Equal(55, again[0].Event.Note.Semitone);
    }

    /// <summary>After an OFF there is nothing ringing, so a retrigger plays nothing.</summary>
    [Fact]
    public void A_retrigger_after_an_off_plays_nothing()
    {
        Line(Plain());
        Line(TrackerEvent.Stop(0));

        Assert.DoesNotContain(Line(Bare('R', 0x03)), one => one.Event.Kind == TrackerEventKind.Trigger);
    }

    /// <summary>An arpeggio moves the pitch through its three steps a tick at a time, saying only the changes.</summary>
    [Fact]
    public void An_arpeggio_steps_through_its_three_notes()
    {
        var shifts = Line(Note('A', 0x47)).Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal(Enumerable.Range(1, 11), shifts.Select(one => one.Tick));
        Assert.Equal(new[] { 4f, 7, 0, 4, 7, 0, 4, 7, 0, 4, 7 }, shifts.Select(one => one.Event.Shift));
        Assert.All(shifts, one => Assert.Equal(48, one.Event.Note.Semitone));
    }

    /// <summary>An arpeggio with one step at nought only says the ticks where the pitch moves.</summary>
    [Fact]
    public void An_arpeggio_says_only_the_ticks_that_move()
    {
        var shifts = Line(Note('A', 0x07)).Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal(new[] { 2, 3, 5, 6, 8, 9, 11 }, shifts.Select(one => one.Tick));
        Assert.Equal(new[] { 7f, 0, 7, 0, 7, 0, 7 }, shifts.Select(one => one.Event.Shift));
    }

    /// <summary>A00 is nothing.</summary>
    [Fact]
    public void A00_is_nothing()
    {
        Assert.Single(Line(Note('A', 0x00)));
    }

    /// <summary>The line after an arpeggio puts the note back where it was played.</summary>
    [Fact]
    public void The_line_after_an_arpeggio_puts_the_note_back()
    {
        Line(Note('A', 0x37));

        var back = Assert.Single(Line());

        Assert.Equal(0, back.Tick);
        Assert.Equal((TrackerEventKind.Shift, 0f, 48), (back.Event.Kind, back.Event.Shift, back.Event.Note.Semitone));
    }

    /// <summary>A new note after an arpeggio is not preceded by the old one being put back.</summary>
    [Fact]
    public void A_new_note_after_an_arpeggio_needs_no_putting_back()
    {
        Line(Note('A', 0x37));

        var next = Plain(semitone: 60);

        Assert.Equal(new TickEvent(0, next), Assert.Single(Line(next)));
    }

    /// <summary>An arpeggio carried on over a bare line keeps going from the note still ringing.</summary>
    [Fact]
    public void An_arpeggio_carries_on_over_a_bare_line()
    {
        Line(Note('A', 0x37, semitone: 40));

        var shifts = Line(Bare('A', 0x37)).Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal((0, 0f), (shifts[0].Tick, shifts[0].Event.Shift));
        Assert.Equal((1, 3f), (shifts[1].Tick, shifts[1].Event.Shift));
        Assert.All(shifts, one => Assert.Equal(40, one.Event.Note.Semitone));
    }

    /// <summary>A column that ended at its own pitch is not put back.</summary>
    [Fact]
    public void A_column_that_ended_in_place_is_not_put_back()
    {
        Line(Note('A', 0x30));

        Assert.Empty(Line());
    }

    /// <summary>Forgetting drops what was ringing and what was off its pitch.</summary>
    [Fact]
    public void Reset_forgets_every_column()
    {
        Line(Note('A', 0x47));
        _commands.Reset();

        Assert.Empty(Line());
        Assert.Single(Line(Bare('R', 0x02)));
    }

    /// <summary>Two columns keep their own memory.</summary>
    [Fact]
    public void Columns_keep_their_own_memory()
    {
        var low = new TrackerEvent(0, 0, TrackerEventKind.Trigger, new Note(36), 0, 1f, TrackerCommand.None);
        var high = new TrackerEvent(0, 1, TrackerEventKind.Trigger, new Note(60), 0, 1f, TrackerCommand.None);
        Line(low, high);

        var bare = new TrackerEvent(0, 1, TrackerEventKind.Adjust, Tracker.Records.Note.Empty, 0, 1f, new TrackerCommand('R', 0x06));
        var again = Line(bare).Single(one => one.Event.Kind == TrackerEventKind.Trigger);

        Assert.Equal((1, 60), (again.Event.Column, again.Event.Note.Semitone));
    }

    /// <summary>A glide slides from the note sounding to the new one over its ticks, without starting it again.</summary>
    [Fact]
    public void A_glide_slides_without_starting_again()
    {
        Line(Plain(semitone: 48));

        var ticks = Line(Note('G', 0x04, semitone: 52));

        Assert.DoesNotContain(ticks, one => one.Event.Kind == TrackerEventKind.Trigger);
        Assert.Equal(TrackerEventKind.Adjust, ticks[0].Event.Kind);

        var shifts = ticks.Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal(new[] { 0, 1, 2, 3 }, shifts.Select(one => one.Tick));
        Assert.Equal(new[] { 1f, 2, 3, 4 }, shifts.Select(one => one.Event.Shift));
        Assert.All(shifts, one => Assert.Equal(48, one.Event.Note.Semitone));
    }

    /// <summary>A glide down goes down, in fractions where the ticks do not divide the interval.</summary>
    [Fact]
    public void A_glide_down_moves_in_fractions()
    {
        Line(Plain(semitone: 60));

        var shifts = Line(Note('G', 0x03, semitone: 58)).Where(one => one.Event.Kind == TrackerEventKind.Shift)
            .Select(one => one.Event.Shift).ToArray();

        Assert.Equal(-2f / 3, shifts[0], 4);
        Assert.Equal(-4f / 3, shifts[1], 4);
        Assert.Equal(-2f, shifts[2], 4);
    }

    /// <summary>G00 takes one whole line.</summary>
    [Fact]
    public void G00_takes_one_line()
    {
        Line(Plain(semitone: 48));

        var shifts = Line(Note('G', 0x00, semitone: 60)).Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal(12, shifts.Length);
        Assert.Equal(12f, shifts[^1].Event.Shift);
        Assert.Empty(Line());
    }

    /// <summary>A glide longer than a line goes on over the lines after it, and stops where it arrives.</summary>
    [Fact]
    public void A_long_glide_carries_over_lines()
    {
        Line(Plain(semitone: 48));
        Line(Note('G', 0x18, semitone: 72));

        var second = Line().Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal(12, second.Length);
        Assert.Equal(24f, second[^1].Event.Shift);
        Assert.Empty(Line());
    }

    /// <summary>A cell written into the column takes over from a glide still on its way.</summary>
    [Fact]
    public void A_new_cell_stops_a_glide_where_it_got_to()
    {
        Line(Plain(semitone: 48));
        Line(Note('G', 0x18, semitone: 72));

        var next = Line(Bare('R', 0x06));

        Assert.DoesNotContain(next, one => one.Event.Kind == TrackerEventKind.Shift);

        var again = next.Single(one => one.Event.Kind == TrackerEventKind.Trigger);

        Assert.Equal(60, again.Event.Note.Semitone);
    }

    /// <summary>With nothing sounding, a glide simply starts its note.</summary>
    [Fact]
    public void A_glide_with_nothing_sounding_starts_its_note()
    {
        var note = Note('G', 0x04);

        Assert.Equal(new TickEvent(0, note), Assert.Single(Line(note)));
    }

    /// <summary>After an OFF there is nothing to slide from.</summary>
    [Fact]
    public void A_glide_after_an_off_starts_its_note()
    {
        Line(Plain());
        Line(TrackerEvent.Stop(0));

        Assert.Equal(TrackerEventKind.Trigger, Assert.Single(Line(Note('G', 0x04, semitone: 55))).Event.Kind);
    }

    /// <summary>An arpeggio after a glide is built on where the glide arrived.</summary>
    [Fact]
    public void An_arpeggio_after_a_glide_steps_around_where_it_arrived()
    {
        Line(Plain(semitone: 48));
        Line(Note('G', 0x04, semitone: 52));

        var shifts = Line(Bare('A', 0x37)).Where(one => one.Event.Kind == TrackerEventKind.Shift).ToArray();

        Assert.Equal(new[] { 7f, 11, 4 }, shifts.Take(3).Select(one => one.Event.Shift));
        Assert.All(shifts, one => Assert.Equal(48, one.Event.Note.Semitone));
    }

    /// <summary>The line after an arpeggio on a glided note goes back to where the glide arrived, not to the note it started on.</summary>
    [Fact]
    public void After_an_arpeggio_on_a_glided_note_it_rests_where_the_glide_arrived()
    {
        Line(Plain(semitone: 48));
        Line(Note('G', 0x04, semitone: 52));
        Line(Bare('A', 0x37));

        var back = Assert.Single(Line());

        Assert.Equal(4f, back.Event.Shift);
    }

    /// <summary>A retrigger after a glide plays the note it is heard at.</summary>
    [Fact]
    public void A_retrigger_after_a_glide_plays_the_glided_note()
    {
        Line(Plain(semitone: 48));
        Line(Note('G', 0x04, semitone: 55));

        var again = Line(Bare('R', 0x06)).Single(one => one.Event.Kind == TrackerEventKind.Trigger);

        Assert.Equal(55, again.Event.Note.Semitone);
    }

    /// <summary>A command added from outside is answered by its letter, which is what makes the module a module.</summary>
    [Fact]
    public void A_command_added_from_outside_is_answered()
    {
        var commands = new LineCommands(new ITickCommand[] { new Late() });

        Assert.Equal(9, Assert.Single(commands.Ticks(new[] { Note('L', 0) })).Tick);
        Assert.Equal(0, Assert.Single(commands.Ticks(new[] { Note('Q', 5) })).Tick);
    }

    /// <summary>A command that always lands on tick nine.</summary>
    private sealed class Late : ITickCommand
    {
        /// <inheritdoc/>
        public char Letter => 'l';

        /// <inheritdoc/>
        public void Spread(TrackerEvent cell, VoiceState voice, int ticks, System.Collections.Generic.ICollection<TickEvent> into) =>
            into.Add(new TickEvent(9, cell));
    }
}
