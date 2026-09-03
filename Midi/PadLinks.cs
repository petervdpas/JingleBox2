using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// What a hardware button can be pointed at on the pads.
/// </summary>
/// <remarks>
/// One entry per pad and nothing else, since a pad is a thing to be hit rather than a set of
/// values: there is no level here to point a fader at, because a pad's level is on the mixer's
/// PADS strip and is one link for all of them.
///
/// <see cref="ControlScope.Fixed"/>, like <see cref="MixLinks"/> and for the same reason. Pad 3
/// is pad 3 from every page: a bank of pads is about all of them at once and none of them is the
/// one you are looking at.
///
/// The whole desk is one thing to point a controller at, so what these make is one card headed
/// Pads with a line per pad, exactly as the mixer is one card rather than one per fader.
///
/// Templates, never handed out as they are. <see cref="Views.Pointable"/> copies one before it is
/// offered, because a link keeps the object it was given.
/// </remarks>
public static class PadLinks
{
    /// <summary>What a pad offers: this pad, wherever you are.</summary>
    /// <remarks>
    /// A fresh mapping each time rather than a kept template, because the pads are rebuilt
    /// whenever the matrix changes size and holding them would mean holding the last matrix's.
    /// </remarks>
    /// <param name="pad">Which pad, counted from nought.</param>
    public static ControlMapping On(int pad) => new()
    {
        Kind = ControlKind.Pad,
        Scope = ControlScope.Fixed,
        Pad = pad,
        Owner = "Pads",
        Name = Said(pad)
    };

    /// <summary>What to call it in a list, where sixteen of them sit together.</summary>
    /// <remarks>
    /// Counted from one, which is what the pad says on the screen. The number is the whole of the
    /// name because a pad's own name is the recording somebody put on it this morning, and a link
    /// named after that would be a link that lies the moment the pad is filled with something
    /// else.
    /// </remarks>
    private static string Said(int pad) => "pad " + (pad + 1);
}
