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
/// the pads by name in SETTINGS, so by the time <see cref="MidiRouter"/> sees a message it has
/// already been decided that this controller drives pads; a knob has no such gate and its
/// mapping has to carry the controller it was learned on.
/// </remarks>
public sealed class MidiMapping
{
    /// <summary>Which pad, counted from nought. Up to sixteen of them, as SETTINGS allows.</summary>
    public int PadIndex { get; set; }

    /// <summary>A note or a controller. Both are buttons on the hardware people use for this.</summary>
    public MidiMessageType Type { get; set; } = MidiMessageType.Note;

    /// <summary>1 to 16, as the message says it.</summary>
    public int Channel { get; set; } = 1;

    /// <summary>The note number, or the controller number, depending on <see cref="Type"/>.</summary>
    public int Value { get; set; }
}
