namespace JingleBox2.Rack.SoundDevices.Faces.Records;

/// <summary>
/// A sound device's own colours, which are nobody else's business.
/// </summary>
/// <remarks>
/// A sound device is exempt from the application's theme. A rack device looks the way it looks
/// whatever the room around it is painted: the blue one is blue on a dark theme and blue on a
/// light one, and you know which sound device you are in front of before you have read anything on
/// it. So these are shades of the sound device's own colour, not mixtures with the theme's.
///
/// Written as distances from the colour rather than as six colours per sound device: a sound device
/// says what it is and how deep its face is, and everything else follows. One that wants a lighter
/// face or a louder mark says so here, and only here.
/// </remarks>
/// <param name="Accent">The colour the box is, which every other shade is made from.</param>
/// <param name="Face">How far the panel's own face is darkened from it.</param>
/// <param name="Panel">And the groups standing on the face, which are lighter than it.</param>
/// <param name="Edge">How far the lines around them are lightened from it.</param>
/// <param name="Mark">The marks, curves and meters, which have to be seen against the face.</param>
/// <param name="Row">How much of the colour a row on a list is washed with.</param>
/// <param name="RowOver">The same row under the pointer.</param>
/// <param name="RowPicked">The row in hand, which is the box you are working on.</param>
public sealed record PanelTheme(
    string Accent,
    double Face = 0.68,
    double Panel = 0.52,
    double Edge = 0.10,
    double Mark = 0.35,
    double Row = 0.18,
    double RowOver = 0.30,
    double RowPicked = 0.58);
