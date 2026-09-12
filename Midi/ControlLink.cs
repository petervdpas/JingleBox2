using Avalonia.Threading;
using System;
using JingleBox2.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// Pointing a knob on a controller at a knob on the screen, by resting the pointer on one and
/// touching the other.
/// </summary>
/// <remarks>
/// There is no dialog and nothing to click. The application has a second mouse mode, switched
/// with Ctrl+Shift+M, in which the panels do not turn: what the pointer rests on is offered,
/// and the next thing you touch on the controller takes it. Sweep along a row of knobs, wiggle
/// a knob on the desk at each one, and a controller is laid out in the time it takes to move
/// your hand along it.
///
/// The offer stands until another control is pointed at, which is what makes it work in
/// practice: you have to look down at the hardware, and looking down means the pointer is
/// nowhere in particular. Leaving the mode clears it.
///
/// The mode is also what makes the panels safe. A machine panel is already built to be deaf to
/// the pointer while it is being laid out, so a press meant to pick a knob up does not turn it;
/// this is that same arrangement with a different thing happening.
/// </remarks>
public sealed class ControlLink
{
    /// <summary>The desk's own layout: the settings' list, held live rather than copied.</summary>
    private readonly List<ControlMapping> _mappings;

    /// <summary>How to say the settings have moved, so they are written down.</summary>
    private readonly Action _changed;

    /// <summary>
    /// Around the list, because it is written from the controller and read from the screen.
    /// </summary>
    /// <remarks>
    /// A message arrives on the MIDI thread and adds a mapping; the list in SETTINGS and the
    /// rings on a panel are read on the drawing one. Without this, a knob touched while that
    /// list was being rebuilt would throw somewhere neither of them could see.
    /// </remarks>
    private readonly object _lock = new();

    /// <param name="mappings">The desk's layout, from the settings, edited in place.</param>
    /// <param name="changed">Told whenever that list moves, so the settings are saved.</param>
    public ControlLink(List<ControlMapping> mappings, Action changed)
    {
        _mappings = mappings;
        _changed = changed;

        _mappings.RemoveAll(one => one.Kind == ControlKind.Plugin);
    }

    /// <summary>
    /// The one this session is using.
    /// </summary>
    /// <remarks>
    /// A static, and the same reason as <see cref="SoundDevices.SoundMachines.SoundMachineProjects"/>:
    /// there is exactly one controller on the desk, the mode it is in is the same mode
    /// everywhere at once, and the panels that have to know are drawn from a description that
    /// has never heard of a view model. Threading a reference through every designer, window
    /// and panel to say one bool would be a lot of wiring to express something that is true of
    /// the application rather than of any part of it.
    /// </remarks>
    public static ControlLink? Current { get; private set; }

    /// <summary>Makes this the one everything asks. Called once, as the window is built.</summary>
    public void UseThis() => Current = this;

    /// <summary>
    /// The templates block, when there is one, which is what the links come to.
    /// </summary>
    /// <remarks>
    /// Set once as the window is built, the same as the rest of these and for the same reason:
    /// nothing here reads it, and what it is for is whoever is handed this. A machine's face
    /// reaches its links through this object already, and the templates are the same fact said
    /// the way a face wants to read it, so handing the block through here is one wiring rather
    /// than one per place a face can be drawn.
    ///
    /// Deliberately not a door of its own. There is one application and so one block, and an
    /// ambient <c>Current</c> is a static class wearing another hat: whatever a test put in it is
    /// still there for the next test, and this executable runs again as a plugin's host where
    /// there are no blocks at all.
    /// </remarks>
    public Interfaces.IControlTemplateBlock? Templates { get; set; }

    /// <summary>
    /// Which MIDI ports this computer has, for settling a template's controller on arrival.
    /// </summary>
    /// <remarks>
    /// The one part of a template that does not travel. A file names the controller as its
    /// profile calls it, since one nanoKONTROL2 is <c>nanoKONTROL2 _ CTRL</c> to the ALSA
    /// sequencer and something else to rawmidi and a third thing on Windows, so what a template
    /// is laid down onto has to be looked up in what this machine can see.
    ///
    /// Nothing here uses it either: it is handed on to whoever turns a template back into links.
    /// A controller that is not plugged in keeps the name the template carried and its links wait
    /// for it, which is the rule a link already keeps.
    /// </remarks>
    public Func<IEnumerable<string>>? Ports { get; set; }

