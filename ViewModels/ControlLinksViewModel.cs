using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Controllers;
using JingleBox2.Midi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using JingleBox2.Midi.Enums;
using JingleBox2.Tracker.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// What your controller is pointed at, as a list to read and to clear.
/// </summary>
/// <remarks>
/// Links are made by pointing rather than here: turn the mode on with Ctrl+Shift+M, rest the
/// pointer on a control, touch the one on the desk. This is the other half of that, and the
/// half a list is actually good for: seeing what you did, finding the one that is wrong, and
/// taking it off.
/// </remarks>
public sealed class ControlLinksViewModel : ObservableObject
{
    /// <summary>Where the links live, read for the list and told when one is taken off.</summary>
    private readonly ControlLink _link;

    /// <summary>
    /// Which of the two layers this list is about.
    /// </summary>
    /// <remarks>
    /// One each, and never both. The settings are the desk: what your controller does whatever
    /// you open, looked after where the hardware is looked after. The tracker's page is the
    /// song's own, which comes and goes with the song like its patterns do.
    ///
    /// They were one list showing everything with a word beside each row saying which layer it
    /// belonged to, and that word was not enough: a link made in a song turned up in the
    /// settings, where nothing about the surroundings suggested a song had anything to do with
    /// it. Two lists, each about one thing, and the boundary is the page you are on.
    /// </remarks>
    private readonly bool _songOnly;

    /// <summary>Reads one layer's links and follows them for as long as this list is on screen.</summary>
    /// <param name="link">Where the links live.</param>
    /// <param name="songOnly">True for the song's own layer, false for the desk.</param>
    public ControlLinksViewModel(ControlLink link, bool songOnly = false)
    {
        _link = link;
        _songOnly = songOnly;

        _link.Changed += Restock;

        Restock();
    }

    /// <summary>What to say when there is nothing to show, which differs between the two.</summary>
    public string Nothing => _songOnly
        ? "This song has no controls of its own. Point a knob at an instrument on a track and it will be kept here, and travel in the song's file."
        : "Nothing is pointed at anything yet. Point a knob at a machine on the rack and it will be kept here, and work in every song.";

    /// <summary>Every link in this layer, flat, in the order the headings put them.</summary>
    public ObservableCollection<ControlLinkRow> Links { get; } = new();

    /// <summary>
    /// The same links, gathered under the controller each was learned on.
    /// </summary>
    /// <remarks>
    /// Flat, the list runs on: one knob can hold a job per machine, so a single encoder taking
    /// the filter on four machines is four rows, and two controllers on the desk interleave by
    /// number into something nobody can read. Under its own heading, a controller's layout is a
    /// thing you can look at and recognise, which is what you are doing when you open this.
    /// </remarks>
    public ObservableCollection<ControlDeviceLinks> Controllers { get; } = new();

    /// <summary>True when there is anything to show, so the page can say <see cref="Nothing"/>.</summary>
    public bool HasLinks => Links.Count > 0;

    /// <summary>Takes every one off at once, for a controller being started again from nothing.</summary>
    /// <remarks>
    /// Always enabled, including with an empty list, where it does nothing visible. Walked over a
    /// copy, since unlinking a row rebuilds the list this is walking.
    /// </remarks>
    public IRelayCommand ForgetAllCommand => new RelayCommand(() =>
    {
        foreach (var row in Links.ToList()) _link.Unlink(row.Mapping);
    });

    /// <summary>
    /// Reads the links again, for when what they belong to has changed underneath.
    /// </summary>
    /// <remarks>
    /// A song's own layout comes and goes with the song, and nothing about opening one passes
    /// through this: the mappings are read per message and were right the whole time. What was
    /// wrong was the list on the screen, which went on showing the layout of a song that is no
    /// longer open.
    /// </remarks>
    public void Reread() => Restock();

    /// <summary>
    /// Builds both lists again off whichever layer this is about.
    /// </summary>
    /// <remarks>
    /// Ordered by controller, then by number within it, then by machine where one number holds a
    /// job on several. That last one matters: a knob can be pointed at the filter of four
    /// machines, and without it those four rows scatter through the list instead of sitting
    /// together and reading as the one knob they are.
    /// </remarks>
    private void Restock()
    {
        Links.Clear();
        Controllers.Clear();

        var order = (_songOnly ? _link.Kept : _link.Desk)
            .OrderBy(one => one.Device, StringComparer.OrdinalIgnoreCase)
            .ThenBy(one => one.Channel)
            .ThenBy(one => one.Cc)
            .ThenBy(one => one.Machine, StringComparer.Ordinal)
            .ToList();

        foreach (var mapping in order) Links.Add(new ControlLinkRow(mapping, _link));

        foreach (var group in order.GroupBy(one => one.Device, StringComparer.OrdinalIgnoreCase))
            Controllers.Add(new ControlDeviceLinks(
                group.Key,
                group.Select(one => new ControlLinkRow(one, _link))));

        OnPropertyChanged(nameof(HasLinks));
    }
}

/// <summary>
/// One controller, and everything learned on it.
/// </summary>
public sealed class ControlDeviceLinks
{
    /// <summary>Gathers one controller's rows under its own heading.</summary>
    public ControlDeviceLinks(string device, IEnumerable<ControlLinkRow> links)
    {
        Device = device;

        foreach (var one in links) Links.Add(one);
    }

    /// <summary>What the controller is called, or nothing for links that name none.</summary>
    public string Device { get; }

