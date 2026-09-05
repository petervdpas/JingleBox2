namespace JingleBox2.Rack.SoundDevices.Interfaces;

/// <summary>
/// How much room under full scale one note of a sound device has to leave, and whether a peak
/// that was measured leaves it.
/// </summary>
/// <remarks>
/// A device that reaches full scale on one note is a device nobody can play a chord on. The
/// second note is already past the end, and every fader, every other track and the master are
/// then working on a signal that has nowhere left to go, which is heard as the whole mix
/// distorting whenever more than one thing plays.
///
/// There is no published standard for how loud a preset should be. There are two for the signal
/// around it, and they agree with each other. EBU R 68 and SMPTE RP 155 put alignment level,
/// which is where a single signal is expected to sit, at -18 dBFS and -20 dBFS: a lone tone at
/// that level is what a correctly lined up desk reads as nominal. EBU R 128 puts a finished
/// programme at -23 LUFS with a true peak ceiling of -1 dBTP. Neither is about presets, and both
/// say the same thing about them, which is that one signal is not supposed to be anywhere near
/// the top.
///
/// <see cref="Least"/> is twelve decibels, and that number is arithmetic rather than taste. Four
/// notes of equal level sum to twelve decibels above one when they line up, so a four note chord
/// at unity still arrives under full scale; eight tracks of unrelated material sum to about nine.
/// It also lands between the two alignment levels above and the ceiling, which is where a device
/// that has to be audible beside somebody's own recordings can honestly sit.
///
/// It is a guideline and not a gate. Nothing here refuses a device for being loud: what this
/// answers is a reading to put in front of whoever is choosing the number, at the moment they
/// are choosing it. A machine deliberately built to be slammed is allowed to say so, and the
/// person who built it should have to mean it.
/// </remarks>
public interface IHeadroom
{
    /// <summary>The least room, in decibels, one note should leave under full scale.</summary>
    double Least { get; }

    /// <summary>
    /// The room a measured peak leaves under full scale, in decibels.
    /// </summary>
    /// <remarks>
    /// Nought is full scale exactly and a peak past it reads negative, so the number is read the
    /// way a meter is read: bigger is quieter, and below nought is over.
    ///
    /// Silence has all the room there is and answers a large finite number rather than infinity,
    /// since this reading is shown to somebody and a screen has no good way to draw that.
    /// </remarks>
    /// <param name="peak">The loudest sample of one note, as an amplitude where one is full scale.</param>
    double Room(double peak);

    /// <summary>Whether a measured peak leaves less room than <see cref="Least"/>.</summary>
    /// <param name="peak">The loudest sample of one note, as an amplitude where one is full scale.</param>
    bool Cramped(double peak);
}
