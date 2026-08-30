namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What rate the sound card is opened at, which has to be the rate the mixer works at.
/// </summary>
/// <remarks>
/// **One number, said in one place, used at both ends of the output.** The card was opened at a
/// literal 44100 while the mixer read the setting, so choosing anything else bought a resample
/// down to the card and another back up by the system mixer: two conversions to arrive where none
/// were needed, and the middle one throws away everything above half the card's rate for good.
///
/// Nothing reports it and nothing sounds broken when it happens. The sound is merely worse than it
/// should be, which is exactly why it wants a rule rather than a literal in two files: two
/// spellings of one number eventually disagree, and here the way they fail is silent.
/// </remarks>
public interface IOutputRate
{
    /// <summary>The rate to open at, given what the settings hold.</summary>
    /// <param name="setting">What the settings hold, or nought for nothing chosen.</param>
    int Chosen(int setting);
}
