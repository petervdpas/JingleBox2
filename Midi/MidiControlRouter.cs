using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Controllers.Interfaces;
using JingleBox2.Controllers;

namespace JingleBox2.Midi;

/// <summary>
/// Turns a knob or a fader on a controller into a value somewhere in the program.
/// </summary>
/// <remarks>
/// The third router, and the same shape as the two before it: this one knows the mappings and
/// nothing about the application, and an adapter reaches the things being moved. See
/// <see cref="MidiRouter"/> for pads and <see cref="MidiNoteRouter"/> for notes.
///
/// A knob is not a key, and the difference is the whole of the work here. A key is an event: it
/// happened, and the pad plays. A knob is a stream, around a hundred messages a second while a
/// hand is on it, and it arrives carrying an opinion about where the parameter should be that
/// has nothing to do with where the parameter is. Reconciling those two is
/// <see cref="ControlPickup"/>, and it is what decides whether a controller feels attached to
/// the sound or merely wired to it.
/// </remarks>
public sealed class MidiControlRouter
{
    /// <summary>What is known about the controllers plugged in. Holds a cache, so it is shared rather than made twice.</summary>
    private readonly IControllerProfiles _profiles;

    /// <summary>The top of a continuous controller's range.</summary>
    private const double Full = 127.0;

    /// <summary>The middle of a relative encoder's range: turned no distance at all.</summary>
    private const int Still = 64;

    private readonly Func<IReadOnlyList<ControlMapping>> _mappings;
    private readonly IControlTargets _targets;

    /// <summary>
    /// What each mapping has seen, so pickup has something to compare against.
    /// </summary>
    /// <remarks>
    /// Keyed by the mapping itself rather than by its channel and number, so editing a mapping
    /// does not carry the old one's state across. Held weakly: a mapping the user deleted is
    /// not something this should be keeping alive to remember a knob position for.
    /// </remarks>
    private readonly ConditionalWeakTable<ControlMapping, Hand> _hands = new();

    /// <summary>Where a hand last had one control, and whether it has caught up yet.</summary>
    private sealed class Hand
    {
        /// <summary>Where the knob was last message, in the parameter's own units.</summary>
        /// <remarks>Not a number until the first message: there is no side to be on yet.</remarks>
        public double Last = double.NaN;

        /// <summary>True once the hand has passed the value and is driving it.</summary>
        public bool Caught;

        /// <summary>Whether a button is down, so a press is told from being held.</summary>
        public bool Down;

        /// <summary>The last number this control sent, for reading one message against the next.</summary>
        public int Was = -1;

        /// <summary>
        /// Which end this is sitting against: -1 the bottom, 1 the top, 0 neither.
        /// </summary>
        /// <remarks>
        /// A control that has driven a parameter into one of its ends is put aside until the
        /// stream turns round. See <see cref="Parked"/>.
        /// </remarks>
        public int Against;

        /// <summary>What kind of control this is, while that is still being worked out.</summary>
        public readonly ControlSense Sense = new();

        /// <summary>Whether the hunt for the parameter has been mentioned yet.</summary>
        /// <remarks>
        /// Once per hunt, not once per message. A hand on a knob sends a hundred a second and
        /// every one of them is reaching, so saying so each time would bury the log in the one
        /// situation somebody is reading it to understand.
        ///
        /// It was the one outcome on this path that was never said anywhere. A control that picks
        /// up does nothing at all until your hand passes the value the parameter already holds,
        /// which is correct and is what a hardware desk does, and from outside is
        /// indistinguishable from a link that has stopped working. Every other outcome here
        /// writes a line; this one raised an event nobody had subscribed to.
        /// </remarks>
        public bool Told;
    }

