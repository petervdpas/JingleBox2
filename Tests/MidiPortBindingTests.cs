using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using Xunit;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Tests;

/// <summary>
/// Which controller is allowed to do what, as plain functions on a list.
/// </summary>
/// <remarks>
/// The class says it exists to be tested away from ports and windows, and this is the test it
/// was written for. The rule that matters most is in <see cref="IMidiPortBindings.AnyRole"/>:
/// a job missing from that mask is a job silently stripped off every device on the way in, and a
/// device given only that job is a binding that quietly disappears. That happened once.
///
/// The order is the life of a binding: what survives being stored and read again, what an older
/// settings file turns into, then setting and clearing a job, and last the merge that produces
/// the list SETTINGS shows.
/// </remarks>
public class MidiPortBindingTests
{
    /// <summary>The rules under test. Holds nothing, so one serves every test in the class.</summary>
    private readonly IMidiPortBindings _bindings = new MidiPortBindings();

    /// <summary>A list holding one device with one job, which is what most of these start from.</summary>
    private static List<MidiPortBinding> One(string device, MidiPortRole role) =>
        new() { new MidiPortBinding { Device = device, Role = role } };

    /// <summary>
    /// Every job in the enum comes back off a stored binding unchanged.
    /// </summary>
    /// <remarks>
    /// The one that has been wrong before: a role added to the enum and not to the mask is
    /// taken off every device the next time the settings are read. Nothing is said, and what
    /// the owner sees is a controller that has forgotten what it was for.
    /// </remarks>
    [Fact]
    public void Every_role_survives_being_stored_and_read_again()
    {
        foreach (var role in new[]
                 {
                     MidiPortRole.Pads, MidiPortRole.Tracker,
                     MidiPortRole.Controls, MidiPortRole.Transport
                 })
        {
            var config = new MidiConfig { Devices = One("Minilab3 MIDI", role) };

            _bindings.Normalize(config);

            Assert.Equal(role, _bindings.RoleFor(config.Devices, "Minilab3 MIDI"));
        }
    }

    /// <summary>A binding that drives nothing is a row in SETTINGS saying nothing, so it goes.</summary>
    [Fact]
    public void A_device_with_no_job_at_all_is_not_kept()
    {
        var config = new MidiConfig { Devices = One("Nothing", MidiPortRole.None) };

        _bindings.Normalize(config);

        Assert.Empty(config.Devices);
    }

    /// <summary>
    /// A device named twice ends up once, holding both jobs.
    /// </summary>
    /// <remarks>
    /// The roles are a mask, so two rows for one port are two halves of one answer rather than
    /// a contradiction, and leaving both in would have the second silently beat the first.
    /// </remarks>
    [Fact]
    public void Two_bindings_for_one_device_become_one_with_both_jobs()
    {
        var config = new MidiConfig
        {
            Devices = new List<MidiPortBinding>
            {
                new() { Device = "Minilab3 MIDI", Role = MidiPortRole.Pads },
                new() { Device = "Minilab3 MIDI", Role = MidiPortRole.Controls }
            }
        };

        _bindings.Normalize(config);

        Assert.Single(config.Devices);
        Assert.Equal(MidiPortRole.Pads | MidiPortRole.Controls,
                     _bindings.RoleFor(config.Devices, "Minilab3 MIDI"));
    }

    /// <summary>
    /// The single input device an older settings file names becomes a binding that drives the pads.
    /// </summary>
    /// <remarks>
    /// That is what the field meant when there was only one, so a file written before several
    /// devices were possible opens with the controller doing what it always did, and the old
    /// field is cleared so it cannot be applied twice.
    /// </remarks>
    [Fact]
    public void A_settings_file_from_before_there_were_several_devices_names_one_that_drove_the_pads()
    {
        var config = new MidiConfig { InputDevice = "MPD218 Port A" };

        _bindings.Normalize(config);

        Assert.Equal(MidiPortRole.Pads, _bindings.RoleFor(config.Devices, "MPD218 Port A"));
        Assert.Null(config.InputDevice);
    }

    /// <summary>
    /// A stored name matches the port however the driver padded it.
    /// </summary>
    /// <remarks>
    /// A device is known here by its port name, so a name that fails to match is a controller
    /// that has lost every job it was given.
    /// </remarks>
    [Fact]
    public void Names_are_matched_however_they_were_padded()
    {
        var bindings = One("MPD218 Port A", MidiPortRole.Pads);

        Assert.Equal(MidiPortRole.Pads, _bindings.RoleFor(bindings, "MPD218 Port A   "));
    }

