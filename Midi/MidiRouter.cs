using System.Linq;

namespace JingleBox2.Midi;

/// <summary>
/// Turns a button on a controller into a pad being fired.
/// </summary>
/// <remarks>
/// The first router, and the shape all the others copy: this one knows the mappings and nothing
/// about the application, and an adapter on the far side of <see cref="IPadTrigger"/> knows the
/// pads and nothing about MIDI. See <see cref="MidiNoteRouter"/> for notes,
/// <see cref="MidiControlRouter"/> for knobs, <see cref="MidiTransportRouter"/> for the transport
/// and <see cref="MidiMackieRouter"/> for a control surface.
///
/// A pad mapping is matched on the kind, the channel and the number and not on the device, which
/// is the one place here that differs from a control mapping. A pad box is pointed at the pads
/// in SETTINGS by name, so by the time a message reaches this it has already been established
/// that this device drives pads; a knob has no such gate, which is why
/// <see cref="ControlMapping"/> has to carry the device it was learned on.
/// </remarks>
public sealed class MidiRouter
{
    private readonly MidiConfig _cfg;
    private readonly IPadTrigger _padTrigger;

    /// <param name="cfg">The settings, read live, so a mapping learned a moment ago is in force.</param>
    /// <param name="padTrigger">
    /// The far side of the seam: what actually fires a pad. This knows nothing about the pads
    /// and it knows nothing about MIDI.
    /// </param>
    public MidiRouter(MidiConfig cfg, IPadTrigger padTrigger)
    {
        _cfg = cfg;
        _padTrigger = padTrigger;
    }

    /// <summary>
    /// Fires the pad this message is mapped to, if any.
    /// </summary>
    /// <remarks>
    /// The press only. A button sends both halves and a pad fired on each would start on the way
    /// down and stop on the way up, which is a pad that only plays while a finger is held.
    /// Nothing else in MIDI has a pressed-ness, so this same line is what keeps a transport byte
    /// and a system exclusive message out of the pads without either being named here.
    ///
    /// Toggle or start is one setting for every pad rather than one per mapping: a pad box is
    /// used one way or the other for a whole show, and per-pad it would be sixteen decisions
    /// nobody wants to make.
    /// </remarks>
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
