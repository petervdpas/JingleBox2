using JingleBox2.Rack.SoundDevices.Faces.Records;

namespace JingleBox2.Rack.SoundDevices.Interfaces;

/// <summary>
/// What a device is, as far as anything outside it is concerned.
/// </summary>
/// <remarks>
/// A device is a box on the rack: a face over an engine, with an id it is known by in files, a
/// name, a line saying what it is, and colours of its own. A soundmachine is one and an effect is
/// one, and everything here is true of both: the host has to be able to list what there is, tell
/// them apart, put one on the rack and paint it, and that is the whole of this.
///
/// What differs between the two is what happens to a device once it is in a song. A soundmachine
/// is played, and there it becomes an instrument with your name and your settings; an effect is
/// handed a whole track's audio and stands on that track's chain. Neither of those is a question
/// this answers, which is why one contract serves both: it was two, `ISoundDevice` and `ISoundDevice`,
/// declaring the same four members in two files.
///
/// Everything here is fixed for the life of the device. What it holds, the sound it makes or
/// works on, and the panel it is laid out on are separate contracts, so a device can be described
/// without any of them being loaded.
/// </remarks>
public interface ISoundDevice
{
    /// <summary>
    /// The name this device is known by in files, forever.
    /// </summary>
    /// <remarks>
    /// Written into every song that uses it, so it can never change: a device that renames itself
    /// orphans everything anybody made with it. Two devices with the same id are the same device,
    /// whoever wrote them, which is how one edited in its own project replaces the installed copy
    /// of itself.
    ///
    /// It is also what decides whether the device can be had at all. A device is a face over one
    /// of the engines built into the application, and the id is what says which: an id this build
    /// has no engine for is read off disc and never reaches the rack. That is deliberate, since a
    /// box that cannot sound is worse than no box, and it is the piece that has to move before a
    /// device written by somebody else can be installed.
    /// </remarks>
    string Id { get; }

    /// <summary>What it is called on the rack.</summary>
    string Name { get; }

    /// <summary>The one line under the name saying what sort of thing it is.</summary>
    string Summary { get; }

    /// <summary>
    /// Its colours, which are its own and not the application's.
    /// </summary>
    /// <remarks>
    /// A device is exempt from the application's theme: the blue one is blue on a dark theme and
    /// blue on a light one, and you know which box you are in front of before you have read
    /// anything on it.
    /// </remarks>
    PanelTheme Theme { get; }

    /// <summary>The colour it is, which every other shade of it is made from.</summary>
    /// <remarks>
    /// Answered from <see cref="Theme"/> rather than kept beside it, so the two cannot disagree.
    /// </remarks>
    string Colour => Theme.Accent;
}
