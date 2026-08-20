using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// One effect command: a letter and a byte parameter, as trackers have always written them.
/// The set is deliberately small; unknown commands are stored and ignored by the player
/// rather than rejected, so a song from a later version still loads.
/// </summary>
public readonly record struct TrackerEffect(char Command, int Parameter)
{
    public const char NoCommand = '\0';

    public static readonly TrackerEffect None = new(NoCommand, 0);

    // The commands the player understands. Anything else round-trips but does nothing.
    public const char SetVolume = 'V';   // Vxx: set voice volume, 00-40
    public const char SetPan = 'P';      // Pxx: pan, 00 left, 40 centre, 80 right
    public const char Retrigger = 'R';   // Rxx: retrigger every xx ticks
    public const char Arpeggio = 'A';    // Axy: cycle note, note+x, note+y

    public bool IsNone => Command == NoCommand;

    public bool IsKnown => Command is SetVolume or SetPan or Retrigger or Arpeggio;

    public override string ToString() =>
        IsNone ? "..." : $"{Command}{Parameter.ToString("X2", CultureInfo.InvariantCulture)}";
}
