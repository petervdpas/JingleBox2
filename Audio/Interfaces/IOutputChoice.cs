using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// Which of the outputs on this machine is the one that was chosen last time.
/// </summary>
/// <remarks>
/// **A device's number is a place in a list, and the list moves.** The sound library enumerates
/// what the machine has when it is asked, so an interface plugged in or taken away shifts
/// everything after it, and so does a conversion plugin that opens today and refused yesterday.
/// The number that was written down still names a row; it stops naming the same device.
///
/// It really happens and it is quiet when it does. Read out of one evening's log: at 19:47 the
/// twelfth row was the sound server and the thirteenth was the compatibility layer over it; an
/// hour later the twelfth row was the compatibility layer, and the setting that had said twelve
/// all along was opening something else. Nothing anywhere says so, because from inside, opening
/// device twelve is exactly what it was told to do.
///
/// So the name is written down beside the number and asked first. It is the same rule a song
/// already keeps about a plugin, and for the same reason: what travels between one run and the
/// next is what a thing is called, not where it happened to sit.
///
/// **The number is still kept and still tried**, since it is what every settings file written
/// before this has, and on a machine that has not changed it is right. It is the second question
/// rather than the first.
/// </remarks>
public interface IOutputChoice
{
    /// <summary>
    /// Finds the chosen output among the ones this machine is offering.
    /// </summary>
    /// <remarks>
    /// The name first, without regard to case or the room around it, since that is what survives
    /// a device being unplugged. Then the number, which is what a settings file written before
    /// the name existed carries. Nothing where neither matches, which is a device that is not on
    /// this machine at all: the caller then falls back on whatever the machine does offer, and
    /// says nothing, since a device somebody chose on another day being absent is ordinary.
    /// </remarks>
    /// <param name="offered">What this machine is offering now.</param>
    /// <param name="name">What the chosen one was called, or nothing.</param>
    /// <param name="id">The number it had, or minus one where none was stored.</param>
    AudioOutput? Among(IReadOnlyList<AudioOutput>? offered, string? name, int id);
}
