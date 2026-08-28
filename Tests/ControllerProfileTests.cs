using JingleBox2.Controllers;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Controllers.Interfaces;

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
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles = new ControllerProfiles();

    /// <summary>Where a controller's own files live, and how one is matched to a port.</summary>
    private readonly IControllerFolder _folder = new ControllerFolder();

    /// <summary>A MiniLab 3's main port, the one its knobs, faders and pads arrive on.</summary>
    private const string Lab = "Minilab3 MIDI";

    /// <summary>An MPD218, whose file was filled in by hand with the hardware on the desk.</summary>
    private const string Mpd = "MPD218 Port A";

    /// <summary>A nanoKONTROL2, whose every control is fixed and published.</summary>
    private const string Korg = "nanoKONTROL2 MIDI 1";

    /// <summary>A controller nobody has ever written a file for, which is most of them.</summary>
    private const string Nobodys = "Some Other Box Port 1";

    /// <summary>
    /// Reads the controller folder again before each test, so every one of them starts from the
    /// files as they are on disc rather than from whatever the last one left behind.
    /// </summary>
    public ControllerProfileTests() => _profiles.Reload();

    /// <summary>One device turns up under several port names, all of them that device.</summary>
    /// <remarks>
    /// A port is not called the same thing on two operating systems, and plugging a second one in
    /// puts a number in front of the name, which is why the match is a pattern rather than a name.
    /// </remarks>
    [Theory]
    [InlineData("Minilab3 MIDI")]
    [InlineData("Minilab3 MCU/HUI")]
    [InlineData("2- MiniLab3 MIDI")]
    public void A_minilab_is_known_by_any_of_the_names_it_goes_by(string port) =>
        Assert.Equal("MiniLab 3", _profiles.Called(port));

    /// <summary>With no file a device keeps its port's name and nothing else is claimed.</summary>
    [Fact]
    public void A_device_with_no_file_is_called_what_its_port_is_called()
    {
        Assert.Equal(Nobodys, _profiles.Called(Nobodys));
        Assert.False(_profiles.Knows(Nobodys));
    }

    /// <summary>Each of a device's ports has a stated job, which is the answer to why one
    /// controller shows up four times in SETTINGS.</summary>
    /// <remarks>
    /// A device with no file says nothing here, and its one port goes on doing everything.
    /// </remarks>
    [Fact]
    public void Each_port_says_what_it_is_for()
    {
        Assert.Contains("notes", _profiles.PortIs(Lab));
        Assert.Contains("Mackie", _profiles.PortIs("Minilab3 MCU/HUI"));
        Assert.Contains("output", _profiles.PortIs("Minilab3 DIN THRU"));

        Assert.Equal("", _profiles.PortIs(Nobodys));
    }

    /// <summary>
    /// Which of a device's programs is running is inferred from the numbers arriving, and switching
    /// the device moves it along with the first message of the new program.
    /// </summary>
    /// <remarks>
    /// A MiniLab has seven programs and switching rearranges everything it sends, with nothing
    /// announced; the programs do not overlap, so one message is usually enough. This is not a
    /// workaround for a missing question. Arturia's own settings protocol was read with
    /// sysex-controls on 2026-08-27 and its Selected Preset Name field is write-only: the device
    /// will accept a name for the current preset and will not say which one is loaded. Nothing
    /// has been seen yet, so nothing is claimed.
    /// </remarks>
    [Fact]
    public void Which_program_the_device_is_in_is_worked_out_from_what_arrives()
    {
        Assert.Equal("", _profiles.ProgramOn(Lab));

        _profiles.Saw(Lab, 1, 86);
        Assert.Equal("DAW", _profiles.ProgramOn(Lab));

        _profiles.Saw(Lab, 1, 74);
        Assert.Equal("Arturia", _profiles.ProgramOn(Lab));
    }

    /// <summary>
    /// A control is called what is printed beside it on the device, so a link reads Encoder 1
    /// rather than CC 86 ch 1.
    /// </summary>
    [Fact]
    public void A_controller_is_named_as_the_front_of_the_device_names_it()
    {
        _profiles.Saw(Lab, 1, 86);

        Assert.Equal("Encoder 1", _profiles.Named(Lab, 1, 86));
        Assert.Equal("Slider 4", _profiles.Named(Lab, 1, 31));
        Assert.Equal("Play", _profiles.Named(Lab, 1, 107));
    }

    /// <summary>
    /// A control that sends the same number in every program is named without asking which
    /// program is running, since the answer cannot change it.
    /// </summary>
    [Fact]
    public void A_control_true_in_every_program_is_named_whatever_the_device_is_doing()
    {
        Assert.Equal("Mod strip", _profiles.Named(Lab, 1, 1));
    }

    /// <summary>
    /// A number no file accounts for is left unnamed rather than given a guess, and it is then
    /// shown by its number, which is what it always was.
    /// </summary>
    [Fact]
    public void A_number_the_file_does_not_mention_is_named_nothing()
    {
        Assert.Equal("", _profiles.Named(Lab, 1, 3));
        Assert.Equal("", _profiles.Named(Nobodys, 1, 86));
    }

    /// <summary>
    /// A file saying an encoder reports a position has it read as movement between messages,
    /// which beats anything watching the stream could work out.
    /// </summary>
    /// <remarks>
    /// The case the whole thing exists for: an endless encoder walking through its range is
    /// indistinguishable from a fader until it comes round, so watching it says fader and
    /// every session then opens with a hunt using a knob that has no end to hunt from. Nine
    /// links in one song, five of them on encoders, all saved as Takeover.
    /// </remarks>
    [Fact]
    public void An_encoder_that_reports_a_position_is_read_as_movement()
    {
        _profiles.Saw(Lab, 1, 86);

        Assert.Equal(ControlPickup.Endless, _profiles.Pickup(Lab, 1, 86));
    }

    /// <summary>
    /// Saying what an encoder is did not change what the controls beside it are: a fader is
    /// still picked up and a button still jumps.
    /// </summary>
    [Fact]
    public void A_fader_still_picks_up_and_a_button_still_jumps()
    {
        _profiles.Saw(Lab, 1, 86);

        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Lab, 1, 31));
        Assert.Equal(ControlPickup.Jump, _profiles.Pickup(Lab, 1, 107));
    }

    /// <summary>
    /// An encoder that counts notches is left to be watched, because which of the two counting
    /// conventions it uses is not in the file and guessing wrong throws a parameter across its
    /// range.
    /// </summary>
    [Fact]
    public void Nothing_is_claimed_for_an_encoder_that_counts_notches()
    {
        _profiles.Saw(Lab, 1, 74);

        Assert.Null(_profiles.Pickup(Lab, 1, 74));
    }

    /// <summary>
    /// The identity a device answers the universal request with is written down in its file.
    /// </summary>
    /// <remarks>
    /// The one name a device has that does not change with the operating system, the port
    /// it is plugged into, or how many of them are plugged in. Read off the wire: the MPD218
    /// answers F0 7E 00 06 02 47 34 00 19 ... and then its serial number in ASCII.
    /// Manufacturer 47 is Akai, family 0034, member 0019.
    /// </remarks>
    [Fact]
    public void A_device_that_answers_who_it_is_has_that_written_down()
    {
        var known = _profiles.For(Mpd)?.Identity;

        Assert.NotNull(known);
        Assert.Equal("47", known!.Manufacturer);
        Assert.Equal("0034", known.Family);
        Assert.Equal("0019", known.Member);
    }

    /// <summary>
    /// A knob is a fader that happens to be round: it says where it is, so it is picked up.
    /// </summary>
    /// <remarks>
    /// Measured on the device rather than read anywhere: an MPD218's six are sold as 360
    /// degree, and one turned steadily walked 35 to 127 in two seconds and then sat at 127
    /// for another seven while it was still being turned. So they have ends and they say
    /// where they are, which is a fader that happens to be round. 360 degree describes the
    /// absence of a detent, not the behaviour of the value.
    /// <para>
    /// The second half of this asks the same of a bank whose numbers are scattered rather than
    /// consecutive, which is bank A, where Akai stepped around 7 and 10 and 11.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_knob_that_reports_a_position_is_picked_up()
    {
        _profiles.Saw(Mpd, 1, 22);

        Assert.Equal("Knob 1", _profiles.Named(Mpd, 1, 22));
        Assert.Equal("Knob 6", _profiles.Named(Mpd, 1, 27));

        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Mpd, 1, 22));
        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Mpd, 1, 27));

        _profiles.Saw(Mpd, 1, 3);

        Assert.Equal("Knob 3", _profiles.Named(Mpd, 1, 12));
        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Mpd, 1, 15));
    }

    /// <summary>
    /// Which of the three control banks is running is read off the numbers, since CTRL BANK
    /// cycles them with nothing announced on the wire.
    /// </summary>
    /// <remarks>
    /// Three banks, nothing announced, and no screen on the device to say which is running.
    /// The numbers do not overlap, so one message settles it. Bank A is scattered (3, 9, 12,
    /// 13, 14, 15) because Akai stepped around the controllers everybody else uses; B and C are
    /// plain runs from 16 and from 22. Which letter is which was confirmed twice, once by the
    /// owner reading the device and once by the cycle wrapping where it should.
    /// </remarks>
    [Fact]
    public void The_knob_banks_are_told_apart_by_the_numbers_arriving()
    {
        _profiles.Saw(Mpd, 1, 3);
        Assert.Equal("Control Bank A", _profiles.ProgramOn(Mpd));

        _profiles.Saw(Mpd, 1, 17);
        Assert.Equal("Control Bank B", _profiles.ProgramOn(Mpd));

        _profiles.Saw(Mpd, 1, 27);
        Assert.Equal("Control Bank C", _profiles.ProgramOn(Mpd));
    }

    /// <summary>
    /// A number outside everything the file describes is answered the way it would be for a
    /// device with no file at all.
    /// </summary>
    /// <remarks>
    /// Everything this device sends is now described: eighteen knob assignments across three
    /// banks, and the pads, which are notes and so belong nowhere in a file about continuous
    /// controllers. A number outside all of that is a number the device does not send, and it
    /// is answered the way it would be for a device with no file at all.
    /// </remarks>
    [Fact]
    public void A_knob_the_file_does_not_describe_is_left_to_be_watched()
    {
        _profiles.Saw(Mpd, 1, 3);

        Assert.Equal("", _profiles.Named(Mpd, 1, 111));
        Assert.Null(_profiles.Pickup(Mpd, 1, 111));
    }

    /// <summary>
    /// With no file, nothing is said about how a control is read and it goes on being worked out
    /// by watching, which is what every device did before profiles existed.
    /// </summary>
    [Fact]
    public void Nothing_is_claimed_for_a_device_with_no_file()
    {
        Assert.Null(_profiles.Pickup(Nobodys, 1, 86));
        Assert.Null(_profiles.Pickup(Nobodys, 1, 20));
    }

    /// <summary>
    /// A job is ticked once on the device and lands on whichever of its ports really does it.
    /// </summary>
    /// <remarks>
    /// A controller is often several ports with nearly identical names and only one of them
    /// carries the knobs. Mackie Control on a port of its own takes the transport and nothing
    /// else; a screen and an output take no job at all; and a port nothing knows about takes
    /// everything, because a silent refusal would be worse than a port that does too much.
    /// </remarks>
    [Fact]
    public void Which_port_takes_which_job_comes_from_the_file()
    {
        Assert.True(_profiles.PortTakes(Lab, MidiDeviceRole.Controls));
        Assert.True(_profiles.PortTakes(Lab, MidiDeviceRole.Transport));

        Assert.True(_profiles.PortTakes("Minilab3 MCU/HUI", MidiDeviceRole.Transport));
        Assert.False(_profiles.PortTakes("Minilab3 MCU/HUI", MidiDeviceRole.Pads));

        Assert.False(_profiles.PortTakes("Minilab3 ALV", MidiDeviceRole.Controls));
        Assert.False(_profiles.PortTakes("Minilab3 DIN THRU", MidiDeviceRole.Pads));

        Assert.True(_profiles.PortTakes(Nobodys, MidiDeviceRole.Controls));
    }

    /// <summary>The other files here name their devices as the MiniLab's does.</summary>
    [Theory]
    [InlineData("KeyLab mkII 49 MIDI", "KeyLab mkII")]
    [InlineData("KeyLab mkII 88 MCU/HUI", "KeyLab mkII")]
    [InlineData("MPD218 Port A", "MPD218")]
    public void The_other_controllers_with_a_file(string port, string called) =>
        Assert.Equal(called, _profiles.Called(port));

    /// <summary>
    /// A KeyLab is several ports with nearly the same name too, and its file says which is which
    /// even though it names not one of the device's controls.
    /// </summary>
    [Fact]
    public void A_keylabs_ports_say_what_they_are_for()
    {
        Assert.Contains("notes", _profiles.PortIs("KeyLab mkII 49 MIDI"));
        Assert.Contains("Mackie", _profiles.PortIs("KeyLab mkII 49 MCU/HUI"));
    }

    /// <summary>
    /// Every one of a nanoKONTROL2's fifty one controls is named and read the way its file says.
    /// </summary>
    /// <remarks>
    /// The plainest surface anybody makes, and the one where saying which control is which
    /// buys the most: eight sliders, eight knobs, and thirty five buttons that all send
    /// plain controllers and would otherwise be numbers. The numbers are fixed: sliders 0-7,
    /// knobs 16-23, solo 32-39, mute 48-55, rec 64-71, transport 41-46 and 58-62. They hold in
    /// CC mode, which is the factory mode, and mean nothing in the five DAW modes where the same
    /// controls speak that DAW's protocol. This is the first file here written from somebody
    /// else's reading of a device rather than from the wire: Korg's parameter guide has a page
    /// per control type explaining what CC Number means and never prints one, so the numbers
    /// come from Mixxx's mapping for the device as shipped, agreeing with every community list.
    /// <para>A slider is picked up, a knob is picked up, a button jumps.</para>
    /// </remarks>
    [Fact]
    public void A_nanokontrol_is_a_mixer_and_its_knobs_are_not()
    {
        _profiles.Saw(Korg, 1, 0);

        Assert.Equal("nanoKONTROL2", _profiles.Called(Korg));

        Assert.Equal("Slider 1", _profiles.Named(Korg, 1, 0));
        Assert.Equal("Slider 8", _profiles.Named(Korg, 1, 7));
        Assert.Equal("Knob 1", _profiles.Named(Korg, 1, 16));
        Assert.Equal("Mute 3", _profiles.Named(Korg, 1, 50));
        Assert.Equal("Play", _profiles.Named(Korg, 1, 41));

        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Korg, 1, 0));
        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Korg, 1, 16));
        Assert.Equal(ControlPickup.Jump, _profiles.Pickup(Korg, 1, 41));
    }

    /// <summary>
    /// The MiniLab's main knob is named the same in every program, and nothing is claimed about
    /// how it counts.
    /// </summary>
    /// <remarks>
    /// Three controls on one knob, read off the device rather than the wire: turning it,
    /// turning it with Shift held, and pressing it. Identical on two presets, so it sits
    /// with the modulation strip rather than inside a program. All three were missing from the
    /// file altogether until Arturia's settings protocol was read on 2026-08-27: CC 114 turning,
    /// 112 with Shift, 115 pressed. It has no absolute or relative option of its own, unlike the
    /// eight knobs beside it, so nothing is said about its counting; the press is a press, which
    /// is the one thing that can be said for certain.
    /// </remarks>
    [Fact]
    public void The_main_knob_is_the_same_whatever_program_is_running()
    {
        _profiles.Saw(Lab, 1, 86);
        Assert.Equal("Main knob", _profiles.Named(Lab, 1, 114));

        _profiles.Saw(Lab, 1, 74);
        Assert.Equal("Main knob", _profiles.Named(Lab, 1, 114));

        Assert.Equal("Main knob + Shift", _profiles.Named(Lab, 1, 112));
        Assert.Equal("Main knob click", _profiles.Named(Lab, 1, 115));

        Assert.Null(_profiles.Pickup(Lab, 1, 114));

        Assert.Equal(ControlPickup.Jump, _profiles.Pickup(Lab, 1, 115));
    }

    /// <summary>
    /// The file that says a device cannot be described: named, with none of its encoders
    /// claimed.
    /// </summary>
    /// <remarks>
    /// The KeyStep Pro's five encoders have no factory controller number at all: its manual
    /// marks a default for every neighbouring parameter and none for these. So there is
    /// nothing to write down even in principle, and they are learned by touch like any
    /// control on a device nobody has described. Measuring one would report what its owner
    /// assigned rather than a fact about the hardware.
    /// <para>
    /// The one control the manual does fix is the Looper strip, which is picked up rather than
    /// followed. It sends CC 9 with its MIDI send off until a menu is visited, which reads as
    /// broken hardware.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_device_with_no_factory_numbers_has_none_written_down()
    {
        const string Ksp = "KeyStep Pro MIDI 1";

        Assert.Equal("KeyStep Pro", _profiles.Called(Ksp));
        Assert.Equal("", _profiles.Named(Ksp, 1, 74));
        Assert.Null(_profiles.Pickup(Ksp, 1, 74));

        Assert.Equal("Looper strip", _profiles.Named(Ksp, 1, 9));
        Assert.Equal(ControlPickup.Takeover, _profiles.Pickup(Ksp, 1, 9));
    }

    /// <summary>
    /// A file that names a device and none of its controls claims nothing about any of them.
    /// </summary>
    /// <remarks>
    /// The KeyLab file describes a device without naming a single controller, because
    /// Arturia does not publish the numbers for it and nobody here owns one to measure.
    /// Naming the device is the whole of what it does, and a profile is never allowed to
    /// guess. The MPD218 was in the same state until somebody sat down with the hardware
    /// and turned every knob, which is the only way that file could ever have been filled:
    /// the MPD218 answers the universal identity request and refuses Akai's own settings
    /// protocol, so sysex-controls asks and gets ETIMEDOUT.
    /// </remarks>
    [Fact]
    public void A_file_with_no_control_map_claims_nothing_about_any_control()
    {
        Assert.Equal("", _profiles.Named("KeyLab mkII 49 MIDI", 1, 74));
        Assert.Null(_profiles.Pickup("KeyLab mkII 49 MIDI", 1, 74));
    }

    /// <summary>
    /// Naming no controls does not stop a file putting each job on the port that does it.
    /// </summary>
    [Fact]
    public void And_the_transport_still_lands_on_the_right_port()
    {
        Assert.True(_profiles.PortTakes("KeyLab mkII 49 MIDI", MidiDeviceRole.Controls));
        Assert.True(_profiles.PortTakes("KeyLab mkII 49 MCU/HUI", MidiDeviceRole.Transport));
        Assert.False(_profiles.PortTakes("KeyLab mkII 49 MCU/HUI", MidiDeviceRole.Pads));
        Assert.False(_profiles.PortTakes("KeyLab mkII 49 DIN THRU", MidiDeviceRole.Controls));
    }

    /// <summary>
    /// The pattern match itself, including the empty pattern, which matches nothing rather than
    /// everything.
    /// </summary>
    [Theory]
    [InlineData("Minilab3*", "Minilab3 MIDI", true)]
    [InlineData("*MiniLab3*", "2- MiniLab3 MIDI", true)]
    [InlineData("Minilab3*", "MPD218 Port A", false)]
    [InlineData("MIDI", "Minilab3 MIDI", true)]
    [InlineData("", "anything", false)]
    public void A_port_is_matched_by_pattern_because_it_is_not_called_the_same_thing_everywhere(
        string pattern, string port, bool matches) =>
        Assert.Equal(matches, _folder.Like(pattern, port));
}