    /// <param name="mappings">
    /// Asked per message rather than held, because a link made a second ago has to answer now.
    /// </param>
    /// <param name="learned">
    /// Told when a control turned out to be something, so the answer can be written down. What is
    /// worked out here is written onto the mapping, which is the settings' own object, so all
    /// this has to do is make the settings be saved.
    /// </param>
    /// <param name="targets">
    /// The application, as things stand this second: what a mapping turns into when it is
    /// answered. The router knows mappings and nothing else.
    /// </param>
    /// <param name="layout">What a control does before anybody has pointed it at anything.</param>
    /// <param name="profiles">
    /// What is known about the controllers plugged in. Left out, one of its own; the application
    /// hands the same one to everything, since what a device is doing is remembered in it.
    /// </param>
    public MidiControlRouter(Func<IReadOnlyList<ControlMapping>> mappings, IControlTargets targets,
                             Action? learned = null, DefaultLayout? layout = null,
                             IControllerProfiles? profiles = null)
    {
        _profiles = profiles ?? new ControllerProfiles();
        _mappings = mappings;
        _targets = targets;
        _learned = learned;
        _layout = layout;
    }

    /// <summary>What a control does before anybody has pointed it at anything, or nothing.</summary>
    private readonly DefaultLayout? _layout;

    /// <summary>Told when a control turned out to be something, so the answer can be kept.</summary>
    private readonly Action? _learned;

    /// <summary>
    /// Raised when a control moved something, with what it moved and where it moved it to.
    /// </summary>
    /// <remarks>
    /// For the status line, and for a panel that wants to light the control being turned. Not
    /// for making the sound: that has already happened by the time this is raised.
    /// </remarks>
    public event Action<ControlMapping, IControlTarget, double>? Moved;

    /// <summary>
    /// Raised while a control is being ignored because it has not yet passed the value it is
    /// about to take over, so something can say why nothing is happening.
    /// </summary>
    /// <remarks>
    /// Carries the mapping as well as the target, because the only surface where this is worth
    /// saying is the controller's own screen, and reaching one means knowing which device the
    /// hand is on.
    /// </remarks>
    public event Action<ControlMapping, IControlTarget, double>? Reaching;

    /// <summary>
    /// Says a control already has the parameter it names, so it moves it from the next message.
    /// </summary>
    /// <remarks>
    /// For a link that has just been made. Pickup exists because the knob and the parameter
    /// disagree and your hand has not arrived yet, and neither is true here: you pointed at the
    /// parameter and turned that knob, a second ago, on purpose. Made to wait it would sit dead
    /// until it happened to sweep past the value, which reads as a link that did not work.
    ///
    /// Only the one that was just made. Everything else picks up as it always did, and this
    /// mapping will too, the next time the application starts.
    /// </remarks>
    public void Caught(ControlMapping mapping)
    {
        if (mapping is null) return;

        var hand = _hands.GetValue(mapping, _ => new Hand());

        hand.Caught = true;
        hand.Last = double.NaN;
    }

    /// <summary>
    /// Moves whatever this message is pointed at, and nothing when it is pointed at nothing.
    /// </summary>
    /// <remarks>
    /// Every mapping that answers, not the first. One knob on two parameters is a thing people do
    /// on purpose, and it is not this router's place to decide it is a mistake: a link only
    /// answers while a track is playing its machine, so two links on one knob naming two machines
    /// make an encoder "the filter, on whatever machine I am looking at".
    ///
    /// The default layout is asked last and only when nothing at all answered. Anything anybody
    /// pointed at anything wins, always, because a link names its parameter and a layout only
    /// names a place. The first time a control falls back on the layout it is treated as already
    /// caught: the layout has just watched three messages of it moving to decide what it is, so
    /// the hand is demonstrably on it, and made to pick up as well it would sit dead until it
    /// happened to sweep past the parameter, which reads as a control that does not work.
    ///
    /// Nothing is said about a control that reaches nothing. <see cref="ControlTargets"/> says it
    /// already, in the same breath as which track and which machine were asked, which is the half
    /// worth having.
    /// </remarks>
    public void Handle(MidiMessage message)
    {
        if (message is null || message.Type != MidiMessageType.ControlChange) return;

        var mappings = _mappings();
        if (mappings is null) return;

        bool answered = false;

        foreach (var mapping in mappings)
        {
            if (!mapping.Answers(message)) continue;

            answered = true;

            var target = _targets.Find(mapping);
            if (target is null) continue;

            Apply(mapping, target, message.Data);
        }

        if (!answered && _layout?.For(message) is { } fallback
                      && _targets.Find(fallback) is { } waiting)
        {
            if (!_hands.TryGetValue(fallback, out _)) Caught(fallback);

            Apply(fallback, waiting, message.Data);
        }
    }

