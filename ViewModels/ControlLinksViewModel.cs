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
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Midi.Interfaces;

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
    /// <summary>
    /// What is known about the controllers plugged in.
    /// </summary>
    /// <remarks>
    /// Handed in and passed down to every row rather than made again per row. It remembers what
    /// a device has been seen doing, so two of them are two different answers to the same
    /// question, and the rows would be reading one nobody is telling anything.
    /// </remarks>
    private readonly IControllerProfiles _profiles;

    /// <summary>Where the links live, read for the list and told when one is taken off.</summary>
    private readonly ControlLink _link;

    /// <summary>
    /// What a target is called and which parameter, shared with the file so the two agree.
    /// </summary>
    /// <remarks>
    /// The page cuts the list into cards by exactly the rule a template is written out by, and
    /// two spellings of that rule would eventually disagree. The way that fails is a template
    /// that means one thing to whoever exported it and another to whoever opened it.
    /// </remarks>
    private readonly ILinkTargets _naming;

    /// <summary>Writing a template out and reading one back.</summary>
    private readonly IControlTemplates _templates;

    /// <summary>
    /// Which MIDI ports this computer has, asked when a template arrives.
    /// </summary>
    /// <remarks>
    /// Asked rather than held, since a controller can be plugged in while the page is open. A
    /// list that was read when the page was built would refuse a template for the device
    /// somebody has just connected.
    /// </remarks>
    private readonly Func<IEnumerable<string>>? _ports;

    /// <summary>
    /// Which of the two layers this list is about.
    /// </summary>
    /// <remarks>
    /// One each, and never both. The desk is what your controller does whatever you open; a
    /// song's own comes and goes with the song like its patterns do.
    ///
    /// They were one list showing everything with a word beside each row saying which layer it
    /// belonged to, and that word was not enough: a link made in a song turned up in the
    /// settings, where nothing about the surroundings suggested a song had anything to do with
    /// it. One list is one layer, and that is the rule. It is not the same rule as one page is
    /// one layer, which is what this used to lean on and is now wrong: the tracker's MIDI CC
    /// page draws both, one under the other, each headed with how far its links reach. Two
    /// lists side by side are two lists; what could not work was the two poured into one.
    /// </remarks>
    private readonly bool _songOnly;

    /// <summary>Reads one layer's links and follows them for as long as this list is on screen.</summary>
    /// <param name="link">Where the links live.</param>
    /// <param name="songOnly">True for the song's own layer, false for the desk.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    /// <param name="ports">
    /// Which MIDI ports this computer has, for working out which is the controller a template
    /// names. Left out, a template still reads and its links wait for the controller it names.
    /// </param>
    /// <param name="templates">Writing a template out and reading one back.</param>
    /// <param name="naming">What a target is called, shared with the file so the two agree.</param>
    public ControlLinksViewModel(
        ControlLink link,
        bool songOnly = false,
        IControllerProfiles? profiles = null,
        Func<IEnumerable<string>>? ports = null,
        IControlTemplates? templates = null,
        ILinkTargets? naming = null)
    {
        _profiles = profiles ?? new ControllerProfiles();
        _link = link;
        _songOnly = songOnly;
        _naming = naming ?? new LinkTargets();
        _templates = templates ?? new ControlTemplates(_naming);
        _ports = ports;

        _link.Changed += Restock;

        Restock();
    }

    /// <summary>
    /// What this list is, in the two or three words that head it.
    /// </summary>
    /// <remarks>
    /// Named for how far the links reach rather than for where they are kept, because that is
    /// the whole of the difference and it is what somebody looking at two lists on one page is
    /// trying to tell apart. Where they are kept is a fact about files and is of no use while
    /// you are looking at a knob wondering what it will do.
    ///
    /// Templates, plural, because the cards under it are the templates and the heading is the
    /// shelf they sit on. A link that works in every song, on a machine that is the same
    /// machine on everybody's installation, is a thing you could hand to somebody else, and
    /// that is what the whole of this layer is for.
    /// </remarks>
    public string Title => _songOnly ? "This song" : "Templates";

    /// <summary>
    /// How a link is made, said above the list rather than left to be discovered.
    /// </summary>
    /// <remarks>
    /// The gesture is the same for both layers and only what it is pointed at decides which
    /// list the link lands in, so each half says that in its own words: point at an instrument
    /// on a track and it is the song's, point at a machine on the rack and it is every song's.
    ///
    /// Neither says where the other one is drawn. The same list is shown in two places now,
    /// and a sentence saying "listed below" is true on one page and a wild goose chase on the
    /// other.
    /// </remarks>
    public string Hint => _songOnly
        ? "Press Ctrl+Shift+M with an instrument's panel open, rest the pointer on a knob until it glows, and touch the control on your desk. What you point at here is this song's and travels in its file."
        : "What your controller does whatever you open, one card for each thing it is pointed at. Press Ctrl+Shift+M with a machine's panel open on the rack, rest the pointer on a knob until it glows, and touch the control on your desk. What you point at on an instrument in a song belongs to that song instead.";

    /// <summary>
    /// Whether this list offers to take every link off at once.
    /// </summary>
    /// <remarks>
    /// The desk's does and a song's does not. Clearing the desk is starting a controller again
    /// from nothing, which is a thing people really do and the reason that button exists. A
    /// song's layout is part of the song: it is taken back with the song's own undo, it travels
    /// in the file, and it has no more business behind a button that empties it in one press
    /// than the song's patterns have.
    /// </remarks>
    public bool Clears => !_songOnly;

    /// <summary>
    /// Whether this layer takes a template from outside.
    /// </summary>
    /// <remarks>
    /// The desk's does. A template is what one controller does to one machine wherever it is
    /// met, which is the desk in as many words, and it is the layer somebody else's file is
    /// about. A song's layout is about this piece of music and is not a thing you receive.
    /// </remarks>
    public bool Imports => !_songOnly;

    /// <summary>
    /// What just happened, in a line, or nothing.
    /// </summary>
    /// <remarks>
    /// Every outcome of an import needs saying and none of them looks like anything: a template
    /// for a controller that is not plugged in applies perfectly and moves nothing until it is,
    /// which without a word would read exactly like a file that failed to open.
    /// </remarks>
    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    /// <summary>Behind <see cref="Status"/>.</summary>
    private string _status = "";

    /// <summary>True when there is something to say, so the line is not an empty gap.</summary>
    public bool HasStatus => Status.Length > 0;

    /// <summary>What to say when there is nothing to show, which differs between the two.</summary>
    public string Nothing => _songOnly
        ? "This song has no controls of its own. Point a knob at an instrument on a track and it will be kept here, and travel in the song's file."
        : "Nothing is pointed at anything yet. Point a knob at a machine on the rack and it will be kept here, and work in every song.";

    /// <summary>Every link in this layer, flat, in the order the headings put them.</summary>
    public ObservableCollection<ControlLinkRow> Links { get; } = new();

    /// <summary>
    /// The same links, gathered under the thing each is pointed at, and under the controller
    /// within that.
    /// </summary>
    /// <remarks>
    /// Two levels, and each one is a question somebody actually asks. What does my hardware do
    /// to OddSkilla is the first; which of my controllers is the second. Flat, neither can be
    /// answered: one knob can hold a job per machine, so a single encoder taking the filter on
    /// four machines is four rows scattered through the list, and two controllers interleave by
    /// number into something nobody can read.
    ///
    /// The pair is also the unit the whole of this is for. One controller against one target is
    /// a template: it is the same on every installation, since a machine's id decides its
    /// engine and a plugin's parameters are numbered by the plugin, so it is the thing that can
    /// be handed to somebody else. Drawing it as one card per target with the controllers
    /// inside is drawing the templates.
    /// </remarks>
    public ObservableCollection<ControlTargetLinks> Targets { get; } = new();

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
        Targets.Clear();

        var order = (_songOnly ? _link.Kept : _link.Desk)
            .OrderBy(one => one.Device, StringComparer.OrdinalIgnoreCase)
            .ThenBy(one => one.Channel)
            .ThenBy(one => one.Cc)
            .ThenBy(one => one.Machine, StringComparer.Ordinal)
            .ToList();

        foreach (var mapping in order) Links.Add(new ControlLinkRow(mapping, _link, _profiles));

        foreach (var target in order
                     .GroupBy(_naming.KeyOf, StringComparer.Ordinal)
                     .OrderBy(one => _naming.RankOf(one.First()))
                     .ThenBy(one => _naming.TitleOf(one), StringComparer.OrdinalIgnoreCase))
            Targets.Add(Card(target));

        OnPropertyChanged(nameof(HasLinks));
    }

    /// <summary>
    /// One card: the thing pointed at, and its links under the controller each was learned on.
    /// </summary>
    /// <remarks>
    /// The heading is worked out once and handed down to every row under it, so the rows can
    /// leave it off. Worked out rather than read, because a link made before the name was kept
    /// has only its ids, and a card headed with a folder name is a card nobody recognises.
    /// </remarks>
    /// <param name="target">Every link pointed at one thing.</param>
    private ControlTargetLinks Card(IEnumerable<ControlMapping> target)
    {
        var all = target.ToList();
        string owner = _naming.TitleOf(all);

        return new ControlTargetLinks(
            owner,
            _naming.KindOf(all[0]),
            all.GroupBy(one => one.Device, StringComparer.OrdinalIgnoreCase)
                .Select(controller => new ControllerLinks(
                    controller.Key,
                    controller.Select(one => new ControlLinkRow(one, _link, _profiles, owner)),
                    controller.ToList(),
                    _profiles)));
    }

    /// <summary>Where a template is written by default, and where the picker opens.</summary>
    public string Folder() => _templates.Folder();

    /// <summary>What to call the file this section would be written to.</summary>
    /// <param name="which">The controller's links on one target.</param>
    public string Suggest(ControllerLinks? which) =>
        which is null || Written(which) is not { } template
            ? "template"
            : _templates.FileName(template);

    /// <summary>
    /// Writes one controller's links on one target out as a template.
    /// </summary>
    /// <remarks>
    /// One section rather than the whole card, because that pair is what means the same thing on
    /// somebody else's machine. Two controllers on one machine are two templates, and rightly:
    /// whoever receives them has one of the two devices, or neither, or both.
    /// </remarks>
    /// <param name="which">The controller's links on one target.</param>
    /// <param name="path">Where to write it.</param>
    public void Export(ControllerLinks? which, string path)
    {
        if (which is null || path is not { Length: > 0 }) return;

        if (Written(which) is not { } template)
        {
            Status = "Nothing to write.";

            return;
        }

        try
        {
            _templates.Write(path, template);

            Status = "Wrote " + template.Controls.Count
                     + (template.Controls.Count == 1 ? " control for " : " controls for ")
                     + template.Target.Name + " to " + System.IO.Path.GetFileName(path) + ".";
        }
        catch (Exception ex)
        {
            Status = "Could not write it: " + ex.Message;
        }
    }

    /// <summary>
    /// Reads a template and lays its links down in this layer.
    /// </summary>
    /// <remarks>
    /// The rules are the ones a link made by hand keeps, since <see cref="ControlLink.Take"/> is
    /// what does it: one control does one job, so an arriving link displaces whatever held its
    /// control. That is what makes importing the same template twice do nothing the second time
    /// rather than piling up.
    ///
    /// Every outcome is said, because none of them looks like anything on its own. A template
    /// for a controller that is not plugged in applies perfectly and moves nothing until it is,
    /// and that is exactly the case somebody would otherwise read as the import having failed.
    /// </remarks>
    /// <param name="path">The file to read.</param>
    public void Import(string path)
    {
        if (path is not { Length: > 0 }) return;

        if (_templates.Open(path) is not { } template)
        {
            Status = System.IO.Path.GetFileName(path) + " is not a control template.";

            return;
        }

        var reading = _templates.Take(template, _ports?.Invoke(), _profiles.Called);

        if (reading.Links.Count == 0)
        {
            Status = "Nothing in " + System.IO.Path.GetFileName(path) + " could be read here.";

            return;
        }

        int took = _link.Take(reading.Links, _songOnly);

        string said = "Took " + took + (took == 1 ? " control for " : " controls for ")
                      + (template.Target.Name.Length > 0 ? template.Target.Name : template.Target.Kind);

        if (!reading.Found)
            said += ", waiting for " + reading.Controller;

        if (reading.Skipped > 0)
            said += ". " + reading.Skipped
                    + (reading.Skipped == 1 ? " control was" : " controls were")
                    + " written for a newer version and left out";

        Status = said + ".";
    }

    /// <summary>This section as a template, or nothing when there is nothing to write.</summary>
    /// <param name="which">The controller's links on one target.</param>
    private ControlTemplate? Written(ControllerLinks which) =>
        _templates.Describe(
            _profiles.Called(which.Device),
            which.Mappings,
            (channel, cc) => _profiles.Named(which.Device, channel, cc));
}