    /// <summary>
    /// What a profile calls the device a port belongs to, or nothing where nobody knows.
    /// </summary>
    /// <remarks>
    /// Set once as the window is built, beside <see cref="Ports"/>. It is what tells this layer
    /// that two ports are one box, which nothing in a port name says and which decides whether
    /// learning a knob on one of them takes the link off the other.
    ///
    /// Nothing is claimed where it is not set: a device with no profile is compared by its port
    /// name exactly as it always was.
    /// </remarks>
    public Func<string, string>? Called { get; set; }

    /// <summary>
    /// What a controller is called, which is what a link stores.
    /// </summary>
    /// <remarks>
    /// **The contract has always said the device is the controller by name**, and what was
    /// written into it was the port a message arrived on. A box shows up as several ports, a
    /// MiniLab 3 as four of them, so one knob learned while a second port was delivering made a
    /// second link that nothing displaced: one control doing one job twice.
    ///
    /// The port name is kept where nobody knows any better, which is a device with no profile,
    /// and that is exactly what a port name is for there: it is the only name the thing has.
    /// </remarks>
    /// <param name="port">The port a message arrived on.</param>
    private string Named(string port) => Called is { } called ? called(port) : port;

    /// <summary>Whether the panels are being laid out rather than played.</summary>
    private bool _linking;

    /// <summary>
    /// Whether the pointer is laying out the controller rather than playing.
    /// </summary>
    /// <remarks>
    /// Leaving the mode clears whatever was being offered, because an offer is about where the
    /// pointer was resting in a mode that has been left.
    /// </remarks>
    public bool IsLinking
    {
        get => _linking;
        set
        {
            if (_linking == value) return;

            _linking = value;

            Log.Write(LogArea.Midi, () => "link: pointing mode " + (_linking ? "on" : "off"));

            if (!_linking) _offered = null;

            Say(() => Changed?.Invoke());
        }
    }

    /// <summary>What the pointer is resting on, or nothing.</summary>
    private ControlMapping? _offered;

    /// <summary>What is being offered to the controller, for a panel that wants to light it.</summary>
    public ControlMapping? Offered => _offered;

    /// <summary>Raised when the mode, the offer or the mappings changed.</summary>
    /// <remarks>
    /// Always on the drawing thread, whatever thread caused it. A link is made by a message
    /// from the controller, which arrives on the MIDI thread, and everything listening to this
    /// is a panel or a list: an observable collection rebuilt from a MIDI callback is a throw
    /// at best, and at worst a list that is quietly half rebuilt. Raised through
    /// <c>Say</c> for that reason and no other.
    /// </remarks>
    public event Action? Changed;

    /// <summary>Raised when a hardware control took something, with what it took.</summary>
    public event Action<ControlMapping>? Linked;

    /// <summary>
    /// Keeps what a mapping has learned about itself, and tells whatever is showing it.
    /// </summary>
    /// <remarks>
    /// For the router, which works out what kind of control is sending and writes the answer
    /// onto the mapping. The mapping is the settings' own object, so it is already changed by
    /// then; what is left is to write the settings down and let the list redraw.
    /// </remarks>
    public void Say()
    {
        _changed();

        Say(() => Changed?.Invoke());
    }

    /// <summary>Says something happened, on the thread the things listening are drawn on.</summary>
    /// <remarks>
    /// Every change to either list ends here, so this is also where the merged list is told it
    /// is out of date. Two of the callers are not changes at all, the pointing mode going on and
    /// off, and those cost one rebuild on the next message, which is nothing and is much cheaper
    /// than remembering to say so in seven places and forgetting in the eighth.
    /// </remarks>
    private void Say(Action said)
    {
        Edited();

        if (Dispatcher.UIThread.CheckAccess()) said();
        else Dispatcher.UIThread.Post(said);
    }

