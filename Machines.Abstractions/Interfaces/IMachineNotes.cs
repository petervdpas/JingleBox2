
namespace JingleBox2.Machines.Interfaces;

/// <summary>
/// What a key is called, for the parts of a machine that have to write one down.
/// </summary>
/// <remarks>
/// The same twelve names the tracker uses, said again here because a machine cannot reach into
/// the tracker: a machine is drawn against the contract and nothing else, and a pad button
/// declaring the key it answers to has to be able to say it.
///
/// Two letters and an octave, which is what the pattern editor writes and therefore what
/// somebody hunting for a pad on a keyboard is looking for. The two directions are a pair, and
/// a pair that disagrees is a pad that sounds a different note from the one written on it.
/// </remarks>
public interface IMachineNotes
{
    /// <summary>That semitone as a note, or nothing when it is not one.</summary>
    string Name(int semitone);

    /// <summary>
    /// That note as a semitone, or -1 when it is not one.
    /// </summary>
    /// <remarks>
    /// The other way round, because a machine writes the note and the engine plays the number. A
    /// plain number is taken too, for a machine written before the notes were spelled out, and
    /// the note is tried first: the two forms overlap at three characters, so choosing between
    /// them by length loses every plain number from 100 to 119.
    /// </remarks>
    int Semitone(string said);
}
