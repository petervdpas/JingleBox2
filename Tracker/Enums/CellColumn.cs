using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Enums;

/// <summary>Which of a cell's four columns the cursor is on.</summary>
/// <remarks>
/// The numbers are the order the columns are drawn in and are used as indexes into
/// <see cref="PatternMetrics.ColumnWidths"/>, so they are written out rather than left implicit.
/// </remarks>
public enum CellColumn
{
    /// <summary>What to play.</summary>
    Note = 0,

    /// <summary>Which instrument to play it on.</summary>
    Instrument = 1,

    /// <summary>How loud.</summary>
    Volume = 2,

    /// <summary>One effect command.</summary>
    Effect = 3
}
