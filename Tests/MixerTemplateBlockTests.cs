using System.Collections.Generic;
using System.Linq;
using JingleBox2.Controllers;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The mixer's own card, reading and writing its templates through the block.
/// </summary>
/// <remarks>
/// **The mixer is one thing to point a controller at, however many strips a link is on.** A knob
/// pointed at a machine is that machine's business; a fader pointed at strip three is the desk's,
/// and what somebody wants to see on the mixer is what their nanoKONTROL2 does to the mixer
/// rather than a menu per fader.
///
/// So the mixer is the case that says whether the block really carries what a face needs, since
/// its templates are the ones whose target names nothing: the strip is written on each line
/// instead of in the target, and a menu that compared ids would list none of them.
/// </remarks>
public sealed class MixerTemplateBlockTests
{
    /// <summary>A fader pointed at one of the mixer's strips, learned on a desk.</summary>
    private static ControlMapping Fader(MixControl what, int strip, int cc, string desk = "Desk One") => new()
    {
        Device = desk,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.Mix,
        Mix = what,
        Track = strip,
        Scope = ControlScope.Fixed,
        Name = "Mixer " + what + " on " + (strip + 1),
    };

    /// <summary>The desk, the block behind it kept up the way the application keeps it, and the card.</summary>
    private sealed class Bench
    {
        /// <summary>What is pointed at the mixer.</summary>
        public List<ControlMapping> Desk { get; }

        /// <summary>The block every face reads.</summary>
        public ControlTemplateBlock Block { get; } = new();

        /// <summary>The link the gesture writes.</summary>
        public ControlLink Link { get; }

        /// <summary>The mixer's own card.</summary>
        public IPanelMenu Card { get; }

        /// <summary>The write path, run by hand here.</summary>
        /// <remarks>
        /// **The window runs this off <see cref="ControlLink.Changed"/>, and that is delivered on
        /// the drawing thread.** A test has no drawing thread, and worse, it has one only
        /// sometimes: the moment any other test in the suite has pinned a dispatcher, every post
        /// waits for a loop that is never going to run, so a test that asserted straight after an
        /// edit passed on its own and failed in a full run. What is being tested here is the
        /// path, so the path is run where the window would run it and the delivery is somebody
        /// else's business.
        /// </remarks>
        public ControlTemplatesFromLinks Filling { get; }

        /// <summary>Wires the write path and the read path the way the window does.</summary>
        /// <param name="links">What is on the desk to begin with.</param>
        public Bench(params ControlMapping[] links)
        {
            Desk = new List<ControlMapping>(links);
            Link = new ControlLink(Desk, () => { });

            Filling = new ControlTemplatesFromLinks(Block, () => Desk, new ControllerProfiles());

            Filling.Fill(said: false);

            Link.Templates = Block;

            Card = new ControlMenu(() => "", () => "the mixer", () => Link, kind: LinkTargets.Mixer);
        }
    }

    /// <summary>A mixer nobody has pointed anything at offers the one thing there is to do.</summary>
    [Fact]
    public void A_mixer_nobody_has_pointed_at_offers_only_learning()
    {
        var bench = new Bench();

        Assert.Single(bench.Card.Read());
        Assert.Empty(bench.Block.Templates);
    }

    /// <summary>
    /// **One desk on five strips is one template**, which is the rule the mixer is the test of.
    /// </summary>
    [Fact]
    public void One_desk_on_many_strips_is_one_template()
    {
        var bench = new Bench(
            Fader(MixControl.Volume, 0, 0),
            Fader(MixControl.Volume, 1, 1),
            Fader(MixControl.Pan, 2, 2));

        var one = Assert.Single(bench.Block.Templates);

        Assert.Equal(3, one.Controls.Count);
        Assert.Equal("", one.Target.Id);
        Assert.Equal(LinkTargets.Mixer, one.Target.Kind);
    }

    /// <summary>And two desks on the mixer are two, since neither can answer the other's messages.</summary>
    [Fact]
    public void Two_desks_on_the_mixer_are_two_templates()
    {
        var bench = new Bench(
            Fader(MixControl.Volume, 0, 0),
            Fader(MixControl.Pan, 1, 1, "Desk Two"));

        Assert.Equal(2, bench.Block.Templates.Count);
        Assert.Equal(3, bench.Card.Read().Count);
    }

    /// <summary>**A fader learned reaches the card**, which is the write path seen from the read one.</summary>
    [Fact]
    public void A_fader_learned_reaches_the_card()
    {
        var bench = new Bench();

        Assert.Single(bench.Card.Read());

        bench.Link.Take(new[] { Fader(MixControl.Volume, 0, 0) });
        bench.Filling.Fill(said: true);

        Assert.Equal(2, bench.Card.Read().Count);
        Assert.Single(bench.Block.Templates);
    }

    /// <summary>And one taken off leaves it.</summary>
    [Fact]
    public void A_fader_taken_off_leaves_the_card()
    {
        var bench = new Bench(Fader(MixControl.Volume, 0, 0));

        Assert.Equal(2, bench.Card.Read().Count);

        bench.Link.Forget("Desk One");
        bench.Filling.Fill(said: true);

        Assert.Single(bench.Card.Read());
        Assert.Empty(bench.Block.Templates);
    }

    /// <summary>
    /// **Picking a desk lays its strips back down**, and it goes through the template rather
    /// than through the links it was cut from.
    /// </summary>
    /// <remarks>
    /// Which is the whole of what the read path is: what the face has is a template, and the one
    /// way back to links is the door an import goes through. The strip each control is on is
    /// written on its own line here rather than in the target, since the mixer names none, so a
    /// round trip that lost it would quietly point every fader at strip one.
    /// </remarks>
    [Fact]
    public void Picking_a_desk_lays_its_strips_back_down()
    {
        var bench = new Bench(
            Fader(MixControl.Volume, 0, 0),
            Fader(MixControl.Volume, 2, 1));

        bench.Desk.Clear();

        var offer = bench.Card.Read().First(one => one.Said.StartsWith("Desk One", System.StringComparison.Ordinal));

        offer.Chosen!();

        Assert.Equal(2, bench.Desk.Count);
        Assert.Equal(new[] { 0, 2 }, bench.Desk.Select(one => one.Track).OrderBy(one => one).ToArray());
        Assert.All(bench.Desk, one => Assert.Equal(ControlKind.Mix, one.Kind));
    }

    /// <summary>
    /// **A controller that is not plugged in keeps the name the template carried.**
    /// </summary>
    /// <remarks>
    /// The rule a link has always kept about a device left in the other room, and it is the one
    /// thing in a template that does not travel: a file names the controller as its profile calls
    /// it, and what port that is is a fact about the machine it arrives on. There are no ports
    /// here, so nothing is found and the links wait.
    /// </remarks>
    [Fact]
    public void A_desk_that_is_not_plugged_in_still_lays_down_and_waits()
    {
        var bench = new Bench(Fader(MixControl.Volume, 0, 0));

        bench.Desk.Clear();

        bench.Card.Read().First(one => one.Said.StartsWith("Desk One", System.StringComparison.Ordinal)).Chosen!();

        Assert.Equal("Desk One", Assert.Single(bench.Desk).Device);
    }
}
