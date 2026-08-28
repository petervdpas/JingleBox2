using JingleBox2.Machines.Ui;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// An ADSR as a curve over time, which is what the picture on a machine's panel is drawn from.
/// </summary>
/// <remarks>
/// The shape is a rule holder with no window behind it, so it can be asked what it is at a given
/// moment without anything being drawn. Everything here is read in seconds although a patch is
/// written in milliseconds, since that is the unit the picture and the voice both work in.
/// </remarks>
public class EnvelopeTests
{
    /// <summary>
    /// A shape with all four stages long enough to be told apart: a 2ms attack, a 40ms decay to
    /// a sustain of 0.6, held for 0.4 seconds, and an 80ms release.
    /// </summary>
    private static EnvelopeShape Default =>
        EnvelopeShape.FromMilliseconds(attackMs: 2, decayMs: 40, sustain: 0.6, releaseMs: 80, holdSeconds: 0.4);

    /// <summary>
    /// The attack is a straight climb from silence, read at its start, middle and top.
    /// </summary>
    [Fact]
    public void It_starts_at_nothing_and_climbs_through_the_attack()
    {
        var shape = Default;

        Assert.Equal(0, shape.LevelAt(0));
        Assert.Equal(0.5, shape.LevelAt(0.001), 3);
        Assert.Equal(1.0, shape.LevelAt(0.002), 3);
    }

    /// <summary>
    /// The decay runs from the top down to the sustain and stops there, not at nought.
    /// </summary>
    [Fact]
    public void Falls_through_the_decay_to_the_sustain()
    {
        var shape = Default;

        Assert.Equal(0.8, shape.LevelAt(0.022), 2);
        Assert.Equal(0.6, shape.LevelAt(0.042), 2);
    }

    /// <summary>The sustain is flat for as long as the hold lasts, however long that is.</summary>
    [Fact]
    public void Holds_there_while_the_key_is_down()
    {
        var shape = Default;

        Assert.Equal(0.6, shape.LevelAt(0.2), 3);
        Assert.Equal(0.6, shape.LevelAt(0.4), 3);
    }

    /// <summary>
    /// The release begins where the attack, the decay and the hold have finished, and takes the
    /// curve to nought exactly at the end rather than somewhere near it.
    /// </summary>
    [Fact]
    public void And_falls_away_after_it_is_let_go()
    {
        var shape = Default;

        Assert.Equal(0.442, shape.ReleaseStarts, 3);
        Assert.Equal(0.3, shape.LevelAt(0.482), 2);
        Assert.Equal(0, shape.LevelAt(shape.Length));
    }

    /// <summary>
    /// A patch that decays to silence is over when the decay is, hold or no hold.
    /// </summary>
    /// <remarks>
    /// So the picture is as long as the sound, rather than trailing a flat line nobody hears.
    /// </remarks>
    [Fact]
    public void A_patch_with_no_sustain_ends_on_its_decay()
    {
        var shape = EnvelopeShape.FromMilliseconds(2, 40, sustain: 0, releaseMs: 80, holdSeconds: 0.4);

        Assert.Equal(0, shape.LevelAt(0.1));
        Assert.Equal(0.042, shape.ReleaseStarts, 3);
    }

    /// <summary>
    /// A patch with every stage at nought still has to be drawable: a curve of no length would
    /// be a division by nought in whatever is scaling it across the panel.
    /// </summary>
    [Fact]
    public void Nothing_at_all_still_has_a_length_worth_drawing()
    {
        var shape = EnvelopeShape.FromMilliseconds(0, 0, 0, 0, 0);

        Assert.Equal(EnvelopeShape.MinimumLength, shape.Length);
    }

    /// <summary>Negative times and a sustain of NaN come off a file somebody has edited.</summary>
    /// <remarks>
    /// A patch is JSON on disc and nothing stops a hand from putting anything in it. NaN is the
    /// one that matters, because it is neither above nor below any bound, so a comparison lets
    /// it through and it then poisons every arithmetic it touches.
    /// </remarks>
    [Fact]
    public void Nonsense_from_a_file_does_not_make_a_nonsense_curve()
    {
        var shape = EnvelopeShape.FromMilliseconds(-5, -5, double.NaN, -5, -5);

        Assert.Equal(0, shape.AttackSeconds);
        Assert.Equal(0, shape.Sustain);
        Assert.Equal(0, shape.LevelAt(double.NaN));
    }

    /// <summary>
    /// A time before nought reads as silence rather than running the attack backwards.
    /// </summary>
    [Fact]
    public void A_level_is_never_asked_for_before_the_note_began()
    {
        Assert.Equal(0, Default.LevelAt(-1));
    }
}
