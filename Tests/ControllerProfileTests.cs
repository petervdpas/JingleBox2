using JingleBox2.Controllers;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// What a controller file says, and what the application does with it.
/// </summary>
/// <remarks>
/// The rule underneath all of it: a profile may add names, shape and shortcuts and may never add
/// capability. So every one of these has a counterpart asking what happens for a device nobody
/// has written a file for, and the answer is always "the same as before, without the names".
/// </remarks>
public class ControllerProfileTests
{
    private const string Lab = "Minilab3 MIDI";
    private const string Mpd = "MPD218 Port A";

    /// <summary>A controller nobody has ever written a file for, which is most of them.</summary>
    private const string Nobodys = "Some Other Box Port 1";

    public ControllerProfileTests() => ControllerProfiles.Reload();

    [Theory]
    [InlineData("Minilab3 MIDI")]
    [InlineData("Minilab3 MCU/HUI")]
    [InlineData("2- MiniLab3 MIDI")]
    public void A_minilab_is_known_by_any_of_the_names_it_goes_by(string port) =>
        Assert.Equal("MiniLab 3", ControllerProfiles.Called(port));

    [Fact]
    public void A_device_with_no_file_is_called_what_its_port_is_called()
    {
        Assert.Equal(Nobodys, ControllerProfiles.Called(Nobodys));
        Assert.False(ControllerProfiles.Knows(Nobodys));
    }

    [Fact]
    public void Each_port_says_what_it_is_for()
    {
        Assert.Contains("notes", ControllerProfiles.PortIs(Lab));
        Assert.Contains("Mackie", ControllerProfiles.PortIs("Minilab3 MCU/HUI"));
        Assert.Contains("output", ControllerProfiles.PortIs("Minilab3 DIN THRU"));

        Assert.Equal("", ControllerProfiles.PortIs(Nobodys));
    }

    [Fact]
    public void Which_program_the_device_is_in_is_worked_out_from_what_arrives()
    {
        Assert.Equal("", ControllerProfiles.ProgramOn(Lab));

        ControllerProfiles.Saw(Lab, 1, 86);
        Assert.Equal("DAW", ControllerProfiles.ProgramOn(Lab));

        // Switch the device and its first message moves this along with it.
        ControllerProfiles.Saw(Lab, 1, 74);
        Assert.Equal("Arturia", ControllerProfiles.ProgramOn(Lab));
    }

    [Fact]
    public void A_controller_is_named_as_the_front_of_the_device_names_it()
    {
        ControllerProfiles.Saw(Lab, 1, 86);

        Assert.Equal("Encoder 1", ControllerProfiles.Named(Lab, 1, 86));
        Assert.Equal("Slider 4", ControllerProfiles.Named(Lab, 1, 31));
        Assert.Equal("Play", ControllerProfiles.Named(Lab, 1, 107));
    }

    [Fact]
    public void A_control_true_in_every_program_is_named_whatever_the_device_is_doing()
    {
        Assert.Equal("Mod strip", ControllerProfiles.Named(Lab, 1, 1));
    }

    [Fact]
    public void A_number_the_file_does_not_mention_is_named_nothing()
    {
        Assert.Equal("", ControllerProfiles.Named(Lab, 1, 3));
        Assert.Equal("", ControllerProfiles.Named(Nobodys, 1, 86));
    }

    [Fact]
    public void An_encoder_that_reports_a_position_is_read_as_movement()
    {
        ControllerProfiles.Saw(Lab, 1, 86);

        // The case the whole thing exists for: an endless encoder walking through its range is
        // indistinguishable from a fader until it comes round, so watching it says fader and
        // every session then opens with a hunt using a knob that has no end to hunt from.
        Assert.Equal(ControlPickup.Endless, ControllerProfiles.Pickup(Lab, 1, 86));
    }

