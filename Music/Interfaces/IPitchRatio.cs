using JingleBox2.Tracker.Records;

namespace JingleBox2.Music.Interfaces;

/// <summary>
/// Turns a note into a playback rate. A sample recorded at one pitch is played faster or
/// slower to reach another, the way a tracker has always done it: no synthesis, just resampling.
/// </summary>
public interface IPitchRatio
{
    /// <summary>Twelve semitones double the rate, so an octave up plays twice as fast.</summary>
    int SemitonesPerOctave { get; }

    /// <summary>
    /// BASS clamps playback rate to a sane band. Past roughly six octaves either way the
    /// result is inaudible or unusably aliased, so refuse it rather than hand BASS junk.
    /// </summary>
    int MaxSemitoneShift { get; }

    /// <summary>Rate multiplier to hear <paramref name="note"/> from a sample recorded at <paramref name="baseNote"/>.</summary>
    /// <param name="note">The note wanted.</param>
    /// <param name="baseNote">The pitch the recording was made at.</param>
    double For(Note note, Note baseNote);

    /// <summary>The sample rate to hand BASS for that note, given the file's own rate.</summary>
    /// <param name="note">The note wanted.</param>
    /// <param name="baseNote">The pitch the recording was made at.</param>
    /// <param name="sampleRate">The rate the file itself holds.</param>
    double FrequencyFor(Note note, Note baseNote, int sampleRate);
}
