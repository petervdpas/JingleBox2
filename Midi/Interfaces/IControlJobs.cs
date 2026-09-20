namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// What a control on a desk is for: what kind of thing its kind drives, and whether it is one of
/// the two wheels beside a keyboard.
/// </summary>
/// <remarks>
/// One rule with two askers, and it exists because they were three classes calling each other.
/// <see cref="DefaultLayout"/> has to leave alone exactly what <see cref="MidiWheelRouter"/>
/// takes, or one gesture drives a link and bends a track at the same time; written out in both,
/// each called into the other and neither owned the answer.
///
/// The two questions are one question at two grains. What a kind is for says whether the desk
/// points links at a control of that sort or leaves it to be played, and whether a controller
/// number is a wheel is that same answer for the one control whose meaning the specification
/// fixes. Splitting them would put the halves back in two places.
///
/// Nothing here touches a song, a window or a port, so the whole of it can be put a question to
/// with nothing plugged in.
/// </remarks>
public interface IControlJobs
{
    /// <summary>
    /// Whether the desk points links at a control of this kind, rather than leaving it to be
    /// played.
    /// </summary>
    /// <remarks>
    /// A fader, a knob and an encoder are driven: they sit still where they are left, and a
    /// layout can pin one to a track's level or to a machine's third control. A pad, a button, a
    /// modulation strip and a wheel are not: the first two are a press, and the last two spring
    /// back, which is a track dropping to nothing the moment a thumb comes off.
    /// </remarks>
    /// <param name="kind">What the controller's own file calls the control, or nothing.</param>
    bool Drives(string kind);

    /// <summary>
    /// What a control of this kind is pointed at where nobody has pointed it anywhere:
    /// <see cref="Mix"/>, <see cref="Machine"/>, or nothing.
    /// </summary>
    /// <param name="kind">What the controller's own file calls the control, or nothing.</param>
    string For(string kind);

    /// <summary>The mixer, which is where a fader goes.</summary>
    string Mix { get; }

    /// <summary>Whatever face is in front of you, which is what a knob follows.</summary>
    string Machine { get; }

    /// <summary>
    /// Whether that message is one of the two wheels beside a keyboard.
    /// </summary>
    /// <remarks>
    /// A pitch wheel has a message of its own and is never anything else. A modulation wheel is
    /// controller one, which the specification fixes, so a keyboard nobody has written a file
    /// for has one without being told; a device whose file says the desk drives that control has
    /// said it is a knob, and a nanoKONTROL2's controller one is Slider 2.
    ///
    /// Asked as whether the desk drives it rather than as whether the file says the word wheel,
    /// which is the narrower test and is wrong: every profile written before that word existed
    /// calls the control something else, and a MiniLab 3 and a KeyStep Pro both say `strip`,
    /// which is what their modulation wheel physically is.
    /// </remarks>
    /// <param name="message">What arrived.</param>
    bool Turns(MidiMessage? message);
}
