using System;

namespace JingleBox2.Config.Interfaces;

/// <summary>
/// The settings file, kept saying what the settings block says.
/// </summary>
/// <remarks>
/// **One writer, on a clock of its own, and nobody else decides when.** The settings used to be
/// written from thirty four places, each one a page that had just changed something and had
/// remembered to say so. What that cost is worth listing, because every item on it is a fault
/// this codebase has actually had: a level dragged across a strip wrote the file a hundred times;
/// two threads wrote it at once and one of them wrote through the other's half-written copy; two
/// pages hand-rolled a timer of their own to slow it down, at two different rates; and a setting
/// added with nobody remembering the call was simply never kept.
///
/// **It is told and it also looks, and the two answer different halves.** Being told is what
/// makes the file keep up: a hint arrives the moment somebody changes something and the file
/// follows a moment later. Looking is what makes it right: a document is a wall of fields with
/// lists in it that are added to in place, so there is no arrangement of events that could
/// promise to catch everything, and the only honest test of whether the file is behind is what
/// would be written against what was. So a hint nobody sent costs a moment rather than somebody's
/// work.
///
/// **Nothing it does reaches into what anybody else is holding.** Writing the settings down does
/// not change them, which is <see cref="IConfigStore.Save"/>'s promise and is what lets this run
/// where it likes.
/// </remarks>
public interface ISettingsOnDisc : IDisposable
{
    /// <summary>
    /// Looks once, and writes the file where it is behind.
    /// </summary>
    /// <remarks>
    /// Called by its own clock, on the way out, and by hand in a test. It answers whether it
    /// really wrote, which on an idle application is no for the rest of the session.
    /// </remarks>
    /// <returns>True where the file was behind and has been brought up to date.</returns>
    bool Check();
}
