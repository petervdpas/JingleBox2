using Avalonia.Threading;
using System;
using JingleBox2.Diagnostics;
using System.Collections.Generic;
using System.Linq;

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
    private readonly List<ControlMapping> _mappings;
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

    public ControlLink(List<ControlMapping> mappings, Action changed)
    {
        _mappings = mappings;
        _changed = changed;
    }

    /// <summary>
    /// The one this session is using.
    /// </summary>
    /// <remarks>
    /// A static, and the same reason as <see cref="Tracker.Machines.MachineProjects"/>: there is
    /// exactly one controller on the desk, the mode it is in is the same mode everywhere at
    /// once, and the panels that have to know are drawn from a description that has never heard
    /// of a view model. Threading a reference through every designer, window and panel to say
    /// one bool would be a lot of wiring to express something that is true of the application
    /// rather than of any part of it.
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

    private bool _linking;

    /// <summary>Whether the pointer is laying out the controller rather than playing.</summary>
    public bool IsLinking
    {
        get => _linking;
        set
        {
            if (_linking == value) return;

            _linking = value;

            Log.Write(LogArea.Midi, () => "link: pointing mode " + (_linking ? "on" : "off"));

            // An offer is about where the pointer was in a mode that has been left.
            if (!_linking) _offered = null;

            Say(() => Changed?.Invoke());
        }
    }

    /// <summary>What the pointer is resting on, or nothing.</summary>
    private ControlMapping? _offered;

    /// <summary>
    /// Whether what is being offered belongs to the song or to the desk.
    /// </summary>
    /// <remarks>
    /// Decided by where you pointed, not by whether a song happens to be open. A machine on the
    /// rack is the machine itself, and a knob pointed at it there is a fact about your hardware
    /// and that machine: true in every song you ever open. An instrument on a track is this
    /// song's, and a knob pointed at it there is about this piece of music.
    ///
    /// Laying out a controller on the rack with a song open used to put the whole layout in
    /// that song, which is the opposite of what anybody means by it.
    /// </remarks>
    private bool _offeredToSong;

    /// <summary>What is being offered to the controller, for a panel that wants to light it.</summary>
    public ControlMapping? Offered => _offered;

    /// <summary>Raised when the mode, the offer or the mappings changed.</summary>
    /// <remarks>
    /// Always on the drawing thread, whatever thread caused it. A link is made by a message
    /// from the controller, which arrives on the MIDI thread, and everything listening to this
    /// is a panel or a list: an observable collection rebuilt from a MIDI callback is a throw
    /// at best, and at worst a list that is quietly half rebuilt. Raised through
    /// <see cref="Say"/> for that reason and no other.
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
    private void Say(Action said)
    {
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
    /// <param name="keep">
    /// True when this belongs to the song being worked on: an instrument on a track. False for
    /// the machine itself on the rack, which is about the machine and not about any song.
    /// </param>
    public void Offer(ControlMapping? what, bool keep = false)
    {
        if (!_linking) return;
        if (ReferenceEquals(_offered, what)) return;

        _offered = what;
        _offeredToSong = keep;

        Diagnostics.Log.Write(Diagnostics.LogArea.Midi, () =>
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
    /// </remarks>
    public ControlMapping? Handle(MidiMessage message)
    {
        if (message is null) return null;

        // Nothing said for either of these. Not pointing at anything is the state the
        // application is in almost always, and saying so on every message is a log about
        // itself rather than about what happened.
        if (!_linking) return null;

        if (message.Type != MidiMessageType.ControlChange) return null;

        if (_offered is not { } wanted) return null;

        wanted.Device = message.Device ?? "";
        wanted.Channel = message.Channel;
        wanted.Cc = message.Value;

        // One knob does one thing, when a knob is learned. The router will happily drive two
        // things from one control and that is a real arrangement, but it is one to build on
        // purpose in the list, not one to arrive at by pointing at a second knob and forgetting
        // the first was taken.
        // Two things are displaced by this, and both lists are asked, because a link on the
        // desk and a link in the song are both things a new one can be replacing.
        //
        // What was on this control, so one knob does one thing. And what was on this target,
        // so one knob on the screen answers to one knob on the desk: pointing a second at a
        // filter is saying you want that one on it, not that you want two. Which is also the
        // only thing that ever displaces a mapping whose controller is not plugged in.
        Displace(Song?.Invoke(), wanted);
        lock (_lock) Displace(_mappings, wanted);

        if (_offeredToSong && Song?.Invoke() is { } keeping)
        {
            keeping.Add(wanted);

            SongChanged?.Invoke();
        }
        else
        {
            lock (_lock) _mappings.Add(wanted);
        }

        // Held rather than offered again. Wiggling the same knob twice is one link, and the
        // second wiggle should not make a second mapping out of the same offer.
        _offered = null;

        _changed();

        Log.Write(LogArea.Midi, () =>
            "link: CC " + wanted.Cc + " ch" + wanted.Channel + " now moves "
            + (wanted.Name.Length > 0 ? wanted.Name : wanted.Key) + ", " + _mappings.Count + " in all");

        Say(() =>
        {
            Linked?.Invoke(wanted);
            Changed?.Invoke();
        });

        return wanted;
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

            ControlMapping[] desk;

            lock (_lock) desk = _mappings.ToArray();

            if (song is null || song.Count == 0) return desk;

            var all = new List<ControlMapping>(song);

            foreach (var one in desk)
                if (!song.Any(said => said.Channel == one.Channel && said.Cc == one.Cc))
                    all.Add(one);

            return all;
        }
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
    private static void Displace(List<ControlMapping>? from, ControlMapping wanted) =>
        from?.RemoveAll(one => one.SameTarget(wanted)
                               || (one.SameControl(wanted) && !Apart(one, wanted)));

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
    /// A link naming no machine answers for all of them, so it is never apart from anything.
    /// </remarks>
    private static bool Apart(ControlMapping one, ControlMapping wanted)
    {
        if (one.Kind != wanted.Kind) return false;

        return one.Kind switch
        {
            ControlKind.Instrument or ControlKind.Action =>
                one.Machine.Length > 0 && wanted.Machine.Length > 0
                && !string.Equals(one.Machine, wanted.Machine, StringComparison.Ordinal),

            ControlKind.Insert =>
                one.Plugin.Length > 0 && wanted.Plugin.Length > 0
                && !string.Equals(one.Plugin, wanted.Plugin, StringComparison.Ordinal),

            // A strip has no machine to tell two of them apart, so both would answer.
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
            if (one.Kind != ControlKind.Instrument || one.Key.Length == 0) continue;

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
        int gone = Song?.Invoke()?.RemoveAll(one =>
            (one.Kind == ControlKind.Instrument || one.Kind == ControlKind.Action)
            && string.Equals(one.Machine, machine, StringComparison.Ordinal)
            && string.Equals(one.Key, key, StringComparison.Ordinal)) ?? 0;

        lock (_lock) gone += _mappings.RemoveAll(one =>
            (one.Kind == ControlKind.Instrument || one.Kind == ControlKind.Action)
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

    /// <summary>Which of a plugin's parameters already have something pointed at them.</summary>
    public IReadOnlyCollection<uint> ParametersOn(string plugin)
    {
        var taken = new HashSet<uint>();

        foreach (var one in Mappings)
        {
            if (one.Kind != ControlKind.Insert) continue;
            if (!string.Equals(one.Plugin, plugin, StringComparison.Ordinal)) continue;

            taken.Add(one.Parameter);
        }

        return taken;
    }

    /// <summary>Takes off whatever is pointed at that parameter of that plugin.</summary>
    public void UnlinkPlugin(string plugin, uint parameter)
    {
        int gone = Song?.Invoke()?.RemoveAll(one =>
            one.Kind == ControlKind.Insert
            && string.Equals(one.Plugin, plugin, StringComparison.Ordinal)
            && one.Parameter == parameter) ?? 0;

        lock (_lock) gone += _mappings.RemoveAll(one =>
            one.Kind == ControlKind.Insert
            && string.Equals(one.Plugin, plugin, StringComparison.Ordinal)
            && one.Parameter == parameter);

        if (gone == 0) return;

        _changed();
        Say(() => Changed?.Invoke());
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
