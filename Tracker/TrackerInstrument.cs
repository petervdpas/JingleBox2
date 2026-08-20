using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

/// <summary>
/// A sample and how to play it. There is no synthesis: an instrument is one of your
/// recordings plus the pitch it was recorded at, which is what makes transposing correct.
/// </summary>
public sealed class TrackerInstrument
{
    public string Name { get; set; } = "";

    /// <summary>Absolute path to the WAV file.</summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    /// The pitch the sample actually sounds at. Playing this note reproduces the file
    /// untouched; every other note is a resample relative to it.
    /// </summary>
    public int BaseNoteSemitone { get; set; } = Note.C4.Semitone;

    /// <summary>0-1 gain applied on top of the cell's volume column.</summary>
    public double Volume { get; set; } = 1.0;

    public bool Loop { get; set; }

    [JsonIgnore]
    public Note BaseNote
    {
        get => new(BaseNoteSemitone);
        set => BaseNoteSemitone = value.Semitone;
    }

    public TrackerInstrument Clone() => new()
    {
        Name = Name,
        FilePath = FilePath,
        BaseNoteSemitone = BaseNoteSemitone,
        Volume = Volume,
        Loop = Loop
    };
}
