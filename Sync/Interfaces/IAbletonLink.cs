using System;

namespace JingleBox2.Sync.Interfaces;

/// <summary>
/// This application as a peer in an Ableton Link session: a tempo, a beat timeline and a start and
/// stop shared with every other Link program on the network.
/// </summary>
/// <remarks>
/// **Link shares a timeline and not ticks**, and that is the whole of how it is used. Each peer
/// keeps a mapping between its own clock and beats, and the peers agree on it over the network.
/// So the transport does not wait for anything to arrive: it asks which moment a beat falls on
/// and schedules its lines against that, earlier by however long its own output takes to reach
/// the speaker. Network jitter only changes how quickly the peers agree, never when a beat
/// sounds, which is why it stays tight over wifi where MIDI clock does not.
///
/// It carries a tempo, a beat and phase against a quantum, and start and stop. No notes, no
/// controllers, no audio, and **no song position**: two programs can be made to start a bar
/// together, never at the same place in two songs.
///
/// **Off until somebody chooses it**, and choosing it is the moment the network is touched: a
/// socket is opened and a multicast group joined, which on Windows is the firewall's question the
/// first time. A machine where it was never chosen never touches the network; one where it was
/// joins again when the application starts, the way a followed MIDI port is opened again.
///
/// **The library is optional.** It is built by <c>native/abletonlink/build.sh</c> and carried
/// beside BASS, and a build or a machine without it loses Link and nothing else:
/// <see cref="Present"/> says so before anything is called, and <see cref="Missing"/> says why.
///
/// **Threads.** <see cref="Now"/>, <see cref="BeatAt"/> and <see cref="TimeAt"/> are asked by the
/// tracker's clock thread every line; the settings page and the transport call the rest from the
/// drawing thread. None of it may be called from the thread that fills the audio buffer, since
/// every read goes through Link's application side, which takes a lock. The three events are
/// raised on Link's own thread, and whoever listens gets itself to wherever it needs to be.
/// </remarks>
public interface IAbletonLink : IDisposable
{
    /// <summary>Whether the Link library could be loaded on this machine at all.</summary>
    /// <remarks>Asked once and remembered, since the answer cannot change while the program runs.</remarks>
    bool Present { get; }

    /// <summary>Why Link cannot be had here, in a sentence for the settings page, or null where it can.</summary>
    string? Missing { get; }

    /// <summary>Whether this is a peer on the network now.</summary>
    bool IsOn { get; }

    /// <summary>Joins the session or leaves it.</summary>
    /// <remarks>
    /// Leaving keeps the timeline, so joining again later goes on from where it was. A machine
    /// without the library answers by staying off, and never throws.
    /// </remarks>
    /// <param name="on">True to join.</param>
    void Use(bool on);

    /// <summary>Whether pressing play and stop here starts and stops the other peers, and theirs this.</summary>
    /// <remarks>
    /// Link's own option, and opt in per peer: a peer that has not turned it on neither hears nor
    /// sends start and stop, whatever this one says.
    /// </remarks>
    bool SharesStartStop { get; set; }

    /// <summary>
    /// How much later than the output's own figure the sound really leaves, in milliseconds.
    /// </summary>
    /// <remarks>
    /// The output knows its buffer and its cushion and nothing past them: a sound card's own
    /// converters, a Bluetooth speaker, a long cable to a desk. This is the hand adjustment for
    /// that, the same offset Live offers, and it may be negative.
    /// </remarks>
    int OffsetMilliseconds { get; set; }

    /// <summary>How many beats have to line up between peers, which is usually a bar of four.</summary>
    /// <remarks>
    /// A pass started while others are in the session waits for the next one of these, so bar one
    /// lands together. Link does not share it: every peer says its own, and the phase lines up
    /// against whichever each one asked for.
    /// </remarks>
    double Quantum { get; set; }

    /// <summary>How many other programs are in the session.</summary>
    int Peers { get; }

    /// <summary>The session's tempo, in beats a minute.</summary>
    /// <remarks>
    /// Kept up to date by Link's own tempo callback and read without going to Link, since the
    /// clock thread asks it every time it waits.
    /// </remarks>
    double Tempo { get; }

    /// <summary>Link's clock, in microseconds.</summary>
    /// <remarks>
    /// The only clock the timeline may be read against. It is not the stopwatch: on Linux the two
    /// are different system clocks that drift apart by however much the time service is slewing,
    /// and two clocks that nearly agree is the fault a sync exists to prevent.
    /// </remarks>
    long Now { get; }

    /// <summary>The beat at a moment on <see cref="Now"/>'s clock, against a quantum.</summary>
    /// <param name="micros">The moment.</param>
    /// <param name="quantum">How many beats have to line up between peers, usually a bar.</param>
    double BeatAt(long micros, double quantum);

    /// <summary>The moment a beat falls on, against a quantum.</summary>
    /// <param name="beat">The beat.</param>
    /// <param name="quantum">How many beats have to line up between peers.</param>
    long TimeAt(double beat, double quantum);

    /// <summary>Whether the session says the transport is playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Puts a tempo to the session, which every peer then follows.</summary>
    /// <remarks>
    /// Does nothing where the session is already at it, so a tempo that arrived from a peer and
    /// was written into the song is not sent straight back as a change of its own.
    /// </remarks>
    /// <param name="bpm">The tempo.</param>
    void Propose(double bpm);

    /// <summary>Tells the session the transport here started or stopped.</summary>
    /// <remarks>
    /// Does nothing unless <see cref="SharesStartStop"/>, and nothing where the session already
    /// says so, which is what keeps a start that came from a peer from going round again.
    /// </remarks>
    /// <param name="playing">True for started.</param>
    void Play(bool playing);

    /// <summary>Raised when the number of other programs in the session moves.</summary>
    event Action<int>? PeersMoved;

    /// <summary>
    /// Raised when another peer moves the tempo. Not raised for a tempo this one proposed.
    /// </summary>
    event Action<double>? TempoHeard;

    /// <summary>Raised when another peer starts or stops the session, while start and stop are shared.</summary>
    event Action<bool>? PlayingHeard;
}
