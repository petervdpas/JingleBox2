using System.Collections.Generic;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The MIDI block in front of a track's chain: what it offers and what picking writes into the strip.
/// </summary>
public class TrackMidiBlockTests
{
    /// <summary>A fresh strip reads as listening on any port with no channel, and sending nowhere.</summary>
    [Fact]
    public void A_fresh_strip_reads_off()
    {
        var (block, _, _) = Block(new TrackMix());

        Assert.Equal(TrackMidiViewModel.AnyPort, block.InPort);
        Assert.Equal(0, block.InChannel);
        Assert.Equal(TrackMidiViewModel.NoPort, block.OutPort);
        Assert.Equal(0, block.OutChannel);
        Assert.Equal(17, block.Channels.Count);
        Assert.Equal("Off", block.Channels[0]);
        Assert.Equal("16", block.Channels[16]);
    }

    /// <summary>Picking writes the strip, announces the change once before it, and says so after.</summary>
    [Fact]
    public void Picking_writes_the_strip_and_announces_it()
    {
        var mix = new TrackMix();
        var (block, changing, changed) = Block(mix);

        block.InPort = "KeyStep Pro MIDI 1";
        block.InChannel = 10;
        block.OutPort = "Synth";
        block.OutChannel = 3;

        Assert.Equal(new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 10 }, mix.MidiIn);
        Assert.Equal(new TrackMidiRoute { Port = "Synth", Channel = 3 }, mix.MidiOut);
        Assert.Equal(4, changing.Count);
        Assert.All(changing, what => Assert.Equal("a track's MIDI", what));
        Assert.Equal(4, changed.Count);
    }

    /// <summary>Any port and no port are stored as no name, and picking what is already there announces nothing.</summary>
    [Fact]
    public void The_two_words_are_empty_and_a_repeat_is_quiet()
    {
        var mix = new TrackMix
        {
            MidiIn = new TrackMidiRoute { Port = "KeyStep Pro MIDI 1", Channel = 2 },
            MidiOut = new TrackMidiRoute { Port = "Synth", Channel = 1 }
        };
        var (block, changing, _) = Block(mix);

        block.InChannel = 2;
        block.InPort = "KeyStep Pro MIDI 1";
        Assert.Empty(changing);

        block.InPort = TrackMidiViewModel.AnyPort;
        block.OutPort = TrackMidiViewModel.NoPort;

        Assert.Equal("", mix.MidiIn.Port);
        Assert.Equal("", mix.MidiOut.Port);
        Assert.Equal(2, changing.Count);
    }

    /// <summary>A port the song names that is not plugged in is still offered, so it shows rather than going blank.</summary>
    [Fact]
    public void A_port_that_is_not_here_is_still_offered()
    {
        var mix = new TrackMix
        {
            MidiIn = new TrackMidiRoute { Port = "Gone In", Channel = 2 },
            MidiOut = new TrackMidiRoute { Port = "Gone Out", Channel = 1 }
        };
        var (block, _, _) = Block(mix);

        Assert.Contains("Gone In", block.InPorts);
        Assert.Contains("Gone Out", block.OutPorts);
        Assert.Equal("Gone In", block.InPort);
        Assert.Equal(TrackMidiViewModel.AnyPort, block.InPorts[0]);
        Assert.Equal(TrackMidiViewModel.NoPort, block.OutPorts[0]);
    }

    /// <summary>A channel past the end, or a port that is nothing, is refused rather than stored.</summary>
    [Fact]
    public void Nonsense_is_refused()
    {
        var mix = new TrackMix();
        var (block, changing, _) = Block(mix);

        block.InChannel = 17;
        block.OutChannel = -1;
        block.InPort = null!;
        block.OutPort = "  ";

        Assert.False(mix.MidiIn.IsOn);
        Assert.False(mix.MidiOut.IsOn);
        Assert.Empty(changing);
    }

    private static (TrackMidiViewModel Block, List<string> Changing, List<bool> Changed) Block(TrackMix mix)
    {
        var changing = new List<string>();
        var changed = new List<bool>();

        var block = new TrackMidiViewModel(mix, new[] { "KeyStep Pro MIDI 1", "MiniLab" }, new[] { "Synth" },
            what => changing.Add(what), () => changed.Add(true));

        return (block, changing, changed);
    }
}
