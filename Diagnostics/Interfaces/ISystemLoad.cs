using JingleBox2.Diagnostics.Records;

namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// How busy the computer is, and how much of it this program is, for SETTINGS, System.
/// </summary>
/// <remarks>
/// "This program" is JingleBox2 and every process it started, however deep. A plugin runs in a
/// process of its own, and a plugin's face may run in another behind that, so the program alone
/// would leave out most of what a song with plugins in it costs.
///
/// Busy is measured between two readings, so the first reading has no figure for it and says
/// nought. Read about once a second: much faster and the figures jump about with whatever
/// happened in the last few milliseconds, and much slower and a spike is gone before it is shown.
/// </remarks>
public interface ISystemLoad
{
    /// <summary>
    /// Reads the figures now, or answers nothing on a system this cannot ask.
    /// </summary>
    /// <remarks>
    /// Not on the drawing thread: walking every process on the computer takes a few milliseconds.
    /// </remarks>
    LoadReading? Read();

    /// <summary>What the computer is, asked once and kept.</summary>
    MachineFacts Facts();
}
