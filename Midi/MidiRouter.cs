using System.Linq;

namespace JingleBox2.Midi;

public enum PadTriggerAction
{
    Toggle,
    Start,
    Stop
}

public interface IPadTrigger
{
    void TriggerPad(int padIndex, PadTriggerAction action);
}

public sealed class MidiRouter
{
    private readonly MidiConfig _cfg;
    private readonly IPadTrigger _padTrigger;

    public MidiRouter(MidiConfig cfg, IPadTrigger padTrigger)
    {
        _cfg = cfg;
        _padTrigger = padTrigger;
    }

    public void Handle(MidiMessage msg)
    {
        if (!msg.IsOn)
            return;

        var mapping = _cfg.Pads.FirstOrDefault(p =>
            p.Type == msg.Type &&
            p.Channel == msg.Channel &&
            p.Value == msg.Value);

        if (mapping == null)
            return;

        var action = _cfg.ToggleMode
            ? PadTriggerAction.Toggle
            : PadTriggerAction.Start;

        _padTrigger.TriggerPad(mapping.PadIndex, action);
    }
}