/// <summary>
/// One thing a controller is pointed at, and every link on it.
/// </summary>
/// <remarks>
/// A machine, an effect, a mixer strip or the transport. They are one type here because to a
/// knob they are the same thing: something with parameters that can be written into, which is
/// what <see cref="Midi.Interfaces.IControlTarget"/> has meant since the beginning.
///
/// What it is not called is a device. On a page about MIDI that word already means the box on
/// the desk, and this is the other end of the wire.
/// </remarks>
public sealed class ControlTargetLinks
{
    /// <summary>Gathers everything pointed at one target under a heading naming it.</summary>
    /// <param name="title">What it is called, worked out once for the card and its rows alike.</param>
    /// <param name="kind">Which sort of thing it is, in the one word a person would use.</param>
    /// <param name="controllers">Its links, gathered under the controller each was learned on.</param>
    public ControlTargetLinks(string title, string kind, IEnumerable<ControllerLinks> controllers)
    {
        Title = title;
        Kind = kind;

        foreach (var one in controllers) Controllers.Add(one);
    }

    /// <summary>What it is called, which is the heading over the card.</summary>
    public string Title { get; }

    /// <summary>Which sort of thing it is, in the one word a person would use for it.</summary>
    public string Kind { get; }

    /// <summary>Its links, gathered under the controller each was learned on.</summary>
    public ObservableCollection<ControllerLinks> Controllers { get; } = new();

}

