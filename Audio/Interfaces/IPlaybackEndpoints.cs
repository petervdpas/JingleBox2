using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>Where this machine can play audio out, by the system's own names for it.</summary>
/// <remarks>
/// **For pointing another program somewhere, not for choosing our own output.** Ours goes
/// through BASS and is a number in that library's list; this is the system's endpoint list, and
/// the only reason it exists is that telling Windows where a program should play takes the
/// system's own address for the place.
///
/// Empty is an ordinary answer and means the machine cannot be asked, which is every machine
/// that is not Windows.
/// </remarks>
public interface IPlaybackEndpoints
{
    /// <summary>Every output the system has, ready to be played out of.</summary>
    IReadOnlyList<AudioEndpoint> Outputs();
}
