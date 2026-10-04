namespace JingleBox2.Midi.Enums;

/// <summary>
/// Whose clock the transport runs on.
/// </summary>
/// <remarks>
/// **One setting with three answers, and it has to be chosen rather than inferred.** A machine
/// keeps its own time, follows a clock arriving on a MIDI port, or shares a timeline with an
/// Ableton Link session, and the three are exclusive: a transport cannot be placed by a stopwatch,
/// by arriving ticks and by a shared timeline at once. That is why this is an enum on its own
/// rather than a flag alongside the four jobs a port can be given, which are things any number of
/// ports may do at the same time.
///
/// It lives with the MIDI settings although Link is not MIDI, because it is one question and that
/// is where the question is asked: the clock card on the MIDI page, beside the outputs a clock is
/// sent to.
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
    Followed = 1,

    /// <summary>
    /// A shared timeline with every Ableton Link program on the network.
    /// </summary>
    /// <remarks>
    /// Choosing it is what joins the network. A machine whose build has no Link library offers it
    /// greyed and says why, and a settings file naming it there plays on the transport's own
    /// clock rather than refusing to play, the rule <see cref="Followed"/> keeps for a port that
    /// is not plugged in.
    /// </remarks>
    AbletonLink = 2
}
