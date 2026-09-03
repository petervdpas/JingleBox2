using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Config;
using JingleBox2.Controllers;
using JingleBox2.Midi;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Controllers.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The MIDI page: what is plugged in, what each device drives, and which key fires which pad.
/// </summary>
/// <remarks>
/// The list is what a person owns rather than what is plugged in at this moment. A controller
/// that has been given a job stays on it when it is unplugged, since the jobs are what somebody
/// set up and the cable is not.
///
/// It is also the one place that decides which ports are open: nothing else holds a port, so the
/// stored jobs are the whole answer to what is listening.
/// </remarks>
public sealed partial class MidiViewModel : ObservableObject
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>Which device has been pointed at which half of the application.</summary>
    /// <remarks>Holds nothing of its own, so one is enough for the page's whole life.</remarks>
    private readonly IMidiPortBindings _bindings = new MidiPortBindings();

    /// <summary>Where the settings are written when a job or a pad mapping moves.</summary>
    private readonly ConfigStore _store;

    /// <summary>The settings themselves, edited in place and then saved whole.</summary>
    private readonly AppConfig _cfg;

    /// <summary>The ports, opened and closed from the jobs and listened to while learning.</summary>
    private readonly IMidiService _midi;

    /// <summary>Every port, connected or merely remembered. What the system offers.</summary>
    public ObservableCollection<MidiPortViewModel> Devices { get; } = new();

    /// <summary>
    /// The same ports gathered into the hardware they belong to. What a person owns.
    /// </summary>
    /// <remarks>
    /// Grouped by what a profile calls the device, and a port with no profile is its own
    /// surface, named after itself, which is exactly the row it always had.
    /// </remarks>
    public ObservableCollection<ControlSurfaceViewModel> Surfaces { get; } = new();

    /// <summary>True when there is any port at all, so the page can say so rather than look empty.</summary>
    [ObservableProperty] private bool hasDevices;

    /// <summary>True when there is any hardware to list on the Control Surfaces page.</summary>
    [ObservableProperty] private bool hasSurfaces;

    /// <summary>Whether a pad's key toggles it or only starts it.</summary>
    [ObservableProperty] private bool toggleMode;

    /// <summary>The line under the table saying what just happened, or what is wrong.</summary>
    [ObservableProperty] private string status = "";

    /// <summary>Reads the ports, opens the ones with a job, and starts listening for learning.</summary>
    /// <remarks>
    /// The three parts of the MIDI settings are filled in if they are missing. Settings written by
    /// an older version, or by hand, can be short of any of them, and a page that assumed they
    /// were there would fail before it had drawn anything.
    /// </remarks>
    /// <param name="store">Where the settings are written when a device's job changes.</param>
    /// <param name="cfg">The settings as they stand, which is what the page shows.</param>
    /// <param name="midi">The ports, for what is plugged in and what it is saying.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    public MidiViewModel(ConfigStore store, AppConfig cfg, IMidiService midi, IControllerProfiles? profiles = null)
    {
        _profiles = profiles ?? new ControllerProfiles();
        _store = store;
        _cfg = cfg;
        _midi = midi;

        _cfg.Midi ??= new MidiConfig();
        _cfg.Midi.Devices ??= new();

        ToggleMode = _cfg.Midi.ToggleMode;

        RefreshDevices();

        _midi.MessageReceived += OnMidi;
    }

    /// <summary>Reads the ports again, for hardware plugged in after the page was opened.</summary>
    /// <remarks>Always enabled: asking twice costs nothing and the answer can have changed.</remarks>
    public IRelayCommand RefreshDevicesCommand => new RelayCommand(RefreshDevices);

    /// <summary>Reads the ports, merges them with what is remembered, and opens what has a job.</summary>
    private void RefreshDevices()
    {
        Devices.Clear();
        foreach (var entry in _bindings.Merge(_midi.GetInputDevices(), _cfg.Midi.Devices))
            Devices.Add(new MidiPortViewModel(entry.Device, entry.IsConnected, entry.Role, OnDeviceRoleChanged, Forget));

        HasDevices = Devices.Count > 0;

        Regroup();

        ApplyBindings();
        Status = DescribeDevices();
    }

    /// <summary>
    /// Gathers the ports into the hardware they came out of.
    /// </summary>
    /// <remarks>
    /// Ordered so a device that is plugged in comes before one that is only remembered, since
    /// the first is what somebody is looking at and the second is what they are keeping.
    /// </remarks>
    private void Regroup()
    {
        Surfaces.Clear();

        foreach (var group in Devices
                     .GroupBy(one => _profiles.Called(one.Name), StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Any(one => one.IsConnected))
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            Surfaces.Add(new ControlSurfaceViewModel(group.Key, group.ToList(), Forget));

        HasSurfaces = Surfaces.Count > 0;
    }

    /// <summary>
    /// Takes a controller off the list for good, with whatever was learned on it.
    /// </summary>
    /// <remarks>
    /// Not the same as unticking its jobs. A device with nothing ticked is still a device the
    /// list remembers, waiting to be plugged back in; this is for one that is not coming back.
    /// Its links go with it, because a layout for hardware nobody owns is a list of
    /// instructions to nothing.
    ///
    /// Only offered for a device that is not connected: one that is plugged in comes straight
    /// back the next time the list is read.
    /// </remarks>
    private void Forget(MidiPortViewModel device)
    {
        if (device is null || device.IsConnected) return;

        _bindings.SetRole(_cfg.Midi.Devices, device.Name, MidiPortRole.None);

        int gone = Midi.ControlLink.Current?.Forget(device.Name) ?? 0;

        Devices.Remove(device);

        Regroup();

        ApplyBindings();
        SaveMidi();

        Status = gone == 0
            ? $"Forgot '{device.Name}'."
            : gone == 1
                ? $"Forgot '{device.Name}' and the one control that was pointed at something."
                : $"Forgot '{device.Name}' and the {gone} controls that were pointed at something.";
    }

    /// <summary>A job was ticked or unticked, so the ports follow and the settings are written.</summary>
    private void OnDeviceRoleChanged(MidiPortViewModel device)
    {
        _bindings.SetRole(_cfg.Midi.Devices, device.Name, device.Role);

        ApplyBindings();
        SaveMidi();

        Status = device.Role == MidiPortRole.None
            ? $"'{device.Name}' drives nothing."
            : $"'{device.Name}' drives {DescribeRole(device.Role)}.";
    }

    /// <summary>
    /// Opens exactly the devices that were given a job, and closes the rest. Nothing else holds
    /// a port open, so the role list is the single source of truth for what is listening.
    /// </summary>
    private void ApplyBindings()
    {
        var wanted = _bindings.DevicesWith(_cfg.Midi.Devices, MidiPortBindings.EveryRole);

        foreach (var open in _midi.OpenDevices)
        {
            if (!wanted.Contains(open, StringComparer.OrdinalIgnoreCase))
                _midi.Close(open);
        }

        foreach (var device in wanted)
            _midi.Open(device);
    }

    /// <summary>
    /// The status line for the list as a whole.
    /// </summary>
    /// <remarks>
    /// Nothing ticked and nothing plugged in are told apart, because they are two situations and
    /// only one of them is something to do about it: nothing ticked is a controller waiting to be
    /// given a job, everything ticked and nothing open is hardware that is not plugged in.
    /// </remarks>
    private string DescribeDevices()
    {
        if (Devices.Count == 0) return "No MIDI devices found.";

        int open = _midi.OpenDevices.Count;

        if (open == 0)
        {
            bool anyBound = Devices.Any(d => d.Role != MidiPortRole.None);

            return anyBound
                ? "Nothing is plugged in. What is listed keeps what it was set to drive."
                : "No controller assigned yet. Tick Pads, Tracker or Controls.";
        }

        var missing = Devices.Where(d => !d.IsConnected && d.Role != MidiPortRole.None).ToList();
        if (missing.Count > 0)
            return $"{open} controller(s) open. Not connected: {string.Join(", ", missing.Select(d => d.Name))}.";

        return $"{open} controller(s) open.";
    }

    /// <summary>The jobs a device has, in words, for the status line.</summary>
    /// <remarks>
    /// Written out per combination rather than joined from parts, because "the pads and the
    /// tracker" is a sentence and "Pads, Tracker" is a list of tick boxes read aloud.
    /// </remarks>
    private static string DescribeRole(MidiPortRole role) => role switch
    {
        MidiPortRole.Pads => "the pads",
        MidiPortRole.Tracker => "the tracker",
        MidiPortRole.Controls => "the knobs and faders",
        MidiPortRole.Pads | MidiPortRole.Tracker => "the pads and the tracker",
        MidiPortRole.Pads | MidiPortRole.Controls => "the pads and the knobs",
        MidiPortRole.Tracker | MidiPortRole.Controls => "the tracker and the knobs",
        MidiPortBindings.EveryRole => "the pads, the tracker and the knobs",
        _ => "nothing"
    };

    /// <summary>Stores the toggle setting, which is read by the router rather than held here.</summary>
    partial void OnToggleModeChanged(bool value)
    {
        _cfg.Midi.ToggleMode = value;
        SaveMidi();
    }

    /// <summary>
    /// MIDI arrives on its own thread, so the status text and the learn result are handed to
    /// the UI thread before anything bound to them is touched.
    /// </summary>
    private void OnMidi(object? sender, MidiMessage msg) => Dispatcher.UIThread.Post(() => HandleMidi(msg));

    /// <summary>Writes what this page holds back to the settings.</summary>
    /// <remarks>
    /// It used to write the pad mapping table as well, which was the only thing here that was
    /// not already the settings' own object: the jobs a port is given are written straight onto
    /// the settings by the rows themselves, so this is the save and nothing more.
    /// </remarks>
    private void SaveMidi() => _store.Save(_cfg);

    /// <summary>
    /// Says what arrived, so the page shows the wire.
    /// </summary>
    /// <remarks>
    /// A line rather than a mapping. Learning used to happen here, on a row of a table of its
    /// own; the pads are pointed at by the same gesture as everything else now, so what is left
    /// is the one thing this page is still the place for, which is telling somebody that the
    /// message they just sent arrived at all.
    /// </remarks>
    private void HandleMidi(MidiMessage msg) =>
        Status = $"{msg.Device}: {msg.Type} ch{msg.Channel} val={msg.Value} data={msg.Data} on={msg.IsOn}";

}