    /// <summary>Setting a job replaces the row rather than adding a second one for that device.</summary>
    [Fact]
    public void Setting_a_role_replaces_whatever_that_device_had()
    {
        var bindings = One("d", MidiPortRole.Pads);

        _bindings.SetRole(bindings, "d", MidiPortRole.Controls);

        Assert.Equal(MidiPortRole.Controls, _bindings.RoleFor(bindings, "d"));
        Assert.Single(bindings);
    }

    /// <summary>Unticking the last job takes the device off the list rather than storing an empty one.</summary>
    [Fact]
    public void Setting_no_role_takes_the_device_off_the_list()
    {
        var bindings = One("d", MidiPortRole.Pads);

        _bindings.SetRole(bindings, "d", MidiPortRole.None);

        Assert.Empty(bindings);
    }

    /// <summary>
    /// The list SETTINGS shows is what is plugged in first, then what is only remembered.
    /// </summary>
    /// <remarks>
    /// Unplugging a controller must not lose what it was set to drive: it stays on the list,
    /// marked as not connected, so plugging it back in restores it rather than starting again.
    /// </remarks>
    [Fact]
    public void The_list_shows_what_is_plugged_in_and_then_what_is_only_remembered()
    {
        var bindings = new List<MidiPortBinding>
        {
            new() { Device = "Gone", Role = MidiPortRole.Pads },
            new() { Device = "Here", Role = MidiPortRole.Controls }
        };

        var shown = _bindings.Merge(new[] { "Here", "Fresh" }, bindings);

        Assert.Equal(new[] { "Here", "Fresh", "Gone" }, shown.Select(one => one.Device));
        Assert.True(shown[0].IsConnected);
        Assert.True(shown[1].IsConnected);

        Assert.False(shown[2].IsConnected);
        Assert.Equal(MidiPortRole.Pads, shown[2].Role);
    }

    /// <summary>
    /// Asking which devices hold a job lists each once, and the mask of every job lists them all.
    /// </summary>
    /// <remarks>
    /// A device with two jobs would otherwise be opened twice, which is a port opened twice and
    /// every message read twice.
    /// </remarks>
    [Fact]
    public void Devices_with_a_job_are_listed_once_each()
    {
        var bindings = new List<MidiPortBinding>
        {
            new() { Device = "a", Role = MidiPortRole.Pads | MidiPortRole.Controls },
            new() { Device = "b", Role = MidiPortRole.Tracker }
        };

        Assert.Equal(new[] { "a" }, _bindings.DevicesWith(bindings, MidiPortRole.Controls));
        Assert.Equal(new[] { "a", "b" }, _bindings.DevicesWith(bindings, MidiPortBindings.EveryRole));
    }

    /// <summary>Settings with one device holding one job, and the clock arranged as asked.</summary>
    /// <param name="device">The device with a job.</param>
    /// <param name="role">The job it holds.</param>
    /// <param name="source">Whose clock the transport is on.</param>
    /// <param name="port">The port followed, which need not be a device with a job.</param>
    private static MidiConfig Settings(string device, MidiPortRole role,
                                       MidiClockSource source, string? port) =>
        new()
        {
            Devices = One(device, role),
            ClockSource = source,
            ClockPort = port
        };

    /// <summary>On its own clock, what is listened to is exactly what has a job.</summary>
    /// <remarks>
    /// Every installation that has not asked to follow anything, which is every one of them
    /// until somebody ticks the box, so this is the answer that must not have moved.
    /// </remarks>
    [Fact]
    public void On_its_own_clock_only_a_job_is_a_reason_to_listen()
    {
        var cfg = Settings("a", MidiPortRole.Pads, MidiClockSource.Own, port: null);

        Assert.Equal(new[] { "a" }, _bindings.Listening(cfg));
    }

    /// <summary>A port chosen but not followed is not listened to.</summary>
    /// <remarks>
    /// The port is kept when following is turned off rather than cleared, so that it does not
    /// have to be found again, which means a chosen port and a followed one are two different
    /// facts and only the second is a reason to open anything.
    /// </remarks>
    [Fact]
    public void A_port_chosen_while_not_following_is_not_listened_to()
    {
        var cfg = Settings("a", MidiPortRole.Pads, MidiClockSource.Own, "clock");

        Assert.Equal(new[] { "a" }, _bindings.Listening(cfg));
    }

