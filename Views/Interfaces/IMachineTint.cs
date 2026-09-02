using Avalonia.Controls;
using Avalonia.Media;
using JingleBox2.Views.Records;

namespace JingleBox2.Views.Interfaces;

/// <summary>
/// A machine's own colour, mixed into the theme's, and hung on the panel that wears it.
/// </summary>
/// <remarks>
/// A machine says one colour and a panel needs a dozen: a surface, a card, an edge, a shade for
/// a control that is pressed. They are mixed rather than listed, so a machine says the one thing
/// a person can choose and everything else follows from it and from whichever theme is on.
///
/// Which is why the mixing is here rather than in a theme file. A theme cannot know what colours
/// the machines will be, and a machine cannot know which theme it will be opened under, so the
/// two are combined at the moment a panel is drawn and again whenever the theme is swapped.
///
/// The machine's colour goes where the theme's is, on the panel itself, so everything inside
/// it reads the machine's shade instead of the application's without knowing anything has
/// changed: drawn controls reading the theme palette and borders bound to
/// the theme's brushes alike.
///
/// The lettering follows the face rather than the theme: a pale machine gets dark lettering and
/// a dark one gets pale, so a panel is readable wherever it is standing.
///
/// The arithmetic is worth being able to ask about on its own: a mix that comes out too dark
/// is a panel nobody can read, and that is a number rather than a picture.
/// </remarks>
public interface IMachineTint
{
    /// <summary>
    /// Puts the machine's shades on the panel, or takes them off again when there is no
    /// machine to show.
    /// </summary>
    /// <remarks>
    /// Taken off first in every case, so what shows through when a machine says nothing is the
    /// application's own colour and not the last machine's.
    /// </remarks>
    void Apply(Control panel, Rack.Faces.Records.PanelTheme? machine);

    /// <summary>
    /// The same, and every drawn control inside it told to draw itself again.
    /// </summary>
    /// <remarks>
    /// A control bound to the theme's brushes hears a resource change on its own. One that
    /// paints itself does not: it reads the colours once per render, and nothing has asked it
    /// to render. That is invisible while a machine is only ever tinted as it opens, and it is
    /// the whole of the feedback while somebody is moving the colour about, so the panel is
    /// told outright.
    /// </remarks>
    void Repaint(Control panel, Rack.Faces.Records.PanelTheme? machine);

    /// <summary>
    /// What a machine's theme comes to, once the distances have been worked out from its colour.
    /// </summary>
    /// <remarks>
    /// Here rather than inside <see cref="Apply"/> because two things want the answer and only
    /// one of them is a panel: somebody setting the distances has to be shown what they do, and
    /// a preview drawn from a second copy of the arithmetic is a preview of something else.
    ///
    /// Whatever the face turned out to be, the lettering has to be readable on it: a pale machine
    /// gets dark lettering the same way a dark one gets pale.
    /// </remarks>
    MachineShades? Shades(Rack.Faces.Records.PanelTheme? machine);

    /// <summary>A colour written the way a machine writes one down.</summary>
    string Hex(Color colour);

    /// <summary>The colour a machine is painted in, or nothing when it does not say.</summary>
    bool Hue(string? colour, out Color hue);
}
