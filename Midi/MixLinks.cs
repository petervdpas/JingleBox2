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

    private static ControlMapping Strip(MixControl what, string said) => new()
    {
        Kind = ControlKind.Mix,
        Scope = ControlScope.Focused,
        Mix = what,
        Name = said
    };
}
