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
}