    /// <summary>Everything learned on it, in the order the list put them.</summary>
    public ObservableCollection<ControlLinkRow> Links { get; } = new();

    /// <summary>The heading: the controller, and how much of it is spoken for.</summary>
    /// <remarks>
    /// A mapping made before controllers were recorded names none, and says so rather than
    /// sitting under a blank heading as if the name had gone missing.
    /// </remarks>
    public string Said =>
        (Device.Length > 0 ? ControllerProfiles.Called(Device) : "Learned before controllers were recorded")
        + "  ·  " + Links.Count + (Links.Count == 1 ? " control" : " controls");
}

/// <summary>One line of it: which control, what it moves, and how it picks it up.</summary>
public sealed class ControlLinkRow
{
    /// <summary>Where the links live, so a row can change or remove its own.</summary>
    private readonly ControlLink _link;

    /// <summary>One row over one mapping, which it edits in place rather than copying.</summary>
    public ControlLinkRow(ControlMapping mapping, ControlLink link)
    {
        Mapping = mapping;
        _link = link;
    }

    /// <summary>The link itself, which is what the buttons on the row work on.</summary>
    public ControlMapping Mapping { get; }

    /// <summary>
    /// The hardware, as the controller's own manual would put it.
    /// </summary>
    /// <remarks>
    /// What is printed on the front of the device where a profile knows, and the number
    /// otherwise. `Encoder 3` is a thing you can find with your hand; `CC 89 ch 1` is a thing
    /// you can only find with a manual, and there is no manual for most of what people own.
    ///
    /// The number is not a failure to fall back to. It is what this said for its whole life
    /// until now and it works, which is the entire reason a profile is allowed to be optional.
    /// </remarks>
    public string Control =>
        ControllerProfiles.Named(Mapping.Device, Mapping.Channel, Mapping.Cc) is { Length: > 0 } named
            ? named
            : "CC " + Mapping.Cc.ToString(CultureInfo.InvariantCulture)
              + "  ch " + Mapping.Channel.ToString(CultureInfo.InvariantCulture);

    /// <summary>Which controller it was learned on, or nothing when it names none.</summary>
    public string Device => Mapping.Device;

    /// <summary>True when there is a controller name worth showing.</summary>
    public bool HasDevice => Mapping.Device.Length > 0;

    /// <summary>What it moves, named when it was learned.</summary>
    public string What => Mapping.Name.Length > 0 ? Mapping.Name : Mapping.Key;

    /// <summary>
    /// Which track, said only when it is pinned to one.
    /// </summary>
    /// <remarks>
    /// Nothing at all for the ordinary kind. Almost every link follows you, so a column saying
    /// "the track you are on" on every row is a column that says the same thing eleven times
    /// and nothing about any of them. Blank where it is the usual case, and a track number
    /// where it is not, which is the only time it is worth reading.
    /// </remarks>
    public string Where => Mapping.Scope == ControlScope.Fixed
        ? "TR-" + (Mapping.Track + 1).ToString("00", CultureInfo.InvariantCulture)
        : "";

    /// <summary>True when this one is pinned, and so has something to say about where.</summary>
    public bool IsPinned => Mapping.Scope == ControlScope.Fixed;

    /// <summary>What kind of control it turned out to be, or that it is still listening.</summary>
    public string How => ControlSense.Describe(Mapping.Pickup, Mapping.Turn);

    /// <summary>
    /// Whether this belongs to the song that is open or to the desk.
    /// </summary>
    /// <remarks>
    /// Kept for the tip on the button rather than shown. Each list is one layer now, so a
    /// column saying which would say the same thing on every row of it.
    /// </remarks>
    public string Home => _link.IsSong(Mapping) ? "this song" : "the desk";

    /// <summary>
    /// Says it is something else, for the times the guess was wrong.
    /// </summary>
    /// <remarks>
    /// Working out what a control is from what it sends is right almost always and cannot be
    /// right every time: a button that repeats while it is held looks exactly like an encoder
    /// counting notches, because that is what both of them send. So the answer is a control you
    /// can press, and pressing past the end of the list puts it back to listening, which is how
    /// you ask it to work it out again from your next turn of the knob.
    ///
    /// Always enabled. The list comes round, so there is no end to be stuck against and no state
    /// in which pressing it would do nothing.
    /// </remarks>
    public IRelayCommand NextCommand => new RelayCommand(() =>
    {
        Mapping.Pickup = Mapping.Pickup switch
        {
            ControlPickup.Takeover => ControlPickup.Jump,
            ControlPickup.Jump => ControlPickup.Relative,
            ControlPickup.Relative when Mapping.Turn == ControlTurn.Offset => Turned(ControlTurn.Twos),
            ControlPickup.Relative => ControlPickup.Endless,
            ControlPickup.Endless => ControlPickup.Sensed,
            _ => ControlPickup.Takeover
        };

        _link.Say();
    });

    /// <summary>The other encoder convention, staying an encoder.</summary>
    private ControlPickup Turned(ControlTurn turn)
    {
        Mapping.Turn = turn;

        return ControlPickup.Relative;
    }

    /// <summary>Takes this one link off, leaving everything else on the controller alone.</summary>
    /// <remarks>Always enabled: a row exists only while there is a link behind it.</remarks>
    public IRelayCommand ForgetCommand => new RelayCommand(() => _link.Unlink(Mapping));
}
