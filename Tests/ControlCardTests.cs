using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a list of links is cut into, which is one card per thing the hardware is pointed at.
/// </summary>
/// <remarks>
/// The pair of headings on a card, the target and the controller inside it, is a template: what
/// one controller does to one machine, one effect or one mixer strip. So the cutting is not a
/// drawing decision that can be got roughly right. Two machines sharing a card, or one machine
/// split across two, is a template that means nothing to whoever it is handed to.
///
/// Everything here is worked out from the mappings alone, which is why it can be asked without
/// a window, a controller or a song.
/// </remarks>
public class ControlCardTests
{
    /// <summary>The rule the page and the file both read, which is why there is one of it.</summary>
    private static readonly ILinkTargets Targets = new LinkTargets();

    /// <summary>A knob on a machine, as the panel writes one down.</summary>
    /// <param name="machine">The machine's id.</param>
    /// <param name="named">Its name, or nothing for a link made before that was kept.</param>
    /// <param name="key">Which parameter.</param>
    private static ControlMapping OnMachine(string machine, string named, string key) => new()
    {
        Kind = ControlKind.SoundDevice,
        Machine = machine,
        Key = key,
        Owner = named,
        Name = (named.Length > 0 ? named : "OddSkilla") + " " + key
    };

    /// <summary>A machine's knobs and its buttons are the same machine, so they share a card.</summary>
    [Fact]
    public void A_machines_knobs_and_its_buttons_are_one_card()
    {
        var knob = OnMachine("machine.oddskilla", "OddSkilla", "cutoff");

        var button = new ControlMapping
        {
            Kind = ControlKind.Action,
            Machine = "machine.oddskilla",
            Key = "preset_next",
            Owner = "OddSkilla",
            Name = "OddSkilla preset next"
        };

        Assert.Equal(Targets.KeyOf(knob), Targets.KeyOf(button));
    }

    /// <summary>
    /// A link made on a track and one made on the rack are the same card.
    /// </summary>
    /// <remarks>
    /// The whole of what makes a template rather than a per-song layout. A knob pointed at
    /// OddSkilla's cutoff on track three and one pointed at the same knob on the rack are the
    /// same sentence, because the link writes the machine's id and the parameter key and never
    /// the track or the instrument's own id. So both gestures fill one card, and either of them
    /// displaces the other.
    /// </remarks>
    [Fact]
    public void The_same_knob_on_a_track_and_on_the_rack_are_one_card()
    {
        var rack = OnMachine("machine.oddskilla", "OddSkilla", "cutoff");

        var track = new ControlMapping
        {
            Kind = ControlKind.SoundDevice,
            Scope = ControlScope.Focused,
            Machine = "machine.oddskilla",
            Key = "cutoff",
            Owner = "OddSkilla",
            Name = "OddSkilla cutoff"
        };

        Assert.Equal(Targets.KeyOf(rack), Targets.KeyOf(track));
    }

    /// <summary>A plugin cannot be pointed at, so a template naming one is refused.</summary>
    /// <remarks>
    /// Counted and left out rather than failing the file, since a template written before this
    /// may carry plugin entries beside machine ones and the machine ones are still good.
    /// </remarks>
    [Fact]
    public void A_template_naming_a_plugin_is_refused()
    {
        Assert.Null(Targets.Point(LinkTargets.Plugin, "vst3:serum", "12", "Serum", "Serum Cutoff"));

        Assert.NotNull(Targets.Point(LinkTargets.SoundDevice, "machine.oddskilla", "cutoff", "OddSkilla", "OddSkilla cutoff"));
    }

    /// <summary>By the id rather than the name, so two machines never share a card.</summary>
    [Fact]
    public void Two_machines_are_two_cards()
    {
        Assert.NotEqual(
            Targets.KeyOf(OnMachine("machine.oddskilla", "OddSkilla", "cutoff")),
            Targets.KeyOf(OnMachine("machine.ouroboros", "Ouroboros", "cutoff")));
    }

    /// <summary>A strip is the card, so everything on it is under one heading.</summary>
    [Fact]
    public void A_strips_level_pan_and_mute_are_one_card()
    {
        var keys = new[] { MixControl.Volume, MixControl.Pan, MixControl.Mute }
            .Select(what => Targets.KeyOf(MixLinks.On(what, 2)))
            .Distinct()
            .ToList();

        Assert.Single(keys);
    }

    /// <summary>
    /// The mixer is one card, however many strips are pointed at.
    /// </summary>
    /// <remarks>
    /// A knob is pointed at the mixer, not at one strip of it: the desk in front of you has a
    /// fader for every strip and what you keep, hand on or lay down again is the whole layout.
    /// Cut by strip it was a card per fader saying the same three words with a number changed,
    /// and a file per fader that nobody could use.
    ///
    /// The master goes in with them, since it is a strip of the same desk.
    /// </remarks>
    [Fact]
    public void The_mixer_is_one_card_however_many_strips()
    {
        var keys = new[]
            {
                MixLinks.On(MixControl.Volume, 0),
                MixLinks.On(MixControl.Volume, 1),
                MixLinks.On(MixControl.Pan, 2),
                MixLinks.On(MixControl.Volume, JingleBox2.Tracker.TrackerPlayer.MasterStrip)
            }
            .Select(Targets.KeyOf)
            .Distinct()
            .ToList();

        Assert.Single(keys);
    }

