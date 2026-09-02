using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Interfaces;

namespace JingleBox2.SoundDevices.Interfaces;

/// <summary>
/// A device as its folder holds it: what it is, plus where it is kept and who made it.
/// </summary>
/// <remarks>
/// <see cref="ISoundDevice"/> is what a device is; this is that same device as a folder on somebody's
/// disc, which adds only the two things a folder knows and a device does not: who wrote it and
/// which version this is, and where it was read from. Nothing about notes or audio is here, which
/// is the test of whether something belongs: a question this cannot answer is a question about
/// one world rather than about the rack.
///
/// It exists so the rules for a folder of boxes can be written once. Which folders ship, which
/// are this installation's, what has been offered, and what is brought up to date against what
/// are facts about a rack rather than about a machine, and they were written for machines first
/// because machines came first.
/// </remarks>
public interface IRackProject : ISoundDevice
{
    /// <summary>Who made it, for one that is going to be handed to somebody else.</summary>
    string Author { get; }

    /// <summary>Bumped by whoever makes it, and shown beside the name wherever it is listed.</summary>
    string Version { get; }

    /// <summary>
    /// How its face is put together, which is what the library draws it from.
    /// </summary>
    /// <remarks>
    /// Every device has one, laid out in the designer and carried in its manifest. A device whose
    /// face has never been laid out has an empty one, which is a panel with nothing on it rather
    /// than no panel at all: what to draw instead is the caller's decision and not the device's.
    /// </remarks>
    Panel Panel { get; }

    /// <summary>The folder it was read from, or empty for one that has never been saved.</summary>
    string Folder { get; }

    /// <summary>Whether it has a folder yet, which is what everything touching the disc holds against.</summary>
    /// <remarks>
    /// Asked rather than worked out from <see cref="Folder"/> at each call site, since one of
    /// those would eventually test the wrong thing about an empty string.
    /// </remarks>
    bool IsSaved { get; }
}
