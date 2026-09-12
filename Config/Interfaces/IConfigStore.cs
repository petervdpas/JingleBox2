
namespace JingleBox2.Config.Interfaces;

/// <summary>
/// The settings file: where it is, what it says, and what it is made to say before anybody
/// reads it.
/// </summary>
/// <remarks>
/// One file for the whole application, written whole every time and never appended to, so a
/// save is either the old settings or the new ones. That is <see cref="Files.Interfaces.ISafeFile"/>'s job
/// rather than this one, but it is the reason a save here can be asked for as often as a level
/// being dragged wants it.
///
/// The part worth knowing is that nothing between loading and using is allowed to be wrong.
/// Every read and every write puts the settings into a shape the rest of the application can
/// take without checking: the matrix is inside what <see cref="PadMatrix"/> allows, there is a
/// profile called "default" and the selected one exists, every profile holds exactly rows times
/// columns pads, and there is one MIDI mapping per pad numbered in order. So a settings file
/// that was edited by hand, written by an older version, or truncated by a crash comes out the
/// far side usable rather than being refused, and a caller never has to ask whether a pad it
/// was handed is really there.
///
/// A file that cannot be read at all is not an error either: the defaults are written over it.
/// The alternative is an application that will not start because of a stray comma, and the
/// settings are not worth that.
/// </remarks>
public interface IConfigStore
{
    /// <summary>Where the settings are, under the application folder.</summary>
    string ConfigPath { get; }

    /// <summary>
    /// The settings as they stand, made usable, and written out if there were none.
    /// </summary>
    /// <remarks>
    /// Anything unreadable is treated as nothing there. A file damaged past parsing is worth
    /// less than a running application, and the one thing that must not happen is a start that
    /// stops halfway with a JSON error on it.
    /// </remarks>
    AppConfig LoadOrCreateDefault();

    /// <summary>
    /// Writes the settings out, having first put them in order.
    /// </summary>
    /// <remarks>
    /// **The tidying happens to a copy, so writing the settings down does not change them.** The
    /// document is put in order when it is read, which is where a file somebody edited by hand or
    /// an older version wrote is dealt with; doing it again on the way out is about the file
    /// rather than about what is in memory. The caller is holding the instance the whole
    /// application is running on, and the thing that most wants correcting on the way out is
    /// every pad's source, which is written as <c>{app}/</c> and is a path the pads are playing
    /// from while it is.
    ///
    /// That is what lets the file be written from a thread of its own. Nothing here reaches into
    /// what anybody else is reading.
    /// </remarks>
    /// <param name="cfg">The settings as the application is holding them.</param>
    void Save(AppConfig cfg);

    /// <summary>
    /// The settings exactly as the file would hold them, without writing anything.
    /// </summary>
    /// <remarks>
    /// **For knowing whether there is anything to write.** Whatever keeps the file up to date has
    /// to be able to answer that without touching the disc, and the only honest answer is the
    /// text itself: a document is a wall of fields, several of them lists that are added to in
    /// place, so nothing shorter than what would be written can say whether it would differ from
    /// what was.
    ///
    /// The same call the writing goes through, so the comparison and the file can never be made
    /// of two different spellings of the document.
    /// </remarks>
    /// <param name="cfg">The settings as the application is holding them.</param>
    string Written(AppConfig cfg);

    /// <summary>
    /// Puts that text in the file, whole.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="Written"/>, for whoever has already worked out what the file
    /// should say in order to find out whether it needed saying. Without it the text would be
    /// made twice for every write, once to compare and once to keep.
    /// </remarks>
    /// <param name="written">What the file should say, as <see cref="Written"/> answered it.</param>
    void Write(string written);
}
