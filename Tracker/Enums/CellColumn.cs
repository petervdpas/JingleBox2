using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Enums;

/// <summary>Which stop of a cell the cursor is on.</summary>
/// <remarks>
/// The numbers are the order the stops are drawn in and are used as indexes into
/// <see cref="PatternMetrics.ColumnWidths"/>, so they are written out rather than left implicit.
///
/// A cell holds four things and has five stops, because the command is two: its letter and its
/// amount. One stop for both meant a key had to be read as either, and A to F are both a letter
/// and a digit, so the commands named by those letters could not be typed at all.
/// </remarks>
public enum CellColumn
{
    /// <summary>What to play.</summary>
    Note = 0,

    /// <summary>Which instrument to play it on.</summary>
    Instrument = 1,

    /// <summary>How loud.</summary>
    Volume = 2,

    /// <summary>The letter of the command: any letter or digit typed here names it.</summary>
    Effect = 3,

    /// <summary>The command's amount, two hex digits, drawn straight after its letter.</summary>
    Amount = 4
}