    /// <summary>
    /// One message against one thing it is pointed at.
    /// </summary>
    /// <remarks>
    /// The order of the questions here is the whole of the behaviour, and every one of them was
    /// arrived at from something going wrong.
    ///
    /// What kind of control this is comes from the controller's own file where there is one, and
    /// from watching otherwise. The file wins, because it is a fact about the hardware and the
    /// other is an inference from three messages, and the inference is blind to the one case that
    /// matters most: an endless encoder reporting a position is indistinguishable from a fader
    /// until it comes round, so it is sensed as a fader, saved as one, and every session then
    /// opens with a hunt for the value using a knob that has nowhere to hunt from. A saved
    /// mapping is corrected by this rather than migrated, since the number in the file was a
    /// guess made before anything knew the device.
    ///
    /// Parking is asked before anything else. Nothing a message says can move a parameter past
    /// its own end, so once a control has driven one into an end the only question left is
    /// whether this message is still pushing the same way. See <see cref="Parked"/>.
    ///
    /// A button is the edge and not the position. A hardware button held down sends the same
    /// value over and over, so only the change counts, and there is nothing to sense and nothing
    /// to pick up: a press is a press.
    ///
    /// Nothing at all is done with a control until it is known what kind of control it is. Three
    /// messages, about thirty milliseconds, and holding them back is the point rather than a
    /// delay to be apologised for: an encoder read as a position throws the parameter to one end
    /// of its range in front of you. The three that told us are spent, deliberately: they were
    /// being listened to, not obeyed. Whatever it turned out to be, the hand is on it by then, so
    /// there is nothing left to pick up from.
    ///
    /// A position that crosses more than half the range between two messages ten milliseconds
    /// apart is not a hand, it is a counter coming round, so the control is read as endless from
    /// then on, for ever. The wrap is the only moment the difference between an endless knob and
    /// a fader ever shows.
    ///
    /// And when none of that applies the control is still reaching for the value. Where the knob
    /// is now is written down either way, since it is what the next message is measured against,
    /// both for the crossing and for the wrap.
    /// </remarks>
    private void Apply(ControlMapping mapping, IControlTarget target, int data)
    {
        var hand = _hands.GetValue(mapping, _ => new Hand());

        var pickup = _profiles.Pickup(mapping.Device, mapping.Channel, mapping.Cc)
                     ?? mapping.Pickup;

        if (pickup != mapping.Pickup)
        {
            Log.Write(LogArea.Midi, () =>
                "controls: CC " + mapping.Cc + " is read as " + ControlSense.Describe(pickup, mapping.Turn)
                + " because its controller's file says so, not as "
                + ControlSense.Describe(mapping.Pickup, mapping.Turn) + ", which is what watching it suggested");

            mapping.Pickup = pickup;
        }

        if (Parked(hand, mapping, data))
        {
            hand.Was = data;
            return;
        }

        if (mapping.Kind == ControlKind.Action)
        {
            bool down = data >= Still;

            if (down == hand.Down) return;

            hand.Down = down;

            if (down) Put(hand, mapping, data, target, 1);

            return;
        }

        if (mapping.Pickup == ControlPickup.Sensed)
        {
            if (!hand.Sense.Saw(data)) return;

            mapping.Pickup = hand.Sense.Pickup ?? ControlPickup.Takeover;
            mapping.Turn = hand.Sense.Turn;

            hand.Caught = true;

            Log.Write(LogArea.Midi, () =>
                "controls: CC " + mapping.Cc + " is " +
                ControlSense.Describe(mapping.Pickup, mapping.Turn) + ", worked out from what it sent");

            _learned?.Invoke();

            return;
        }

        if (mapping.Pickup == ControlPickup.Relative)
        {
            double moved = Turned(data, mapping.Turn) * Notch(target);
            if (moved == 0) return;

            Put(hand, mapping, data, target, target.Value + moved);
            return;
        }

        if (mapping.Pickup == ControlPickup.Endless)
        {
            int step = Step(hand, data);

            hand.Was = data;

            if (step != 0) Put(hand, mapping, data, target, target.Value + step * Notch(target));

            return;
        }

        double wanted = target.Min + Math.Clamp(data / Full, 0, 1) * (target.Max - target.Min);

        if (Wrapped(hand, wanted, target))
        {
            mapping.Pickup = ControlPickup.Endless;
            hand.Was = data;

            Log.Write(LogArea.Midi, () =>
                "controls: CC " + mapping.Cc + " came round the end of its range, so it is a knob "
                + "with no end stop; reading what it sends as movement from here on");

            _learned?.Invoke();

            return;
        }

        if (mapping.Pickup == ControlPickup.Jump)
        {
            hand.Was = data;

            Put(hand, mapping, data, target, wanted);
            return;
        }

        if (Caught(hand, wanted, target.Value))
        {
            if (hand.Told)
            {
                hand.Told = false;

                Log.Write(LogArea.Midi, () => "controls: CC " + mapping.Cc + " has caught " + target.Name);
            }

            hand.Caught = true;
            hand.Last = wanted;
            hand.Was = data;

            Put(hand, mapping, data, target, wanted);
            return;
        }

        hand.Last = wanted;
        hand.Was = data;

        if (!hand.Told)
        {
            hand.Told = true;

            Log.Write(LogArea.Midi, () =>
                "controls: CC " + mapping.Cc + " is reaching for " + target.Name
                + ": the knob is at " + target.Reads(wanted) + " and the parameter is at "
                + target.Reads(target.Value) + ", so nothing moves until your hand passes it."
                + " That is what picking up is; turn it the other way if you have gone past");
        }

        Reaching?.Invoke(mapping, target, wanted);
    }

