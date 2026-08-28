using System.Collections.Generic;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// Which parts of the application are being written down, and what each is called.
/// </summary>
/// <remarks>
/// The whole of the log's decision-making, kept apart from the queue and the file so it can be
/// asked anything without a disc, a thread or a process. What is left in <see cref="Log"/>
/// itself is a queue, a thread and a file, which are process-wide by nature and are the reason
/// that door stays a static one.
///
/// One list of names for three jobs, so a word that can be typed into the environment variable
/// is a word that appears in the file and a switch on the settings page, and none of the three
/// can drift from the other two.
/// </remarks>
public interface ILogAreas
{
    /// <summary>Every area there is, with the word each is written under.</summary>
    /// <remarks>
    /// For the settings page, so the list of switches is the list of areas and the two cannot
    /// come apart: an area added here turns up there without anybody being told to add it.
    /// </remarks>
    IReadOnlyDictionary<LogArea, string> Everywhere { get; }

    /// <summary>The word an area is written under, or a plain one for a line about several.</summary>
    /// <param name="area">The area a line is about.</param>
    string Short(LogArea area);

    /// <summary>
    /// What the environment variable is asking for, or nothing when it is not asking.
    /// </summary>
    /// <remarks>
    /// "1" is everything, for the hands that have been typing that for months, and "0" is
    /// nothing. Anything else is read as a list of area names, separated by commas, spaces or
    /// semicolons, so one area can be had on its own without the other five burying it. A name
    /// this build does not know is passed over rather than refused, so a variable left set from
    /// a later version still starts the application.
    /// </remarks>
    /// <param name="said">What the variable holds, or null when it is not set at all.</param>
    LogArea Asked(string? said);

    /// <summary>
    /// Which areas end up being written, given the setting, the areas it names and the variable.
    /// </summary>
    /// <remarks>
    /// The variable wins over the setting, and says which areas as well as whether, so a build
    /// that will not start far enough to reach its settings can still be made to talk, and a
    /// run nobody can start is exactly the run worth narrowing by hand. A variable set to "0"
    /// asks for nothing and is not the same as one that is not set: it does not turn the
    /// setting off, it simply says nothing, since that is what "no areas" means everywhere else
    /// here.
    /// </remarks>
    /// <param name="on">Whether the setting says to write a log at all.</param>
    /// <param name="areas">Which areas the setting names, where it is on.</param>
    /// <param name="said">What the environment variable holds, or null when it is not set.</param>
    LogArea Wanted(bool on, LogArea areas, string? said);
}
