using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// The on-disk and on-screen text form of a cell: "C-4 01 40 V20", with ".." and "..." for
/// blank columns. One place that knows the format, so the editor and the file agree.
/// </summary>
public interface ITrackerCellText
{
    /// <summary>How a blank instrument or volume column is written.</summary>
    string BlankByte { get; }

    /// <summary>And a blank effect column, which is three characters wide rather than two.</summary>
    string BlankEffect { get; }

    /// <summary>One cell as the file and the grid both write it.</summary>
    /// <param name="cell">The cell to write out.</param>
    string Write(TrackerCell cell);

    /// <summary>Parses what <see cref="Write"/> produced. Returns false on anything malformed.</summary>
    /// <remarks>
    /// Blank text is not malformed and gives an empty cell, since a song file only stores the
    /// cells that hold something and everything else is read as nothing.
    /// </remarks>
    /// <param name="text">One cell's worth of text, or nothing at all.</param>
    /// <param name="cell">What it said, or an empty cell when it said nothing.</param>
    bool TryRead(string? text, out TrackerCell cell);
}
