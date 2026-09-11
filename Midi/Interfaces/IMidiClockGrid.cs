namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// How a tracker line and a MIDI clock tick are related, in both directions.
/// </summary>
/// <remarks>
/// **The whole of the arithmetic and none of the sending or receiving**, so every decision here
/// can be put a question to without a port, a thread, or a device on the other end. What crosses
/// a socket belongs to the deck; when it crosses, and which line it means, is this.
///
/// MIDI clock is twenty four ticks to the quarter note. That is not a number anybody chose
/// recently: it is in the specification, every sequencer ever built assumes it, and it is what
/// makes a tick 20.833 ms at 120 to the minute. A tracker line is a different unit again, set
/// per song by <c>LinesPerBeat</c>, so the two have to be related rather than counted off
/// against each other.
///
/// **Both directions are here because the machine can be at either end**, and the relationship
/// is one fact whichever way it is being read. Driving other gear asks which ticks are due by
/// now; following other gear asks which line a tick count has reached. Two spellings of one
/// piece of arithmetic would eventually disagree, and the way that fails is a song that plays in
/// time when it is the master and drifts when it is the slave, or the other way about, with
/// nothing to say why.
///
/// **Nothing here counts ticks per line, and that is the point.** At four lines to the beat a
/// line is six ticks and at eight it is three, both whole; at five it is 4.8 and at seven it is
/// 3.43, neither. Anything that assumed a whole number would drift by a fraction of a tick per
/// line and be a semiquaver out by the end of a chorus. So a line's position is worked out from
/// its own number every time, which is exact at every setting.
/// </remarks>
public interface IMidiClockGrid
{
    /// <summary>How many ticks there are to a quarter note, which is twenty four everywhere.</summary>
    int PerBeat { get; }

    /// <summary>
    /// How long one tick lasts, in seconds, at that tempo.
    /// </summary>
    /// <remarks>
    /// The tempo alone decides it. A tracker line is <c>60 / (bpm * linesPerBeat)</c> and a tick
    /// is <c>60 / (bpm * 24)</c>: how a song divides a beat into lines is its own business and no
    /// slave hears about it, which is why these two are worked out apart rather than one from
    /// the other.
    /// </remarks>
    /// <param name="bpm">Quarter notes a minute.</param>
    double TickSeconds(double bpm);

    /// <summary>
    /// How many ticks are due by that moment, counting from the top of a pass.
    /// </summary>
    /// <remarks>
    /// What the master end asks. A count rather than a list, since the caller sends one byte per
    /// tick and does not care which they were; late by more than one answers more than one,
    /// which is what lets a caller that overslept catch up rather than stay behind for the rest
    /// of the pass.
    /// </remarks>
    /// <param name="elapsedSeconds">How long the pass has been running.</param>
    /// <param name="tickSeconds">How long one tick lasts at the tempo now.</param>
    long DueBy(double elapsedSeconds, double tickSeconds);

    /// <summary>
    /// Which tick a line begins on, counting from the top of a pass.
    /// </summary>
    /// <remarks>
    /// Fractional on purpose, since at five or seven lines to the beat a line does not begin on
    /// a tick at all. A follower compares an arrived count against this and steps when it has
    /// been reached, which lands each line on the nearest tick without the error accumulating.
    /// </remarks>
    /// <param name="line">Which line, counting from nought.</param>
    /// <param name="linesPerBeat">How many lines the song puts in a beat.</param>
    double TickOfLine(int line, int linesPerBeat);

    /// <summary>
    /// Which line a tick count has reached.
    /// </summary>
    /// <remarks>
    /// What the slave end asks, and the inverse of <see cref="TickOfLine"/> rather than a second
    /// idea about the same thing. Used to work out where a pass should be after a position
    /// pointer has moved it, and to check a follower has not fallen a line behind.
    /// </remarks>
    /// <param name="ticks">How many ticks have arrived.</param>
    /// <param name="linesPerBeat">How many lines the song puts in a beat.</param>
    int LineAtTick(long ticks, int linesPerBeat);

    /// <summary>
    /// Where a song position pointer says to start, in sixteenth notes from the top.
    /// </summary>
    /// <remarks>
    /// **What makes a master usable rather than a demonstration.** A slave told only to start
    /// plays from its own beginning, so starting a song halfway down leaves every device on the
    /// desk a chorus out. The pointer is the standard's answer and its unit is the sixteenth
    /// note, which is six ticks: not the tracker's line, and not the beat.
    ///
    /// Held to fourteen bits, since the message carries it as two seven-bit halves, and a song
    /// long enough to overflow that would otherwise wrap round to its own beginning.
    /// </remarks>
    /// <param name="line">Which line the transport is starting from.</param>
    /// <param name="linesPerBeat">How many lines the song puts in a beat.</param>
    int PointerFor(int line, int linesPerBeat);

    /// <summary>
    /// And the line a pointer arriving from outside means.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="PointerFor"/>, for a master that starts us somewhere other
    /// than the top. It cannot always be exact: a sixteenth is six ticks and a line at five to
    /// the beat is 4.8, so a pointer can land between two lines. The line it lands in is the
    /// answer, which is the earlier of the two rather than the nearer, and deliberately: this
    /// places a playhead, and a playhead put past where the master actually is has skipped a
    /// line of music that was about to play.
    /// </remarks>
    /// <param name="pointer">Sixteenth notes from the top of the song.</param>
    /// <param name="linesPerBeat">How many lines the song puts in a beat.</param>
    int LineAtPointer(int pointer, int linesPerBeat);
}
