using JingleBox2.Tracker.Records;

namespace JingleBox2.Music.Interfaces;

/// <summary>
/// The two-row piano layout trackers have used since the eighties: the lower letter row is
/// one octave, the upper row the next, with the black keys where they look right.
/// Keys are named as Avalonia reports them, so the view does no translation.
/// </summary>
public interface IKeyboardNoteMap
{
    /// <summary>The key that writes a note-off wherever the cursor is.</summary>
    string NoteOffKey { get; }

    /// <summary>
    /// The other key that writes one, and only while the cursor is on the note column.
    /// </summary>
    /// <remarks>
    /// The digit row is how the instrument and volume columns are typed, so a 1 that always
    /// meant note-off would be a 1 that could never be typed into a volume. On the note column
    /// there is no such thing as typing a digit, so there it is free to mean what it means in
    /// every other tracker.
    /// </remarks>
    string NoteOffDigit { get; }

    /// <summary>
    /// Caps lock, which is where Renoise puts a note-off and where a hand coming from Renoise
    /// will reach for it. Works from any column, as it does there.
    /// </summary>
    /// <remarks>
    /// It goes on being caps lock as well: pressing it still turns the light on and off and
    /// still shifts what the letter keys type, because that happens in the X server long
    /// before anything here is told about it. Nothing can be done about that from this side,
    /// and Renoise on the same machine behaves the same way.
    /// </remarks>
    string NoteOffCapsLock { get; }

    /// <summary>True for the keys that write a note-off from any column.</summary>
    /// <param name="key">The key, named as Avalonia reports it.</param>
    bool IsNoteOff(string key);

    /// <summary>True for any of them, for use when the cursor is on the note column.</summary>
    /// <param name="key">The key, named as Avalonia reports it.</param>
    bool IsNoteOffInNotes(string key);

    /// <summary>
    /// The note this key plays at the given octave, or null if the key is not part of the
    /// layout. Notes past the top of the range are refused rather than clamped, so holding
    /// a high octave does not pile every key onto B-9.
    /// </summary>
    /// <param name="key">The key, named as Avalonia reports it.</param>
    /// <param name="octave">The octave the lower row's C is standing on.</param>
    Note? NoteFor(string key, int octave);

    /// <summary>
    /// True for a key that is part of the layout at all.
    /// </summary>
    /// <remarks>
    /// Asked separately from <see cref="NoteFor"/> because they answer different questions: this
    /// one says whether a key press belongs to the keyboard, and that one says what it plays.
    /// A key that is on the layout but out of range at this octave is still the keyboard's, and
    /// must not fall through to whatever the letter would otherwise have done.
    /// </remarks>
    /// <param name="key">The key, named as Avalonia reports it.</param>
    bool IsNoteKey(string key);
}
