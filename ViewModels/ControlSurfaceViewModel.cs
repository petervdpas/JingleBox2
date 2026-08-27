using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Controllers;
using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JingleBox2.ViewModels;

/// <summary>
/// One piece of hardware, however many ports it turns out to be.
/// </summary>
/// <remarks>
/// The list under SETTINGS was a row per port, which is what the operating system offers and
/// not what anybody owns. A MiniLab 3 is four rows with four nearly identical names, and a
/// person is left to work out which of them their knobs come out of. Nothing about the names
/// says. The manual says, and there is no manual for most of what people own.
///
/// So the row is the device and the ports are listed under it, each with what it is for, and
/// the jobs are ticked once. Where a job lands is then the profile's business rather than
/// somebody's guess, which is the difference between ticking Transport on a MiniLab and having
/// the transport work, and ticking it on the port whose name mentions Mackie Control and
/// spending an evening wondering why the buttons do nothing.
///
/// A view over the rows rather than a replacement for them. Each port is still a
/// <see cref="MidiDeviceViewModel"/> with its own binding, saved and loaded exactly as before,
/// so nothing underneath this had to change and a device with no profile is a surface with one
/// port that behaves as it always did.
/// </remarks>
public sealed partial class ControlSurfaceViewModel : ObservableObject
{
    private readonly IReadOnlyList<MidiDeviceViewModel> _ports;
    private readonly Action<MidiDeviceViewModel>? _forget;

    /// <summary>True while the ticks are being read off the ports, so they do not write back.</summary>
    private bool _reading;

    public ControlSurfaceViewModel(string name, IReadOnlyList<MidiDeviceViewModel> ports,
                                   Action<MidiDeviceViewModel>? forget = null)
    {
        Name = name;
        _ports = ports;
        _forget = forget;

        Ports = new ObservableCollection<ControlSurfacePort>(
            ports.Select(one => new ControlSurfacePort(one.Name, ControllerProfiles.PortIs(one.Name), one.IsConnected)));

        _reading = true;

        drivesPads = Any(MidiDeviceRole.Pads);
        drivesTracker = Any(MidiDeviceRole.Tracker);
        drivesControls = Any(MidiDeviceRole.Controls);
        drivesTransport = Any(MidiDeviceRole.Transport);

        _reading = false;
    }

    /// <summary>What the hardware is called: its own name where one is known, else the port's.</summary>
    public string Name { get; }

    /// <summary>The ports it presents, and what each is for.</summary>
    public ObservableCollection<ControlSurfacePort> Ports { get; }

    /// <summary>True when more than one, which is the only time they are worth listing.</summary>
    public bool HasPorts => Ports.Count > 1 || Ports.Any(one => one.Note.Length > 0);

    public bool IsConnected => _ports.Any(one => one.IsConnected);

    [ObservableProperty] private bool drivesPads;
    [ObservableProperty] private bool drivesTracker;
    [ObservableProperty] private bool drivesControls;
    [ObservableProperty] private bool drivesTransport;

    partial void OnDrivesPadsChanged(bool value) => Put(MidiDeviceRole.Pads, value);
    partial void OnDrivesTrackerChanged(bool value) => Put(MidiDeviceRole.Tracker, value);
    partial void OnDrivesControlsChanged(bool value) => Put(MidiDeviceRole.Controls, value);
    partial void OnDrivesTransportChanged(bool value) => Put(MidiDeviceRole.Transport, value);

    /// <summary>Only for hardware that is not plugged in. One on the desk comes straight back.</summary>
    public bool CanForget => !IsConnected && _forget is not null;

    public IRelayCommand ForgetCommand => new RelayCommand(() =>
    {
        foreach (var port in _ports.ToList()) _forget?.Invoke(port);
    });

    private bool Any(MidiDeviceRole role) => _ports.Any(one => (one.Role & role) != 0);

    /// <summary>
    /// Puts a job on the ports that can do it, and takes it off the ones that cannot.
    /// </summary>
    /// <remarks>
    /// Off everywhere when it is unticked, including ports the profile would not have chosen.
    /// A layout somebody built by hand before this page existed has to be undoable from it, or
    /// the tick reads as broken while a port nobody can see keeps working.
    /// </remarks>
    private void Put(MidiDeviceRole role, bool wanted)
    {
        if (_reading) return;

        foreach (var port in _ports)
        {
            bool on = wanted && ControllerProfiles.PortTakes(port.Name, role);

            switch (role)
            {
                case MidiDeviceRole.Pads: port.DrivesPads = on; break;
                case MidiDeviceRole.Tracker: port.DrivesTracker = on; break;
                case MidiDeviceRole.Controls: port.DrivesControls = on; break;
                case MidiDeviceRole.Transport: port.DrivesTransport = on; break;
            }
        }
    }
}

/// <summary>One of a device's ports, and what the profile says it is for.</summary>
public sealed class ControlSurfacePort
{
    public ControlSurfacePort(string name, string note, bool isConnected)
    {
        Name = name;
        Note = Trim(name, note);
        IsConnected = isConnected;
    }

    public string Name { get; }

    /// <summary>What it is for, without repeating the device's name on every line.</summary>
    public string Note { get; }

    public bool IsConnected { get; }

    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// Takes the device's name off the front of the note.
    /// </summary>
    /// <remarks>
    /// The same text is used on the old per-port list, where it has to say which device it is
    /// about. Under a heading that already says so it would be the same three words on every
    /// line, which is a column that says nothing.
    /// </remarks>
    private static string Trim(string name, string note)
    {
        if (note.Length == 0) return "";

        int at = note.IndexOf("  ·  ", StringComparison.Ordinal);

        return at >= 0 ? note[(at + 5)..] : "";
    }
}
