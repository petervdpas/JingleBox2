namespace JingleBox2.Midi;

/// <summary>
/// What a hardware control can be pointed at on a mixer strip.
/// </summary>
/// <remarks>
/// One set for the whole mixer rather than one per strip, and that is the design rather than a
/// saving. Every one of these follows the cursor, so a knob pointed at Level on any strip is
/// pointed at "the level of the track I am on": select another strip and the same knob drives
/// that one. A link per strip would be eight links to make and eight to remember, for a desk
/// that has one fader.
///
/// Templates, never handed out as they are. <see cref="Views.Pointable"/> copies one before it
/// is offered, because a link keeps the object it was given.
/// </remarks>
public static class MixLinks
{
    public static readonly ControlMapping Level = Strip(MixControl.Volume, "Level");

    public static readonly ControlMapping Pan = Strip(MixControl.Pan, "Pan");

    public static readonly ControlMapping Mute = Strip(MixControl.Mute, "Mute");

    public static readonly ControlMapping Solo = Strip(MixControl.Solo, "Solo");

    public static readonly ControlMapping Duck = Strip(MixControl.Duck, "Duck");

    public static readonly ControlMapping Release = Strip(MixControl.Release, "Duck release");

    /// <summary>
    /// The same handful again, on the one strip that is always the same strip.
    /// </summary>
    /// <remarks>
    /// Fixed rather than following the cursor, and that is the whole difference. Every other
    /// strip is one of many and a knob pointed at one means "the track I am on"; there is only
    /// ever one master, and a knob pointed at its fader means that fader wherever you are. Given
    /// the tracks' templates it would have driven whichever track happened to be selected, which
    /// is a knob doing something other than what you pointed it at.
    /// </remarks>
    public static readonly ControlMapping MasterLevel = Master(MixControl.Volume, "Master level");

    public static readonly ControlMapping MasterPan = Master(MixControl.Pan, "Master pan");

    public static readonly ControlMapping MasterMute = Master(MixControl.Mute, "Master mute");

    private static ControlMapping Strip(MixControl what, string said) => new()
    {
        Kind = ControlKind.Mix,
        Scope = ControlScope.Focused,
        Mix = what,
        Name = said
    };

    private static ControlMapping Master(MixControl what, string said) => new()
    {
        Kind = ControlKind.Mix,
        Scope = ControlScope.Fixed,
        Track = Tracker.TrackerPlayer.MasterStrip,
        Mix = what,
        Name = said
    };
}