    /// <summary>
    /// True when a control that says where it is has crossed more of its range in one message
    /// than a hand can.
    /// </summary>
    /// <remarks>
    /// Only once it is already following. The first message after a link, or after a song is
    /// opened, is always a long way from wherever the parameter sits and is not a wrap; that
    /// gap is what pickup exists to close.
    /// </remarks>
    private static bool Wrapped(Hand hand, double wanted, IControlTarget target)
    {
        if (!hand.Caught || double.IsNaN(hand.Last)) return false;

        double range = Math.Abs(target.Max - target.Min);

        return range > 0 && Math.Abs(wanted - hand.Last) > range / 2;
    }

    /// <summary>
    /// True while a control is pushing a parameter further into an end it has already reached.
    /// </summary>
    /// <remarks>
    /// The end stop, and the one that cannot be argued with. Clamping alone keeps the value in
    /// range but says nothing about what the next message means: a counter that comes round
    /// sends a number at the far end, and read as a position that is a legitimate leap from one
    /// end of the range to the other. Direction is not fooled by it. A hand that has been
    /// turning a knob down is still turning it down, whatever number the firmware happens to
    /// send when its counter runs out, and a parameter on its floor stays there until the hand
    /// turns round.
    ///
    /// Which way a message is going is asked of each kind of control in its own words: an
    /// encoder says it outright, a knob that reports positions says it by the difference from
    /// the last one, and a wrap is unwound before the question is put. Nothing either way is not
    /// a turn back, so it stays parked; anything the other way takes it off the end and it
    /// follows again.
    /// </remarks>
    private static bool Parked(Hand hand, ControlMapping mapping, int data)
    {
        if (hand.Against == 0) return false;

        int going = Going(hand, mapping, data);

        if (going == 0) return true;

        if (going == hand.Against) return true;

        hand.Against = 0;

        return false;
    }

