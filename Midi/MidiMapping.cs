using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// One button on a controller pointed at one pad.
/// </summary>
/// <remarks>
/// The plainest thing in this folder, and deliberately unlike <see cref="ControlMapping"/>: a
/// pad is a fixed place on a grid, so there is nothing here to resolve and no question about
/// which song or which machine. Stored in the settings, learned by pressing Learn and then the
/// button.
///
/// It names no device, which is the one thing worth knowing about it. A device is pointed at
/// the pads by name in SETTINGS, so by the time a message was matched it had already been
/// decided that this controller drives pads; a knob has no such gate and its mapping has to
/// carry the controller it was learned on.
///
/// Nothing makes one of these any more. The pads are pointed at with the same gesture as
/// everything else, so what was a table of these is a card of links, and this type is left for
/// one purpose: reading a settings file written before that and carrying its rows over. See
/// <c>ConfigStore.PadsBecomeLinks</c> and <c>docs/pad-links.md</c>.
/// </remarks>
public sealed class MidiMapping
{
    /// <summary>Which pad, counted from nought.</summary>
    /// <remarks>
    /// As many as the matrix holds, which is <c>PadMatrix.Usual</c> or <c>PadMatrix.Most</c> with
    /// the extended switch on. No number is refused here: this reads a file somebody already has,
    /// and a row for a pad the matrix no longer has is carried over with the rest rather than
    /// dropped.
    /// </remarks>
    public int PadIndex { get; set; }

    /// <summary>A note or a controller. Both are buttons on the hardware people use for this.</summary>
    public MidiMessageType Type { get; set; } = MidiMessageType.Note;

    /// <summary>1 to 16, as the message says it.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>The note number, or the controller number, depending on <see cref="Type"/>.</summary>
    public int Value { get; set; }
}
