using JingleBox2.Midi;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The arithmetic relating a tracker line to a MIDI clock tick, in both directions.
/// </summary>
/// <remarks>
/// This is where a sync feature goes wrong, and it goes wrong quietly: a clock that is a fraction
/// of a tick out per line sounds right for a bar and is a semiquaver out by the end of a chorus,
/// which reads as somebody else's gear drifting rather than as arithmetic. So the settings that
/// do not divide evenly are the ones worth most of the tests here.
///
/// Twenty four ticks to the quarter note is the specification's number, so at four lines to the
/// beat a line is six ticks and at eight it is three, both whole. At five it is 4.8 and at seven
/// 3.43, and those are the cases a tick counter would break on.
/// </remarks>
public class MidiClockGridTests
{
    /// <summary>The seam under test.</summary>
    private readonly IMidiClockGrid _grid = new MidiClockGrid();

    /// <summary>Twenty four to the beat, which nothing is free to change.</summary>
    [Fact]
    public void There_are_twenty_four_ticks_to_a_beat()
    {
        Assert.Equal(24, _grid.PerBeat);
    }

    /// <summary>A tick at 120 to the minute is 20.833 ms, which is the number everything rests on.</summary>
    [Theory]
    [InlineData(120, 0.0208333)]
    [InlineData(60, 0.0416667)]
    [InlineData(240, 0.0104167)]
    [InlineData(140, 0.0178571)]
    public void A_tick_is_this_long(double bpm, double seconds)
    {
        Assert.Equal(seconds, _grid.TickSeconds(bpm), 6);
    }

    /// <summary>A tempo of nonsense is a floor rather than a division by nought.</summary>
    /// <remarks>
    /// Read on the thread that keeps time, so what is refused has to be refused by answering.
    /// </remarks>
    [Fact]
    public void Nonsense_tempo_still_answers_something_finite()
    {
        Assert.True(double.IsFinite(_grid.TickSeconds(0)));
        Assert.True(double.IsFinite(_grid.TickSeconds(-120)));
        Assert.True(double.IsFinite(_grid.TickSeconds(double.NaN)));
        Assert.True(double.IsFinite(_grid.TickSeconds(double.PositiveInfinity)));
    }

    /// <summary>Ticks fall due as their moment passes, and never a hair before.</summary>
    [Fact]
    public void Ticks_fall_due_once_their_moment_has_passed()
    {
        double tick = _grid.TickSeconds(120);

        Assert.Equal(0, _grid.DueBy(0, tick));
        Assert.Equal(0, _grid.DueBy(tick * 0.999, tick));
        Assert.Equal(1, _grid.DueBy(tick, tick));
        Assert.Equal(1, _grid.DueBy(tick * 1.999, tick));
        Assert.Equal(48, _grid.DueBy(1.0, tick));
    }

    /// <summary>
    /// A caller that overslept is told about every tick it missed, not just the next one.
    /// </summary>
    /// <remarks>
    /// What lets the sending end catch up. Told only about one, a thread that lost ten
    /// milliseconds would stay behind for the rest of the pass and the tempo would read as
    /// slightly slow at the other end for ever.
    /// </remarks>
    [Fact]
    public void Oversleeping_is_caught_up_rather_than_lost()
    {
        double tick = _grid.TickSeconds(120);

        Assert.Equal(5, _grid.DueBy(tick * 5.5, tick));
    }

    /// <summary>Nonsense answers nought rather than throwing.</summary>
    [Fact]
    public void Nonsense_is_no_ticks()
    {
        Assert.Equal(0, _grid.DueBy(-1, 0.02));
        Assert.Equal(0, _grid.DueBy(1, 0));
        Assert.Equal(0, _grid.DueBy(1, -0.02));
        Assert.Equal(0, _grid.DueBy(double.NaN, 0.02));
        Assert.Equal(0, _grid.DueBy(1, double.NaN));
    }

    /// <summary>Where a line begins, at the settings that come out whole.</summary>
    [Theory]
    [InlineData(4, 1, 6)]
    [InlineData(4, 4, 24)]
    [InlineData(8, 1, 3)]
    [InlineData(8, 8, 24)]
    [InlineData(6, 1, 4)]
    [InlineData(3, 1, 8)]
    public void A_line_begins_on_this_tick(int linesPerBeat, int line, double tick)
    {
        Assert.Equal(tick, _grid.TickOfLine(line, linesPerBeat), 6);
    }

    /// <summary>
    /// And at the settings that do not, where it lands between two ticks rather than on one.
    /// </summary>
    /// <remarks>
    /// The case the whole design turns on. Five lines to the beat puts a line every 4.8 ticks, so
    /// no line but the fifth begins on a tick at all, and anything that rounded here would gain
    /// or lose a fifth of a tick every line.
    /// </remarks>
    [Theory]
    [InlineData(5, 1, 4.8)]
    [InlineData(5, 2, 9.6)]
    [InlineData(5, 5, 24)]
    [InlineData(7, 1, 24.0 / 7)]
    [InlineData(7, 7, 24)]
    public void A_line_can_begin_between_two_ticks(int linesPerBeat, int line, double tick)
    {
        Assert.Equal(tick, _grid.TickOfLine(line, linesPerBeat), 6);
    }

