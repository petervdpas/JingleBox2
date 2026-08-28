using System.Globalization;

namespace JingleBox2.Machines;

/// <summary>
/// What a key is called, for the parts of a machine that have to write one down.
/// </summary>
/// <remarks>
/// The same twelve names the tracker uses, said again here because a machine cannot reach into
/// the tracker: a machine is drawn against the contract and nothing else, and a pad button
/// declaring the key it answers to has to be able to say it.
///
/// Two letters and an octave, which is what the pattern editor writes and therefore what somebody
/// hunting for a pad on a keyboard is looking for.
/// </remarks>
public static class MachineNotes
{
    /// <summary>
    /// The twelve, each padded to two characters.
    /// </summary>
    /// <remarks>
    /// The hyphen on a natural is what keeps every note the same width, so a column of them
    /// lines up in the monospaced font the pattern is drawn in.
    /// </remarks>
    private static readonly string[] Names =
        { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

    /// <summary>That semitone as a note, or nothing when it is not one.</summary>
    public static string Name(int semitone)
    {
        if (semitone is < 0 or > 119) return "";

        return Names[semitone % 12] + (semitone / 12).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// That note as a semitone, or -1 when it is not one.
    /// </summary>
    /// <remarks>
    /// The other way round, because a machine writes the note and the engine plays the number. A
    /// plain number is taken too, for a machine written before the notes were spelled out.
    /// </remarks>
    public static int Semitone(string said)
    {
        if (said is not { Length: 3 })
            return int.TryParse(said, out int plain) && plain is >= 0 and <= 119 ? plain : -1;

        int at = System.Array.IndexOf(Names, said[..2].ToUpperInvariant());

        if (at < 0) return -1;

        if (!int.TryParse(said[2..], out int octave) || octave is < 0 or > 9) return -1;

        return octave * 12 + at;
    }
}
