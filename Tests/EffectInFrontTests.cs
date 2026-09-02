using JingleBox2.Rack.Faces.Interfaces;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which of our effects has its face in front, which is what a link on one resolves against.
/// </summary>
/// <remarks>
/// A link on one of ours names the effect and the key and never where it is standing, so
/// something has to say which EchoBox. It used to be the chain of the track you are working on,
/// and that is the wrong answer in three ways: a track's chain follows the cursor, so a face left
/// open while an instrument window claims another track resolved against that other track; the
/// master's chain follows nothing and never matched a cursor at all; and a pad's chain is not on
/// a track, so no answer phrased as a track number could reach it.
///
/// The rule is two lines and every one of its cases fails in silence: a knob that moves nothing,
/// or worse, one that moves the wrong box.
/// </remarks>
public class EffectInFrontTests
{
    /// <summary>Nothing is in front until a face says so.</summary>
    [Fact]
    public void Nothing_is_in_front_to_begin_with()
    {
        Assert.Null(new EffectInFront().Shown);
    }

    /// <summary>A face that comes forward is the one in front.</summary>
    [Fact]
    public void A_face_that_comes_forward_is_the_one_in_front()
    {
        var front = new EffectInFront();
        var pad = new Box("effect.echobox", "Pad 03");

        front.InFront(pad);

        Assert.Same(pad, front.Shown);
    }

    /// <summary>And the next one to come forward takes its place.</summary>
    /// <remarks>
    /// Two windows open at once is ordinary, and the one you clicked last is the one you are
    /// working on. Nothing has to be told the first has stood down.
    /// </remarks>
    [Fact]
    public void The_last_one_forward_is_the_one_in_front()
    {
        var front = new EffectInFront();
        var master = new Box("effect.echobox", "MASTER");
        var track = new Box("effect.echobox", "TR-03");

        front.InFront(master);
        front.InFront(track);

        Assert.Same(track, front.Shown);
        Assert.Equal("TR-03", front.Shown!.Where);
    }

    /// <summary>A face that closes stands down.</summary>
    [Fact]
    public void A_face_that_closes_stands_down()
    {
        var front = new EffectInFront();
        var pad = new Box("effect.echobox", "Pad 03");

        front.InFront(pad);
        front.Gone(pad);

        Assert.Null(front.Shown);
    }

    /// <summary>
    /// But closing the window behind the one you are using changes nothing.
    /// </summary>
    /// <remarks>
    /// The same rule the instrument window keeps about tracks, and for the same reason: a window
    /// going away somewhere behind is not a statement about what you are looking at. Without it,
    /// tidying up a stack of open faces would leave the knob in your hand pointed at nothing.
    /// </remarks>
    [Fact]
    public void Closing_the_one_behind_leaves_the_one_in_front()
    {
        var front = new EffectInFront();
        var master = new Box("effect.echobox", "MASTER");
        var track = new Box("effect.echobox", "TR-03");

        front.InFront(master);
        front.InFront(track);
        front.Gone(master);

        Assert.Same(track, front.Shown);
    }

    /// <summary>Nothing said is the same as no face at all, and nothing throws.</summary>
    [Fact]
    public void Nothing_is_a_fair_answer_both_ways()
    {
        var front = new EffectInFront();
        var pad = new Box("effect.echobox", "Pad 03");

        front.Gone(null);
        front.InFront(pad);
        front.InFront(null);

        Assert.Null(front.Shown);

        front.Gone(pad);

        Assert.Null(front.Shown);
    }

    /// <summary>One of our effects with a face on screen, which is three answers and no more.</summary>
    /// <param name="id">Which effect.</param>
    /// <param name="where">Where its chain is.</param>
    private sealed class Box(string id, string where) : IEffectShown
    {
        /// <inheritdoc/>
        public string Id { get; } = id;

        /// <inheritdoc/>
        public string Where { get; } = where;

        /// <inheritdoc/>
        public IPanelValues Values { get; } = new Knobs();

        /// <summary>Knobs that hold what they are given, which is all this rule asks of them.</summary>
        private sealed class Knobs : IPanelValues
        {
            /// <summary>What each knob stands at.</summary>
            private readonly System.Collections.Generic.Dictionary<string, double> _at = new();

            /// <inheritdoc/>
            public double Get(string key) => _at.TryGetValue(key, out double at) ? at : 0;

            /// <inheritdoc/>
            public void Set(string key, double value)
            {
                _at[key] = value;

                Said?.Invoke(key);
            }

            /// <inheritdoc/>
            public event System.Action<string>? Said;
        }
    }
}
