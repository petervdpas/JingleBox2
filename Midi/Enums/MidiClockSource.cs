namespace JingleBox2.Midi.Enums;

/// <summary>
/// Whose clock the transport runs on.
/// </summary>
/// <remarks>
/// **One setting with two answers, and it has to be chosen rather than inferred.** A machine
/// either keeps its own time or follows somebody else's, there is no third answer, and the two
/// are exclusive: a transport cannot be driven by a stopwatch and by arriving ticks at once.
/// That is why this is an enum on its own rather than a flag alongside the four jobs a port can
/// be given, which are things any number of ports may do at the same time.
///
/// **It is deliberately not the same question as which outputs get clock sent to them.** Those
/// are independent: a machine on its own clock may drive two devices, and one following an
/// external clock may pass it on. Running the two together into a single master-or-slave switch
/// was the first shape this took and it was wrong, because it would have made sending impossible
/// while following, which is a thing people do.
///
/// The numbers are written down because they are stored, so a settings file outlives any
/// reordering here.
/// </remarks>
public enum MidiClockSource
{
    /// <summary>
    /// Its own, which is what every song has played on until now.
    /// </summary>
    /// <remarks>
    /// Nought, so a settings file that has never heard of this reads back as what it was doing:
    /// the tracker's own stopwatch, answering to nothing outside.
    /// </remarks>
    Own = 0,

    /// <summary>
    /// Somebody else's, arriving as clock on one named port.
    /// </summary>
    /// <remarks>
    /// One port and not several, since a transport following two clocks is following neither.
    /// Which port is a setting of its own beside this one, and a source of Followed with no port
    /// named, or a port that is not plugged in, leaves the transport on its own clock rather than
    /// refusing to play: a cable left in the other room is not a decision to stop working.
    /// </remarks>
    Followed = 1
}
