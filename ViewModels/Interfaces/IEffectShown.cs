using JingleBox2.Rack.Faces.Interfaces;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// One of our effects with its face on the screen, in the three words a link needs about it.
/// </summary>
/// <remarks>
/// A link on one of ours names the effect's id and the parameter's key and never says where the
/// box is standing, which is what makes it travel: the same link means the same thing on
/// everybody's disc. So something has to answer which EchoBox, and the box whose face you have
/// open is the only answer that is right on a track, on the master and on a pad alike.
///
/// Three things and no more. Which effect it is, so a link naming another one is refused. Where
/// it is standing, which is only ever printed in a sentence on a status line. And what its knobs
/// stand at, which is the one that matters twice over: it is where a value is written, and it is
/// the object the face is reading, so writing through it is what makes the panel redraw. Writing
/// past it into the engine moves the sound and leaves the picture where it was, which reads as a
/// knob that is not linked at all.
/// </remarks>
public interface IEffectShown
{
    /// <summary>Which effect this is, by the id its manifest carries.</summary>
    string Id { get; }

    /// <summary>Where its chain is: a track's name, the master, or a pad.</summary>
    /// <remarks>
    /// For the sentence a status line prints and nothing else. Taken from the chain's owner when
    /// the box was built rather than asked for later, since the chain view under the pattern is
    /// pointed at whichever track the cursor is on and a box outlives that.
    /// </remarks>
    string Where { get; }

    /// <summary>What its knobs stand at, which is what the face is reading.</summary>
    IPanelValues Values { get; }
}