    /// <summary>
    /// The clock being followed is listened to although it has been given no job.
    /// </summary>
    /// <remarks>
    /// The whole of what this member was added for. The picker offers every port on the machine,
    /// so choosing one with no job used to open nothing: no tick arrived and the transport waited
    /// for a clock that was not coming.
    /// </remarks>
    [Fact]
    public void The_clock_being_followed_is_listened_to_without_a_job()
    {
        var cfg = Settings("a", MidiPortRole.Pads, MidiClockSource.Followed, "clock");

        Assert.Equal(new[] { "a", "clock" }, _bindings.Listening(cfg));
    }

    /// <summary>And it is still holding no job, which is the other half of that.</summary>
    /// <remarks>
    /// Listening to a port and pointing it at half the application are two different things.
    /// Were this to grant a job, a keyboard chosen as the clock would start playing the pads as
    /// well, which is nothing anybody asked for.
    /// </remarks>
    [Fact]
    public void Listening_to_a_clock_port_gives_it_no_job()
    {
        var cfg = Settings("a", MidiPortRole.Pads, MidiClockSource.Followed, "clock");

        Assert.Contains("clock", _bindings.Listening(cfg));
        Assert.Equal(MidiPortRole.None, _bindings.RoleFor(cfg.Devices, "clock"));
    }

    /// <summary>A clock port that also has a job is listed once.</summary>
    /// <remarks>
    /// A port opened twice is a port closed once, and the ordinary way to set this up is a
    /// keyboard that is both the clock and the thing the notes are played on.
    /// </remarks>
    [Fact]
    public void A_clock_port_with_a_job_is_listed_once()
    {
        var cfg = Settings("a", MidiPortRole.Tracker, MidiClockSource.Followed, "a");

        Assert.Equal(new[] { "a" }, _bindings.Listening(cfg));
    }

    /// <summary>Told apart without regard to case, like every other port name here.</summary>
    [Fact]
    public void A_clock_port_with_a_job_spelled_differently_is_still_listed_once()
    {
        var cfg = Settings("nanoKONTROL2 _ CTRL", MidiPortRole.Controls,
                           MidiClockSource.Followed, "NANOKONTROL2 _ ctrl");

        Assert.Equal(new[] { "nanoKONTROL2 _ CTRL" }, _bindings.Listening(cfg));
    }

    /// <summary>Following with nothing chosen listens to nothing extra rather than to a blank.</summary>
    /// <remarks>
    /// What the settings hold between ticking the box and choosing a port, and what they hold
    /// for good if nobody ever chooses one. A blank name reaching the opening would be a port
    /// this machine has not got, asked for on every pass.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Following_nothing_listens_to_nothing_extra(string? port)
    {
        var cfg = Settings("a", MidiPortRole.Pads, MidiClockSource.Followed, port);

        Assert.Equal(new[] { "a" }, _bindings.Listening(cfg));
    }

    /// <summary>A port name with room around it is the same port.</summary>
    /// <remarks>
    /// Written down trimmed everywhere else here, so a name arriving with a space on it must not
    /// become a second port beside the one it already names.
    /// </remarks>
    [Fact]
    public void A_clock_port_is_trimmed_before_it_is_compared()
    {
        var cfg = Settings("a", MidiPortRole.Pads, MidiClockSource.Followed, "  a  ");

        Assert.Equal(new[] { "a" }, _bindings.Listening(cfg));
    }

    /// <summary>Following a port on a machine with nothing else bound listens to that alone.</summary>
    [Fact]
    public void A_clock_port_can_be_the_only_thing_listened_to()
    {
        var cfg = new MidiConfig
        {
            ClockSource = MidiClockSource.Followed,
            ClockPort = "clock"
        };

        Assert.Equal(new[] { "clock" }, _bindings.Listening(cfg));
    }

    /// <summary>No settings at all is nothing to listen to rather than a throw.</summary>
    /// <remarks>
    /// Asked while a window is being built, where the settings are read a moment later, so it is
    /// the ordinary case rather than a broken one.
    /// </remarks>
    [Fact]
    public void No_settings_means_nothing_is_listened_to()
    {
        Assert.Empty(_bindings.Listening(null));
    }
}
