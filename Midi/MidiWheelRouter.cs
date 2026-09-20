using System;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Midi.Enums;
using JingleBox2.Midi.Interfaces;
using JingleBox2.Music;
using JingleBox2.Music.Interfaces;

namespace JingleBox2.Midi;

/// <summary>
/// Turns the two wheels beside a keyboard into a lean and an amount.
/// </summary>
/// <remarks>
/// The sixth router, and the same shape as the five before it: this one knows the wire and
/// nothing about the application, and an adapter on the far side of <see cref="IWheels"/> knows
/// where a wheel goes. See <see cref="MidiNoteRouter"/> for the keys beside it.
///
/// **A wheel is a performance control and not a knob on a desk**, which is the whole reason it is
/// read here rather than through <see cref="MidiControlRouter"/>. It springs back, it is played
/// while a note sounds, and it belongs to the notes the same hand is playing. It goes to the
/// port's tracker job for that reason: a wheel goes where the keys go.
///
/// **This class owns whether a message is a wheel at all, and it is the only one that does.**
/// Three things can take a message away from it and all three are asked here: the number and the
/// device's own file, through <see cref="IControlJobs.Turns"/>, and a link somebody pointed at
/// that control, through the question handed in. Written out across the dispatcher, the control
/// router and the layout, those were three classes cooperating on one rule with none of them
/// owning it, and the way that fails is one gesture driving a link and bending a track at once.
///
/// Mackie Control sends its motorised faders as pitch bend and nothing here guards against it,
/// deliberately. A wheel is only read on a port that has been given the keys, which is somebody
/// saying that port plays notes, and on the hardware read here the two never share a wire: a
/// KeyLab speaks Mackie on its DAW port and carries its keys on the other.
/// </remarks>
public sealed class MidiWheelRouter
{
    private readonly IWheels _wheels;

    /// <summary>How the two wheels are read off the wire.</summary>
    private readonly IMidiWheelInput _wire;

    /// <summary>What each control on a desk is for, asked whether a message is a wheel.</summary>
    private readonly IControlJobs _jobs;

    /// <summary>
    /// Whether something was pointed at this control, in which case it is a knob and not a wheel.
    /// </summary>
    /// <remarks>
    /// A modulation wheel is a continuous controller like any other, so pointing it at a filter
    /// cutoff has always worked and goes on working; what may not happen is both. A link is a
    /// deliberate act and wins, which is the rule <see cref="MidiControlRouter"/> already keeps
    /// about a layout.
    ///
    /// A question rather than the router itself, so this can be put one without a desk, and
    /// answered by the one class that knows how a mapping matches: two spellings of that would
    /// eventually disagree.
    /// </remarks>
    private readonly Func<MidiMessage, bool>? _pointed;

    /// <param name="wheels">Where a wheel goes. Not a view model, so this can be tested.</param>
    /// <param name="jobs">What each control is for, defaulted to the real rule.</param>
    /// <param name="pointed">
    /// Whether a link holds this control. Left out, nothing is pointed at anything, which is
    /// what a test wants and is the truthful answer for a router standing on its own.
    /// </param>
    /// <param name="wire">
    /// How a wheel is read off the wire. Left out, the real reading; given, whatever a test
    /// wants the wire to have meant.
    /// </param>
    public MidiWheelRouter(IWheels wheels, IControlJobs? jobs = null,
                           Func<MidiMessage, bool>? pointed = null, IMidiWheelInput? wire = null)
    {
        _wheels = wheels;
        _jobs = jobs ?? new ControlJobs();
        _pointed = pointed;
        _wire = wire ?? new MidiWheelInput();
    }

    /// <summary>
    /// Bends or modulates, and says which in the log.
    /// </summary>
    /// <remarks>
    /// Said out loud for the reason the note router says both halves of a key: a wheel that
    /// reaches nothing and a wheel that was never sent look identical from every point in the
    /// program above this one, and the difference is the whole of what anybody reading such a
    /// log is after.
    ///
    /// Asked before the line is built. A hand on a wheel is tens of messages a second, which is
    /// not thousands, but it is enough that the closure is worth not allocating while nobody is
    /// reading.
    /// </remarks>
    /// <param name="msg">What arrived.</param>
    /// <returns>True when it really was a wheel, so a caller can tell it was taken.</returns>
    public bool Handle(MidiMessage msg)
    {
        if (!_jobs.Turns(msg)) return false;
        if (_pointed?.Invoke(msg) == true) return false;

        if (msg.Type == MidiMessageType.PitchBend)
        {
            double lean = _wire.LeanFor(msg.Data);

            if (Log.On(LogArea.Midi))
                Log.Write(LogArea.Midi, () =>
                    "wheel: '" + msg.Device + "' ch" + msg.Channel + " bend " + msg.Data
                    + " leaning " + lean.ToString("0.###"));

            _wheels.Bend(lean);
            return true;
        }

        double amount = _wire.AmountFor(msg.Data);

        if (Log.On(LogArea.Midi))
            Log.Write(LogArea.Midi, () =>
                "wheel: '" + msg.Device + "' ch" + msg.Channel + " modulation " + msg.Data
                + " at " + amount.ToString("0.###"));

        _wheels.Modulate(amount);
        return true;
    }
}
