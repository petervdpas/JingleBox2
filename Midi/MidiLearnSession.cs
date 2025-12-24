using System;

namespace JingleBox2.Midi;

public sealed class MidiLearnSession
{
    private readonly Action<MidiMessage> _onLearned;
    private bool _active;

    public bool IsActive => _active;

    public MidiLearnSession(Action<MidiMessage> onLearned)
    {
        _onLearned = onLearned;
    }

    public void Start() => _active = true;
    public void Cancel() => _active = false;

    public void Handle(MidiMessage msg)
    {
        if (!_active)
            return;

        _active = false;
        _onLearned(msg);
    }
}
