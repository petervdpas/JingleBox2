namespace JingleBox2.Midi;

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

/// <summary>
/// One thing on a track that could be pointed at, ready to be put in a list.
/// </summary>
/// <remarks>
/// The device is the heading and the name is the row. Apart rather than joined, because a list
/// gathered under its devices is the only shape in which forty parameters can be read: joined,
/// every row would begin with the same word for as long as one device's parameters ran.
/// </remarks>
/// <param name="Mapping">What to ask <see cref="IControlTargets.Find"/> for.</param>
/// <param name="Device">What holds it: a machine, a plugin, or the mixer.</param>
/// <param name="Name">What the parameter is called on its own face.</param>
/// <param name="Unit">What it is measured in, when the thing said. Empty otherwise.</param>
public sealed record ControlChoice(ControlMapping Mapping, string Device, string Name, string Unit = "");