/// <summary>
/// One controller, and everything learned on it against one target.
/// </summary>
/// <remarks>
/// This pair, the controller and the target above it, is a template: what your nanoKONTROL2
/// does to OddSkilla, which is the same sentence on anybody's installation. So it is the thing
/// that is written out and read back, and not the card, which can hold two controllers and
/// would land on somebody who has one of them.
/// </remarks>
public sealed class ControllerLinks
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>Gathers one controller's rows under its own heading.</summary>
    /// <param name="device">What the controller is called, or nothing for links that name none.</param>
    /// <param name="links">Everything learned on it, as rows to read.</param>
    /// <param name="mappings">The same links as they are stored, for writing a template out.</param>
    /// <param name="profiles">What is known about the controllers, handed down rather than made again.</param>
    public ControllerLinks(
        string device,
        IEnumerable<ControlLinkRow> links,
        IReadOnlyList<ControlMapping> mappings,
        IControllerProfiles profiles)
    {
        _profiles = profiles;
        Device = device;
        Mappings = mappings;

        foreach (var one in links) Links.Add(one);
    }

    /// <summary>What the controller is called, or nothing for links that name none.</summary>
    public string Device { get; }

    /// <summary>Everything learned on it, in the order the list put them.</summary>
    public ObservableCollection<ControlLinkRow> Links { get; } = new();

    /// <summary>
    /// The same links as they are stored, which is what a template is written out of.
    /// </summary>
    /// <remarks>
    /// The rows are for reading and have already had the target's name taken off them. A file is
    /// written from the links themselves, since everything that decides is in them and nothing
    /// that decides is in the wording.
    /// </remarks>
    public IReadOnlyList<ControlMapping> Mappings { get; }

    /// <summary>The heading: the controller, and how much of it is spoken for.</summary>
    /// <remarks>
    /// A mapping made before controllers were recorded names none, and says so rather than
    /// sitting under a blank heading as if the name had gone missing.
    /// </remarks>
    public string Said =>
        (Device.Length > 0 ? _profiles.Called(Device) : "Learned before controllers were recorded")
        + "  ·  " + Links.Count + (Links.Count == 1 ? " control" : " controls");
}

