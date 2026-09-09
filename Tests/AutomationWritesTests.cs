using JingleBox2.Midi.Interfaces;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The difference between a hand moving a fader and the song playing a lane that moves it.
/// </summary>
/// <remarks>
/// They are the same write and they are not the same act. Until this was told apart, a lane
/// replaying a fader was read as somebody having moved it: the song was marked as having unsaved
/// changes in it by the act of playing what it already held, and it took an undo step for the
/// privilege. From a chair that is a song that can never be left alone, and it costs more than a
/// flag, since the rescue copy is then written every twenty seconds for ever and each of those
/// walks every plugin on every track.
///
/// What a lane must still do is the sound and the picture. A lane that moved the number and left
/// the fader where it was would look like a lane that had not been played at all.
/// </remarks>
public class AutomationWritesTests
{
    /// <summary>A strip and the two callbacks it can report through, counted.</summary>
    private sealed class Bench
    {
        /// <summary>How many times a hand was reported.</summary>
        public int ByHand;

        /// <summary>How many times the song was reported.</summary>
        public int Played;

        /// <summary>The strip itself, over a mix of its own.</summary>
        public readonly TrackStripViewModel Strip;

        /// <summary>Builds one over track nought.</summary>
        public Bench()
        {
            Strip = new TrackStripViewModel(
                0, new TrackMix(), "", 1, () => ByHand++, () => Played++);
        }
    }

    /// <summary>A fader moved by hand is a change to the song.</summary>
    [Fact]
    public void A_hand_on_the_fader_is_a_change()
    {
        var bench = new Bench();

        bench.Strip.Volume = 0.5;

        Assert.Equal(1, bench.ByHand);
        Assert.Equal(0, bench.Played);
    }

    /// <summary>And the same fader moved by the song is not.</summary>
    [Fact]
    public void The_song_playing_its_own_lane_is_not_a_change()
    {
        var bench = new Bench();

        bench.Strip.Played(() => bench.Strip.Volume = 0.5);

        Assert.Equal(0, bench.ByHand);
        Assert.Equal(1, bench.Played);
    }

    /// <summary>The value still moves, which is the half a lane is played for.</summary>
    [Fact]
    public void The_song_still_moves_the_value()
    {
        var bench = new Bench();

        bench.Strip.Played(() => bench.Strip.Volume = 0.25);

        Assert.Equal(0.25, bench.Strip.Volume, 4);
    }

    /// <summary>A mute is a switch and goes the same way as the fader does.</summary>
    [Fact]
    public void A_switch_played_by_the_song_is_not_a_change_either()
    {
        var bench = new Bench();

        bench.Strip.Played(() => bench.Strip.Mute = true);

        Assert.True(bench.Strip.Mute);
        Assert.Equal(0, bench.ByHand);
        Assert.Equal(1, bench.Played);
    }

    /// <summary>
    /// The flag is put back afterwards, so the next thing a hand does is read as a hand.
    /// </summary>
    /// <remarks>
    /// The one way a scoped flag goes wrong is being left set, and what that would leave is an
    /// application where nothing anybody does is ever worth saving.
    /// </remarks>
    [Fact]
    public void A_hand_after_a_lane_is_still_a_hand()
    {
        var bench = new Bench();

        bench.Strip.Played(() => bench.Strip.Volume = 0.25);
        bench.Strip.Volume = 0.75;

        Assert.Equal(1, bench.ByHand);
        Assert.Equal(1, bench.Played);
    }

    /// <summary>And it is put back even when the write throws on the way through.</summary>
    [Fact]
    public void A_lane_that_throws_still_puts_the_flag_back()
    {
        var bench = new Bench();

        Assert.Throws<System.InvalidOperationException>(() =>
            bench.Strip.Played(() => throw new System.InvalidOperationException()));

        bench.Strip.Volume = 0.75;

        Assert.Equal(1, bench.ByHand);
    }

    /// <summary>
    /// A hand on the hardware is a change, and it stays one while automation is being recorded.
    /// </summary>
    /// <remarks>
    /// This is the half that must not be lost in telling a lane from a hand. Every router writes
    /// through <see cref="IControlTarget.Set"/>, so a knob on a control surface, a fader on a
    /// Mackie surface and a mouse on the mixer all land on the same setter a hand lands on, and
    /// every one of them is somebody changing the song. Recording automation is the sharpest
    /// case of it: the hand is moving the knob and the movement is being written into the
    /// pattern, so it is a change twice over.
    ///
    /// Only the song replaying what it already holds is not.
    /// </remarks>
    [Fact]
    public void A_knob_moved_by_hand_is_a_change_even_while_recording()
    {
        var bench = new Bench();

        IControlTarget target = new Strip(bench.Strip);

        target.Set(0.4);
        target.Set(0.6);

        Assert.Equal(2, bench.ByHand);
        Assert.Equal(0, bench.Played);
    }

    /// <summary>A target over a strip, written the two ways the real one is.</summary>
    private sealed class Strip : IControlTarget
    {
        /// <summary>The strip underneath, which is what reports which kind of write it was.</summary>
        private readonly TrackStripViewModel _strip;

        /// <summary>Takes the strip it stands for.</summary>
        public Strip(TrackStripViewModel strip) => _strip = strip;

        /// <inheritdoc/>
        public string Name => "Level";

        /// <inheritdoc/>
        public double Min => 0;

        /// <inheritdoc/>
        public double Max => 1;

        /// <inheritdoc/>
        public double Value => _strip.Volume;

        /// <inheritdoc/>
        public void Set(double value) => _strip.Volume = value;

        /// <inheritdoc/>
        public void Played(double value) => _strip.Played(() => _strip.Volume = value);
    }

    /// <summary>And the same target played by the song is not, through the same two calls.</summary>
    [Fact]
    public void The_same_target_played_by_the_song_is_not()
    {
        var bench = new Bench();

        IControlTarget target = new Strip(bench.Strip);

        target.Played(0.4);
        target.Played(0.6);

        Assert.Equal(0, bench.ByHand);
        Assert.Equal(2, bench.Played);
        Assert.Equal(0.6, target.Value, 4);
    }

    /// <summary>A target that has not thought about it plays exactly where it is set.</summary>
    private sealed class Plain : IControlTarget
    {
        /// <summary>What was written, so the two ways in can be told apart if they differ.</summary>
        public double Written;

        /// <inheritdoc/>
        public string Name => "plain";

        /// <inheritdoc/>
        public double Min => 0;

        /// <inheritdoc/>
        public double Max => 1;

        /// <inheritdoc/>
        public double Value => Written;

        /// <inheritdoc/>
        public void Set(double value) => Written = value;
    }

    /// <summary>
    /// Which is what keeps every other kind of target working: a machine's knob, a plugin's
    /// parameter and the transport all still take a lane exactly as they took a hand.
    /// </summary>
    [Fact]
    public void A_target_with_no_answer_of_its_own_is_played_where_it_is_set()
    {
        IControlTarget target = new Plain();

        target.Played(0.6);

        Assert.Equal(0.6, target.Value);
    }
}
