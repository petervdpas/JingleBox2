using System.Collections.Generic;

namespace JingleBox2.UI.Records;

/// <summary>
/// What is actually carrying audio at this moment, one answer per path through the application.
/// </summary>
/// <remarks>
/// **A cable that is carrying something is drawn solid and one that is not is dashed**, which is
/// the difference between a picture of how this program is wired and a picture of what it is
/// doing. A patchbay that never changed while a show was running would be a diagram.
///
/// Yes or no rather than a level, because that is the question: where the threshold is depends on
/// what is being measured and belongs to whoever is measuring it, and a picture that dimmed with
/// the music would be a second set of meters on a page that is not about levels.
/// </remarks>
/// <param name="Input">Whether anything is arriving at the recorder's input.</param>
/// <param name="Takes">Whether a take is being auditioned.</param>
/// <param name="Pads">Whether a pad is sounding.</param>
/// <param name="Tracks">
/// Which of the song's tracks are sounding, by the name their port carries.
/// </param>
/// <param name="Output">Whether anything at all is leaving through the master.</param>
public readonly record struct PatchSignals(
    bool Input,
    bool Takes,
    bool Pads,
    IReadOnlySet<string>? Tracks,
    bool Output)
{
    /// <summary>Whether a named track is sounding.</summary>
    /// <remarks>
    /// **Per track rather than per song**, because the tracker gives out one pair a track and
    /// lighting them all because something is playing would be saying an empty track is carrying
    /// audio. The names are the ones the mixer's own strips wear, so what the patchbay says
    /// about TR-03 is what the strip headed TR-03 is showing.
    ///
    /// Nothing known is nothing sounding, since a patchbay built where nobody can say what is
    /// playing draws every cable as it always did.
    /// </remarks>
    /// <param name="track">The track's name, as its port carries it.</param>
    public bool Sounding(string track) => Tracks != null && Tracks.Contains(track);
}
