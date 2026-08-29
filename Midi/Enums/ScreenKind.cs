namespace JingleBox2.Midi.Enums;

/// <summary>
/// What picture a reading is drawn in on a controller's screen.
/// </summary>
/// <remarks>
/// About the thing on the screen and not the thing under your hand: a mixer level pointed at by
/// an encoder is still drawn as a fader.
///
/// No numbers on it, deliberately. This is what the application asks for, and what a byte on the
/// wire has to be is each protocol's own business: Arturia draws these as 0x03, 0x04 and 0x05,
/// and a Mackie display cannot draw a picture at all and says the words instead. A value here
/// carrying one manufacturer's number would make every other screen carry it too.
/// </remarks>
public enum ScreenKind
{
    /// <summary>A ring, for a parameter on a machine.</summary>
    Knob,

    /// <summary>A bar, for anything on a mixer strip.</summary>
    Fader,

    /// <summary>A pad, for a button pointed at an action.</summary>
    Pad
}
