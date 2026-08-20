using System;
using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// The on-disk and on-screen text form of a cell: "C-4 01 40 V20", with ".." and "..." for
/// blank columns. One place that knows the format, so the editor and the file agree.
/// </summary>
public static class TrackerCellText
{
    public const string BlankByte = "..";
    public const string BlankEffect = "...";

    public static string Write(TrackerCell cell) =>
        $"{cell.Note} {cell.InstrumentText} {cell.VolumeText} {cell.Effect}";

    /// <summary>Parses what <see cref="Write"/> produced. Returns false on anything malformed.</summary>
    public static bool TryRead(string? text, out TrackerCell cell)
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

    private static bool TryReadByte(string text, NumberStyles style, out int value)
    {
        value = -1;
        if (text == BlankByte) return true;

        if (!int.TryParse(text, style, CultureInfo.InvariantCulture, out int parsed)) return false;

        value = parsed;
        return true;
    }

    private static bool TryReadEffect(string text, out TrackerEffect effect)
    {
        effect = TrackerEffect.None;
        if (text == BlankEffect) return true;
        if (text.Length != 3) return false;

        if (!int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parameter))
            return false;

        effect = new TrackerEffect(char.ToUpperInvariant(text[0]), parameter);
        return true;
    }
}
