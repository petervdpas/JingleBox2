using System;
using System.Collections.Generic;
using JingleBox2.Midi;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// A template laid down has to answer the hardware it was made on.
/// </summary>
/// <remarks>
/// The one thing applying a template is for, and the one thing no test asked. Everything before
/// this said what the links <em>are</em>: that they are cut one per controller per target, that
/// the right ones come back, that nothing is doubled. What decides whether anybody hears it is
/// narrower than any of those, and it is the last comparison in the chain: does a message off the
/// wire find the link that was just laid down.
///
/// It is exactly where a box on two ports bites. A link is kept by the name a profile gives the
/// controller, since that is the one spelling both ports share, and a template that resolved its
/// controller back to a port put one of the two spellings into the link. Turn a knob on the other
/// port and nothing matched: the template applied, said how many controls it had, wrote the
/// links, and moved nothing whatever.
/// </remarks>
public sealed class TemplateLaidDownTests
{
    /// <summary>What the profile calls the box, which is what a link and a template both carry.</summary>
    private const string Box = "MiniLab 3";

    /// <summary>The ports one MiniLab really arrives on here, in the order they are offered.</summary>
    private static readonly string[] Ports = { "Minilab3 ALV", "Minilab3 MIDI" };

    /// <summary>What a profile calls a port, and leaves anything it does not know alone.</summary>
    private static string Called(string port) =>
        port.StartsWith("Minilab3", StringComparison.OrdinalIgnoreCase) ? Box : port;

    /// <summary>A knob on OddSkilla, learned on the box and therefore kept by its name.</summary>
    private static ControlMapping Knob(int cc, string key) => new()
    {
        Device = Box,
        Channel = 1,
        Cc = cc,
        Kind = ControlKind.SoundDevice,
        Machine = "machine.oddskilla",
        Key = key,
        Owner = "OddSkilla",
        Name = "OddSkilla " + key
    };

    /// <summary>One knob being turned, on whichever of the box's ports is delivering.</summary>
    private static MidiMessage Turned(string port, int cc) => new()
    {
        Device = port,
        Type = MidiMessageType.ControlChange,
        Channel = 1,
        Value = cc,
        Data = 64,
        IsOn = true
    };

    /// <summary>Two knobs learned on the box, cut into a template and laid straight back down.</summary>
    /// <param name="ports">What this computer has, which is nothing where the box is unplugged.</param>
    private static ControlTemplateReading Laid(IEnumerable<string>? ports)
    {
        var links = new List<ControlMapping> { Knob(86, "tune"), Knob(87, "cutoff") };

        var templates = new ControlTemplates();

        var template = Assert.Single(templates.Cut(links, Called));

        return templates.Take(template, ports, Called);
    }

    /// <summary>**A knob turned on either port reaches what the template laid down.**</summary>
    /// <remarks>
    /// Both, in one test, because the whole of the fault is that one of the two used to work: a
    /// version that resolves the controller to a port passes on whichever port it happened to
    /// pick and fails on the other, so a test asking about one port says nothing.
    /// </remarks>
    [Theory]
    [InlineData("Minilab3 MIDI")]
    [InlineData("Minilab3 ALV")]
    public void A_knob_on_either_port_answers(string port)
    {
        var reading = Laid(Ports);

        Assert.Equal(2, reading.Links.Count);

        foreach (int cc in new[] { 86, 87 })
            Assert.Contains(reading.Links, one => one.Answers(Turned(port, cc), Called(port)));
    }

    /// <summary>What is written down is the controller's name, which is what every link holds.</summary>
    [Fact]
    public void The_links_name_the_controller_and_never_a_port()
    {
        foreach (var one in Laid(Ports).Links) Assert.Equal(Box, one.Device);
    }

    /// <summary>A controller nobody can see still lays its links down, waiting for it.</summary>
    [Fact]
    public void A_box_that_is_not_plugged_in_still_lays_down()
    {
        var reading = Laid(new[] { "MPD218 Port A" });

        Assert.False(reading.Found);
        Assert.Equal(2, reading.Links.Count);
        Assert.All(reading.Links, one => Assert.Equal(Box, one.Device));
    }

    /// <summary>And one that is plugged in says so, which is what the wording on the line reads.</summary>
    [Fact]
    public void A_box_that_is_plugged_in_says_so()
    {
        Assert.True(Laid(Ports).Found);
    }
}
