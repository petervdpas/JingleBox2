using JingleBox2.Midi.Enums;

namespace JingleBox2.Midi;

/// <summary>
/// What a hardware control can be pointed at on a mixer strip.
/// </summary>
/// <remarks>
/// A link names the strip it was made on. Point a fader at TR-02's level and it is TR-02's
/// level, wherever the cursor goes afterwards, which is what a mixer is.
///
/// It was the other way round first, one shared set for every strip, all of them following the
/// cursor, on the reasoning that a link per strip would be eight links to make and eight to
/// remember for a desk that has one fader. That reasoning names its own hardware assumption and
/// a nanoKONTROL2 breaks it: eight faders, eight knobs and twenty four buttons, and the whole
/// point of such a desk is that fader three is track three. Worse than merely unhelpful, it
/// could not be done at all, because two links following the cursor have the same target, so
/// <see cref="ControlMapping.SameTarget"/> read the second as a replacement for the first and
/// pointing fader two at TR-02 quietly unlinked fader one.
///
/// And it disagreed with the layout a device already gets before anybody points at anything,
/// which pins fader three to track three. Two ways of doing one thing that answer differently is
/// the fault underneath the fault: whichever was right, they could not both be.
///
/// The master is the one strip that is fixed for a different reason. There is only ever one of
/// it, so it is strip -1 rather than a track number, which is why <see cref="On"/> answers for
/// both and nothing else has to know the difference.
///
/// Templates, never handed out as they are. <see cref="Views.Pointable"/> copies one before it
/// is offered, because a link keeps the object it was given.
/// </remarks>
public static class MixLinks
{
    /// <summary>
    /// What a control on that strip offers: this value, on that strip, wherever you are.
    /// </summary>
    /// <remarks>
    /// The one maker for both kinds of strip. A track is its own number and the master is -1,
    /// which is the number it is everywhere else in the application, so the caller hands over
    /// whichever it has and does not ask which kind it is holding.
    ///
    /// A fresh mapping each time rather than a kept template, because there is one per strip per
    /// control and a mixer is rebuilt whenever the song is: holding them would mean holding the
    /// last song's.
    /// </remarks>
    /// <param name="what">Which of the strip's values.</param>
    /// <param name="track">The track it is on, or <c>TrackerPlayer.MasterStrip</c> for the master.</param>
    public static ControlMapping On(MixControl what, int track) => new()
    {
        Kind = ControlKind.Mix,
        Scope = ControlScope.Fixed,
        Track = track,
        Mix = what,
        Name = Said(what, track)
    };

    /// <summary>What to call it in a list of links, which is where eight of them sit together.</summary>
    /// <remarks>
    /// The strip is in the name because a list of eight levels with nothing to tell them apart is
    /// a list nobody can read. The same wording the default layout uses, so a link somebody made
    /// and one the device arrived with read alike.
    /// </remarks>
    private static string Said(MixControl what, int track)
    {
        string value = what switch
        {
            MixControl.Pan => "pan",
            MixControl.Mute => "mute",
            MixControl.Solo => "solo",
            MixControl.Duck => "duck",
            MixControl.Release => "duck release",
            _ => "level"
        };

        return track == Tracker.TrackerPlayer.MasterStrip
            ? "master " + value
            : "track " + (track + 1) + " " + value;
    }
}
