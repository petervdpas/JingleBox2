namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// What a hardware knob is pointed at while the window showing this has the focus.
/// </summary>
/// <remarks>
/// A link names a machine and a parameter key, or an effect and a key, and never says which
/// track, which chain or which of two windows: that half of the question is answered by what you
/// are looking at. So something has to say what that is, and the thing that knows is the window
/// the face is drawn in.
///
/// **The focus decides it, and nothing else can.** It used to be near enough to say that a window
/// claims what it shows while it is open, because an owned window is always in front of the one
/// that owns it and two of them could not both be in front of you. Neither half of that is true
/// now: a device's window can be put behind the application, so a window that is open is not a
/// window you are looking at, and a claim held by an open window would have a knob writing into
/// a panel that is underneath everything else on the screen.
///
/// Nothing is applied by saying it. The mappings are walked per message, so the next thing you
/// touch simply resolves somewhere else.
///
/// Both halves are the same fact and both have to be said, since a claim that is never let go of
/// is exactly the fault this exists to fix.
/// </remarks>
public interface IInFront
{
    /// <summary>This is what is being worked on: its window opened or took the focus.</summary>
    void InFront();

    /// <summary>And it is not: the window lost the focus or closed.</summary>
    /// <remarks>
    /// Whoever keeps the record only clears it when the one leaving is the one it is holding,
    /// since a window losing the focus to another of ours is that other one arriving, and the
    /// two are told in whichever order the desktop chooses.
    /// </remarks>
    void NotInFront();
}
