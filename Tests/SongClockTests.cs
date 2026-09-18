using JingleBox2.Rack.SoundDevices.Timing;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The clock the plugins are told, which is the one thing that lets a plugin with an arpeggiator
/// or a delay in note lengths be in time with the song at all.
/// </summary>
/// <remarks>
/// These run one after another rather than together, because the clock is one thing for the
/// whole application and two tests moving it at once would each be reading the other's answer.
/// Xunit runs the tests in one class in sequence, which is what this relies on, and every test
/// here sets the clock before it reads it.
/// </remarks>
[Collection("plugin clock")]
public class SongClockTests
{
    /// <summary>Forty one thousand, which is a rate nothing else here would land on by accident.</summary>
    private const int Rate = 41000;

    /// <summary>
    /// Every test starts with nothing to check the clock against. A player made by another test
    /// leaves its reference behind, and the ones here that are about the count alone must not be
    /// put right by it.
    /// </summary>
    public SongClockTests() => SongClock.Follow(null);

    /// <summary>A block of 512 at the test rate, which is what the reconciling is measured in.</summary>
    private const int Block = 512;

    /// <summary>Nothing has been said yet, so nothing is playing and the tempo is a sane one.</summary>
    [Fact]
    public void A_clock_nobody_has_set_is_still()
    {
        SongClock.Set(Transport.Still);

        Assert.False(SongClock.Now.Playing);
        Assert.Equal(120.0, SongClock.Now.Bpm);
        Assert.Equal(0.0, SongClock.Now.Beats);
    }

    /// <summary>Starting is what puts the beat back to the top.</summary>
    [Fact]
    public void Starting_goes_back_to_the_top()
    {
        SongClock.Started(140.0);
        SongClock.Advance(Rate, Rate);

        Assert.True(SongClock.Now.Beats > 0);

        SongClock.Started(140.0);

        Assert.Equal(0.0, SongClock.Now.Beats);
        Assert.True(SongClock.Now.Playing);
    }

    /// <summary>
    /// A second at a hundred and twenty is two quarter notes, which is the whole of the
    /// arithmetic and the thing everything else rests on.
    /// </summary>
    [Fact]
    public void A_second_at_a_hundred_and_twenty_is_two_beats()
    {
        SongClock.Started(120.0);
        SongClock.Advance(Rate, Rate);

        Assert.Equal(2.0, SongClock.Now.Beats, 6);
    }

    /// <summary>And the same second at sixty is one, which is the tempo actually being read.</summary>
    [Fact]
    public void And_at_sixty_it_is_one()
    {
        SongClock.Started(60.0);
        SongClock.Advance(Rate, Rate);

        Assert.Equal(1.0, SongClock.Now.Beats, 6);
    }

    /// <summary>A block at a time adds up to the same as the whole second in one go.</summary>
    [Fact]
    public void Blocks_add_up()
    {
        SongClock.Started(120.0);

        for (int block = 0; block < 100; block++) SongClock.Advance(Rate / 100, Rate);

        Assert.Equal(2.0, SongClock.Now.Beats, 6);
    }

    /// <summary>A transport standing still does not move, however much audio goes by.</summary>
    /// <remarks>
    /// The mixer runs whether or not the song is playing, since a key pressed by hand still has
    /// to be heard. A clock that counted those blocks would have the song somewhere it never
    /// went.
    /// </remarks>
    [Fact]
    public void A_stopped_transport_does_not_move()
    {
        SongClock.Started(120.0);
        SongClock.Advance(Rate, Rate);
        SongClock.Stopped();
        SongClock.Advance(Rate * 10, Rate);

        Assert.Equal(2.0, SongClock.Now.Beats, 6);
    }

    /// <summary>Stopping leaves the beat where it was rather than winding it back.</summary>
    [Fact]
    public void Stopping_leaves_the_beat_where_it_was()
    {
        SongClock.Started(120.0);
        SongClock.Advance(Rate, Rate);
        SongClock.Stopped();

        Assert.False(SongClock.Now.Playing);
        Assert.Equal(2.0, SongClock.Now.Beats, 6);
    }

