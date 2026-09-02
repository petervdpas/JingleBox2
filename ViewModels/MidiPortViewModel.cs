using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Controllers;
using System;
using JingleBox2.Midi.Enums;
using JingleBox2.Controllers.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// One row in the MIDI device list: a controller and the jobs it can be given.
/// </summary>
public sealed partial class MidiPortViewModel : ObservableObject
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles = new ControllerProfiles();

    /// <summary>Told when a job was ticked or unticked, so the settings can be written.</summary>
    private readonly Action<MidiPortViewModel> _roleChanged;

    /// <summary>Told to drop the device altogether, or null where forgetting is not offered.</summary>
    private readonly Action<MidiPortViewModel>? _forget;

    /// <summary>
    /// False until the constructor has finished, so the stored jobs are not reported as changes.
    /// </summary>
    /// <remarks>
    /// The tick boxes are set from the stored role while the row is being built, and that must not
    /// read back as somebody changing something: without this, opening SETTINGS would write the
    /// settings once per device and each write would say only what was already there.
    /// </remarks>
    private readonly bool _loaded;

    /// <summary>The port's own name, as the operating system offers it.</summary>
    public string Name { get; }

    /// <summary>False for a device that is bound but not plugged in right now.</summary>
    public bool IsConnected { get; }

    /// <summary>
    /// What this port is for, when a profile knows the device.
    /// </summary>
    /// <remarks>
    /// The row keeps the port's own name at the top, deliberately. A MiniLab 3 is four rows and
    /// naming all four after the device would leave four identical headings, which is worse than
    /// the numbers it replaced. What was actually missing is what each port is <i>for</i>, since
    /// three of the four are wrong for anything a person would guess.
    ///
    /// Empty for a device with no profile, which is most of them, and the row reads as it always
    /// did.
    /// </remarks>
    public string PortIs => _profiles.PortIs(Name);

    /// <summary>True when there is something to say about this port.</summary>
    public bool HasProfile => PortIs.Length > 0;

    /// <summary>Whether this device's notes and buttons fire pads.</summary>
    [ObservableProperty] private bool drivesPads;

    /// <summary>Whether this device's notes are typed and played into the tracker.</summary>
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

    /// <summary>Builds one row, with the jobs set to what the settings say and nothing announced.</summary>
    /// <param name="name">The port's own name.</param>
    /// <param name="isConnected">False for a device that is remembered but not plugged in.</param>
    /// <param name="role">The jobs it already has.</param>
    /// <param name="roleChanged">Called when somebody moves one of the tick boxes.</param>
    /// <param name="forget">Called to drop the device, where forgetting is offered.</param>
    public MidiPortViewModel(string name, bool isConnected, MidiPortRole role,
                               Action<MidiPortViewModel> roleChanged,
                               Action<MidiPortViewModel>? forget = null)
    {
        Name = name;
        IsConnected = isConnected;
        _roleChanged = roleChanged;
        _forget = forget;

        drivesPads = (role & MidiPortRole.Pads) != 0;
        drivesTracker = (role & MidiPortRole.Tracker) != 0;
        drivesControls = (role & MidiPortRole.Controls) != 0;
        drivesTransport = (role & MidiPortRole.Transport) != 0;

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

    /// <summary>The four tick boxes as the one value the settings hold.</summary>
    public MidiPortRole Role =>
        (DrivesPads ? MidiPortRole.Pads : MidiPortRole.None) |
        (DrivesTracker ? MidiPortRole.Tracker : MidiPortRole.None) |
        (DrivesControls ? MidiPortRole.Controls : MidiPortRole.None) |
        (DrivesTransport ? MidiPortRole.Transport : MidiPortRole.None);

    /// <summary>Any of the four moving is one thing as far as the settings are concerned.</summary>
    partial void OnDrivesPadsChanged(bool value) => NotifyRoleChanged();

    /// <inheritdoc cref="OnDrivesPadsChanged(bool)"/>
    partial void OnDrivesTrackerChanged(bool value) => NotifyRoleChanged();

    /// <inheritdoc cref="OnDrivesPadsChanged(bool)"/>
    partial void OnDrivesControlsChanged(bool value) => NotifyRoleChanged();

    /// <inheritdoc cref="OnDrivesPadsChanged(bool)"/>
    partial void OnDrivesTransportChanged(bool value) => NotifyRoleChanged();

    /// <summary>Passes a change of the jobs on, unless it is the row being built.</summary>
    private void NotifyRoleChanged()
    {
        if (_loaded) _roleChanged(this);
    }
}