/// <summary>One line of it: which control, what it moves, and how it picks it up.</summary>
public sealed class ControlLinkRow
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>Where the links live, so a row can change or remove its own.</summary>
    private readonly ControlLink _link;

    /// <summary>One row over one mapping, which it edits in place rather than copying.</summary>
    /// <param name="mapping">The link this row is about.</param>
    /// <param name="link">Where the links live, for a row that takes itself off.</param>
    /// <param name="profiles">What is known about the controllers, handed down rather than made again.</param>
    /// <param name="owner">
    /// What the card over this row is headed with, so the row can leave it off. Nothing when
    /// the row stands on its own, where the whole name is what has to be read.
    /// </param>
    public ControlLinkRow(ControlMapping mapping, ControlLink link, IControllerProfiles profiles, string owner = "")
    {
        _profiles = profiles;
        Mapping = mapping;
        _link = link;
        _owner = owner.Length > 0 ? owner : mapping.Owner;
    }

    /// <summary>What the card over this row is headed with, which this row does not repeat.</summary>
    private readonly string _owner;

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
        _profiles.Named(Mapping.Device, Mapping.Channel, Mapping.Cc) is { Length: > 0 } named
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
    /// The same, without the thing it is on, for a row already under that thing's heading.
    /// </summary>
    /// <remarks>
    /// <see cref="ControlMapping.Name"/> is the owner and the control run together, which is
    /// what it has to be wherever a link is shown on its own. Under a card headed OddSkilla,
    /// "OddSkilla attack" says the machine's name nine times down one column and the word that
    /// differs is the last one, which is the one a column should start with.
    ///
    /// Case is ignored on the way off, since the wording of a strip's controls was written for
    /// a status line and starts in lower case where its heading does not.
    /// </remarks>
    public string Said =>
        _owner.Length > 0
        && What.Length > _owner.Length
        && What.StartsWith(_owner + " ", StringComparison.OrdinalIgnoreCase)
            ? What[(_owner.Length + 1)..]
            : What;

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

    /// <summary>
    /// True when this one is pinned, and so has something to say about where.
    /// </summary>
    /// <remarks>
    /// Never on a mixer link, although every one of those is pinned: its card is the strip, so
    /// the row would be repeating the heading over it. It is only worth reading where the thing
    /// being driven is one thing and the track it is driven on is another, which is a machine
    /// or an effect nailed to a track rather than following the cursor.
    /// </remarks>
    public bool IsPinned =>
        Mapping.Scope == ControlScope.Fixed
        && Mapping.Kind is not (ControlKind.Mix or ControlKind.Transport);

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
