using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Midi;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// One row in the MIDI device list: a controller and the two jobs it can be given.
/// </summary>
public sealed partial class MidiDeviceViewModel : ObservableObject
{
    private readonly Action<MidiDeviceViewModel> _roleChanged;

    // The checkboxes are set from the stored role while the row is being built, and that must
    // not read back as the user changing something.
    private readonly bool _loaded;

    public string Name { get; }

    /// <summary>False for a device that is bound but not plugged in right now.</summary>
    public bool IsConnected { get; }

    [ObservableProperty] private bool drivesPads;
    [ObservableProperty] private bool drivesTracker;

    public MidiDeviceViewModel(string name, bool isConnected, MidiDeviceRole role, Action<MidiDeviceViewModel> roleChanged)
    {
        Name = name;
        IsConnected = isConnected;
        _roleChanged = roleChanged;

        drivesPads = (role & MidiDeviceRole.Pads) != 0;
        drivesTracker = (role & MidiDeviceRole.Tracker) != 0;

        _loaded = true;
    }

    public MidiDeviceRole Role =>
        (DrivesPads ? MidiDeviceRole.Pads : MidiDeviceRole.None) |
        (DrivesTracker ? MidiDeviceRole.Tracker : MidiDeviceRole.None);

    partial void OnDrivesPadsChanged(bool value) => NotifyRoleChanged();
    partial void OnDrivesTrackerChanged(bool value) => NotifyRoleChanged();

    private void NotifyRoleChanged()
    {
        if (_loaded) _roleChanged(this);
    }
}
