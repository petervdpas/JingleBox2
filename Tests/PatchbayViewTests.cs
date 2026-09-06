using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI.Records;
using JingleBox2.ViewModels;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What the patchbay does with a cable, which is one thing: it says what somebody plugged and
/// leaves the deciding to the routing.
/// </summary>
/// <remarks>
/// Every one of these is a gesture that can be made with a mouse in half a second, and most of
/// them are the awkward half: a cable dropped between two things that are nothing to do with us,
/// a source that stopped playing while the cable was in the air, and a cable pulled out of a
/// socket this application cannot yet empty.
///
/// The picture itself is <c>Tests/PatchbayTests.cs</c>. What is asked here is what happens after
/// a hand has let go.
/// </remarks>
public class PatchbayViewTests
{
    /// <summary>A source list that answers plainly and writes down what was asked of it.</summary>
    private sealed class Bench : IInputSource, INotifyPropertyChanged
    {
        /// <inheritdoc/>
        public ObservableCollection<AudioRoute> Routes { get; } = new();

        /// <summary>Backing field for <see cref="SelectedRoute"/>.</summary>
        private AudioRoute? chosen;

        /// <inheritdoc/>
        public AudioRoute? SelectedRoute
        {
            get => chosen;
            set
            {
                if (ReferenceEquals(chosen, value)) return;

                chosen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedRoute)));
            }
        }

        /// <inheritdoc/>
        public bool IsRoutingAvailable => true;

        /// <summary>How many times the graph was asked for again.</summary>
        public int Refreshed { get; private set; }

        /// <summary>How many pages say they are carrying the picker.</summary>
        public int Watching { get; private set; }

        /// <inheritdoc/>
        public void RefreshRoutes() => Refreshed++;

        /// <inheritdoc/>
        public void WatchRoutes() => Watching++;

        /// <inheritdoc/>
        public void LetRoutesGo() => Watching--;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>One source, as the routing would offer it.</summary>
    private static AudioRoute Route(string node) =>
        new(node, node, AudioRouteKind.Application);

    /// <summary>A patchbay over a bench with the given sources on it.</summary>
    private static (PatchbayViewModel Bay, Bench Input) Bay(params string[] nodes)
    {
        var bench = new Bench();

        foreach (string node in nodes) bench.Routes.Add(Route(node));

        return (new PatchbayViewModel(bench), bench);
    }

    /// <summary>The picture is there before anybody looks at it.</summary>
    [Fact]
    public void It_reads_the_machine_as_it_is_built()
    {
        var (bay, _) = Bay("firefox");

        Assert.Contains(bay.Nodes, n => n.Id == "firefox");
        Assert.Contains(bay.Nodes, n => n.IsOurs);
        Assert.DoesNotContain(bay.Links, l => l.To.Node == "record");
    }

    /// <summary>A cable into our own block picks that source.</summary>
    [Fact]
    public void A_cable_into_us_picks_the_source()
    {
        var (bay, bench) = Bay("firefox");

        bay.Plug(Cable(bay, "firefox"));

        Assert.NotNull(bench.SelectedRoute);
        Assert.Equal("firefox", bench.SelectedRoute!.Node);
    }

    /// <summary>And the cable is then in the picture, because the source said so.</summary>
    /// <remarks>
    /// The picture follows the routing rather than the gesture: what is drawn is what is really
    /// feeding the input, so a choice that did not take would leave the cable out and say so.
    /// </remarks>
    [Fact]
    public void The_cable_appears_because_the_source_changed()
    {
        var (bay, _) = Bay("firefox");

        bay.Plug(Cable(bay, "firefox"));

        var link = Assert.Single(bay.Links, l => l.From.Node == "firefox");

        Assert.Equal("record", link.To.Node);
    }

    /// <summary>A cable between two things that are not us changes nothing.</summary>
    /// <remarks>
    /// There is no such cable on the page today, since only our own block has an input, and the
    /// answer is written down anyway: the day a second input is drawn, joining two of somebody
    /// else's programs would be rewiring the machine around us, which this deliberately will not
    /// do.
    /// </remarks>
    [Fact]
    public void A_cable_that_misses_us_changes_nothing()
    {
        var (bay, bench) = Bay("firefox", "mic");

        var from = new PatchPort("firefox", "out", JingleBox2.UI.Enums.PatchSide.Out, JingleBox2.UI.Enums.PatchChannels.Stereo);
        var to = new PatchPort("mic", "input", JingleBox2.UI.Enums.PatchSide.In, JingleBox2.UI.Enums.PatchChannels.Stereo);

        bay.Plug(new PatchLink(from, to));

        Assert.Null(bench.SelectedRoute);
        Assert.NotEmpty(bay.Says);
    }

    /// <summary>A source that stopped playing while the cable was in the air is said, not thrown.</summary>
    [Fact]
    public void A_source_that_went_away_mid_drag_is_answered()
    {
        var (bay, bench) = Bay("firefox");

        var cable = Cable(bay, "firefox");

        bench.Routes.Clear();

        bay.Plug(cable);

        Assert.Null(bench.SelectedRoute);
        Assert.NotEmpty(bay.Says);
    }

    /// <summary>Pulling a cable out says what it would take, and leaves the picture true.</summary>
    /// <remarks>
    /// The input cannot be left on nothing yet, so the honest answer is to say so rather than to
    /// let a cable spring back with no explanation.
    /// </remarks>
    [Fact]
    public void Pulling_a_cable_out_says_what_it_would_take()
    {
        var (bay, bench) = Bay("firefox");

        bay.Plug(Cable(bay, "firefox"));
        bay.Unplug(Assert.Single(bay.Links, l => l.From.Node == "firefox"));

        Assert.NotNull(bench.SelectedRoute);
        Assert.Single(bay.Links, l => l.From.Node == "firefox");
        Assert.NotEmpty(bay.Says);
    }

    /// <summary>A source appearing puts a block on the page without anybody asking.</summary>
    [Fact]
    public void A_source_that_starts_playing_turns_up()
    {
        var (bay, bench) = Bay();

        Assert.DoesNotContain(bay.Nodes, n => n.Id == "firefox");

        bench.Routes.Add(Route("firefox"));

        Assert.Contains(bay.Nodes, n => n.Id == "firefox");
    }

    /// <summary>The picked block survives the machine being read again.</summary>
    /// <remarks>
    /// Every reading builds fresh blocks, so a sidebar holding the old object would go on
    /// describing a block that is no longer on the page.
    /// </remarks>
    [Fact]
    public void The_picked_block_survives_a_reading()
    {
        var (bay, bench) = Bay("firefox");

        bay.Selected = bay.Nodes[0];

        bench.Routes.Add(Route("mic"));

        Assert.NotNull(bay.Selected);
        Assert.Equal("firefox", bay.Selected!.Id);
    }

    /// <summary>And is let go of when its block stops being there.</summary>
    [Fact]
    public void The_picked_block_goes_when_it_goes()
    {
        var (bay, bench) = Bay("firefox");

        bay.Selected = bay.Nodes[0];

        bench.Routes.Clear();

        Assert.Null(bay.Selected);
    }

    /// <summary>Opening the page asks the machine again rather than trusting what was there.</summary>
    [Fact]
    public void Opening_the_page_asks_again()
    {
        var (bay, bench) = Bay("firefox");

        bay.Refresh();

        Assert.Equal(1, bench.Refreshed);
    }

    /// <summary>A block put somewhere opens there next time.</summary>
    /// <remarks>
    /// The whole point of keeping it: the arrangement is somebody's work and this application
    /// throwing it away on every start would be worse than never having remembered it.
    /// </remarks>
    [Fact]
    public void A_block_opens_where_it_was_left()
    {
        var kept = new Shelf();

        kept.Place("firefox", 640, 480);

        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));

        var bay = new PatchbayViewModel(bench, null, kept);

        var block = Assert.Single(bay.Nodes, n => n.Id == "firefox");

        Assert.Equal(640, block.X);
        Assert.Equal(480, block.Y);
    }

    /// <summary>A block nobody moved opens where the graph put it.</summary>
    [Fact]
    public void A_block_nobody_moved_opens_where_it_was_meant_to()
    {
        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));

        var plain = new PatchbayViewModel(bench);
        var kept = new PatchbayViewModel(bench, null, new Shelf());

        Assert.Equal(
            Assert.Single(plain.Nodes, n => n.Id == "firefox").X,
            Assert.Single(kept.Nodes, n => n.Id == "firefox").X);
    }

    /// <summary>Letting go of a block writes down where it was left.</summary>
    [Fact]
    public void Letting_go_writes_it_down()
    {
        var kept = new Shelf();
        var bay = new PatchbayViewModel(new Bench(), null, kept);

        bay.Place("mixer", 30, 40);

        Assert.True(kept.Placed("mixer", out double x, out double y));
        Assert.Equal(30, x);
        Assert.Equal(40, y);
    }

    /// <summary>A patchbay built with nothing keeping places still works.</summary>
    /// <remarks>
    /// Which is what a test, and any page built without a settings file, is: the cost is that
    /// nothing is remembered, and nothing else changes.
    /// </remarks>
    [Fact]
    public void A_patchbay_with_nowhere_to_keep_places_is_fine()
    {
        var bay = new PatchbayViewModel(new Bench());

        bay.Place("mixer", 30, 40);

        Assert.NotEmpty(bay.Nodes);
    }

    /// <summary>The tracker's tracks reach the picture as one pair each.</summary>
    /// <remarks>
    /// The whole of what this buys: the picture says something about the music rather than about
    /// the sum of it.
    /// </remarks>
    [Fact]
    public void The_songs_tracks_are_on_the_picture()
    {
        var bay = new PatchbayViewModel(new Bench(), null, null, new Playing("TR-01", "TR-02"));

        var tracker = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        Assert.Equal(2, tracker.Outs.Count);
    }

    /// <summary>And what is sounding on them is what is drawn solid.</summary>
    [Fact]
    public void A_sounding_track_is_drawn_solid()
    {
        var flowing = new Playing("TR-01", "TR-02") { Sounding = { "TR-02" } };

        var bay = new PatchbayViewModel(new Bench(), null, null, flowing);

        bay.Pulse();

        var live = Assert.Single(bay.Live, l => l.From.Node == "tracker");

        Assert.Equal("TR-02", live.From.Name);
    }

    /// <summary>A song whose tracks nobody can name still draws the mix.</summary>
    [Fact]
    public void No_tracks_named_still_draws_the_mix()
    {
        var bay = new PatchbayViewModel(new Bench());

        Assert.Single(Assert.Single(bay.Nodes, n => n.Id == "tracker").Outs);
    }

    /// <summary>A song with tracks, and whichever of them are sounding.</summary>
    private sealed class Playing : IAudioFlowing
    {
        /// <summary>Takes the tracks the song has.</summary>
        /// <param name="tracks">Their names, as their strips wear them.</param>
        public Playing(params string[] tracks) => Tracks = tracks;

        /// <inheritdoc/>
        public System.Collections.Generic.IReadOnlyList<string> Tracks { get; }

        /// <summary>Which of them are making a sound.</summary>
        public System.Collections.Generic.HashSet<string> Sounding { get; } = new(System.StringComparer.Ordinal);

        /// <inheritdoc/>
        public JingleBox2.UI.Records.PatchSignals Signals =>
            new(false, false, false, Sounding, false);

        /// <summary>What each block is putting out, for whoever wants to say.</summary>
        public System.Collections.Generic.Dictionary<string, JingleBox2.UI.Records.PatchLevel> Levels { get; } =
            new(System.StringComparer.Ordinal);

        /// <inheritdoc/>
        public JingleBox2.UI.Records.PatchLevel Level(string node) =>
            Levels.TryGetValue(node, out var level) ? level : default;

        /// <summary>The strips answering for each block and output, where anything does.</summary>
        public System.Collections.Generic.Dictionary<(string Node, string Port), IStripSwitches> Strips { get; } =
            new();

        /// <inheritdoc/>
        public IStripSwitches? Switches(string node, string port) =>
            Strips.TryGetValue((node, port), out var strip) ? strip : null;
    }

    /// <summary>The picked block's meter is what the sidebar shows.</summary>
    [Fact]
    public void The_sidebar_reads_the_picked_blocks_meter()
    {
        var flowing = new Playing("TR-01");

        flowing.Levels["tracker"] = new JingleBox2.UI.Records.PatchLevel(true, 0.4f, 0.6f);

        var bay = new PatchbayViewModel(new Bench(), null, null, flowing)
        {
            Selected = null
        };

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        Assert.True(bay.Metered);
        Assert.Equal(0.4f, bay.LevelLeft);
        Assert.Equal(0.6f, bay.LevelRight);
    }

    /// <summary>Picking a block reads it at once rather than on the next tick.</summary>
    /// <remarks>
    /// A fifth of a second of an empty meter after clicking a block reads as a block that has
    /// nothing to show.
    /// </remarks>
    [Fact]
    public void Picking_a_block_reads_it_at_once()
    {
        var flowing = new Playing();

        flowing.Levels["mixer"] = new JingleBox2.UI.Records.PatchLevel(true, 0.9f, 0.9f);

        var bay = new PatchbayViewModel(new Bench(), null, null, flowing);

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "mixer");

        Assert.Equal(0.9f, bay.LevelLeft);
    }

    /// <summary>A block on the machine has no meter, rather than one stuck at nought.</summary>
    /// <remarks>
    /// What somebody else's program is putting out is not something this application measures,
    /// and a bar that cannot move reads as silence rather than as a question nobody can answer.
    /// </remarks>
    [Fact]
    public void A_block_on_the_machine_has_no_meter()
    {
        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));

        var bay = new PatchbayViewModel(bench, null, null, new Playing());

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "firefox");

        Assert.False(bay.Metered);
    }

    /// <summary>The page opens on whatever is feeding the input.</summary>
    /// <remarks>
    /// The one block on the picture that was chosen rather than simply being there: everything
    /// else is either this application or a program that happens to be playing.
    /// </remarks>
    [Fact]
    public void It_opens_on_the_source_being_taken()
    {
        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));
        bench.SelectedRoute = bench.Routes[0];

        var bay = new PatchbayViewModel(bench);

        Assert.Equal("firefox", bay.Selected?.Id);
    }

    /// <summary>With no source chosen it opens on nothing, and fills in when one is.</summary>
    [Fact]
    public void With_no_source_it_waits_for_one()
    {
        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));

        var bay = new PatchbayViewModel(bench);

        Assert.Null(bay.Selected);

        bench.SelectedRoute = bench.Routes[0];

        Assert.Equal("firefox", bay.Selected?.Id);
    }

    /// <summary>And having picked one, a fresh reading does not take it away.</summary>
    /// <remarks>
    /// The graph is read every couple of seconds, so a page that chose again on each reading
    /// would take the panel off a block somebody was looking at.
    /// </remarks>
    [Fact]
    public void Opening_on_the_source_happens_once()
    {
        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));
        bench.SelectedRoute = bench.Routes[0];

        var bay = new PatchbayViewModel(bench);

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "mixer");

        bench.Routes.Add(Route("chromium"));

        Assert.Equal("mixer", bay.Selected?.Id);
    }

    /// <summary>Nothing picked shows no meter.</summary>
    [Fact]
    public void Nothing_picked_shows_no_meter()
    {
        var bay = new PatchbayViewModel(new Bench(), null, null, new Playing());

        bay.Selected = null;

        Assert.False(bay.Metered);
    }

    /// <summary>A patchbay with nobody to ask shows none either.</summary>
    [Fact]
    public void With_nobody_to_ask_there_is_no_meter()
    {
        var bay = new PatchbayViewModel(new Bench());

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "mixer");

        Assert.False(bay.Metered);
    }

    /// <summary>Every output of the picked block is a row, whatever the block is.</summary>
    /// <remarks>
    /// The uniform shape is the point: a track, the pads, a take and the master are four things
    /// in this application and the sidebar draws them the same way.
    /// </remarks>
    [Fact]
    public void Every_output_is_a_row()
    {
        var bay = new PatchbayViewModel(new Bench(), null, null, new Playing("TR-01", "TR-02"));

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        Assert.Equal(new[] { "TR-01", "TR-02" }, bay.Outputs.Select(o => o.Name));
    }

    /// <summary>A row over a strip drives that strip, rather than keeping its own answer.</summary>
    /// <remarks>
    /// Pressing M in the sidebar and pressing M on the desk have to be the same press on the
    /// same thing, or the two pages would show a track muted and unmuted at once.
    /// </remarks>
    [Fact]
    public void A_row_drives_the_strip_behind_it()
    {
        var flowing = new Playing("TR-01");
        var strip = new Switchable();

        flowing.Strips[("tracker", "TR-01")] = strip;

        var bay = new PatchbayViewModel(new Bench(), null, null, flowing);

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        Assert.Single(bay.Outputs).Mute = true;

        Assert.True(strip.Mute);
    }

    /// <summary>And it reads back what the strip says, however that was set.</summary>
    [Fact]
    public void A_row_reads_the_strip_back()
    {
        var flowing = new Playing("TR-01");

        flowing.Strips[("tracker", "TR-01")] = new Switchable { Solo = true };

        var bay = new PatchbayViewModel(new Bench(), null, null, flowing);

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        Assert.True(Assert.Single(bay.Outputs).Solo);
    }

    /// <summary>A row over nothing keeps its shape and both switches stay dark.</summary>
    /// <remarks>
    /// Which is every block on the machine: somebody else's program has no mute of ours, and a
    /// row that vanished would make the sidebar a different shape for every block.
    /// </remarks>
    [Fact]
    public void A_row_over_nothing_is_still_a_row()
    {
        var bench = new Bench();
        bench.Routes.Add(Route("firefox"));

        var bay = new PatchbayViewModel(bench, null, null, new Playing());

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "firefox");

        var row = Assert.Single(bay.Outputs);

        Assert.False(row.CanMute);
        Assert.False(row.CanSolo);
    }

    /// <summary>Pressing a switch that cannot be pressed changes nothing.</summary>
    [Fact]
    public void A_switch_that_cannot_be_pressed_does_nothing()
    {
        var bay = new PatchbayViewModel(new Bench(), null, null, new Playing("TR-01"));

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        var row = Assert.Single(bay.Outputs);

        row.Mute = true;
        row.Solo = true;

        Assert.False(row.Mute);
        Assert.False(row.Solo);
    }

    /// <summary>Picking another block gives the sidebar that block's rows.</summary>
    [Fact]
    public void Picking_another_block_changes_the_rows()
    {
        var bay = new PatchbayViewModel(new Bench(), null, null, new Playing("TR-01", "TR-02"));

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");

        Assert.Equal(2, bay.Outputs.Count);

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "mixer");

        Assert.Single(bay.Outputs);
    }

    /// <summary>Nothing picked leaves no rows behind.</summary>
    [Fact]
    public void Nothing_picked_leaves_no_rows()
    {
        var bay = new PatchbayViewModel(new Bench(), null, null, new Playing("TR-01"));

        bay.Selected = Assert.Single(bay.Nodes, n => n.Id == "tracker");
        bay.Selected = null;

        Assert.Empty(bay.Outputs);
    }

    /// <summary>A strip that can be muted and soloed, holding its own two answers.</summary>
    private sealed class Switchable : IStripSwitches
    {
        /// <inheritdoc/>
        public bool CanMute => true;

        /// <inheritdoc/>
        public bool CanSolo => true;

        /// <inheritdoc/>
        public bool Mute { get; set; }

        /// <inheritdoc/>
        public bool Solo { get; set; }
    }

    /// <summary>Somewhere to keep places, holding them in memory.</summary>
    private sealed class Shelf : IPatchPlaces
    {
        /// <summary>What has been put where.</summary>
        private readonly System.Collections.Generic.Dictionary<string, (double X, double Y)> _places = new();

        /// <inheritdoc/>
        public bool Placed(string node, out double x, out double y)
        {
            if (_places.TryGetValue(node, out var at))
            {
                x = at.X;
                y = at.Y;

                return true;
            }

            x = 0;
            y = 0;

            return false;
        }

        /// <inheritdoc/>
        public void Place(string node, double x, double y) => _places[node] = (x, y);
    }

    /// <summary>The cable a hand would have drawn from that source into us.</summary>
    private static PatchLink Cable(PatchbayViewModel bay, string node)
    {
        foreach (var one in bay.Nodes)
        {
            if (one.Id != node) continue;

            foreach (var us in bay.Nodes)
            {
                if (!us.IsOurs) continue;

                return new PatchLink(one.Outs[0], us.Ins[0]);
            }
        }

        return default;
    }
}