    /// <summary>The tempo can be turned under a transport that is already rolling.</summary>
    [Fact]
    public void The_tempo_can_be_turned_while_it_runs()
    {
        SongClock.Started(120.0);
        SongClock.Advance(Rate, Rate);

        SongClock.Tempo(240.0);
        SongClock.Advance(Rate, Rate);

        /* Two beats at a hundred and twenty, then four more at twice that. */
        Assert.Equal(6.0, SongClock.Now.Beats, 6);
        Assert.True(SongClock.Now.Playing);
    }

    /// <summary>A tempo nothing could run at is refused rather than taken.</summary>
    /// <remarks>
    /// A nought or a nonsense would divide the beat by nothing somewhere downstream, and the
    /// place to stop that is where it comes in rather than in every reader.
    /// </remarks>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-30.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_tempo_that_is_no_tempo_is_refused(double bpm)
    {
        SongClock.Started(bpm);

        Assert.Equal(120.0, SongClock.Now.Bpm);
    }

    /// <summary>The bar is the beats rounded down to the last bar line.</summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(3.5, 0.0)]
    [InlineData(4.0, 4.0)]
    [InlineData(9.25, 8.0)]
    public void The_bar_is_the_beats_rounded_down_to_it(double beats, double bar)
    {
        Assert.Equal(bar, new Transport(true, 120.0, beats).BarBeats);
    }

    /// <summary>And follows the time signature rather than assuming four.</summary>
    [Fact]
    public void The_bar_follows_the_time_signature()
    {
        Assert.Equal(3.0, new Transport(true, 120.0, 5.5, 3, 4).BarBeats);
        Assert.Equal(1.5, new Transport(true, 120.0, 2.0, 3, 8).BarBeats);
    }

    /// <summary>With nothing to check against, the beat is the samples and nothing else.</summary>
    [Fact]
    public void With_no_player_the_beat_is_the_samples()
    {
        SongClock.Started(120.0);
        SongClock.Advance(Rate, Rate);

        Assert.Equal(2.0, SongClock.Now.Beats, 9);
    }

    /// <summary>
    /// A player a little ahead is left alone, since its thread always is and never by the same
    /// amount twice; following it would hand every plugin that wobble.
    /// </summary>
    [Fact]
    public void A_player_a_little_ahead_is_left_alone()
    {
        SongClock.Started(120.0);

        double counted = Block / (double)Rate * 2.0;

        SongClock.Follow(_ => counted + 0.01);
        SongClock.Advance(Block, Rate);

        Assert.Equal(counted, SongClock.Now.Beats, 9);
    }

    /// <summary>
    /// A stall: the audio missed a sixth of a second and the player did not. The beat is put where
    /// the player is at once, rather than leaving every plugin that far behind the notes for good.
    /// </summary>
    [Fact]
    public void After_a_stall_the_beat_is_put_where_the_player_is()
    {
        SongClock.Started(120.0);

        double counted = Block / (double)Rate * 2.0;
        double player = counted + 0.35;

        SongClock.Follow(_ => player);
        SongClock.Advance(Block, Rate);

        Assert.Equal(player, SongClock.Now.Beats, 9);
    }

    /// <summary>
    /// Between the two, a drift is eased back a hundredth at a time, which is two clocks that do
    /// not quite agree and is never heard as a jump.
    /// </summary>
    [Fact]
    public void A_drift_is_eased_back()
    {
        SongClock.Started(120.0);

        double counted = Block / (double)Rate * 2.0;
        double player = counted + 0.1;

        SongClock.Follow(_ => player);
        SongClock.Advance(Block, Rate);

        Assert.Equal(counted + 0.1 * 0.01, SongClock.Now.Beats, 9);
    }

    /// <summary>A player that cannot say where it is changes nothing.</summary>
    [Fact]
    public void A_player_that_cannot_say_changes_nothing()
    {
        SongClock.Started(120.0);
        SongClock.Follow(_ => double.NaN);
        SongClock.Advance(Rate, Rate);

        Assert.Equal(2.0, SongClock.Now.Beats, 9);
    }

    /// <summary>And a stopped clock stays stopped, whatever the player says.</summary>
    [Fact]
    public void A_stopped_clock_is_not_moved_by_the_player()
    {
        SongClock.Started(120.0);
        SongClock.Advance(Rate, Rate);
        SongClock.Stopped();
        SongClock.Follow(_ => 50.0);
        SongClock.Advance(Rate, Rate);

        Assert.Equal(2.0, SongClock.Now.Beats, 9);
    }
}
