using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Midi;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// One row in the MIDI device list: a controller and the jobs it can be given.
/// </summary>
public sealed partial class MidiDeviceViewModel : ObservableObject
{
    private readonly Action<MidiDeviceViewModel> _roleChanged;
    private readonly Action<MidiDeviceViewModel>? _forget;

    // The checkboxes are set from the stored role while the row is being built, and that must
    // not read back as the user changing something.
    private readonly bool _loaded;

    public string Name { get; }

    /// <summary>False for a device that is bound but not plugged in right now.</summary>
    public bool IsConnected { get; }

    [ObservableProperty] private bool drivesPads;
    [ObservableProperty] private bool drivesTracker;

    /// <summary>
    /// Whether this device's knobs and faders move parameters.
    /// </summary>
    /// <remarks>
    /// Apart from the other two because a controller is usually two things at once: the keys
    /// play the tracker and the knobs move the machine, and either half is worth switching off
    /// on its own. A keyboard whose modulation wheel is doing something unwanted is the whole
    /// reason.
    /// </remarks>
    [ObservableProperty] private bool drivesControls;

    /// <summary>
    /// Whether this device's transport buttons work the caps at the top of the window.
    /// </summary>
    /// <remarks>
    /// Its own switch because a controller in a DAW mode is two devices as far as this list is
    /// concerned: the buttons come out one port speaking Mackie Control and everything else out
    /// another. On the port they arrive on, note 94 is the play button and not a note anybody
    /// wants to hear, so the pads and the tracker must not be pointed at it.
    /// </remarks>
    [ObservableProperty] private bool drivesTransport;

    public MidiDeviceViewModel(string name, bool isConnected, MidiDeviceRole role,
                               Action<MidiDeviceViewModel> roleChanged,
                               Action<MidiDeviceViewModel>? forget = null)
    {
        Name = name;
        IsConnected = isConnected;
        _roleChanged = roleChanged;
        _forget = forget;

        drivesPads = (role & MidiDeviceRole.Pads) != 0;
        drivesTracker = (role & MidiDeviceRole.Tracker) != 0;
        drivesControls = (role & MidiDeviceRole.Controls) != 0;
        drivesTransport = (role & MidiDeviceRole.Transport) != 0;

        _loaded = true;
    }

    /// <summary>
    /// Only one that is not plugged in, because that is the only one worth forgetting.
    /// </summary>
    /// <remarks>
    /// A controller sitting on the desk comes straight back the next time the list is read, so
    /// forgetting it would be a button that does nothing you can see. What it is for is the row
    /// left behind by hardware that has gone: a name in a list, with a layout attached to it,
    /// for something you no longer own.
    /// </remarks>
    public bool CanForget => !IsConnected;

    /// <summary>Takes the device off the list, and everything learned on it with it.</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand ForgetCommand =>
        new CommunityToolkit.Mvvm.Input.RelayCommand(() => _forget?.Invoke(this));

    public MidiDeviceRole Role =>
        (DrivesPads ? MidiDeviceRole.Pads : MidiDeviceRole.None) |
        (DrivesTracker ? MidiDeviceRole.Tracker : MidiDeviceRole.None) |
        (DrivesControls ? MidiDeviceRole.Controls : MidiDeviceRole.None) |
        (DrivesTransport ? MidiDeviceRole.Transport : MidiDeviceRole.None);

    partial void OnDrivesPadsChanged(bool value) => NotifyRoleChanged();
    partial void OnDrivesTrackerChanged(bool value) => NotifyRoleChanged();
    partial void OnDrivesControlsChanged(bool value) => NotifyRoleChanged();
    partial void OnDrivesTransportChanged(bool value) => NotifyRoleChanged();

    private void NotifyRoleChanged()
    {
        if (_loaded) _roleChanged(this);
    }
}
