namespace JingleBox2.Tracker.Enums;

/// <summary>
/// What a note key press asks for, given what is already being held.
/// </summary>
/// <remarks>
/// Three answers rather than a bool, because writing and sounding are separate acts and a
/// repeated key wants one without the other. They were one call for a long time, and that is
/// what let a held key stack up voices.
/// </remarks>
public enum NoteWant
{
    /// <summary>Nothing at all: the press is a hand resting rather than a note being asked for.</summary>
    Nothing,

    /// <summary>Write it into the pattern, but sound nothing: it is already sounding.</summary>
    Write,

    /// <summary>A fresh note: sound it and write it.</summary>
    SoundAndWrite
}
