using System;
using System.Globalization;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class TrackerCellText : ITrackerCellText
{
    /// <summary>How a blank instrument or volume column is written.</summary>
    public const string BlankByte = "..";

    /// <summary>And a blank effect column, which is three characters wide rather than two.</summary>
    public const string BlankEffect = "...";

    /// <inheritdoc/>
    string ITrackerCellText.BlankByte => BlankByte;

    /// <inheritdoc/>
    string ITrackerCellText.BlankEffect => BlankEffect;

    /// <inheritdoc/>
    public string Write(TrackerCell cell) =>
        $"{cell.Note} {cell.InstrumentText} {cell.VolumeText} {cell.Effect}";

    /// <inheritdoc/>
    public bool TryRead(string? text, out TrackerCell cell)
    {
        cell = TrackerCell.Empty;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return false;

        if (!Note.TryParse(parts[0], out var note)) return false;
        if (!TryReadByte(parts[1], NumberStyles.None, out int instrument)) return false;
        if (!TryReadByte(parts[2], NumberStyles.HexNumber, out int volume)) return false;
        if (!TryReadEffect(parts[3], out var effect)) return false;

        cell = new TrackerCell(note, instrument, TrackerCell.ClampVolume(volume), effect);
        return true;
    }

    /// <summary>
    /// One two-character column, decimal or hex depending on which it is.
    /// </summary>
    /// <remarks>
    /// A blank column reads as -1, which is what both <see cref="TrackerCell.NoInstrument"/> and
    /// <see cref="TrackerCell.NoVolume"/> are, so one function serves the two of them.
    /// </remarks>
    private static bool TryReadByte(string text, NumberStyles style, out int value)
    {
        value = -1;
        if (text == BlankByte) return true;

        if (!int.TryParse(text, style, CultureInfo.InvariantCulture, out int parsed)) return false;

        value = parsed;
        return true;
    }

    /// <summary>
    /// The effect column: a letter and two hex digits.
    /// </summary>
    /// <remarks>
    /// The letter is not checked against the four the player knows. A command from a later
    /// version has to read back and be written out again untouched, or opening a song here
    /// would quietly throw away what somebody else's copy put in it.
    /// </remarks>
    private static bool TryReadEffect(string text, out TrackerCommand effect)
    {
        effect = TrackerCommand.None;
        if (text == BlankEffect) return true;
        if (text.Length != 3) return false;

        if (!int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parameter))
            return false;

        effect = new TrackerCommand(char.ToUpperInvariant(text[0]), parameter);
        return true;
    }
}