    /// <summary>And it is headed with the desk rather than with whichever strip came first.</summary>
    [Fact]
    public void The_mixer_card_is_headed_with_the_desk()
    {
        Assert.Equal("Mixer", Targets.TitleOf(new[] { MixLinks.On(MixControl.Volume, 2) }));

        Assert.Equal("Mixer", Targets.TitleOf(
            new[] { MixLinks.On(MixControl.Volume, JingleBox2.Tracker.TrackerPlayer.MasterStrip) }));
    }

    /// <summary>A machine is still its own card, and two machines are still two.</summary>
    /// <remarks>
    /// The mixer is the one kind whose id is left out of its key, so this says the change did not
    /// reach the rest: a knob on OddSkilla has nothing to do with the machine on the next box.
    /// </remarks>
    [Fact]
    public void Two_machines_are_still_two_cards()
    {
        Assert.NotEqual(Targets.KeyOf(OnMachine("machine.one", "One", "cutoff")),
            Targets.KeyOf(OnMachine("machine.two", "Two", "cutoff")));
    }

    /// <summary>
    /// A link made before the name was kept still heads its card with the machine.
    /// </summary>
    /// <remarks>
    /// Every link anybody has already made is one of these, so falling back to the id would
    /// mean this reads as a list of folder names until every link is made again.
    /// </remarks>
    [Fact]
    public void A_link_that_never_wrote_its_machine_down_gives_it_up_anyway()
    {
        Assert.Equal("OddSkilla", Targets.TitleOf(
            new[] { OnMachine("machine.oddskilla", "", "attack") }));
    }

    /// <summary>An underscored key is written out in words, and is read back either way.</summary>
    [Fact]
    public void A_key_with_an_underscore_is_read_back_from_either_spelling()
    {
        var spelled = OnMachine("machine.oddskilla", "", "pitch_env");

        var spaced = new ControlMapping
        {
            Kind = ControlKind.Action,
            Machine = "machine.oddskilla",
            Key = "preset_next",
            Name = "OddSkilla preset next"
        };

        Assert.Equal("OddSkilla", Targets.TitleOf(new[] { spelled }));
        Assert.Equal("OddSkilla", Targets.TitleOf(new[] { spaced }));
    }

    /// <summary>One named link is enough to name the card the unnamed ones sit on.</summary>
    [Fact]
    public void One_link_that_names_the_machine_names_the_whole_card()
    {
        var links = new List<ControlMapping>
        {
            new()
            {
                Kind = ControlKind.Plugin,
                Plugin = "56534558",
                Parameter = 3,
                Name = "Serum 2 Filter Cutoff"
            },
            new()
            {
                Kind = ControlKind.Plugin,
                Plugin = "56534558",
                Parameter = 4,
                Owner = "Serum 2",
                Name = "Serum 2 Filter Res"
            }
        };

        Assert.Equal("Serum 2", Targets.TitleOf(links));
    }

    /// <summary>
    /// An effect that never wrote its name down keeps its id, and that is the whole fallback.
    /// </summary>
    /// <remarks>
    /// A plugin's parameter is named by the plugin and is not written down here, so there is
    /// nothing to read the name back out of. Plain, and still the right card.
    /// </remarks>
    [Fact]
    public void An_effect_with_nothing_to_read_keeps_its_id()
    {
        Assert.Equal("56534558", Targets.TitleOf(new[]
        {
            new ControlMapping
            {
                Kind = ControlKind.Plugin,
                Plugin = "56534558",
                Parameter = 3,
                Name = "Serum 2 Filter Cutoff"
            }
        }));
    }

    /// <summary>The machines first, then the effects, then the mixer, then the transport.</summary>
    [Fact]
    public void The_cards_come_in_the_order_a_sound_is_made_in()
    {
        int machine = Targets.RankOf(OnMachine("machine.oddskilla", "OddSkilla", "cutoff"));
        int effect = Targets.RankOf(new ControlMapping { Kind = ControlKind.Plugin });
        int mixer = Targets.RankOf(MixLinks.On(MixControl.Volume, 0));
        int transport = Targets.RankOf(TransportLinks.For(TransportKey.Play));

        Assert.True(machine < effect);
        Assert.True(effect < mixer);
        Assert.True(mixer < transport);
    }

    /// <summary>The transport is one card however many of its keys are pointed at.</summary>
    [Fact]
    public void Every_transport_key_is_on_one_card()
    {
        var keys = new[] { TransportKey.Play, TransportKey.Stop, TransportKey.Record, TransportKey.Loop }
            .Select(one => Targets.KeyOf(TransportLinks.For(one)))
            .Distinct()
            .ToList();

        Assert.Single(keys);
    }
}