    [Fact]
    public void A_fader_still_picks_up_and_a_button_still_jumps()
    {
        ControllerProfiles.Saw(Lab, 1, 86);

        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Lab, 1, 31));
        Assert.Equal(ControlPickup.Jump, ControllerProfiles.Pickup(Lab, 1, 107));
    }

    [Fact]
    public void Nothing_is_claimed_for_an_encoder_that_counts_notches()
    {
        ControllerProfiles.Saw(Lab, 1, 74);

        // Which of the two counting conventions it uses is not in the file, and guessing wrong
        // throws a parameter across its range. Left to be watched instead.
        Assert.Null(ControllerProfiles.Pickup(Lab, 1, 74));
    }

    [Fact]
    public void Nothing_is_claimed_for_a_device_with_no_file()
    {
        Assert.Null(ControllerProfiles.Pickup(Nobodys, 1, 86));
        Assert.Null(ControllerProfiles.Pickup(Nobodys, 1, 20));
    }

    [Fact]
    public void Which_port_takes_which_job_comes_from_the_file()
    {
        Assert.True(ControllerProfiles.PortTakes(Lab, MidiDeviceRole.Controls));
        Assert.True(ControllerProfiles.PortTakes(Lab, MidiDeviceRole.Transport));

        // Mackie Control on a port of its own takes the transport and nothing else.
        Assert.True(ControllerProfiles.PortTakes("Minilab3 MCU/HUI", MidiDeviceRole.Transport));
        Assert.False(ControllerProfiles.PortTakes("Minilab3 MCU/HUI", MidiDeviceRole.Pads));

        // A screen and an output take no job at all.
        Assert.False(ControllerProfiles.PortTakes("Minilab3 ALV", MidiDeviceRole.Controls));
        Assert.False(ControllerProfiles.PortTakes("Minilab3 DIN THRU", MidiDeviceRole.Pads));

        // And a port nothing knows about takes everything, because a silent refusal would be
        // worse than a port that does too much.
        Assert.True(ControllerProfiles.PortTakes(Nobodys, MidiDeviceRole.Controls));
    }

    [Theory]
    [InlineData("KeyLab mkII 49 MIDI", "KeyLab mkII")]
    [InlineData("KeyLab mkII 88 MCU/HUI", "KeyLab mkII")]
    [InlineData("MPD218 Port A", "MPD218")]
    public void The_other_controllers_with_a_file(string port, string called) =>
        Assert.Equal(called, ControllerProfiles.Called(port));

    [Fact]
    public void A_keylabs_ports_say_what_they_are_for()
    {
        Assert.Contains("notes", ControllerProfiles.PortIs("KeyLab mkII 49 MIDI"));
        Assert.Contains("Mackie", ControllerProfiles.PortIs("KeyLab mkII 49 MCU/HUI"));
    }

    [Fact]
    public void A_file_with_no_control_map_claims_nothing_about_any_control()
    {
        // Both of these describe a device without naming a single controller, because neither
        // manufacturer publishes the numbers. Naming the device is the whole of what they do,
        // and a profile is never allowed to guess.
        Assert.Equal("", ControllerProfiles.Named("KeyLab mkII 49 MIDI", 1, 74));
        Assert.Null(ControllerProfiles.Pickup("KeyLab mkII 49 MIDI", 1, 74));

        Assert.Equal("", ControllerProfiles.Named("MPD218 Port A", 1, 20));
        Assert.Null(ControllerProfiles.Pickup("MPD218 Port A", 1, 20));
    }

    [Fact]
    public void And_the_transport_still_lands_on_the_right_port()
    {
        Assert.True(ControllerProfiles.PortTakes("KeyLab mkII 49 MIDI", MidiDeviceRole.Controls));
        Assert.True(ControllerProfiles.PortTakes("KeyLab mkII 49 MCU/HUI", MidiDeviceRole.Transport));
        Assert.False(ControllerProfiles.PortTakes("KeyLab mkII 49 MCU/HUI", MidiDeviceRole.Pads));
        Assert.False(ControllerProfiles.PortTakes("KeyLab mkII 49 DIN THRU", MidiDeviceRole.Controls));
    }

    [Theory]
    [InlineData("Minilab3*", "Minilab3 MIDI", true)]
    [InlineData("*MiniLab3*", "2- MiniLab3 MIDI", true)]
    [InlineData("Minilab3*", "MPD218 Port A", false)]
    [InlineData("MIDI", "Minilab3 MIDI", true)]
    [InlineData("", "anything", false)]
    public void A_port_is_matched_by_pattern_because_it_is_not_called_the_same_thing_everywhere(
        string pattern, string port, bool matches) =>
        Assert.Equal(matches, ControllerFolder.Like(pattern, port));
}
