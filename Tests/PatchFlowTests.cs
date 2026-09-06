using System;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;
using JingleBox2.Views;
using JingleBox2.Views.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which cables are carrying audio, and what the two kinds of wire are painted in.
/// </summary>
/// <remarks>
/// A cable that is carrying something is drawn solid and a quiet one dashed, which is what makes
/// the patchbay a picture of what this application is doing rather than a diagram of how it is
/// wired. The mapping is asked here because it is the half that can be wrong without anybody
/// seeing: a wire that stays dashed while a show is going out reads as a path that is not
/// working.
/// </remarks>
public class PatchFlowTests
{
    private readonly IPatchFlow _flow = new PatchFlow();
    private readonly IPatchColours _colours = new PatchColours();

    /// <summary>Nothing at all is sounding.</summary>
    private static readonly PatchSignals Silent = new(false, false, false, false, false);

    /// <summary>A cable from one block to another, as the graph would make it.</summary>
    private static PatchLink Cable(string from, string to) =>
        new(new PatchPort(from, "out", PatchSide.Out, PatchChannels.Stereo),
            new PatchPort(to, "in", PatchSide.In, PatchChannels.Stereo));

    /// <summary>With nothing sounding, no cable is live.</summary>
    [Fact]
    public void Nothing_sounding_leaves_every_cable_quiet()
    {
        var links = new[] { Cable("firefox", "record"), Cable("tracker", "mixer"), Cable("mixer", "output") };

        Assert.Empty(_flow.Live(links, Silent));
    }

    /// <summary>Anything arriving at the recorder makes the cable into it live.</summary>
    [Fact]
    public void Sound_arriving_lights_the_cable_into_the_recorder()
    {
        var link = Cable("firefox", "record");

        var live = _flow.Live(new[] { link }, Silent with { Input = true });

        Assert.Equal(link, Assert.Single(live));
    }

    /// <summary>A take being auditioned lights the recorder's cable into the desk and no other.</summary>
    [Fact]
    public void A_take_lights_the_recorder_into_the_desk()
    {
        var links = new[] { Cable("record", "mixer"), Cable("tracker", "mixer"), Cable("fire", "mixer") };

        var live = _flow.Live(links, Silent with { Takes = true });

        Assert.Equal(Cable("record", "mixer"), Assert.Single(live));
    }

    /// <summary>The song playing lights the tracker's cable and nothing else.</summary>
    [Fact]
    public void The_song_lights_the_tracker()
    {
        var links = new[] { Cable("tracker", "mixer"), Cable("fire", "mixer") };

        var live = _flow.Live(links, Silent with { Tracker = true });

        Assert.Equal(Cable("tracker", "mixer"), Assert.Single(live));
    }

    /// <summary>A pad lights the pads' cable.</summary>
    [Fact]
    public void A_pad_lights_the_pads()
    {
        var links = new[] { Cable("tracker", "mixer"), Cable("fire", "mixer") };

        var live = _flow.Live(links, Silent with { Pads = true });

        Assert.Equal(Cable("fire", "mixer"), Assert.Single(live));
    }

    /// <summary>Whatever is leaving lights the cable out of the desk.</summary>
    [Fact]
    public void What_is_leaving_lights_the_way_out()
    {
        var live = _flow.Live(new[] { Cable("mixer", "output") }, Silent with { Output = true });

        Assert.Single(live);
    }

    /// <summary>Two paths sounding at once light both.</summary>
    [Fact]
    public void Two_things_sounding_light_both()
    {
        var links = new[] { Cable("tracker", "mixer"), Cable("fire", "mixer"), Cable("mixer", "output") };

        var live = _flow.Live(links, Silent with { Tracker = true, Pads = true, Output = true });

        Assert.Equal(3, live.Count);
    }

