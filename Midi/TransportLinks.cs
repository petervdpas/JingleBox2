using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// What a hardware button can be pointed at on the transport.
/// </summary>
/// <remarks>
/// The transport is four keys and it is the same four wherever you are, so these are
/// <see cref="ControlScope.Fixed"/> and name no track. That is the difference between this and
/// <see cref="MixLinks"/>, whose every entry follows the cursor because a strip is one of many.
/// There is one transport.
///
/// It belongs to the desk rather than to a song, for the reason a machine on the rack does: a
/// button pointed at play means play in every song you will ever open, and a link that travelled
/// in a file would arrive on somebody else's machine telling their hardware what to do.
///
/// Why this had to exist at all. A controller's own transport buttons are read directly, in
/// three dialects, and that covers a device which sends one of them. A great many do not: a
/// nanoKONTROL2's play button is a plain controller like its mute buttons, so the only way to
/// reach the transport with it is to point it there, and until this there was nothing on the
/// transport to point at. Ticking the device for Transport in SETTINGS looks like the answer and
/// is not: that is the switch for reading the protocols, and a device that speaks none of them
/// gains nothing by it.
///
/// Templates, never handed out as they are. <see cref="Views.Pointable"/> and the transport's own
/// drawing both copy one before it is offered, because a link keeps the object it was given.
/// </remarks>
public static class TransportLinks
{
    /// <summary>Play, wherever you are.</summary>
    public static readonly ControlMapping Play = Key(TransportKey.Play, "Play");

    /// <summary>Pause.</summary>
    public static readonly ControlMapping Pause = Key(TransportKey.Pause, "Pause");

    /// <summary>Stop.</summary>
    public static readonly ControlMapping Stop = Key(TransportKey.Stop, "Stop");

    /// <summary>Record.</summary>
    public static readonly ControlMapping Record = Key(TransportKey.Record, "Record");

    /// <summary>The template for one of the four, so a drawn key can ask for its own.</summary>
    /// <param name="key">Which of the four.</param>
    public static ControlMapping For(TransportKey key) => key switch
    {
        TransportKey.Pause => Pause,
        TransportKey.Stop => Stop,
        TransportKey.Record => Record,
        _ => Play
    };

    /// <summary>One key, fixed, named for the status line.</summary>
    private static ControlMapping Key(TransportKey key, string said) => new()
    {
        Kind = ControlKind.Transport,
        Scope = ControlScope.Fixed,
        Transport = key,
        Owner = "Transport",
        Name = said
    };
}
