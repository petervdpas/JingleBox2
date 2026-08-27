using System.Collections.Generic;
using System.Linq;
using JingleBox2.Midi;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Which controller is allowed to do what, as plain functions on a list.
/// </summary>
/// <remarks>
/// The class says it exists to be tested away from ports and windows, and this is the test it
/// was written for. The rule that matters most is in <see cref="MidiDeviceBindings.AnyRole"/>:
/// a job missing from that mask is a job silently stripped off every device on the way in, and a
/// device given only that job is a binding that quietly disappears. That happened once.
/// </remarks>
public class MidiDeviceBindingTests
{
    private static List<MidiDeviceBinding> One(string device, MidiDeviceRole role) =>
        new() { new MidiDeviceBinding { Device = device, Role = role } };

    [Fact]
    public void Every_role_survives_being_stored_and_read_again()
    {
        // The one that has been wrong before: a role added to the enum and not to the mask is
        // taken off every device the next time the settings are read.
        foreach (var role in new[]
                 {
                     MidiDeviceRole.Pads, MidiDeviceRole.Tracker,
                     MidiDeviceRole.Controls, MidiDeviceRole.Transport
                 })
        {
            var config = new MidiConfig { Devices = One("Minilab3 MIDI", role) };

            MidiDeviceBindings.Normalize(config);

            Assert.Equal(role, MidiDeviceBindings.RoleFor(config.Devices, "Minilab3 MIDI"));
        }
    }

    [Fact]
    public void A_device_with_no_job_at_all_is_not_kept()
    {
        var config = new MidiConfig { Devices = One("Nothing", MidiDeviceRole.None) };

        MidiDeviceBindings.Normalize(config);

        Assert.Empty(config.Devices);
    }

    [Fact]
    public void Two_bindings_for_one_device_become_one_with_both_jobs()
    {
        var config = new MidiConfig
        {
            Devices = new List<MidiDeviceBinding>
            {
                new() { Device = "Minilab3 MIDI", Role = MidiDeviceRole.Pads },
                new() { Device = "Minilab3 MIDI", Role = MidiDeviceRole.Controls }
            }
        };

        MidiDeviceBindings.Normalize(config);

        Assert.Single(config.Devices);
        Assert.Equal(MidiDeviceRole.Pads | MidiDeviceRole.Controls,
                     MidiDeviceBindings.RoleFor(config.Devices, "Minilab3 MIDI"));
    }

    [Fact]
    public void A_settings_file_from_before_there_were_several_devices_names_one_that_drove_the_pads()
    {
        var config = new MidiConfig { InputDevice = "MPD218 Port A" };

        MidiDeviceBindings.Normalize(config);

        Assert.Equal(MidiDeviceRole.Pads, MidiDeviceBindings.RoleFor(config.Devices, "MPD218 Port A"));
        Assert.Null(config.InputDevice);
    }

    [Fact]
    public void Names_are_matched_however_they_were_padded()
    {
        var bindings = One("MPD218 Port A", MidiDeviceRole.Pads);

        Assert.Equal(MidiDeviceRole.Pads, MidiDeviceBindings.RoleFor(bindings, "MPD218 Port A   "));
    }

    [Fact]
    public void Setting_a_role_replaces_whatever_that_device_had()
    {
        var bindings = One("d", MidiDeviceRole.Pads);

        MidiDeviceBindings.SetRole(bindings, "d", MidiDeviceRole.Controls);

        Assert.Equal(MidiDeviceRole.Controls, MidiDeviceBindings.RoleFor(bindings, "d"));
        Assert.Single(bindings);
    }

    [Fact]
    public void Setting_no_role_takes_the_device_off_the_list()
    {
        var bindings = One("d", MidiDeviceRole.Pads);

        MidiDeviceBindings.SetRole(bindings, "d", MidiDeviceRole.None);

        Assert.Empty(bindings);
    }

    [Fact]
    public void The_list_shows_what_is_plugged_in_and_then_what_is_only_remembered()
    {
        var bindings = new List<MidiDeviceBinding>
        {
            new() { Device = "Gone", Role = MidiDeviceRole.Pads },
            new() { Device = "Here", Role = MidiDeviceRole.Controls }
        };

        var shown = MidiDeviceBindings.Merge(new[] { "Here", "Fresh" }, bindings);

        Assert.Equal(new[] { "Here", "Fresh", "Gone" }, shown.Select(one => one.Device));
        Assert.True(shown[0].IsConnected);
        Assert.True(shown[1].IsConnected);

        // Unplugging a controller must not lose what it was set to drive.
        Assert.False(shown[2].IsConnected);
        Assert.Equal(MidiDeviceRole.Pads, shown[2].Role);
    }

    [Fact]
    public void Devices_with_a_job_are_listed_once_each()
    {
        var bindings = new List<MidiDeviceBinding>
        {
            new() { Device = "a", Role = MidiDeviceRole.Pads | MidiDeviceRole.Controls },
            new() { Device = "b", Role = MidiDeviceRole.Tracker }
        };

        Assert.Equal(new[] { "a" }, MidiDeviceBindings.DevicesWith(bindings, MidiDeviceRole.Controls));
        Assert.Equal(new[] { "a", "b" }, MidiDeviceBindings.DevicesWith(bindings, MidiDeviceBindings.AnyRole));
    }
}