    /// <summary>Which way this message is going: 1 up, -1 down, 0 nowhere.</summary>
    private static int Going(Hand hand, ControlMapping mapping, int data) => mapping.Pickup switch
    {
        ControlPickup.Relative => Math.Sign(Turned(data, mapping.Turn)),
        _ => Math.Sign(Step(hand, data))
    };

    /// <summary>
    /// How far an endless knob moved, with the wrap unwound.
    /// </summary>
    /// <remarks>
    /// Nought and a hundred and twenty seven are next to each other on a knob that turns for
    /// ever, so a difference of more than half the range is the short way round, the other way.
    /// </remarks>
    private static int Step(Hand hand, int data)
    {
        if (hand.Was < 0) return 0;

        int step = data - hand.Was;

        if (step > 64) step -= 128;
        if (step < -64) step += 128;

        return step;
    }

    /// <summary>
    /// True when the knob may take the parameter over: it is already holding it, or it has just
    /// passed it.
    /// </summary>
    /// <remarks>
    /// Passing rather than nearly touching. A test on how close the two are lets a knob a
    /// hair's breadth away grab the value from the wrong side, which is the lurch this exists to
    /// prevent, only smaller. Crossing is unambiguous: the knob was one side of the value and is
    /// now the other, so the hand has physically arrived at it.
    ///
    /// The first message after a mapping is made or a song is opened has nothing to compare
    /// against, and is never a catch. It is what tells us which side the hand is on.
    /// </remarks>
    private static bool Caught(Hand hand, double wanted, double held)
    {
        if (hand.Caught) return true;

        if (double.IsNaN(hand.Last))
        {
            hand.Last = wanted;
            return false;
        }

        return (hand.Last <= held && wanted >= held)
            || (hand.Last >= held && wanted <= held);
    }

    /// <summary>
    /// How far an endless encoder says it was turned, and which way.
    /// </summary>
    /// <remarks>
    /// Both conventions, because there is no standard and a controller read by the wrong one
    /// turns backwards and jumps the length of the range doing it. Which one a control uses is
    /// worked out from what it sends rather than asked of the user: see <see cref="ControlSense"/>.
    ///
    /// From the middle: the centre of the range is standing still, above it is clockwise, below
    /// it anticlockwise, and the distance from the centre is how fast.
    ///
    /// Two's complement: it counts up from nothing one way and down from a hundred and
    /// twenty eight the other, so 1 is one notch clockwise and 127 is one notch back.
    /// </remarks>
    private static int Turned(int data, ControlTurn turn) =>
        turn == ControlTurn.Twos
            ? (data <= 63 ? data : data - 128)
            : data - Still;

    /// <summary>How much of a parameter's range one notch of an encoder is worth.</summary>
    /// <remarks>
    /// A hundred and twenty eight notches across the whole range, so an encoder sweeps the same
    /// distance in the same turn as a knob does, and a parameter with a small range does not
    /// need a hand crank.
    /// </remarks>
    private static double Notch(IControlTarget target) => (target.Max - target.Min) / Full;

    /// <summary>
    /// Writes the value, notes whether it landed on an end, and says so once.
    /// </summary>
    /// <remarks>
    /// Landing on an end puts the control aside until it turns round, so nothing it sends
    /// afterwards can come out the other side. See <see cref="Parked"/>.
    ///
    /// One line a message, which is what moved and where to; everything else about the journey is
    /// only worth saying when something went wrong on it. The log is asked before the line is
    /// built, unlike almost everywhere else in this application: this is the one that runs per
    /// message, and the closure holding the target and the mapping is allocated at the call site
    /// whether or not anybody is reading.
    /// </remarks>
    private void Put(Hand hand, ControlMapping mapping, int data, IControlTarget target, double value)
    {
        double landed = Math.Clamp(value, target.Min, target.Max);

        hand.Against = landed <= target.Min ? -1 : landed >= target.Max ? 1 : 0;

        target.Set(landed);

        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "controls: " + target.Name + " moved to " + landed.ToString("0.####")
                + " (CC " + mapping.Cc + " sent " + data + ")");

        Moved?.Invoke(mapping, target, landed);
    }
}
