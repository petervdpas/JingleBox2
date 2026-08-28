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
    /// <summary>The fader on the track you are working on.</summary>
    public static readonly ControlMapping Level = Strip(MixControl.Volume, "Level");

    /// <summary>Its pan.</summary>
    public static readonly ControlMapping Pan = Strip(MixControl.Pan, "Pan");

    /// <summary>Its mute, which a knob writes as anything over a half.</summary>
    public static readonly ControlMapping Mute = Strip(MixControl.Mute, "Mute");

    /// <summary>And its solo.</summary>
    public static readonly ControlMapping Solo = Strip(MixControl.Solo, "Solo");

    /// <summary>How far it is ducked by whatever is ducking it.</summary>
    public static readonly ControlMapping Duck = Strip(MixControl.Duck, "Duck");

    /// <summary>
    /// How long the ducking takes to come back up.
    /// </summary>
    /// <remarks>
    /// The one that was missing, and it was found by going over the strip control by control
    /// rather than by anybody reporting it: every other value on a strip had a name a link could
    /// use, and the knob beside Duck had none, so it was the one thing on the mixer no controller
    /// could reach.
    /// </remarks>
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

    /// <summary>The master's pan, fixed on strip -1 for the same reason its level is.</summary>
    public static readonly ControlMapping MasterPan = Master(MixControl.Pan, "Master pan");

    /// <summary>And its mute, which is the whole song.</summary>
    public static readonly ControlMapping MasterMute = Master(MixControl.Mute, "Master mute");

    /// <summary>One of the tracks' templates: this control, on whichever strip is picked.</summary>
    private static ControlMapping Strip(MixControl what, string said) => new()
    {
        Kind = ControlKind.Mix,
        Scope = ControlScope.Focused,
        Mix = what,
        Name = said
    };

    /// <summary>And one of the master's: this control, on strip -1, wherever you are.</summary>
    private static ControlMapping Master(MixControl what, string said) => new()
    {
        Kind = ControlKind.Mix,
        Scope = ControlScope.Fixed,
        Track = Tracker.TrackerPlayer.MasterStrip,
        Mix = what,
        Name = said
    };
}
