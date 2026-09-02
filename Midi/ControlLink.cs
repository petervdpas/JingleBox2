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

        _mappings.RemoveAll(one => one.Kind == ControlKind.Insert);
    }

    /// <summary>
    /// The one this session is using.
    /// </summary>
    /// <remarks>
    /// A static, and the same reason as <see cref="Devices.SoundMachines.SoundMachineProjects"/>:
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
    /// The open song's own layout, when there is one, and how to say it has changed.
    /// </summary>
    /// <remarks>
    /// Set once as the window is built. A link is not a setting of the song in the way a
    /// pattern is: nothing here reaches into the tracker, it is handed a list and a way of
    /// saying the list moved.
    /// </remarks>
    public Func<List<ControlMapping>?>? Song { get; set; }

    /// <summary>Told when the song's own layout changed, so the song reads as unsaved.</summary>
    public Action? SongChanged { get; set; }

    /// <summary>True when there is a song open for a link to be kept in or moved to.</summary>
    public bool HasSong => Song?.Invoke() is not null;

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

        if (message.Type != MidiMessageType.ControlChange) return null;

        if (_offered is not { } wanted) return null;

        wanted.Device = message.Device ?? "";
        wanted.Channel = message.Channel;
        wanted.Cc = message.Value;

        Changing();

        Displace(Song?.Invoke(), wanted);

        int held;

        lock (_lock)
        {
            Displace(_mappings, wanted);

            _mappings.Add(wanted);

            held = _mappings.Count;
        }

        _offered = null;

        _changed();

        Log.Write(LogArea.Midi, () =>
            "link: CC " + wanted.Cc + " ch" + wanted.Channel + " now moves "
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

        Changing();

        foreach (var one in all)
        {
            Displace(Song?.Invoke(), one);

            lock (_lock)
            {
                Displace(_mappings, one);

                _mappings.Add(one);
            }
        }

        _changed();

        Log.Write(LogArea.Midi, () => "link: took " + all.Count + " links on to the desk");

        Say(() =>
        {
            foreach (var one in all) Linked?.Invoke(one);

            Changed?.Invoke();
        });

        return all.Count;
    }

    /// <summary>
    /// Everything pointed at anything: the song's layout first, then the desk's.
    /// </summary>
    /// <remarks>
    /// The song's win where both name the same control, which is what makes the song's an
    /// override rather than a second list. The desk is what a control does unless this song has
    /// something to say about it.
    ///
    /// A copy, taken safely, because the desk's half is written from the MIDI thread.
    /// </remarks>
    public IReadOnlyList<ControlMapping> Mappings
    {
        get
        {
            var song = Song?.Invoke();

            lock (_lock)
            {
                if (_merged is not null
                    && ReferenceEquals(_songWas, song)
                    && _songCount == (song?.Count ?? 0)
                    && _deskWas == _edits)
                    return _merged;

                var desk = _mappings;

                if (song is null || song.Count == 0)
                {
                    _merged = desk.ToArray();
                }
                else
                {
                    var all = new List<ControlMapping>(song.Count + desk.Count);
                    all.AddRange(song);

                    foreach (var one in desk)
                    {
                        bool covered = false;

                        for (int at = 0; at < song.Count && !covered; at++)
                            covered = song[at].Channel == one.Channel && song[at].Cc == one.Cc;

                        if (!covered) all.Add(one);
                    }

                    _merged = all;
                }

                _songWas = song;
                _songCount = song?.Count ?? 0;
                _deskWas = _edits;

                return _merged;
            }
        }
    }

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

    /// <summary>The song's list the merge was built from, compared by reference.</summary>
    private List<ControlMapping>? _songWas;

    /// <summary>How long it was then, since a song edits its own list in place.</summary>
    private int _songCount = -1;

    /// <summary>What <see cref="_edits"/> stood at then, which covers the desk's half.</summary>
    private int _deskWas = -1;

    /// <summary>How many times either list has been edited through this.</summary>
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

    /// <summary>
    /// Told before the song's own list is about to change, so it can be taken back.
    /// </summary>
    /// <remarks>
    /// Apart from <see cref="SongChanged"/>, which says it already happened. A history needs the
    /// state being left rather than the one being arrived at, and afterwards the first is gone.
    /// Nothing at all for the desk's half: that lives in the settings and is not part of any
    /// song, so undoing a song has nothing to say about it.
    /// </remarks>
    public Action? SongChanging;

    /// <summary>Says the song is about to lose or gain a link, when there is a song.</summary>
    private void Changing()
    {
        if (Song?.Invoke() is not null) SongChanging?.Invoke();
    }

    /// <summary>Just the desk's, for a list that shows the two apart.</summary>
    public IReadOnlyList<ControlMapping> Desk
    {
        get { lock (_lock) return _mappings.ToArray(); }
    }

    /// <summary>And just the song's.</summary>
    public IReadOnlyList<ControlMapping> Kept => Song?.Invoke()?.ToArray() ?? Array.Empty<ControlMapping>();

    /// <summary>True when that mapping is one the song keeps rather than one of the desk's.</summary>
    public bool IsSong(ControlMapping mapping) => Song?.Invoke()?.Contains(mapping) == true;

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
    private static void Displace(List<ControlMapping>? from, ControlMapping wanted) =>
        from?.RemoveAll(one => SameDesk(one, wanted)
                               && (one.SameTarget(wanted)
                                   || (one.SameControl(wanted) && !Apart(one, wanted))));

    /// <summary>True when those two links could ever answer the same controller.</summary>
    /// <remarks>
    /// A link naming no controller is the wildcard a link made before controllers were recorded
    /// reads as: <see cref="ControlMapping.Answers"/> lets it answer every device, so it really
    /// would fire beside an arriving link and it is displaced by any of them.
    /// </remarks>
    /// <param name="one">A link already on the desk.</param>
    /// <param name="wanted">The link arriving.</param>
    private static bool SameDesk(ControlMapping one, ControlMapping wanted) =>
        one.Device.Length == 0
        || wanted.Device.Length == 0
        || MidiService.SameName(one.Device, wanted.Device);

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
    /// neither is a mixer link: a strip has no machine to tell two of them apart, so both would
    /// answer the same message.
    /// </remarks>
    private static bool Apart(ControlMapping one, ControlMapping wanted)
    {
        if (one.Kind != wanted.Kind) return false;

        return one.Kind switch
        {
            ControlKind.Device or ControlKind.Action =>
                one.Machine.Length > 0 && wanted.Machine.Length > 0
                && !string.Equals(one.Machine, wanted.Machine, StringComparison.Ordinal),

            ControlKind.Insert =>
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
            if (one.Kind != ControlKind.Device || one.Key.Length == 0) continue;

            if (one.Machine.Length > 0 && !string.Equals(one.Machine, machine, StringComparison.Ordinal))
                continue;

            keys.Add(one.Key);
        }

        return keys;
    }

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
        Changing();

        int gone = Song?.Invoke()?.RemoveAll(one =>
            (one.Kind == ControlKind.Device || one.Kind == ControlKind.Action)
            && string.Equals(one.Machine, machine, StringComparison.Ordinal)
            && string.Equals(one.Key, key, StringComparison.Ordinal)) ?? 0;

        lock (_lock) gone += _mappings.RemoveAll(one =>
            (one.Kind == ControlKind.Device || one.Kind == ControlKind.Action)
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

        Changing();

        int gone = Song?.Invoke()?.RemoveAll(one => MidiService.SameName(one.Device, device)) ?? 0;

        lock (_lock) gone += _mappings.RemoveAll(one => MidiService.SameName(one.Device, device));

        if (gone == 0) return 0;

        _changed();
        SongChanged?.Invoke();

        Say(() => Changed?.Invoke());

        return gone;
    }

    /// <summary>Takes a link off, for a control that is pointed at and clicked.</summary>
    public void Unlink(ControlMapping? mapping)
    {
        if (mapping is null) return;

        bool removed = Song?.Invoke()?.Remove(mapping) == true;

        if (!removed) lock (_lock) removed = _mappings.Remove(mapping);

        if (!removed) return;

        _changed();
        Say(() => Changed?.Invoke());
    }
}
