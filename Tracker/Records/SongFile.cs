namespace JingleBox2.Tracker.Records;

/// <summary>
/// A saved song on disk. Name is what the user sees, path is what the loader needs, and the
/// description is what the song says about itself, for telling two of them apart in a list.
/// </summary>
/// <param name="Name">The file's own name without its extension, which is what a picker shows.</param>
/// <param name="Path">Where the file is, which is all the loader needs.</param>
/// <param name="Description">
/// What the song says about itself, read out of the file rather than remembered anywhere else,
/// so a song written on another machine still describes itself here.
/// </param>
/// <param name="Saved">
/// When the file was last written, by the file's own clock, or the default for one that would
/// not say.
/// </param>
public sealed record SongFile(
    string Name, string Path, string Description = "", System.DateTime Saved = default)
{
    /// <summary>True when there is something to show under the name.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>True when the file had a date to give, which every real one does.</summary>
    public bool HasSaved => Saved != System.DateTime.MinValue && Saved != default;

    /// <summary>
    /// When it was last saved, said the way somebody scanning a list of songs would say it.
    /// </summary>
    /// <remarks>
    /// The time alone for today, the day and the time for this week, and the date for anything
    /// older. A list of songs is read to find the one you were working on, and "14:32" answers
    /// that where "30/08/2026 14:32" makes every row the same width of digits and none of them
    /// worth reading.
    ///
    /// The machine's own format for the date and the time, since it is a date being shown to
    /// the person sitting at the machine rather than anything that travels.
    /// </remarks>
    public string SavedText
    {
        get
        {
            if (!HasSaved) return "";

            var now = System.DateTime.Now;

            if (Saved.Date == now.Date) return Saved.ToString("t", System.Globalization.CultureInfo.CurrentCulture);

            if (now - Saved < System.TimeSpan.FromDays(6))
                return Saved.ToString("ddd HH:mm", System.Globalization.CultureInfo.CurrentCulture);

            return Saved.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    /// <summary>The name alone, since a list of these is bound straight to a picker.</summary>
    public override string ToString() => Name;
}
