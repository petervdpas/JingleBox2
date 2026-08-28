using System.Globalization;

namespace JingleBox2.Tracker;

/// <summary>
/// One effect command: a letter and a byte parameter, as trackers have always written them.
/// The set is deliberately small; unknown commands are stored and ignored by the player
/// rather than rejected, so a song from a later version still loads.
/// </summary>
/// <param name="Command">The letter, upper case, or <see cref="NoCommand"/> for a blank column.</param>
/// <param name="Parameter">The byte after it, shown as two hex digits.</param>
public readonly record struct TrackerEffect(char Command, int Parameter)
{
    /// <summary>The letter a blank effect column carries.</summary>
    public const char NoCommand = '\0';

    /// <summary>A blank effect column.</summary>
    public static readonly TrackerEffect None = new(NoCommand, 0);

    /// <summary>
    /// <c>Vxx</c>: set the voice's volume, 00 to 40.
    /// </summary>
    /// <remarks>
    /// One of the four commands the player understands. Anything else is kept in the cell and
    /// written back out unchanged, and does nothing while the song plays.
    /// </remarks>
    public const char SetVolume = 'V';

    /// <summary><c>Pxx</c>: pan the voice, 00 hard left, 40 centre, 80 hard right.</summary>
    public const char SetPan = 'P';

    /// <summary><c>Rxx</c>: retrigger the voice every xx ticks.</summary>
    public const char Retrigger = 'R';

    /// <summary><c>Axy</c>: cycle the note, the note plus x, and the note plus y.</summary>
    public const char Arpeggio = 'A';

    /// <summary>True when the column is blank.</summary>
    public bool IsNone => Command == NoCommand;

    /// <summary>True for one of the four the player acts on, rather than one merely kept.</summary>
    public bool IsKnown => Command is SetVolume or SetPan or Retrigger or Arpeggio;

    /// <summary>Three characters, as every column here is: "..." when blank, else "V40".</summary>
    public override string ToString() =>
        IsNone ? "..." : $"{Command}{Parameter.ToString("X2", CultureInfo.InvariantCulture)}";
}
