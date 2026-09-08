using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Records;
using JingleBox2.ViewModels.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which of this application's own sources may be patched into the recorder's input, and what
/// happens to the cable once it is drawn.
/// </summary>
/// <remarks>
/// **Everything else in the picture is read and this one part is decided.** The pads reach the
/// desk because that is what a desk is, and drawing that cable differently would be lying about
/// the engine. Whether the song or the pads are also going into the recorder is nobody's business
/// but the person at the desk, so it is the one cable between our own blocks that may be drawn,
/// and the one that has to still be there in the morning.
/// </remarks>
public sealed class PatchedInTests
{
    /// <summary>The rule under test.</summary>
    private readonly PatchWiring _wiring = new();

    /// <summary>The recorder's input, which is where these cables land.</summary>
    private static PatchPort Capture =>
        new(PatchNodes.Record, PatchPorts.Capture, PatchSide.In, PatchChannels.Stereo);

    /// <summary>What the song sums to.</summary>
    private static PatchPort Song =>
        new(PatchNodes.Song, PatchPorts.Song, PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>What the pads sum to.</summary>
    private static PatchPort Pads =>
        new(PatchNodes.Fire, PatchPorts.Pads, PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>What the whole desk sums to, which is the one that would howl.</summary>
    private static PatchPort Desk =>
        new(PatchNodes.Mixer, PatchPorts.Master, PatchSide.Out, PatchChannels.Stereo, Fixed: true);

    /// <summary>Where the song arrives on the desk.</summary>
    private static PatchPort DeskSong =>
        new(PatchNodes.Mixer, PatchPorts.Song, PatchSide.In, PatchChannels.Stereo, Fixed: true);

    /// <summary>A program on the machine, which is not ours and never fixed.</summary>
    private static PatchPort Browser =>
        new("Firefox", PatchPorts.Out, PatchSide.Out, PatchChannels.Stereo);

    /// <summary>The song goes into the recorder.</summary>
    [Fact]
    public void The_song_may_be_patched_into_the_input()
    {
        Assert.True(_wiring.Allowed(Song, Capture));
    }

    /// <summary>So do the pads.</summary>
    [Fact]
    public void The_pads_may_be_patched_into_the_input()
    {
        Assert.True(_wiring.Allowed(Pads, Capture));
    }

    /// <summary>
    /// And it does not matter which end the hand started from, since a cable is dragged in
    /// whichever direction is convenient.
    /// </summary>
    [Fact]
    public void The_cable_may_be_drawn_from_either_end()
    {
        Assert.True(_wiring.Allowed(Capture, Song));
    }

    /// <summary>
    /// The desk's own master may not, because that one is everything summed and the input is in
    /// it: patched back it would be the input arriving into itself.
    /// </summary>
    [Fact]
    public void The_desk_may_not_be_patched_into_the_input()
    {
        Assert.False(_wiring.Allowed(Desk, Capture));
    }

    /// <summary>And it may be put back on the desk, since a source has to go somewhere.</summary>
    [Fact]
    public void The_song_may_be_put_back_on_the_desk()
    {
        Assert.True(_wiring.Allowed(Song, DeskSong));
    }

    /// <summary>But only onto its own point, since the desk's points are not interchangeable.</summary>
    [Fact]
    public void A_source_may_not_be_dropped_on_another_sources_point()
    {
        var deskPads = new PatchPort(PatchNodes.Mixer, PatchPorts.Pads, PatchSide.In, PatchChannels.Stereo, Fixed: true);

        Assert.False(_wiring.Allowed(Song, deskPads));
    }

    /// <summary>The rest of our own wiring is still nobody's to take apart.</summary>
    [Fact]
    public void The_wiring_of_the_desk_itself_stays_fixed()
    {
        var deskTakes = new PatchPort(PatchNodes.Mixer, PatchPorts.Takes, PatchSide.In, PatchChannels.Stereo, Fixed: true);
        var recordOut = new PatchPort(PatchNodes.Record, PatchPorts.Takes, PatchSide.Out, PatchChannels.Stereo, Fixed: true);

        Assert.False(_wiring.Allowed(recordOut, deskTakes));
    }

    /// <summary>And the input's other point is not where these land.</summary>
    [Fact]
    public void The_song_may_not_be_patched_onto_the_recorders_output()
    {
        var takes = new PatchPort(PatchNodes.Record, PatchPorts.Takes, PatchSide.In, PatchChannels.Stereo, Fixed: true);

        Assert.False(_wiring.Allowed(Song, takes));
    }

    /// <summary>A program on the machine reaches the input exactly as it always did.</summary>
    [Fact]
    public void A_program_still_reaches_the_input()
    {
        Assert.True(_wiring.Allowed(Browser, Capture));
    }

    /// <summary>Two points on one block are refused, which is the connection made by accident.</summary>
    [Fact]
    public void A_block_cannot_be_joined_to_itself()
    {
        var into = new PatchPort(PatchNodes.Record, PatchPorts.Capture, PatchSide.In, PatchChannels.Stereo);
        var outOf = new PatchPort(PatchNodes.Record, PatchPorts.Takes, PatchSide.Out, PatchChannels.Stereo);

        Assert.False(_wiring.Allowed(outOf, into));
    }

    /// <summary>Two outputs are not a cable.</summary>
    [Fact]
    public void Two_ends_of_the_same_kind_are_refused()
    {
        Assert.False(_wiring.Allowed(Song, Pads));
    }

    /// <summary>A point with no block behind it is refused rather than drawn.</summary>
    [Fact]
    public void A_point_naming_no_block_is_refused()
    {
        var nowhere = new PatchPort("", PatchPorts.Out, PatchSide.Out, PatchChannels.Stereo);

        Assert.False(_wiring.Allowed(nowhere, Capture));
    }

    /// <summary>
    /// A hand may take hold of one of ours that feeds the recorder, since a drag has to be able
    /// to start before it can be judged where it lands.
    /// </summary>
    [Fact]
    public void A_source_of_ours_may_be_taken_hold_of()
    {
        Assert.True(_wiring.Wirable(Song));
        Assert.True(_wiring.Wirable(Pads));
    }

    /// <summary>
    /// And the rest of our own wiring may not, so a cable that could never be put down anywhere
    /// cannot be picked up in the first place.
    /// </summary>
    /// <remarks>
    /// This is also what stops the engine's own cables being pulled out: a point that could never
    /// be one end of a cable somebody drew cannot be one they undraw, and dropping a picked-up
    /// cable on nothing is what unplugs it.
    /// </remarks>
    [Fact]
    public void The_rest_of_our_wiring_may_not_be_taken_hold_of()
    {
        var deskTakes = new PatchPort(PatchNodes.Mixer, PatchPorts.Takes, PatchSide.In, PatchChannels.Stereo, Fixed: true);

        Assert.False(_wiring.Wirable(Desk));
        Assert.False(_wiring.Wirable(deskTakes));
    }

    /// <summary>The desk's own song and pads points do answer, or the cable could never go back.</summary>
    [Fact]
    public void Where_a_source_belongs_may_be_taken_hold_of()
    {
        Assert.True(_wiring.Wirable(DeskSong));
    }

    /// <summary>Anything that is not fixed may be, which is every source on the machine.</summary>
    [Fact]
    public void A_point_that_is_not_fixed_may_be_taken_hold_of()
    {
        Assert.True(_wiring.Wirable(Browser));
        Assert.True(_wiring.Wirable(Capture));
    }

    /// <summary>A point naming no block may not, whichever question is asked of it.</summary>
    [Fact]
    public void A_point_naming_no_block_may_not_be_taken_hold_of()
    {
        Assert.False(_wiring.Wirable(new PatchPort("", PatchPorts.Out, PatchSide.Out, PatchChannels.Stereo)));
    }

    /// <summary>What is kept remembers one cable once, however many times it is drawn.</summary>
    [Fact]
    public void One_cable_is_kept_once()
    {
        var kept = new Kept();

        kept.Add(PatchNodes.Song);
        kept.Add(PatchNodes.Song);

        Assert.Single(kept.Sources);
        Assert.True(kept.Holds(PatchNodes.Song));
    }

    /// <summary>Pulling out one that was never in changes nothing.</summary>
    [Fact]
    public void Pulling_out_what_was_never_in_does_nothing()
    {
        var kept = new Kept();

        kept.Add(PatchNodes.Song);
        kept.Remove(PatchNodes.Fire);

        Assert.Single(kept.Sources);
        Assert.False(kept.Holds(PatchNodes.Fire));
    }

    /// <summary>
    /// A source moved to the recorder goes there **instead of** to the desk, which is the whole
    /// rule: two cables would put it on the master twice, the second a capture buffer late, which
    /// is a comb filter rather than a mix.
    /// </summary>
    [Fact]
    public void A_source_moved_to_the_recorder_leaves_the_desk()
    {
        var scene = Drawn(PatchNodes.Song);

        Assert.Contains(scene.Links, l => l.From.Node == PatchNodes.Song && l.To.Node == PatchNodes.Record);
        Assert.DoesNotContain(scene.Links, l => l.From.Node == PatchNodes.Song && l.To.Node == PatchNodes.Mixer);
    }

    /// <summary>And the one that was not moved stays exactly where it was.</summary>
    [Fact]
    public void A_source_that_was_not_moved_stays_on_the_desk()
    {
        var scene = Drawn(PatchNodes.Song);

        Assert.Contains(scene.Links, l => l.From.Node == PatchNodes.Fire && l.To.Node == PatchNodes.Mixer);
        Assert.DoesNotContain(scene.Links, l => l.From.Node == PatchNodes.Fire && l.To.Node == PatchNodes.Record);
    }

    /// <summary>The picture, with whichever sources have been moved to the recorder.</summary>
    private static PatchScene Drawn(params string[] moved) =>
        new PatchGraph().Read(Array.Empty<AudioRoute>(), null, null, new[] { "TR-01" }, moved);

    /// <summary>
    /// And an id naming no block of ours is passed over rather than drawn from nowhere, which is
    /// what a settings file written by a later version looks like from here.
    /// </summary>
    [Fact]
    public void An_id_we_do_not_know_is_passed_over()
    {
        var scene = Drawn("something-later");

        Assert.Empty(Into(scene.Links));
        Assert.Contains(scene.Links, l => l.From.Node == PatchNodes.Song && l.To.Node == PatchNodes.Mixer);
    }

    /// <summary>
    /// Nothing moved is both of them on the desk, which is every fresh installation and every
    /// settings file written before this existed.
    /// </summary>
    [Fact]
    public void Nothing_moved_leaves_both_on_the_desk()
    {
        var scene = Drawn();

        Assert.Empty(Into(scene.Links));

        Assert.Contains(scene.Links, l => l.From.Node == PatchNodes.Song && l.To.Node == PatchNodes.Mixer);
        Assert.Contains(scene.Links, l => l.From.Node == PatchNodes.Fire && l.To.Node == PatchNodes.Mixer);
    }

    /// <summary>Every cable landing on the recorder's input.</summary>
    private static IEnumerable<PatchLink> Into(IReadOnlyList<PatchLink> links) =>
        links.Where(l =>
            string.Equals(l.To.Node, PatchNodes.Record, StringComparison.Ordinal) &&
            string.Equals(l.To.Name, PatchPorts.Capture, StringComparison.Ordinal));

    /// <summary>What is kept, in memory, so the rule can be asked without a settings file.</summary>
    private sealed class Kept : IPatchedIn
    {
        /// <summary>The ids, in the order they were drawn.</summary>
        private readonly List<string> _sources = new();

        /// <inheritdoc/>
        public IReadOnlyList<string> Sources => _sources;

        /// <inheritdoc/>
        public bool Holds(string node) => _sources.Contains(node, StringComparer.Ordinal);

        /// <inheritdoc/>
        public void Add(string node)
        {
            if (Holds(node)) return;

            _sources.Add(node);
        }

        /// <inheritdoc/>
        public void Remove(string node) => _sources.RemoveAll(one => string.Equals(one, node, StringComparison.Ordinal));
    }
}
