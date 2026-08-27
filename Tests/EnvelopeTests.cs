using JingleBox2.Machines.Ui;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// An ADSR as a curve over time, which is what the picture on a machine's panel is drawn from.
/// </summary>
public class EnvelopeTests
{
    private static EnvelopeShape Default =>
        EnvelopeShape.FromMilliseconds(attackMs: 2, decayMs: 40, sustain: 0.6, releaseMs: 80, holdSeconds: 0.4);

    [Fact]
    public void It_starts_at_nothing_and_climbs_through_the_attack()
    {
        var shape = Default;

        Assert.Equal(0, shape.LevelAt(0));
        Assert.Equal(0.5, shape.LevelAt(0.001), 3);
        Assert.Equal(1.0, shape.LevelAt(0.002), 3);
    }

    [Fact]
    public void Falls_through_the_decay_to_the_sustain()
    {
        var shape = Default;

        Assert.Equal(0.8, shape.LevelAt(0.022), 2);
        Assert.Equal(0.6, shape.LevelAt(0.042), 2);
    }

    [Fact]
    public void Holds_there_while_the_key_is_down()
    {
        var shape = Default;

        Assert.Equal(0.6, shape.LevelAt(0.2), 3);
        Assert.Equal(0.6, shape.LevelAt(0.4), 3);
    }

    [Fact]
    public void And_falls_away_after_it_is_let_go()
    {
        var shape = Default;

        Assert.Equal(0.442, shape.ReleaseStarts, 3);
        Assert.Equal(0.3, shape.LevelAt(0.482), 2);
        Assert.Equal(0, shape.LevelAt(shape.Length));
    }

    [Fact]
    public void A_patch_with_no_sustain_ends_on_its_decay()
    {
        // So the picture is as long as the sound, rather than trailing a flat line nobody hears.
        var shape = EnvelopeShape.FromMilliseconds(2, 40, sustain: 0, releaseMs: 80, holdSeconds: 0.4);

        Assert.Equal(0, shape.LevelAt(0.1));
        Assert.Equal(0.042, shape.ReleaseStarts, 3);
    }

    [Fact]
    public void Nothing_at_all_still_has_a_length_worth_drawing()
    {
        var shape = EnvelopeShape.FromMilliseconds(0, 0, 0, 0, 0);

        Assert.Equal(EnvelopeShape.MinimumLength, shape.Length);
    }

    [Fact]
    public void Nonsense_from_a_file_does_not_make_a_nonsense_curve()
    {
        var shape = EnvelopeShape.FromMilliseconds(-5, -5, double.NaN, -5, -5);

        Assert.Equal(0, shape.AttackSeconds);
        Assert.Equal(0, shape.Sustain);
        Assert.Equal(0, shape.LevelAt(double.NaN));
    }

    [Fact]
    public void A_level_is_never_asked_for_before_the_note_began()
    {
        Assert.Equal(0, Default.LevelAt(-1));
    }
}
