using System.Globalization;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// One effect command: a letter and a byte parameter, as trackers have always written them.
/// The set is deliberately small; unknown commands are stored and ignored by the player
/// rather than rejected, so a song from a later version still loads.
/// </summary>
/// <remarks>
/// <c>V</c> and <c>P</c> are levels and are read where a note is given its level. The four that
/// say what happens inside a line, <c>Q</c>, <c>C</c>, <c>R</c> and <c>A</c>, are played by the
/// commands module, <see cref="JingleBox2.Tracker.Commands.Interfaces.ILineCommands"/>, which
/// splits a line into ticks.
/// </remarks>
/// <param name="Command">The letter, upper case, or <see cref="NoCommand"/> for a blank column.</param>
/// <param name="Parameter">The byte after it, shown as two hex digits.</param>
public readonly record struct TrackerCommand(char Command, int Parameter)
{
    /// <summary>The letter a blank effect column carries.</summary>
    public const char NoCommand = '\0';

    /// <summary>A blank effect column.</summary>
    public static readonly TrackerCommand None = new(NoCommand, 0);

    /// <summary>
    /// <c>Vxx</c>: set the voice's volume, 00 to 80, which is the volume column's own scale.
    /// </summary>
    public const char SetVolume = 'V';

    /// <summary><c>Pxx</c>: pan the voice, 00 hard left, 40 centre, 80 hard right.</summary>
    public const char SetPan = 'P';

    /// <summary>
    /// <c>Rxy</c>: play the note again every y ticks, each time x sixteenths quieter than the
    /// last; x of nought keeps the level.
    /// </summary>
    public const char Retrigger = 'R';

    /// <summary>
    /// <c>Axy</c>: cycle the note, the note plus x semitones and the note plus y, one step a
    /// tick, without starting it again.
    /// </summary>
    public const char Arpeggio = 'A';

    /// <summary><c>Qxx</c>: start the note xx ticks into its line.</summary>
    public const char Delay = 'Q';

    /// <summary><c>Cxx</c>: silence the note xx ticks into its line, with no release.</summary>
    public const char Cut = 'C';

    /// <summary>True when the column is blank.</summary>
    public bool IsNone => Command == NoCommand;

    /// <summary>True for one of the letters this names.</summary>
    public bool IsKnown => Command is SetVolume or SetPan or Retrigger or Arpeggio or Delay or Cut;

    /// <summary>Three characters, as every column here is: "..." when blank, else "V40".</summary>
    public override string ToString() =>
        IsNone ? "..." : $"{Command}{Parameter.ToString("X2", CultureInfo.InvariantCulture)}";
}
