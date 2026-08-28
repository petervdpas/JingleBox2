using System;

namespace JingleBox2.Midi;

/// <summary>
/// Waiting for one press, so a pad can be told which button on the desk fires it.
/// </summary>
/// <remarks>
/// The whole of learning a pad: arm it, and the next message that arrives is the answer. It is
/// deliberately not the way a knob is learned. A knob is pointed at by resting the pointer on
/// the control and touching the hardware, because a bank of eight knobs is eight of these one
/// after another and a dialog per knob is a dialog too many; a pad is learned once and the
/// button beside it is where a hand already is.
///
/// It disarms itself on the first message rather than on the first press. Any message counts,
/// including a note off, because a controller whose buttons only send releases is a controller
/// nobody could otherwise map, and the mapping records what arrived rather than what was
/// expected.
/// </remarks>
public sealed class MidiLearnSession
{
    private readonly Action<MidiMessage> _onLearned;

    /// <summary>Whether the next message is the answer or an ordinary message.</summary>
    private bool _active;

    /// <summary>True while it is waiting, for a button that wants to say so.</summary>
    public bool IsActive => _active;

    /// <param name="onLearned">Told the message that arrived, once, on the thread it arrived on.</param>
    public MidiLearnSession(Action<MidiMessage> onLearned)
    {
        _onLearned = onLearned;
    }

    /// <summary>Arms it: the next message is the answer.</summary>
    public void Start() => _active = true;

    /// <summary>Disarms it, for somebody who changed their mind.</summary>
    public void Cancel() => _active = false;

    /// <summary>
    /// Takes this message as the answer, when it is waiting for one.
    /// </summary>
    /// <remarks>
    /// Disarmed before the answer is handed over rather than after, because what is told is
    /// going to write a mapping and redraw a list, and a second message arriving during that is
    /// an ordinary thing for a controller to send.
    /// </remarks>
    public void Handle(MidiMessage msg)
    {
        if (!_active)
            return;

        _active = false;
        _onLearned(msg);
    }
}