    /// <summary>
    /// The pointer came to rest on something a hardware control could drive.
    /// </summary>
    /// <remarks>
    /// The mapping arrives with everything but the controller filled in: which machine, which
    /// parameter, whether it follows the cursor. What is missing is the half only the hardware
    /// can say, and that is what <see cref="Handle"/> fills in.
    /// </remarks>
    /// <param name="what">
    /// What the pointer is resting on, as a mapping with its controller half still empty, or
    /// null when the pointer has left and there is nothing on offer.
    /// </param>
    public void Offer(ControlMapping? what)
    {
        if (!_linking) return;
        if (ReferenceEquals(_offered, what)) return;

        _offered = what;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Midi, () =>
            what == null
                ? "link: nothing offered"
                : "link: offering " + what.Kind + " " + (what.Name.Length > 0 ? what.Name : what.Key)
                  + " (machine '" + what.Machine + "' key '" + what.Key + "')");

        Say(() => Changed?.Invoke());
    }

    /// <summary>
    /// A message arrived while the controller was being laid out.
    /// </summary>
    /// <returns>
    /// The link it just made, or null when it made none.
    /// </returns>
    /// <remarks>
    /// A note is taken as well as a controller, and only its press half. Every link made before
    /// the pads joined this layer was a knob or a button sending a controller; a pad box sends
    /// notes, so a gesture that would only learn a controller is a gesture that does nothing on
    /// most of the hardware people own. A note off arriving while something is offered is the
    /// hand coming off the pad it just learned, and learning it a second time from that would
    /// point the release at whatever was offered next.
    ///
    /// Nothing is swallowed. Pointing mode used to eat the message that made a link, on the
    /// reasoning that a knob being assigned should not also be turning something, and that was
    /// wrong in the only way that matters: you point at a filter, turn the knob, and nothing
    /// whatever happens. There is no way to tell a link that worked from one that did not.
    ///
    /// So the message goes on to the router afterwards and the control answers at once. That
    /// is the confirmation, and it is a better one than any light: the thing you pointed at
    /// moves. A knob already linked moves its parameter in this mode too, which is how you
    /// check what a controller is wired to without leaving the mode to find out.
    ///
    /// Nothing is said in the log for a message that arrives while nothing is being pointed at.
    /// That is the state the application is in almost always, and saying so per message would be
    /// a log about itself rather than about what happened.
    ///
    /// One knob does one thing, when a knob is learned, so both lists are asked to give up what
    /// this displaces. Two things are displaced: whatever was on this control, and whatever was
    /// on this target, because pointing a second knob at a filter is saying you want that one on
    /// it and not that you want two. The second of those is also the only thing that ever takes
    /// a link off a controller that is not plugged in. The router will happily drive two things
    /// from one control and that is a real arrangement, but it is one to build on purpose in the
    /// list rather than one to arrive at by forgetting the first was taken.
    ///
    /// The offer is held rather than made again afterwards: wiggling the same knob twice is one
    /// link, and the second wiggle must not make a second mapping out of the same offer.
    ///
    /// Every link goes on the desk. A song used to be able to hold links of its own, made by
    /// pointing at an instrument on a track or at a strip on the mixer, and they are templates
    /// now: what a knob does to a machine is true of every song that plays that machine, so a
    /// copy per song was the same work done again and could be handed to nobody. What an older
    /// song is still holding is read and is still displaced by an arriving link, so nothing that
    /// was already laid down starts fighting what is laid down now.
    /// </remarks>
    public ControlMapping? Handle(MidiMessage message)
    {
        if (message is null) return null;

        if (!_linking) return null;

        if (message.Type is not (MidiMessageType.ControlChange or MidiMessageType.Note)) return null;

        if (message.Type == MidiMessageType.Note && !message.IsOn) return null;

        if (_offered is not { } wanted) return null;

        wanted.Device = Named(message.Device ?? "");
        wanted.Channel = message.Channel;
        wanted.Cc = message.Value;
        wanted.Sends = message.Type;

        int held;

        lock (_lock)
        {
            Displace(wanted);

            _mappings.Add(wanted);

            held = _mappings.Count;
        }

        Wake(new[] { wanted });

        _offered = null;

        _changed();

        Log.Write(LogArea.Midi, () =>
            "link: " + (wanted.Sends == MidiMessageType.Note ? "note " : "CC ")
            + wanted.Cc + " ch" + wanted.Channel + " now moves "
            + (wanted.Name.Length > 0 ? wanted.Name : wanted.Key)
            + ", " + held + " on the desk");

        Say(() =>
        {
            Linked?.Invoke(wanted);
            Changed?.Invoke();
        });

        return wanted;
    }

    /// <summary>
    /// Lays several links down at once, as one act.
    /// </summary>
    /// <remarks>
    /// What an import is. The rules are the ones a link made by hand keeps, and they have to be:
    /// one control does one job, so an arriving link displaces whatever held its control and
    /// whatever else was pointed at its target, including anything an older song is still
    /// holding. A template that half applied because it was laid down some other way would be
    /// worse than one that was refused.
    ///
    /// One act rather than a run of them. The list is said to have changed once, so the page is
    /// not rebuilt forty times, and the settings are written once. That is also why this is here
    /// rather than a loop at the caller: a caller looping over <see cref="Handle"/> would be
    /// right about every link and wrong about the whole.
    /// </remarks>
    /// <param name="arriving">The links to lay down. Each is taken as it is, hardware and all.</param>
    public int Take(IEnumerable<ControlMapping>? arriving)
    {
        var all = arriving?.Where(one => one is not null).ToList() ?? new List<ControlMapping>();

        if (all.Count == 0) return 0;

        foreach (var one in all)
        {
            lock (_lock)
            {
                Displace(one);

                _mappings.Add(one);
            }
        }

        Wake(all, alone: true);

        _changed();

        Log.Write(LogArea.Midi, () =>
            "link: took " + all.Count + " links on to the desk, live for this session");

        Say(() =>
        {
            foreach (var one in all) Linked?.Invoke(one);

            Changed?.Invoke();
        });

        return all.Count;
    }

    /// <summary>
    /// Everything pointed at anything, which is the library.
    /// </summary>
    /// <remarks>
    /// **One layer.** A knob pointed at something is a fact about the box on your desk and about
    /// the thing it drives, true of every song that plays it, so there is one list and everything
    /// lands on it. It was two for a while, a song's and the desk's, and which one a link landed
    /// in depended on which of two identical looking panels the pointer happened to be over.
    ///
    /// Not what the hardware reaches, which is <see cref="Live"/>: this is every template there
    /// is and that is the part somebody has applied.
    ///
    /// A copy, taken safely, because it is written from the MIDI thread.
    /// </remarks>
    public IReadOnlyList<ControlMapping> Mappings
    {
        get
        {
            lock (_lock)
            {
                if (_merged is not null && _deskWas == _edits) return _merged;

                _merged = _mappings.ToArray();
                _deskWas = _edits;

                return _merged;
            }
        }
    }

    /// <summary>
    /// What is live this session, which is what the hardware actually reaches.
    /// </summary>
    /// <remarks>
    /// **Nothing whatever is live when the application starts.** The links on the disc are the
    /// library: every template you have ever made, for the mixer, for the pads and for each sound
    /// device, and most of them are for hardware that is not on the desk this afternoon and for
    /// machines this song does not play. A library that wired itself up on start would mean the
    /// first knob you touched doing whatever you last pointed it at, months ago, with nothing
    /// anywhere saying so.
    ///
    /// Two things make a link live and both are somebody asking: applying a template from a
    /// Menu, and learning a control, which is live from the moment it is learned or the gesture
    /// would appear not to have worked. It stays live for the session and no longer, since being
    /// live is not a fact about the link and is not written down.
    ///
    /// By the object rather than by what it names, so applying a template wakes exactly the links
    /// it laid down. The stored ones it displaced are gone from the list by then, and anything
    /// else in the library is left asleep where it is.
    /// </remarks>
    private readonly System.Collections.Generic.HashSet<ControlMapping> _live =
        new(System.Collections.Generic.ReferenceEqualityComparer.Instance as
            System.Collections.Generic.IEqualityComparer<ControlMapping>);

    /// <summary>
    /// Everything live, which is what the routing reads.
    /// </summary>
    /// <remarks>
    /// <see cref="Mappings"/> is the library and this is the part of it somebody has asked for.
    /// Apart rather than one filtered list, because the two answer different questions and both
    /// are asked: the MIDI CC page draws every template there is, and a knob may only reach what
    /// has been applied.
    ///
    /// **Off the desk rather than off the merged list, and that is not a shortcut.** The merge
    /// drops a desk link whenever the song holds one on the same channel and number, whatever the
    /// two are pointed at, which is right for two things competing to answer a message and wrong
    /// here: a song link is never applied, so it is not live, and filtering the merge let a dead
    /// link mask a live one. What that looked like was a template applied on the rack, its twelve
    /// links on the desk, and a knob driving the instrument of the track the cursor was on,
    /// because with nothing live to answer the message the default layout took it.
    ///
    /// Which also says what a song's own links now are, which is nothing: they are read so an
    /// older song still opens, they are still displaced by an arriving link, and they never drive
    /// anything, since the only two things that wake a link are on the desk.
    ///
    /// Kept until the desk is edited, since it is asked once per message.
    /// </remarks>
    public IReadOnlyList<ControlMapping> Live
    {
        get
        {
            lock (_lock)
            {
                if (_awake is not null && _awakeWas == _edits) return _awake;

                var kept = new List<ControlMapping>();

                foreach (var one in _mappings)
                    if (_live.Contains(one)) kept.Add(one);

                _awake = kept;
                _awakeWas = _edits;

                return kept;
            }
        }
    }

    /// <summary>The live list, kept until the desk is edited.</summary>
    private IReadOnlyList<ControlMapping>? _awake;

    /// <summary>What <see cref="_edits"/> stood at when it was built.</summary>
    private int _awakeWas = -1;

    /// <summary>
    /// Makes those links live for the rest of the session.
    /// </summary>
    /// <remarks>
    /// Said out loud in the log, because being live is the difference between a knob that works
    /// and one that does nothing, and it is the one thing about a link that nothing on the disc
    /// records.
    /// </remarks>
    /// <param name="links">What was applied or learned.</param>
    /// <param name="alone">
    /// Whether these are the only thing that may be live on what they are pointed at, which is
    /// true of a template being applied and false of one control being learned.
    /// </param>
    private void Wake(IReadOnlyList<ControlMapping> links, bool alone = false)
    {
        lock (_lock)
        {
            if (alone) Sleep(links);

            foreach (var one in links) _live.Add(one);

            _edits++;
            _awake = null;
            _merged = null;
        }
    }

    /// <summary>
    /// Puts to sleep whatever else is live on the things these are pointed at.
    /// </summary>
    /// <remarks>
    /// **One controller drives one thing at a time, and that is the whole reason applying is a
    /// deliberate act.** Two boxes on the desk both have a template for the mixer; you choose
    /// which of them is driving it this afternoon by applying that one, and choosing is
    /// meaningless if the one you chose last simply joins in.
    ///
    /// It is not a displacement and nothing is lost. A link taken off here is still on the desk
    /// and still in its own template, ready to be applied again in a moment; what it stops being
    /// is live. The links being laid down at the same moment do the displacing, by the ordinary
    /// rule, which is one control doing one job.
    ///
    /// By what a target is called rather than by <see cref="ControlMapping.SameTarget"/>, since a
    /// mixer template is the whole desk and same target there is one strip: matched that way,
    /// applying one controller's mixer template would silence the other's fader four and leave
    /// its fader five running.
    /// </remarks>
    /// <param name="links">What is arriving.</param>
    private void Sleep(IReadOnlyList<ControlMapping> links)
    {
        var taken = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);

        foreach (var one in links) taken.Add(_naming.KeyOf(one));

        int gone = _live.RemoveWhere(one => taken.Contains(_naming.KeyOf(one)));

        if (gone > 0) _awake = null;

        if (gone > 0)
            Log.Write(LogArea.Midi, () =>
                "link: " + gone + " link(s) already live on the same thing went to sleep");
    }

    /// <summary>What a target is called, so this sleeps by the rule a template is cut by.</summary>
    private readonly Interfaces.ILinkTargets _naming = new LinkTargets();

    /// <summary>
    /// The merged list, kept until something moves underneath it.
    /// </summary>
    /// <remarks>
    /// This is asked once per message, which with a hand on three knobs is three hundred times a
    /// second, and it was rebuilding both lists into a new one every time: measured at 1688 bytes
    /// and two microseconds a message, all of it thrown away immediately. That is continuous
    /// rubbish for the collector to sweep at exactly the moment nothing should be pausing, and
    /// it gets worse rather than better once automation is recording from the same stream.
    ///
    /// Kept against three things, because there are three ways it can go stale: the song handing
    /// over a different list, that list growing or shrinking, and this one being edited. Every
    /// method here that touches either list counts an edit, so a link made or taken off is seen
    /// on the next message.
    /// </remarks>
    private IReadOnlyList<ControlMapping>? _merged;

    /// <summary>What <see cref="_edits"/> stood at when it was built.</summary>
    private int _deskWas = -1;

    /// <summary>How many times the list has been edited through this.</summary>
    private int _edits;

    /// <summary>Says the merged list is out of date. Called by everything that edits either.</summary>
    private void Edited()
    {
        lock (_lock)
        {
            _edits++;
            _merged = null;
        }
    }

    /// <summary>Everything on the desk, which is every link there is.</summary>
    /// <remarks>
    /// The same list <see cref="Mappings"/> gives, under the name a page that shows the links
    /// asks for. There was a second list beside it, the open song's own, and there is not now.
    /// </remarks>
    public IReadOnlyList<ControlMapping> Desk
    {
        get { lock (_lock) return _mappings.ToArray(); }
    }

    /// <summary>Takes off whatever the new link is replacing: its control, and its target.</summary>
    /// <remarks>
    /// Both halves are about one controller. The same physical control pointed somewhere else is
    /// obviously so; the same target pointed at by something else has to be, or a second box on
    /// the desk deletes the first box's layout as it is learned.
    ///
    /// That is what it did. A link records the controller it was learned on and answers only its
    /// own messages, so two desks pointed at one machine can never both fire and are not
    /// competing for it: A and B both drive machine 1 and neither displaces the other, which is
    /// the whole of what makes hardware A and B against machines 1 and 2 four templates rather
    /// than a fight. Without the controller in the test it was one template, silently, and the
    /// half of it that had been learned again elsewhere was gone.
    ///
    /// It cost twice, because a template here is the links themselves rather than a file: what
    /// the surfaces line on a machine's face lists is what survived, so the repair somebody
    /// reaches for was itself made out of the damage.
    /// </remarks>
    private void Displace(ControlMapping wanted) =>
        _mappings.RemoveAll(one => SameDesk(one, wanted)
                                   && (one.SameTarget(wanted)
                                       || (SameKnob(one, wanted) && !Apart(one, wanted))));

    /// <summary>
    /// True when those two are the same control, the box having already been settled.
    /// </summary>
    /// <remarks>
    /// **Deliberately narrower than <see cref="ControlMapping.SameControl"/>**, which also
    /// compares the port a link was learned on. Here <see cref="SameDesk"/> has already answered
    /// which box this is, and a box is not its port: with the port compared again, a knob learned
    /// while the MiniLab's screen port was delivering was a different control from the same knob
    /// on its main port, so pointing it somewhere else left the first link firing.
    /// </remarks>
    /// <param name="one">A link already on the desk.</param>
    /// <param name="wanted">The link arriving.</param>
    private static bool SameKnob(ControlMapping one, ControlMapping wanted) =>
        one.Sends == wanted.Sends && one.Channel == wanted.Channel && one.Cc == wanted.Cc;

    /// <summary>True when those two links could ever answer the same controller.</summary>
    /// <remarks>
    /// A link naming no controller is the wildcard a link made before controllers were recorded
    /// reads as: <see cref="ControlMapping.Answers"/> lets it answer every device, so it really
    /// would fire beside an arriving link and it is displaced by any of them.
    ///
    /// **A box on two ports is one desk**, which the port names do not say: a MiniLab 3 is
    /// <c>Minilab3 MIDI</c> and <c>Minilab3 ALV</c> to this machine and is one thing under the
    /// hand, and the same knob arrives down both. Compared by port alone, learning that knob
    /// displaced nothing, so one control did one job twice: two links, two rows under one card,
    /// and both of them firing. What a profile calls the device is the only thing here that knows
    /// the two ports are one box, which is why <see cref="Called"/> is asked as well.
    ///
    /// By port first, since that answers with no profile at all, and a device nobody has written
    /// a file for is the case this whole layer is built to work in.
    /// </remarks>
    /// <param name="one">A link already on the desk.</param>
    /// <param name="wanted">The link arriving.</param>
    private bool SameDesk(ControlMapping one, ControlMapping wanted)
    {
        if (one.Device.Length == 0 || wanted.Device.Length == 0) return true;

        if (MidiService.SameName(one.Device, wanted.Device)) return true;

        if (Called is not { } called) return false;

        string mine = called(one.Device);
        string theirs = called(wanted.Device);

        return mine.Length > 0 && theirs.Length > 0 && MidiService.SameName(mine, theirs);
    }

    /// <summary>
    /// True when two links share a control but can never answer the same message.
    /// </summary>
    /// <remarks>
    /// One knob does one job, which is why a new link takes the old one off the control it was
    /// on. But a link about a machine only answers while the track is playing that machine, so
    /// two on one knob naming two machines are not competing for it: at most one of them can
    /// ever match, and which depends on where you are.
    ///
    /// That turns one encoder into "the filter, on whatever machine I am looking at", spelled
    /// out once per machine, which is one job and not several. Pointing the same knob at a
    /// different parameter of the same machine still replaces, which is the case the rule was
    /// protecting: that really would be two jobs on one knob and both would fire.
    ///
    /// A link naming no machine answers for all of them, so it is never apart from anything, and
    /// neither is a second mixer link: a strip has no machine to tell two of them apart, so both
    /// would answer the same message.
    ///
    /// **And two links of different kinds are apart, whatever they are on**, which is the shape
    /// the whole layer is arranged in: one controller against the mixer, the pads, or one
    /// machine, is one template apiece. A desk pointed at the mixer and then at a machine keeps
    /// both, and a knob that drives a fader and a filter is a knob doing one job in each of two
    /// places a person thinks of separately.
    ///
    /// It was the other way round, and the two ports of a MiniLab hid it: mixer links learned
    /// while one port was delivering and machine links on the other were two desks, so neither
    /// displaced the other. One box, one desk, and the mixer's template went the moment a machine
    /// was learned on the same knob.
    /// </remarks>
    private static bool Apart(ControlMapping one, ControlMapping wanted)
    {
        if (one.Kind != wanted.Kind) return true;

        return one.Kind switch
        {
            ControlKind.SoundDevice or ControlKind.Action =>
                one.Machine.Length > 0 && wanted.Machine.Length > 0
                && !string.Equals(one.Machine, wanted.Machine, StringComparison.Ordinal),

            ControlKind.Plugin =>
                one.Plugin.Length > 0 && wanted.Plugin.Length > 0
                && !string.Equals(one.Plugin, wanted.Plugin, StringComparison.Ordinal),

            _ => false
        };
    }

    /// <summary>What is pointed at this, if anything, for a panel that wants to say so.</summary>
    public ControlMapping? LinkOn(ControlKind kind, string machine, string key) =>
        _mappings.FirstOrDefault(one =>
            one.Kind == kind
            && string.Equals(one.Machine, machine, StringComparison.Ordinal)
            && string.Equals(one.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// Which of a machine's parameters already have something pointed at them.
    /// </summary>
    /// <remarks>
    /// For a panel that wants to ring them. A mapping naming no machine is counted too: it is
    /// one somebody made to mean any machine, and on this machine it is one of these.
    /// </remarks>
    public IReadOnlyCollection<string> KeysOn(string machine)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var one in Mappings)
        {
            if (one.Kind != ControlKind.SoundDevice || one.Key.Length == 0) continue;

            if (one.Machine.Length > 0 && !string.Equals(one.Machine, machine, StringComparison.Ordinal))
                continue;

            keys.Add(one.Key);
        }

        return keys;
    }

    /// <summary>
    /// Whether anything on the desk is already pointed at what this names.
    /// </summary>
    /// <remarks>
    /// **The other half of the hook, and the one that was only ever built for a drawn panel.** A
    /// face of a machine is handed <see cref="KeysOn"/> and rings the parameters somebody has
    /// already wired, so turning the mode on is how you see what your controller does. The mixer
    /// and the pads are ordinary controls rather than a drawn face, so nothing asked this of them
    /// and nothing marked them: a template applied there was invisible until a knob was turned.
    ///
    /// Asked with the same object the control offers, since that is what a control already
    /// carries and is the only thing on the page that knows what it is pointed at. So it is one
    /// question whatever kind of thing is asking, which is what keeps the mixer, the pads and a
    /// device's face from each having a rule of their own that could drift.
    ///
    /// By the target and never by the controller: the mark says this fader has something on it,
    /// not which desk. Two controllers pointed at one fader is two links and one ring, which is
    /// right, since what the ring is warning about is that pointing here replaces something.
    /// </remarks>
    /// <param name="wanted">What the control offers, which names the target and nothing else.</param>
    public bool Holds(ControlMapping? wanted) =>
        wanted is not null && Live.Any(one => one.SameTarget(wanted));

    /// <summary>
    /// Takes off whatever is pointed at that parameter or that button of that machine.
    /// </summary>
    /// <remarks>
    /// Both kinds at once, because what arrives from a panel is the name of the thing that was
    /// pressed and a panel does not say which sort it was. A parameter key and an action name
    /// are both just a word, and one machine will not have the same word for both.
    /// </remarks>
    public void Unlink(string machine, string key)
    {
        int gone;

        lock (_lock) gone = _mappings.RemoveAll(one =>
            (one.Kind == ControlKind.SoundDevice || one.Kind == ControlKind.Action)
            && string.Equals(one.Machine, machine, StringComparison.Ordinal)
            && string.Equals(one.Key, key, StringComparison.Ordinal));

        if (gone == 0) return;

        _changed();
        Say(() => Changed?.Invoke());
    }

    /// <summary>Which of a machine's buttons already have something pointed at them.</summary>
    public IReadOnlyCollection<string> ActionsOn(string machine)
    {
        var doing = new HashSet<string>(StringComparer.Ordinal);

        foreach (var one in Mappings)
        {
            if (one.Kind != ControlKind.Action || one.Key.Length == 0) continue;

            if (one.Machine.Length > 0 && !string.Equals(one.Machine, machine, StringComparison.Ordinal))
                continue;

            doing.Add(one.Key);
        }

        return doing;
    }

    /// <summary>
    /// Takes off everything learned on one controller, because it is being forgotten.
    /// </summary>
    /// <remarks>
    /// The one thing that unwires an absent device, and it is a decision rather than a
    /// circumstance. A controller not plugged in keeps its links: it is in the other room, and
    /// the layout has to be there when it comes back. A controller somebody has pressed Forget
    /// on is a controller they are done with, and leaving its links behind would leave a list
    /// full of instructions for hardware that is not coming back.
    /// </remarks>
    /// <returns>How many were taken off, for a page that wants to say so.</returns>
    public int Forget(string device)
    {
        if (string.IsNullOrWhiteSpace(device)) return 0;

        int gone;

        lock (_lock) gone = _mappings.RemoveAll(one => MidiService.SameName(one.Device, device));

        if (gone == 0) return 0;

        _changed();

        Say(() => Changed?.Invoke());

        return gone;
    }

    /// <summary>Takes a link off, for a control that is pointed at and clicked.</summary>
    public void Unlink(ControlMapping? mapping)
    {
        if (mapping is null) return;

        bool removed;

        lock (_lock) removed = _mappings.Remove(mapping);

        if (!removed) return;

        _changed();
        Say(() => Changed?.Invoke());
    }
}
