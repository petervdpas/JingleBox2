namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Who is looking at the recording input, so it is open while anybody is.
/// </summary>
/// <remarks>
/// The input is a capture device and holding one open for a page nobody is watching is rude to
/// whatever else wants the microphone, so it follows whether anybody is looking. **It used to
/// follow whether RECORD was looking**, which is why the mixer's IN strip drew a meter that never
/// moved: its reading is the recorder's own level, and with the input closed that is nought.
/// Nothing was broken there, and the meter was reporting the truth.
///
/// So it is counted rather than switched. Two pages show that meter and either of them is reason
/// enough to have the input open, and a rule written as a flag would have whichever page left
/// last close it under the one still up.
/// </remarks>
public interface IInputWatch
{
    /// <summary>Says a page showing the input's meter is on screen.</summary>
    void Watch();

    /// <summary>Says one has gone. The input closes once the last of them has.</summary>
    /// <remarks>
    /// Not at once: a theme swap and other re-templating detach a page and put it straight back,
    /// and closing the input in between would lose the routing every time, since the system wires
    /// a new capture stream to its own default. So a departure has to prove itself.
    /// </remarks>
    void LetGo();
}
