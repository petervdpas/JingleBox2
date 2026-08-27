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
    private const string Korg = "nanoKONTROL2 MIDI 1";

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
    public void A_device_that_answers_who_it_is_has_that_written_down()
    {
        // The one name a device has that does not change with the operating system, the port
        // it is plugged into, or how many of them are plugged in. Read off the wire: the MPD218
        // answers F0 7E 00 06 02 47 34 00 19 ... and then its serial number in ASCII.
        var known = ControllerProfiles.For(Mpd)?.Identity;

        Assert.NotNull(known);
        Assert.Equal("47", known!.Manufacturer);
        Assert.Equal("0034", known.Family);
        Assert.Equal("0019", known.Member);
    }

    [Fact]
    public void A_knob_that_reports_a_position_is_picked_up()
    {
        // Measured on the device rather than read anywhere: an MPD218's six are sold as 360
        // degree, and one turned steadily walked 35 to 127 in two seconds and then sat at 127
        // for another seven while it was still being turned. So they have ends and they say
        // where they are, which is a fader that happens to be round.
        ControllerProfiles.Saw(Mpd, 1, 22);

        Assert.Equal("Knob 1", ControllerProfiles.Named(Mpd, 1, 22));
        Assert.Equal("Knob 6", ControllerProfiles.Named(Mpd, 1, 27));

        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Mpd, 1, 22));
        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Mpd, 1, 27));

        // And the same knob in a bank whose numbers are scattered rather than consecutive,
        // which is bank A, where Akai stepped around 7 and 10 and 11.
        ControllerProfiles.Saw(Mpd, 1, 3);

        Assert.Equal("Knob 3", ControllerProfiles.Named(Mpd, 1, 12));
        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Mpd, 1, 15));
    }

    [Fact]
    public void The_knob_banks_are_told_apart_by_the_numbers_arriving()
    {
        // Three banks, nothing announced, and no screen on the device to say which is running.
        // The numbers do not overlap, so one message settles it.
        ControllerProfiles.Saw(Mpd, 1, 3);
        Assert.Equal("Control Bank A", ControllerProfiles.ProgramOn(Mpd));

        ControllerProfiles.Saw(Mpd, 1, 17);
        Assert.Equal("Control Bank B", ControllerProfiles.ProgramOn(Mpd));

        ControllerProfiles.Saw(Mpd, 1, 27);
        Assert.Equal("Control Bank C", ControllerProfiles.ProgramOn(Mpd));
    }

    [Fact]
    public void A_knob_the_file_does_not_describe_is_left_to_be_watched()
    {
        // Everything this device sends is now described: eighteen knob assignments across three
        // banks, and the pads, which are notes and so belong nowhere in a file about continuous
        // controllers. A number outside all of that is a number the device does not send, and it
        // is answered the way it would be for a device with no file at all.
        ControllerProfiles.Saw(Mpd, 1, 3);

        Assert.Equal("", ControllerProfiles.Named(Mpd, 1, 111));
        Assert.Null(ControllerProfiles.Pickup(Mpd, 1, 111));
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
    public void A_nanokontrol_is_a_mixer_and_its_knobs_are_not()
    {
        // The plainest surface anybody makes, and the one where saying which control is which
        // buys the most: eight sliders, eight knobs, and thirty five buttons that all send
        // plain controllers and would otherwise be numbers.
        ControllerProfiles.Saw(Korg, 1, 0);

        Assert.Equal("nanoKONTROL2", ControllerProfiles.Called(Korg));

        Assert.Equal("Slider 1", ControllerProfiles.Named(Korg, 1, 0));
        Assert.Equal("Slider 8", ControllerProfiles.Named(Korg, 1, 7));
        Assert.Equal("Knob 1", ControllerProfiles.Named(Korg, 1, 16));
        Assert.Equal("Mute 3", ControllerProfiles.Named(Korg, 1, 50));
        Assert.Equal("Play", ControllerProfiles.Named(Korg, 1, 41));

        // A slider is picked up, a knob is picked up, a button jumps.
        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Korg, 1, 0));
        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Korg, 1, 16));
        Assert.Equal(ControlPickup.Jump, ControllerProfiles.Pickup(Korg, 1, 41));
    }

    [Fact]
    public void The_main_knob_is_the_same_whatever_program_is_running()
    {
        // Three controls on one knob, read off the device rather than the wire: turning it,
        // turning it with Shift held, and pressing it. Identical on two presets, so it sits
        // with the modulation strip rather than inside a program.
        ControllerProfiles.Saw(Lab, 1, 86);
        Assert.Equal("Main knob", ControllerProfiles.Named(Lab, 1, 114));

        ControllerProfiles.Saw(Lab, 1, 74);
        Assert.Equal("Main knob", ControllerProfiles.Named(Lab, 1, 114));

        Assert.Equal("Main knob + Shift", ControllerProfiles.Named(Lab, 1, 112));
        Assert.Equal("Main knob click", ControllerProfiles.Named(Lab, 1, 115));

        // Nothing claimed about how it counts: it has no absolute or relative option of its
        // own, unlike the eight knobs beside it.
        Assert.Null(ControllerProfiles.Pickup(Lab, 1, 114));

        // And the press is a press, which is the one thing that can be said for certain.
        Assert.Equal(ControlPickup.Jump, ControllerProfiles.Pickup(Lab, 1, 115));
    }

    [Fact]
    public void A_device_with_no_factory_numbers_has_none_written_down()
    {
        const string Ksp = "KeyStep Pro MIDI 1";

        // The KeyStep Pro's five encoders have no factory controller number at all: its manual
        // marks a default for every neighbouring parameter and none for these. So there is
        // nothing to write down even in principle, and they are learned by touch like any
        // control on a device nobody has described.
        Assert.Equal("KeyStep Pro", ControllerProfiles.Called(Ksp));
        Assert.Equal("", ControllerProfiles.Named(Ksp, 1, 74));
        Assert.Null(ControllerProfiles.Pickup(Ksp, 1, 74));

        // The one control the manual does fix, and it is picked up rather than followed.
        Assert.Equal("Looper strip", ControllerProfiles.Named(Ksp, 1, 9));
        Assert.Equal(ControlPickup.Takeover, ControllerProfiles.Pickup(Ksp, 1, 9));
    }

    [Fact]
    public void A_file_with_no_control_map_claims_nothing_about_any_control()
    {
        // The KeyLab file describes a device without naming a single controller, because
        // Arturia does not publish the numbers for it and nobody here owns one to measure.
        // Naming the device is the whole of what it does, and a profile is never allowed to
        // guess. The MPD218 was in the same state until somebody sat down with the hardware
        // and turned every knob, which is the only way that file could ever have been filled.
        Assert.Equal("", ControllerProfiles.Named("KeyLab mkII 49 MIDI", 1, 74));
        Assert.Null(ControllerProfiles.Pickup("KeyLab mkII 49 MIDI", 1, 74));
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
