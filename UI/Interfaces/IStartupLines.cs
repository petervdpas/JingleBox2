using System.Collections.Generic;

namespace JingleBox2.UI.Interfaces;

/// <summary>
/// What the application says about itself while it is starting, for whoever is showing it.
/// </summary>
/// <remarks>
/// A seam rather than the splash window itself, because the thing doing the talking is the
/// constructor of the one window and the thing listening is a window that will be gone a moment
/// later. Told rather than watched: the steps named here are plain statements in that
/// constructor, and a startup with a notification system in front of it would be a worse startup.
///
/// Two members and one line on the screen. A step is what is under way, which is the answer when
/// somebody reports that the application takes a while to open; the devices are what this
/// installation actually has on its rack, which is the first thing worth knowing when a box
/// somebody made is not where they left it.
///
/// Nothing here is a question, so an application starting with nobody watching hands in nothing
/// and every call is skipped.
/// </remarks>
public interface IStartupLines
{
    /// <summary>Says what the application is busy with now, and clears what was under it.</summary>
    /// <remarks>
    /// The heading of the two lines. What is under it belongs to the step that put it there, so
    /// a new step arrives with nothing beneath it until it says otherwise.
    /// </remarks>
    /// <param name="what">The step under way, in the words somebody reading a log would want.</param>
    void Doing(string what);

    /// <summary>Says one thing under the step: a device being read, a setting being applied.</summary>
    /// <remarks>
    /// One at a time, each replacing the last, which is what makes the pair a sentence: the
    /// heading is what is going on and this is which one of them. It is also why nothing here
    /// repeats the heading's words.
    /// </remarks>
    /// <param name="one">The thing itself, named as shortly as it can honestly be.</param>
    void Under(string one);

    /// <summary>Names the devices on this installation's rack as they are read.</summary>
    /// <remarks>
    /// Devices, which is the word for both of them: a soundmachine is played and an effect is
    /// not, and on the way up that difference is nobody's business. Called once per world, since
    /// the two are read one after the other, and what arrives is treated the same either way.
    ///
    /// Every one, whoever made it: what is read is the installed folder, so a box somebody built
    /// themselves is named beside the ones that ship, which is the whole reason the names are
    /// worth saying rather than a count.
    ///
    /// Handed over as a list rather than called once per box, because the pace they reach the
    /// screen at is the splash's own business and not the caller's.
    /// </remarks>
    /// <param name="names">What they are called, in any order.</param>
    void Devices(IEnumerable<string> names);
}
