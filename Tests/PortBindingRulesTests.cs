using System;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Views;
using JingleBox2.Views.Interfaces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The small rules that were statics until they had a seam: picking a capture device, reading
/// what the PipeWire tools print, and what a drag carries.
/// </summary>
public class PortBindingRulesTests
{
    private readonly IAudioInputSelector _devices = new AudioInputSelector();
    private readonly IPipeWireGraph _graph = new PipeWireGraph();

    /// <summary>The device somebody chose is kept when it is still plugged in.</summary>
    [Fact]
    public void A_device_still_there_is_kept()
    {
        var there = new[] { "Scarlett 2i2", "Built-in" };

        Assert.Equal("Built-in", _devices.Pick(there, "Built-in"));
    }

    /// <summary>A device that has gone falls to the first one there is.</summary>
    [Fact]
    public void A_device_that_has_gone_falls_to_the_first()
    {
        Assert.Equal("Built-in", _devices.Pick(new[] { "Built-in", "Other" }, "Scarlett 2i2"));
    }

    /// <summary>Nothing chosen falls to the first one there is.</summary>
    [Fact]
    public void Nothing_chosen_falls_to_the_first()
    {
        Assert.Equal("Built-in", _devices.Pick(new[] { "Built-in" }, null));
        Assert.Equal("Built-in", _devices.Pick(new[] { "Built-in" }, ""));
    }

    /// <summary>Nothing plugged in at all falls to what the system resolves.</summary>
    [Fact]
    public void Nothing_at_all_falls_to_the_system()
    {
        Assert.Equal(_devices.Fallback, _devices.Pick(Array.Empty<string>(), null));
        Assert.Equal(_devices.Fallback, _devices.Pick(Array.Empty<string>(), "Scarlett 2i2"));
        Assert.Equal(_devices.Fallback, _devices.Pick(null!, "Scarlett 2i2"));
    }

    /// <summary>
    /// The match is exact, since a device is matched by name and names are not tidied.
    /// </summary>
    /// <remarks>
    /// A near miss falling back to the first device is the right failure: it is audible at once,
    /// where quietly matching a device whose name merely resembles the chosen one would record
    /// the wrong input and say nothing.
    /// </remarks>
    [Fact]
    public void The_match_is_exact()
    {
        Assert.Equal("Built-in", _devices.Pick(new[] { "Built-in", "SCARLETT 2I2" }, "Scarlett 2i2"));
    }

    /// <summary>A port line is a node and a port, split at the first colon.</summary>
    [Fact]
    public void A_port_line_splits_at_the_first_colon()
    {
        var port = _graph.ParsePort("Firefox:output_FL");

        Assert.NotNull(port);
        Assert.Equal("Firefox", port!.Value.Node);
        Assert.Equal("output_FL", port.Value.Port);
    }

    /// <summary>A port whose own name holds a colon keeps it, since only the first counts.</summary>
    /// <remarks>
    /// A MIDI port is the case: the node never holds a colon and the port sometimes does, so
    /// splitting on the last would take the node's name apart.
    /// </remarks>
    [Fact]
    public void A_port_may_hold_a_colon_of_its_own()
    {
        var port = _graph.ParsePort("Midi-Bridge:Launchkey:(capture_0) Launchkey MIDI");

        Assert.NotNull(port);
        Assert.Equal("Midi-Bridge", port!.Value.Node);
        Assert.Equal("Launchkey:(capture_0) Launchkey MIDI", port.Value.Port);
    }

    /// <summary>A listing asked for with ids has the number taken off the front.</summary>
    [Fact]
    public void A_leading_id_is_dropped()
    {
        var port = _graph.ParsePort("  42 Firefox:output_FL");

        Assert.NotNull(port);
        Assert.Equal("Firefox", port!.Value.Node);
        Assert.Equal("output_FL", port.Value.Port);
    }

    /// <summary>Something in front that is not a number is left alone.</summary>
    [Fact]
    public void Something_that_is_not_an_id_is_left_alone()
    {
        var port = _graph.ParsePort("alsa Firefox:output_FL");

        Assert.NotNull(port);
        Assert.Equal("alsa Firefox", port!.Value.Node);
    }

