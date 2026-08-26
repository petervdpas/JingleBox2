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
    public void Offer(ControlMapping? what)
    {
        if (!_linking) return;
        if (ReferenceEquals(_offered, what)) return;

        _offered = what;

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

        wanted.Channel = message.Channel;
        wanted.Cc = message.Value;

        // One knob does one thing, when a knob is learned. The router will happily drive two
        // things from one control and that is a real arrangement, but it is one to build on
        // purpose in the list, not one to arrive at by pointing at a second knob and forgetting
        // the first was taken.
        lock (_lock)
        {
            _mappings.RemoveAll(one => one.Channel == wanted.Channel && one.Cc == wanted.Cc);
            _mappings.Add(wanted);
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

    /// <summary>Everything pointed at anything, for a list to show. A copy, taken safely.</summary>
    public IReadOnlyList<ControlMapping> Mappings
    {
        get { lock (_lock) return _mappings.ToArray(); }
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
        int gone;

        lock (_lock) gone = _mappings.RemoveAll(one =>
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
        int gone;

        lock (_lock) gone = _mappings.RemoveAll(one =>
            one.Kind == ControlKind.Insert
            && string.Equals(one.Plugin, plugin, StringComparison.Ordinal)
            && one.Parameter == parameter);

        if (gone == 0) return;

        _changed();
        Say(() => Changed?.Invoke());
    }

    /// <summary>Takes a link off, for a control that is pointed at and clicked.</summary>
    public void Unlink(ControlMapping? mapping)
    {
        bool removed;

        lock (_lock) removed = mapping is not null && _mappings.Remove(mapping);

        if (!removed) return;

        _changed();
        Say(() => Changed?.Invoke());
    }
}
