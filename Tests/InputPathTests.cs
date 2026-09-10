using System.Collections.Generic;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the desk's input channel does to the machine, asked without a window or a graph.
/// </summary>
/// <remarks>
/// **The two facts the IN strip holds were read in four places in the page and produced no
/// arrangement at all.** A browser chosen as the input went on playing out of the speakers, and
/// Hear it wrote a level and nothing else, so what anybody heard with the tick on was the same
/// audio twice a buffer apart and with it off was the browser exactly as before. There is one
/// answer to make here, so this is the one thing that makes it.
///
/// The route under it is a double. What the taking actually is differs per machine, links moved
/// on a graph and a program pointed at another output where there is none, and none of that is
/// in the module: it is <c>PipeWireRouting</c> and <c>WindowsRouting</c> behind
/// <see cref="IAudioRouting"/>, which is the only place the two systems part company. So every
/// rule below holds on both.
/// </remarks>
public sealed class InputPathTests
{
    /// <summary>A browser, which is the source the whole thing was reported on.</summary>
    private static readonly AudioRoute Firefox =
        new("firefox", "Firefox", AudioRouteKind.Application);

    /// <summary>Another one, for what happens when the input is pointed somewhere else.</summary>
    private static readonly AudioRoute Player =
        new("player", "Music player", AudioRouteKind.Application);

    /// <summary>What an output is playing, which is the one kind that can be ours coming back.</summary>
    private static readonly AudioRoute Speakers =
        new("speakers", "Speakers", AudioRouteKind.Monitor);

    /// <summary>A route that does as it is told and counts what it was told.</summary>
    private sealed class Route : IAudioRouting
    {
        /// <summary>How many times a source was taken off its own output.</summary>
        public int Aside { get; private set; }

        /// <summary>How many times whatever was moved was put back.</summary>
        public int Back { get; private set; }

        /// <summary>How many times the arrangement was held over something creeping back.</summary>
        public int Held { get; private set; }

        /// <summary>Which source was last taken aside.</summary>
        public AudioRoute? Took { get; private set; }

        /// <summary>Whether taking one aside works at all, for the machine that cannot.</summary>
        public bool Can { get; set; } = true;

        /// <summary>What this says about whose output a monitor is.</summary>
        public bool? Ours { get; set; }

        /// <inheritdoc/>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public IReadOnlyList<AudioRoute> GetRoutes() => new[] { Firefox, Player, Speakers };

        /// <inheritdoc/>
        public AudioRoute? GetCurrentRoute() => null;

        /// <inheritdoc/>
        public bool Connect(AudioRoute route) => true;

        /// <inheritdoc/>
        public bool CanTakeAside => Can;

        /// <inheritdoc/>
        public string AsideNote => Can ? "" : "this machine cannot";

        /// <inheritdoc/>
        public bool TakeAside(AudioRoute route)
        {
            if (!Can) return false;

            Aside++;
            Took = route;

            return true;
        }

        /// <inheritdoc/>
        public bool HoldAside(AudioRoute route)
        {
            Held++;

            return true;
        }

        /// <inheritdoc/>
        public void GiveBack() => Back++;

        /// <inheritdoc/>
        public bool? IsOurOutput(AudioRoute source, string? output) => Ours;
    }

    /// <summary>What this application plays out of, for the loop test.</summary>
    private const string Out = "Speakers";