    /// <summary>A line that is not a port at all comes back as nothing.</summary>
    [Fact]
    public void A_line_that_is_not_a_port_is_nothing()
    {
        Assert.Null(_graph.ParsePort(null));
        Assert.Null(_graph.ParsePort(""));
        Assert.Null(_graph.ParsePort("   "));
        Assert.Null(_graph.ParsePort("no colon here"));
    }

    /// <summary>A whole listing comes back in the order the tool printed it.</summary>
    [Fact]
    public void A_listing_keeps_its_order()
    {
        var ports = _graph.ParsePorts("A:out_FL\nB:out_FL\nA:out_FR");

        Assert.Equal(3, ports.Count);
        Assert.Equal("A", ports[0].Node);
        Assert.Equal("B", ports[1].Node);
        Assert.Equal("A", ports[2].Node);
    }

    /// <summary>Blank lines and headings are dropped rather than reported.</summary>
    /// <remarks>
    /// A listing that has grown a line nobody expected should cost the line rather than the
    /// whole reading: the other forty ports are still usable.
    /// </remarks>
    [Fact]
    public void Lines_that_are_not_ports_are_dropped()
    {
        var ports = _graph.ParsePorts("\n\nPorts:\nA:out_FL\n   \nnonsense\nB:out_FR\n");

        Assert.Equal(2, ports.Count);
    }

    /// <summary>Nothing at all is an empty listing rather than a failure.</summary>
    [Fact]
    public void Nothing_at_all_is_an_empty_listing()
    {
        Assert.Empty(_graph.ParsePorts(null));
        Assert.Empty(_graph.ParsePorts(""));
        Assert.Empty(_graph.ParseLinks(null));
        Assert.Empty(_graph.ParseLinks(""));
    }

    /// <summary>Only the stereo audio ports are worth offering.</summary>
    [Fact]
    public void Only_stereo_audio_is_offered()
    {
        Assert.True(_graph.IsStereoAudio("output_FL"));
        Assert.True(_graph.IsStereoAudio("output_FR"));
        Assert.True(_graph.IsStereoAudio("capture_FL"));

        Assert.False(_graph.IsStereoAudio(null));
        Assert.False(_graph.IsStereoAudio(""));
        Assert.False(_graph.IsStereoAudio("output_MONO"));
    }

    /// <summary>A port says which side it is, for pairing it with the capture's.</summary>
    [Fact]
    public void A_port_says_which_side_it_is()
    {
        Assert.Equal("FR", _graph.Channel("output_FR"));
        Assert.Equal("FL", _graph.Channel("output_FL"));
    }

    /// <summary>A dragged track and a dragged instrument carry the number they were given.</summary>
    [Fact]
    public void A_drag_carries_the_number_it_was_given()
    {
        IDragPayload tracks = new TrackDragData();
        IDragPayload instruments = new InstrumentDragData();

        Assert.Equal(3, tracks.IndexFrom(tracks.For(3)));
        Assert.Equal(0, tracks.IndexFrom(tracks.For(0)));
        Assert.Equal(7, instruments.IndexFrom(instruments.For(7)));
    }

    /// <summary>
    /// Neither can read the other's, which is the whole reason they are separate formats.
    /// </summary>
    /// <remarks>
    /// A drop tells them apart by asking rather than by guessing from what happens to be under
    /// the pointer: dragging an instrument onto a track points that track at it, and dragging a
    /// track moves the track.
    /// </remarks>
    [Fact]
    public void Neither_drag_can_read_the_others()
    {
        IDragPayload tracks = new TrackDragData();
        IDragPayload instruments = new InstrumentDragData();

        Assert.Equal(-1, tracks.IndexFrom(instruments.For(3)));
        Assert.Equal(-1, instruments.IndexFrom(tracks.For(3)));
    }

    /// <summary>An empty hand is minus one rather than a crash or a nought.</summary>
    [Fact]
    public void An_empty_hand_is_minus_one()
    {
        IDragPayload tracks = new TrackDragData();

        Assert.Equal(-1, tracks.IndexFrom(null));
    }
}
