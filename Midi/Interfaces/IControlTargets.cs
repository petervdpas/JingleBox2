using JingleBox2.Midi.Records;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// Where a mapping is turned into the thing it names, as things stand this second.
/// </summary>
/// <remarks>
/// Asked on every message rather than resolved once and held, because what a mapping names
/// moves underneath it: a track's instrument is swapped, a plugin is taken out of a chain, a
/// song is closed. Holding a target across any of those is holding something that has gone.
/// Answering null is ordinary and means the knob does nothing, which is the right thing for a
/// mapping that is about a track this song has not got.
///
/// Every hardware path in the application writes through this: a link somebody made by hand, a
/// control surface speaking Mackie Control, and an automation lane arriving at line 32. The
/// clock and the knob are one act, which is why they go through one door.
/// </remarks>
public interface IControlTargets
{
    /// <summary>The thing that mapping names, or nothing when this song has no such thing.</summary>
    IControlTarget? Find(ControlMapping mapping);

    /// <summary>
    /// What the modulation wheel drives on the machine a track plays, or nothing where it drives
    /// nothing.
    /// </summary>
    /// <remarks>
    /// A wheel is not pointed at anything and never has been: which parameter it moves is the
    /// machine's own declaration, carried in its manifest and travelling in its zip. So the
    /// machine has to be found before the mapping can be built, which is the one thing
    /// <see cref="Find"/> cannot do for a caller: it is handed a parameter and this has to go
    /// and ask for one.
    ///
    /// It still comes back as an <see cref="IControlTarget"/> rather than as a write, because
    /// what a wheel does to a machine parameter is exactly what a knob does to it: through the
    /// panel's own values, so the picture moves with the sound, in the parameter's own range, and
    /// answering nothing where the track plays nothing.
    ///
    /// Nothing for a plugin, which is sent the wheel as it arrived and decides for itself.
    ///
    /// Answers nothing unless a class means it to, the same as <see cref="On"/> and for the same
    /// reason: every implementation but one here is a test standing in for the program.
    /// </remarks>
    /// <param name="track">The track, counted from nought.</param>
    IControlTarget? Wheel(int track) => null;

    /// <summary>
    /// The same, for the machine open on the rack, which is what the rack's own keyboard plays.
    /// </summary>
    /// <remarks>
    /// Its own question rather than <see cref="Wheel"/> with a track that means the rack, because
    /// there is no track: an instrument being built on the rack may be in no song at all, which
    /// is the same reason its notes go to a bus of their own. What differs is only where the
    /// values are read and written, and both answers are worked out in one class so the wheel
    /// cannot mean two things depending on which keyboard is under the hand.
    /// </remarks>
    IControlTarget? WheelOnRack() => null;

    /// <summary>
    /// The same, for whichever instrument is being played by hand.
    /// </summary>
    /// <remarks>
    /// **Found by the instrument rather than by where a cursor is**, which is the whole reason
    /// this exists beside the other two. A panel is about one instrument and a hand on its wheels
    /// means that one, wherever the pattern happens to be pointing: a track's instrument window
    /// resolved against the rack turned a knob on a machine nobody was looking at, and one
    /// resolved against the cursor turned a knob on whichever track an arrow key last landed on.
    ///
    /// A song's own instrument is reached through the track that plays it, so the values written
    /// are the ones that panel is drawn from and the picture moves with the sound. One that is in
    /// no song is the rack's, which is the only other place an instrument can be played by hand.
    /// </remarks>
    /// <param name="instrument">What is under the hand.</param>
    IControlTarget? WheelFor(Tracker.TrackerInstrument? instrument) => null;

    /// <summary>
    /// Everything on a track that could be pointed at, and what to call each of them.
    /// </summary>
    /// <remarks>
    /// The other direction, and it exists for automation rather than for hardware. A link is
    /// made by pointing at a control and touching a knob, so nothing ever had to produce a list;
    /// a lane is made by choosing a parameter from one, which means the program has to be able
    /// to say what a track has on it.
    ///
    /// Not targets, because a target is resolved against this second and a list is looked at for
    /// as long as somebody is reading it. What comes back is what to ask for, and
    /// <see cref="Find"/> is still how you ask. But not bare mappings either: a mapping says
    /// which parameter and not what it is called, and the naming is already worked out here
    /// while the machine and the plugin are in hand. Asked again later it would come back as a
    /// target's name, which is written for a status line and ends in the track it is on, and a
    /// list of forty rows all ending in the same three words is a list nobody can scan.
    ///
    /// It answers nothing unless a class means it to. Every implementation but one here is a
    /// test standing in for the program, and a stand-in listing nothing is the truthful answer
    /// for it. The one that means it is <see cref="ControlTargets"/>, which is the only class
    /// that knows what a track is playing.
    /// </remarks>
    System.Collections.Generic.IEnumerable<ControlChoice> On(int track) =>
        System.Array.Empty<ControlChoice>();
}
