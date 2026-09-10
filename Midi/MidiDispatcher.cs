using System;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;

namespace JingleBox2.Midi;

/// <summary>
/// Sends each message where its device is pointed. Everything upstream of this is device
/// agnostic, and everything downstream never has to ask who sent what.
/// </summary>
/// <remarks>
/// The one place the settings' answer to "what does this controller drive" is applied. A role is
/// flags rather than a choice, so a message can go to two places at once, which is what one
/// controller doing two jobs looks like: the keys play the tracker and the knobs move the
/// machine.
///
/// The role is looked up per message rather than per device, because a device can be given a
/// different job while it is plugged in and the next message should already obey.
/// </remarks>
public sealed class MidiDispatcher
{
    private readonly MidiConfig _cfg;
    private readonly Action<MidiMessage>? _pads;
    private readonly Action<MidiMessage>? _tracker;
    private readonly Action<MidiMessage>? _controls;
    private readonly Action<MidiMessage>? _transport;

    /// <param name="cfg">The settings, read live, so a job given a moment ago is already in force.</param>
    /// <param name="pads">Where a message for the pads goes: buttons on their way to being fired.</param>
    /// <param name="tracker">Where the keys go, to be played and to be typed into a pattern.</param>
    /// <param name="controls">Where the knobs go, to whatever they have been pointed at.</param>
    /// <param name="transport">Where play, stop and the rest go, in whichever of the three dialects.</param>
    /// <remarks>
    /// Every one of the four is optional, because the pieces are wired at different points as the
    /// window is built and a half wired dispatcher is better than a null one.
    /// </remarks>
    /// <param name="bindings">Which device does which job, defaulted to the real rules.</param>
    /// <param name="follow">
    /// The clock being followed, where one is. Left out, nothing follows anything.
    /// </param>
    /// <param name="deck">
    /// What this machine drives, so a followed clock can be passed on to it. Left out, nothing is
    /// passed anywhere.
    /// </param>
    public MidiDispatcher(MidiConfig cfg, Action<MidiMessage>? pads, Action<MidiMessage>? tracker,
                          Action<MidiMessage>? controls = null, Action<MidiMessage>? transport = null,
                          IMidiPortBindings? bindings = null, IMidiClockFollow? follow = null,
                          IMidiClockDeck? deck = null)
    {
        _bindings = bindings ?? new MidiPortBindings();
        _cfg = cfg;
        _pads = pads;
        _tracker = tracker;
        _controls = controls;
        _transport = transport;
        _follow = follow;
        _deck = deck;
    }

    /// <summary>
    /// The transport's own clock, when it is running on somebody else's.
    /// </summary>
    /// <remarks>
    /// Handed in rather than reached for, so a dispatcher can be put a question to without one.
    /// Null means nothing follows anything, which is every machine that has not asked to.
    /// </remarks>
    private readonly IMidiClockFollow? _follow;

    /// <summary>
    /// The outputs this machine drives, so a clock being followed can be passed on to them.
    /// </summary>
    /// <remarks>
    /// **Passing a clock on is a fact about the wire and not about the transport**, which is why
    /// it is done here rather than in the player. The player waits on ticks; what a third device
    /// needs is the tick itself, and the only place that has it is where it arrives.
    ///
    /// Null means nothing is driven, which is every machine that has not ticked an output.
    /// </remarks>
    private readonly IMidiClockDeck? _deck;

    /// <summary>Which device has been pointed at which half of the application.</summary>
    private readonly IMidiPortBindings _bindings;

    /// <summary>
    /// Hands the message to each half its device has been pointed at.
    /// </summary>
    /// <remarks>
    /// A line is written only when it goes nowhere. A message that is delivered says so further
    /// down in the words of whoever it reached; one dropped here has nobody left to speak for it,
    /// and a controller that does nothing because it was never given a job in SETTINGS is the
    /// single most common thing anybody is looking for in this log.
    /// </remarks>
    public void Handle(MidiMessage msg)
    {
        if (msg is null) return;

        if (Followed(msg)) return;

        var role = _bindings.RoleFor(_cfg.Devices, msg.Device);

        if (role == MidiPortRole.None)
            Log.Write(LogArea.Midi, () =>
                "dispatch " + msg.Type + " ch" + msg.Channel + " val=" + msg.Value
                + " from '" + msg.Device + "' DRIVES NOTHING: it has been given no job in SETTINGS");

        if ((role & MidiPortRole.Pads) != 0) _pads?.Invoke(msg);
        if ((role & MidiPortRole.Tracker) != 0) _tracker?.Invoke(msg);
        if ((role & MidiPortRole.Controls) != 0) _controls?.Invoke(msg);
        if ((role & MidiPortRole.Transport) != 0) _transport?.Invoke(msg);
    }