    /// <summary>A cable between two things that are nothing to do with us stays quiet.</summary>
    /// <remarks>
    /// There is nothing to know about it: what the machine's own programs are doing to each
    /// other is not something this application measures, and a wire drawn solid on a guess would
    /// be saying something it cannot know.
    /// </remarks>
    [Fact]
    public void A_cable_that_misses_us_stays_quiet()
    {
        var live = _flow.Live(
            new[] { Cable("firefox", "speech-dispatcher") },
            new PatchSignals(true, true, true, true, true));

        Assert.Empty(live);
    }

    /// <summary>No cables at all, and none handed over, are both ordinary answers.</summary>
    [Fact]
    public void Nothing_to_read_is_an_ordinary_answer()
    {
        Assert.Empty(_flow.Live(Array.Empty<PatchLink>(), Silent));
        Assert.Empty(_flow.Live(null!, Silent));
    }

    /// <summary>The counter colour is opposite the one it was given.</summary>
    [Fact]
    public void The_counter_colour_is_across_the_wheel()
    {
        var accent = Color.FromRgb(0xFB, 0x8C, 0x00);

        var counter = _colours.Counter(accent);

        double one = accent.ToHsv().H;
        double other = counter.ToHsv().H;

        Assert.Equal(180, Math.Abs(one - other), 1);
    }

    /// <summary>And it keeps the theme's own strength, so it belongs to that palette.</summary>
    [Fact]
    public void The_counter_colour_belongs_to_the_same_theme()
    {
        var accent = Color.FromRgb(0x0B, 0x6B, 0xFF);

        var counter = _colours.Counter(accent);

        Assert.Equal(accent.ToHsv().S, counter.ToHsv().S, 3);
        Assert.Equal(accent.ToHsv().V, counter.ToHsv().V, 3);
        Assert.Equal(accent.A, counter.A);
    }

    /// <summary>Turning it twice comes home.</summary>
    [Fact]
    public void Twice_round_comes_home()
    {
        var accent = Color.FromRgb(0xFB, 0x8C, 0x00);

        var there = _colours.Counter(_colours.Counter(accent));

        Assert.True(Math.Abs(accent.R - there.R) <= 1);
        Assert.True(Math.Abs(accent.G - there.G) <= 1);
        Assert.True(Math.Abs(accent.B - there.B) <= 1);
    }

    /// <summary>
    /// The patchbay is panned by the same press every other picture in this application is.
    /// </summary>
    /// <remarks>
    /// It is the waveform's own rule rather than a second one, so the two cannot drift; what is
    /// pinned here is that the gesture somebody learns in the trim dialog is the one that works
    /// on the patchbay. Ctrl and Shift together is the one that was asked for and it matches,
    /// since it holds both.
    /// </remarks>
    [Fact]
    public void The_page_is_moved_by_the_same_press_as_every_other_picture()
    {
        var press = new JingleBox2.Rack.Controls.WaveformPress();

        Assert.True(press.MeansPan(false, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.True(press.MeansPan(false, KeyModifiers.Control));
        Assert.True(press.MeansPan(false, KeyModifiers.Shift));
        Assert.True(press.MeansPan(true, KeyModifiers.None));
    }

    /// <summary>And a plain press is not that, since a plain press already means something here.</summary>
    /// <remarks>
    /// A block is dragged by a plain press and a dot starts a cable with one, so a page that
    /// panned on a plain drag would have no way left to move a block.
    /// </remarks>
    [Fact]
    public void A_plain_press_does_not_move_the_page()
    {
        Assert.False(new JingleBox2.Rack.Controls.WaveformPress().MeansPan(false, KeyModifiers.None));
    }

    /// <summary>A grey has nothing opposite it and is left alone.</summary>
    /// <remarks>
    /// A theme can have a colourless accent, and turning a grey produces another grey: two wires
    /// the same colour reads as the distinction being broken rather than as there being none.
    /// Left as it is, both wires are that grey and only the dashes tell them apart, which is
    /// honest.
    /// </remarks>
    [Fact]
    public void A_grey_has_nothing_opposite_it()
    {
        var grey = Color.FromRgb(0x80, 0x80, 0x80);

        Assert.Equal(grey, _colours.Counter(grey));
    }
}
