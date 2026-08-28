namespace JingleBox2.Config;

/// <summary>
/// The settings file: where it is, what it says, and what it is made to say before anybody
/// reads it.
/// </summary>
/// <remarks>
/// One file for the whole application, written whole every time and never appended to, so a
/// save is either the old settings or the new ones. That is <see cref="SafeFile"/>'s job
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
    /// The tidying happens to the object it was handed rather than to a copy, so the caller's
    /// settings and the file agree afterwards. That matters more than it sounds: the caller is
    /// usually holding the same instance the application is running on, and a save that quietly
    /// corrected the file while leaving the running settings wrong would put the two out of step
    /// until the next start.
    /// </remarks>
    void Save(AppConfig cfg);
}
