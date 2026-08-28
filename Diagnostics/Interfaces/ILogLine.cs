using System;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// The shape of one line in the log.
/// </summary>
/// <remarks>
/// The time, the area and the process go on the front. The process because plugins run in
/// processes of their own and write to this same file, so their account of what happened sits
/// beside the application's in the order it happened, and without the number nobody could tell
/// which of them said what.
///
/// Everything is padded to a fixed width so the messages line up down the page. A log is read
/// by eye, usually in a hurry, and a column that moves is a column nobody can scan.
/// </remarks>
public interface ILogLine
{
    /// <summary>One line, ready to be written, newline included.</summary>
    /// <param name="area">Which part of the application is speaking.</param>
    /// <param name="at">When it spoke.</param>
    /// <param name="processId">Which process it was, since a plugin's own writes here too.</param>
    /// <param name="message">What it had to say.</param>
    string Format(LogArea area, DateTime at, int processId, string message);

    /// <summary>The line that says how many were dropped, for a log that could not keep up.</summary>
    /// <remarks>
    /// Said once at the end of a batch rather than per line, which is the only way it can be
    /// said at all: the lines that went missing went missing because there was no room to say
    /// anything, and a note per line would be the same flood under another name.
    /// </remarks>
    /// <param name="lost">How many lines went unwritten.</param>
    string Lost(int lost);
}
