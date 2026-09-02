using System.Globalization;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Rack.SoundDevices.Faces;

/// <inheritdoc/>
public sealed class PanelNotes : IPanelNotes
{
    /// <summary>
    /// The twelve, each padded to two characters.
    /// </summary>
    /// <remarks>
    /// The hyphen on a natural is what keeps every note the same width, so a column of them
    /// lines up in the monospaced font the pattern is drawn in.
    /// </remarks>
    private readonly string[] Names =
        { "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-" };

    /// <inheritdoc/>
    public string Name(int semitone)
    {
        if (semitone is < 0 or > 119) return "";

        return Names[semitone % 12] + (semitone / 12).ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The note is tried first and the plain number second, rather than choosing between them
    /// by how long the text is. Length decided it once, and three characters is both a note and
    /// a three digit number: every plain number from 100 to 119 was read as a note, failed to
    /// be one, and came back as nothing. That is the top two octaves, which are notes a machine
    /// can really be asked to play.
    /// </remarks>
    public int Semitone(string said)
    {
        if (said is { Length: 3 })
        {
            int at = System.Array.IndexOf(Names, said[..2].ToUpperInvariant());

            if (at >= 0 && int.TryParse(said[2..], out int octave) && octave is >= 0 and <= 9)
                return octave * 12 + at;
        }

        return int.TryParse(said, out int plain) && plain is >= 0 and <= 119 ? plain : -1;
    }
}