    /// <summary>Clock and its four siblings, as the specification numbers them.</summary>
    /// <remarks>
    /// Written out here as well as in <c>MidiService</c> because these are the numbers the wire
    /// uses and both ends of it have to say the same thing. There is no enum for them: what
    /// arrives is the byte.
    /// </remarks>
    private const int Clock = 0xF8;

    /// <inheritdoc cref="Clock"/>
    private const int Positioned = 0xF2;

    /// <inheritdoc cref="Clock"/>
    private const int Started = 0xFA;

    /// <inheritdoc cref="Clock"/>
    private const int Continued = 0xFB;

    /// <inheritdoc cref="Clock"/>
    private const int Stopped = 0xFC;

    /// <summary>
    /// Gives a message to the clock being followed, and says whether it went no further.
    /// </summary>
    /// <remarks>
    /// **A short circuit rather than a fifth role, and there are two reasons.** Clock arrives
    /// ninety six times a second at a brisk tempo, and walking the whole routing to have every
    /// router decline it is work paid per tick for nothing. And the transport messages beside it
    /// have exactly one owner while a clock is being followed: the port they arrived on is the
    /// clock, so a start on it means the master starting, and letting it also reach the transport
    /// router would start the transport twice.
    ///
    /// **Only on the port being followed.** A start from a control surface somewhere else is
    /// still that surface's transport button and still goes where it always did, which is the
    /// whole reason this asks which port the message came from rather than only what it is.
    ///
    /// The five bytes are taken and everything else on that port carries on: a keyboard that also
    /// sends the clock is still a keyboard.
    ///
    /// **And each of the five is passed on to whatever this machine drives, before the follower is
    /// told.** A transport running on somebody else's clock may still be the only thing a drum
    /// machine is plugged into, which is what <see cref="MidiConfig.ClockOutputs"/> has said since
    /// it was written; without this the drum machine got a start and a stop and no time. Passed on
    /// first because the follower's tick wakes a thread that then does a line's worth of note
    /// work, and a clock byte behind that inherits its jitter.
    ///
    /// **Unchanged, and never worked out again.** See <see cref="IMidiClockDeck.Begin"/> for why
    /// the pointer that arrived is the pointer that leaves.
    ///
    /// Nothing here guards against the followed port being driven as well, and it does not have
    /// to: <see cref="MidiConfig.ClockDriven"/> leaves it out of the list, so a clock cannot be
    /// echoed at the machine that sent it.
    /// </remarks>
    /// <param name="msg">What arrived.</param>
    /// <returns>True when it was the followed clock's and needs no further delivery.</returns>
    private bool Followed(MidiMessage msg)
    {
        if (_follow?.IsFollowing != true) return false;
        if (msg.Type != MidiMessageType.Realtime) return false;

        string? port = _cfg.ClockPort;

        if (string.IsNullOrWhiteSpace(port)) return false;
        if (!string.Equals(port, msg.Device, StringComparison.OrdinalIgnoreCase)) return false;

        var deck = _deck;
        bool driving = deck?.IsDriving == true;

        switch (msg.Value)
        {
            case Clock:
                if (driving) deck!.Ticks(1);
                _follow.Tick();
                return true;

            case Positioned:
                if (driving) deck!.Place(msg.Data);
                _follow.Placed(msg.Data);
                return true;

            case Started:
                if (driving) deck!.Begin();
                _follow.Start();
                return true;

            case Continued:
                if (driving) deck!.Resume();
                _follow.Resume();
                return true;

            case Stopped:
                if (driving) deck!.Halt();
                _follow.Cease();
                return true;

            default:
                return false;
        }
    }
}
