using JingleBox2.Rack.SoundDevices.Timing;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A length of musical time, which is what lets a wobble or a delay be in time with the song.
/// </summary>
/// <remarks>
/// One control with a Free position at one end, so every device that can do this does it the same
/// way and a preset written for one reads the same as a preset written for another.
/// </remarks>
public class DivisionTests
{
    /// <summary>A hundred and twenty, where a beat is half a second and the sums are readable.</summary>
    private static readonly Transport Steady = new(true, 120.0, 0.0);

    /// <summary>Free is not a length, and it is where a knob that has never been touched sits.</summary>
    [Fact]
    public void Free_is_not_a_length()
    {
        Assert.False(Division.Synced(Division.Free));
        Assert.Equal(0, Division.Free);
        Assert.Equal(0.0, Division.BeatsIn(Division.Free));
    }

    /// <summary>And on Free the knob beside it is the whole of the answer.</summary>
    [Theory]
    [InlineData(3.75)]
    [InlineData(0.0)]
    public void On_free_the_knob_decides(double free)
    {
        Assert.Equal(free, Division.HertzIn(Division.Free, Steady, free));
        Assert.Equal(free, Division.SecondsIn(Division.Free, Steady, free));
    }

    /// <summary>A position nobody has heard of is Free rather than something worse.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void A_length_that_is_not_there_is_free(int at)
    {
        Assert.False(Division.Synced(at));
        Assert.Equal(7.0, Division.HertzIn(at, Steady, 7.0));
    }

    /// <summary>The lengths are the lengths, counted in quarter notes.</summary>
    [Theory]
    [InlineData(1, 4.0)]
    [InlineData(2, 2.0)]
    [InlineData(3, 1.0)]
    [InlineData(4, 0.5)]
    [InlineData(6, 0.25)]
    [InlineData(8, 0.125)]
    public void The_lengths_are_what_they_say(int at, double beats)
    {
        Assert.Equal(beats, Division.BeatsIn(at), 9);
    }

    /// <summary>A triplet is two thirds of the length it is named after.</summary>
    [Fact]
    public void A_triplet_is_two_thirds_of_its_name()
    {
        Assert.Equal(Division.BeatsIn(4) * 2.0 / 3.0, Division.BeatsIn(5), 9);
        Assert.Equal(Division.BeatsIn(6) * 2.0 / 3.0, Division.BeatsIn(7), 9);
    }

    /// <summary>At a hundred and twenty a quarter note is half a second, and so twice a second.</summary>
    [Fact]
    public void A_quarter_at_a_hundred_and_twenty_is_twice_a_second()
    {
        Assert.Equal(0.5, Division.SecondsIn(3, Steady, 0.0), 9);
        Assert.Equal(2.0, Division.HertzIn(3, Steady, 0.0), 9);
    }

    /// <summary>Twice the tempo is half the time and twice the rate.</summary>
    [Fact]
    public void Twice_the_tempo_is_half_the_time()
    {
        var quick = new Transport(true, 240.0, 0.0);

        Assert.Equal(Division.SecondsIn(3, Steady, 0.0) / 2.0, Division.SecondsIn(3, quick, 0.0), 9);
        Assert.Equal(Division.HertzIn(3, Steady, 0.0) * 2.0, Division.HertzIn(3, quick, 0.0), 9);
    }

    /// <summary>Every length has a name for a panel to put on a switch.</summary>
    [Fact]
    public void Every_length_is_named()
    {
        Assert.Equal("Free", Division.Names[Division.Free]);
        Assert.Equal(Division.Most + 1, Division.Names.Length);

        for (int at = 0; at <= Division.Most; at++) Assert.False(string.IsNullOrWhiteSpace(Division.Names[at]));
    }

    /// <summary>
    /// The names and the lengths are in the same order, which is what a saved preset relies on.
    /// </summary>
    /// <remarks>
    /// A preset writes down the number and nothing else. Reordering these would silently turn
    /// every eighth note somebody had set into something else.
    /// </remarks>
    [Fact]
    public void The_order_is_the_order_a_preset_wrote_down()
    {
        Assert.Equal("1/1", Division.Names[1]);
        Assert.Equal("1/2", Division.Names[2]);
        Assert.Equal("1/4", Division.Names[3]);
        Assert.Equal("1/8", Division.Names[4]);
        Assert.Equal("1/8T", Division.Names[5]);
        Assert.Equal("1/16", Division.Names[6]);
        Assert.Equal("1/16T", Division.Names[7]);
        Assert.Equal("1/32", Division.Names[8]);
    }
}