    /// <summary>
    /// Choosing a source takes it off its own output, with nothing else asked for.
    /// </summary>
    /// <remarks>
    /// The fault said as one test, and the state it was reported in: a browser on the IN strip,
    /// Hear it off, and the browser still playing out of the speakers.
    /// </remarks>
    [Fact]
    public void Choosing_a_source_takes_it_off_its_own_output()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: false, Out);

        Assert.Equal(1, route.Aside);
        Assert.Equal(Firefox, route.Took);
    }

    /// <summary>Hearing it is a separate fact and does not move it again.</summary>
    /// <remarks>
    /// The tick releases the source into the mix and says nothing about where it was playing, so
    /// turning it on must not be a second reason to unplug anything.
    /// </remarks>
    [Fact]
    public void Hearing_is_remembered_and_changes_nothing_about_the_output()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: true, Out);

        Assert.True(path.Heard);
        Assert.Equal(1, route.Aside);
    }

    /// <summary>And the source stays off its own output when the tick goes off again.</summary>
    /// <remarks>
    /// Unticking says stop putting it through the desk, not hand it back to the speakers. A
    /// source that came back on air because somebody stopped listening to it is the worst thing
    /// this could do on a station.
    /// </remarks>
    [Fact]
    public void Unhearing_does_not_hand_the_source_back()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: true, Out);
        path.Set(Firefox, heard: false, Out);

        Assert.Equal(2, route.Aside);
        Assert.Equal(Firefox, route.Took);
    }

    /// <summary>Pointing the input elsewhere puts the old source back and takes the new one.</summary>
    /// <remarks>
    /// The one that is no longer the input has no business staying unplugged, and the one that
    /// now is has every business being.
    /// </remarks>
    [Fact]
    public void Another_source_puts_the_first_one_back()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: true, Out);
        path.Set(Player, heard: true, Out);

        Assert.Equal(2, route.Back);
        Assert.Equal(Player, route.Took);
    }

    /// <summary>Pointing it at nothing puts the source back and takes nothing.</summary>
    [Fact]
    public void Nothing_chosen_puts_the_source_back_and_moves_nothing()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: true, Out);

        int aside = route.Aside;

        path.Set(null, heard: true, Out);

        Assert.True(route.Back > 0);
        Assert.Equal(aside, route.Aside);
        Assert.Null(path.Source);
    }

    /// <summary>
    /// What this application plays out of is left where it is.
    /// </summary>
    /// <remarks>
    /// Hearing that through the same output sends it round again, so it is refused a place on the
    /// desk; unplugging it as well would take a source off its own output and give nothing back
    /// for it, which is silence with nothing on the screen saying why.
    /// </remarks>
    [Fact]
    public void Our_own_output_is_left_where_it_is()
    {
        var route = new Route { Ours = true };
        var path = new InputPath(route);

        string said = path.Set(Speakers, heard: true, Out);

        Assert.Equal(0, route.Aside);
        Assert.False(path.CanHear(Speakers, Out));
        Assert.Contains("loop", said, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Cannot tell is read as ours, since the other way round is a room full of it.</summary>
    /// <remarks>
    /// A subsystem that has not been taught to tell says nothing, and being wrong that way is a
    /// switch that does nothing where being wrong the other way is feedback at whatever the
    /// master is set to.
    /// </remarks>
    [Fact]
    public void A_monitor_nobody_can_place_is_read_as_ours()
    {
        var route = new Route { Ours = null };
        var path = new InputPath(route);

        path.Set(Speakers, heard: true, Out);

        Assert.False(path.CanHear(Speakers, Out));
        Assert.Equal(0, route.Aside);
    }

    /// <summary>Another output's monitor is an ordinary source and is taken like one.</summary>
    [Fact]
    public void Another_outputs_monitor_is_taken_like_anything_else()
    {
        var route = new Route { Ours = false };
        var path = new InputPath(route);

        path.Set(Speakers, heard: true, Out);

        Assert.True(path.CanHear(Speakers, Out));
        Assert.Equal(1, route.Aside);
    }

    /// <summary>Nothing but a monitor is even asked the question.</summary>
    /// <remarks>
    /// A microphone, a line in and a program have never been near an output, so a route that
    /// answers ours for everything cannot silence them.
    /// </remarks>
    [Fact]
    public void Only_a_monitor_can_be_our_own_output()
    {
        var route = new Route { Ours = true };
        var path = new InputPath(route);

        Assert.True(path.CanHear(Firefox, Out));
    }

    /// <summary>A machine that cannot take a source aside says so rather than pretending.</summary>
    [Fact]
    public void A_machine_that_cannot_move_a_source_says_so()
    {
        var route = new Route { Can = false };
        var path = new InputPath(route);

        string said = path.Set(Firefox, heard: true, Out);

        Assert.Contains("could not be taken off", said, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The arrangement is held over anything that creeps back.</summary>
    /// <remarks>
    /// The graph belongs to the machine and its session manager wires a stream back to the
    /// speakers whenever the stream is remade, so this is asked again on every reading.
    /// </remarks>
    [Fact]
    public void What_creeps_back_is_taken_off_again()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: false, Out);

        Assert.True(path.Hold());
        Assert.Equal(1, route.Held);
    }

    /// <summary>
    /// Nothing is held for a source that was never moved.
    /// </summary>
    /// <remarks>
    /// **Holding is not the same question as taking.** The route's own hold takes a source aside
    /// outright where it is holding nothing, so asked about one this deliberately left alone it
    /// would unplug it on the next reading: a source silenced by the very rule that had decided
    /// to leave it be.
    /// </remarks>
    [Fact]
    public void Nothing_is_held_for_a_source_that_was_never_moved()
    {
        var route = new Route { Ours = true };
        var path = new InputPath(route);

        path.Set(Speakers, heard: true, Out);

        Assert.False(path.Hold());
        Assert.Equal(0, route.Held);
    }

    /// <summary>And nothing is held where nothing is chosen at all.</summary>
    [Fact]
    public void Nothing_is_held_where_nothing_is_chosen()
    {
        var route = new Route();
        var path = new InputPath(route);

        Assert.False(path.Hold());
        Assert.Equal(0, route.Held);
    }

    /// <summary>Giving back is the way out, and it reaches the machine.</summary>
    /// <remarks>
    /// What was unplugged is somebody's own machine, so a browser left silent after this program
    /// has closed is the worst thing this could do.
    /// </remarks>
    [Fact]
    public void Giving_back_reaches_the_machine()
    {
        var route = new Route();
        var path = new InputPath(route);

        path.Set(Firefox, heard: true, Out);

        int back = route.Back;

        path.GiveBack();

        Assert.True(route.Back > back);
    }
}