    /// <summary>
    /// A hundred beats of lines land exactly on a hundred beats of ticks, at every setting.
    /// </summary>
    /// <remarks>
    /// **The drift test, and the reason none of this counts ticks per line.** Whatever the lines
    /// to the beat, the line that begins the hundredth beat has to begin on tick 2400 to the
    /// last decimal place: a fraction lost per line would show here as several ticks, which is
    /// the audible fault this arithmetic exists to prevent.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(16)]
    public void A_hundred_beats_of_lines_is_a_hundred_beats_of_ticks(int linesPerBeat)
    {
        Assert.Equal(2400, _grid.TickOfLine(linesPerBeat * 100, linesPerBeat), 6);
    }

    /// <summary>Reading a tick count back as a line agrees with where the line began.</summary>
    /// <remarks>
    /// The two directions against each other rather than each against a number typed here, which
    /// is what stops them drifting apart when either is touched.
    /// </remarks>
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void A_line_read_back_from_its_own_tick_is_itself(int linesPerBeat)
    {
        for (int line = 0; line < 200; line++)
        {
            long ticks = (long)System.Math.Ceiling(_grid.TickOfLine(line, linesPerBeat));

            Assert.Equal(line, _grid.LineAtTick(ticks, linesPerBeat));
        }
    }

    /// <summary>A position pointer counts sixteenth notes, which is six ticks.</summary>
    /// <remarks>
    /// **A quarter note is four sixteenths, not one**, which is what this table got wrong the
    /// first time it was written: one beat was put down as pointer 1. So at four lines to the
    /// beat a line is a sixteenth exactly and the two numbers agree, which is a coincidence of
    /// that setting rather than the rule, and the rows at two and eight to the beat are here to
    /// stop it reading as one.
    /// </remarks>
    [Theory]
    [InlineData(4, 0, 0)]
    [InlineData(4, 4, 4)]
    [InlineData(4, 16, 16)]
    [InlineData(4, 64, 64)]
    [InlineData(8, 8, 4)]
    [InlineData(2, 2, 4)]
    [InlineData(1, 1, 4)]
    [InlineData(16, 16, 4)]
    public void A_pointer_counts_sixteenths(int linesPerBeat, int line, int pointer)
    {
        Assert.Equal(pointer, _grid.PointerFor(line, linesPerBeat));
    }

    /// <summary>The top of a song is the top, whatever the setting.</summary>
    [Fact]
    public void The_top_of_the_song_is_pointer_nought()
    {
        Assert.Equal(0, _grid.PointerFor(0, 4));
        Assert.Equal(0, _grid.PointerFor(-1, 4));
        Assert.Equal(0, _grid.LineAtPointer(0, 4));
        Assert.Equal(0, _grid.LineAtPointer(-1, 4));
    }

    /// <summary>
    /// A pointer never overflows the fourteen bits the message carries it in.
    /// </summary>
    /// <remarks>
    /// The message holds it as two seven-bit halves, so a song long enough to pass 16383
    /// sixteenths would wrap round to its own beginning and start every slave in the wrong place.
    /// Held instead, which is wrong by a bounded amount rather than wrong by a whole song.
    /// </remarks>
    [Fact]
    public void A_pointer_is_held_inside_fourteen_bits()
    {
        Assert.Equal(16383, _grid.PointerFor(int.MaxValue, 4));
        Assert.Equal(16383, _grid.PointerFor(1000000, 1));
    }

    /// <summary>A pointer sent and read back is the line it was made from, where it can be.</summary>
    /// <remarks>
    /// Only where a line is a whole number of sixteenths: at four lines to the beat a line is a
    /// sixteenth exactly, so the trip is lossless. At five it is not, and the nearer line is
    /// meant, which is what the other test below says.
    /// </remarks>
    [Fact]
    public void A_pointer_round_trips_where_a_line_is_a_sixteenth()
    {
        for (int line = 0; line < 400; line += 4)
            Assert.Equal(line, _grid.LineAtPointer(_grid.PointerFor(line, 4), 4));
    }

    /// <summary>And where it cannot be, it lands within a line rather than anywhere.</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(3)]
    public void A_pointer_lands_within_a_line_where_it_cannot_be_exact(int linesPerBeat)
    {
        for (int line = 0; line < 200; line++)
        {
            int back = _grid.LineAtPointer(_grid.PointerFor(line, linesPerBeat), linesPerBeat);

            Assert.InRange(back, line - linesPerBeat, line);
        }
    }
}
