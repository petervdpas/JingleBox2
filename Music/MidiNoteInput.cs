using System;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Music;

/// <inheritdoc/>
public sealed class MidiNoteInput : IMidiNoteInput
{
    /// <inheritdoc cref="IMidiNoteInput.SemitoneOffset"/>
    public const int SemitoneOffset = -12;

    /// <inheritdoc cref="IMidiNoteInput.MinMidiNote"/>
    public const int MinMidiNote = 0;

    /// <inheritdoc cref="IMidiNoteInput.MaxMidiNote"/>
    public const int MaxMidiNote = 127;

    /// <inheritdoc cref="IMidiNoteInput.MaxVelocity"/>
    public const int MaxVelocity = 127;

    /// <inheritdoc/>
    int IMidiNoteInput.SemitoneOffset => SemitoneOffset;

    /// <inheritdoc/>
    int IMidiNoteInput.MinMidiNote => MinMidiNote;

    /// <inheritdoc/>
    int IMidiNoteInput.MaxMidiNote => MaxMidiNote;

    /// <inheritdoc/>
    int IMidiNoteInput.MaxVelocity => MaxVelocity;

    /// <inheritdoc/>
    public bool TryNote(int midiNote, out Note note)
    {
        note = Note.Empty;
        if (midiNote < MinMidiNote || midiNote > MaxMidiNote) return false;

        int semitone = midiNote + SemitoneOffset;
        if (semitone < Note.MinSemitone || semitone > Note.MaxSemitone) return false;

        note = new Note(semitone);
        return true;
    }

    /// <inheritdoc/>
    public int VolumeFor(int velocity) => Math.Clamp(velocity, 0, MaxVelocity);

    /// <inheritdoc/>
    public bool TryMidi(Note note, out int midiNote)
    {
        midiNote = 0;
        if (!note.IsPlayable) return false;

        int number = note.Semitone - SemitoneOffset;
        if (number < MinMidiNote || number > MaxMidiNote) return false;

        midiNote = number;
        return true;
    }

    /// <inheritdoc/>
    public int VelocityFor(int volume) =>
        volume == TrackerCell.NoVolume ? MaxVelocity : Math.Clamp(volume, 1, MaxVelocity);
}
