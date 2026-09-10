using System.Collections.Generic;

namespace JingleBox2.Midi.Interfaces;

/// <summary>
/// What this machine puts on the wire when it is the clock other gear runs on.
/// </summary>
/// <remarks>
/// **The sending and none of the arithmetic**, which is <see cref="IMidiClockGrid"/>'s. What is
/// here is which bytes, to which ports, in which order, and the order is most of it.
///
/// Four messages and a pointer. Clock is <c>0xF8</c>, twenty four to the quarter note and by far
/// the commonest thing this sends. Start is <c>0xFA</c>, which means from the beginning; continue
/// is <c>0xFB</c>, which means from wherever the pointer last said; stop is <c>0xFC</c>. The
/// pointer is <c>0xF2</c> with the position in two seven-bit halves.
///
/// **A start from anywhere but the top has to be a pointer and a continue, not a start**, and
/// getting that wrong is the difference between a usable master and a demonstration: told plain
/// start, every device on the desk plays from its own bar one while this plays from line 32. So
/// <see cref="Play"/> decides between them by where the transport actually begins.
///
/// **Nothing is sent to a port that has not been opened first**, and the reason is measured
/// rather than assumed: opening a MIDI output costs 80 ms on a real port and 197 on a software
/// one, and this is called from the thread that keeps time, where a tick is 20.8 ms at 120 to
/// the minute. See <see cref="Drive"/>.
///
/// **Empty is the ordinary state.** A fresh installation drives nothing, because clock arriving
/// at a device nobody pointed it at is a device that starts running when its owner did not ask.
/// </remarks>
public interface IMidiClockDeck
{
    /// <summary>True while there is at least one port to send to.</summary>
    /// <remarks>
    /// What the clock thread asks before working anything out, so a machine driving nothing pays
    /// one comparison per line rather than a tick schedule it will throw away.
    /// </remarks>
    bool IsDriving { get; }

    /// <summary>
    /// Says which outputs are driven from now on, opening each one.
    /// </summary>
    /// <remarks>
    /// Called when the choice is made rather than when the first tick is due, which is the whole
    /// point: the open is the expensive part and it must not land on the clock. A port that will
    /// not open is dropped and said in the log rather than kept and retried per tick, since a
    /// device that is not there will not be there in a moment either.
    ///
    /// Replaces whatever was being driven. Handed nothing, it drives nothing, which is how the
    /// setting is turned off.
    /// </remarks>
    /// <param name="outputs">The outputs to drive, by name.</param>
    void Drive(IReadOnlyList<string>? outputs);

    /// <summary>
    /// The transport has started at that line: says where, then says go.
    /// </summary>
    /// <remarks>
    /// From the top it is a plain start. From anywhere else it is a pointer followed by a
    /// continue, in that order, since a continue means "from where I last told you" and the
    /// pointer is what tells it.
    /// </remarks>
    /// <param name="line">Which line the transport is beginning on.</param>
    /// <param name="linesPerBeat">How many lines the song puts in a beat.</param>
    void Play(int line, int linesPerBeat);

    /// <summary>Sends that many clock ticks.</summary>
    /// <remarks>
    /// More than one at a time is the ordinary case when a thread has overslept, and they go out
    /// back to back rather than being spread: the receiving end counts ticks and does not measure
    /// the gaps between them, so catching up is right and pacing the catch-up would only make the
    /// tempo read low for longer.
    /// </remarks>
    /// <param name="howMany">How many ticks are due. Nought and below send nothing.</param>
    void Ticks(int howMany);

    /// <summary>The transport has stopped.</summary>
    void Halt();

    /// <summary>
    /// Sends a plain start, exactly as it arrived from the clock being followed.
    /// </summary>
    /// <remarks>
    /// **This and the two members below it are the pass-through half, and they exist because
    /// <see cref="Play"/> may not be used for it.** <see cref="Play"/> decides between a start and
    /// a pointer-and-continue from where the transport is beginning, which is right when this
    /// machine is the one deciding and wrong when another machine has already decided: told a
    /// line, it would work a pointer out again from a line that was itself worked out from a
    /// pointer, and <see cref="IMidiClockGrid.PointerFor"/> and
    /// <see cref="IMidiClockGrid.LineAtPointer"/> are not each other's inverse at every setting.
    /// At six lines to the beat, sixteenth 5 reads back as line 7 and line 7 reads out as
    /// sixteenth 4, so a chain of three machines would lose a sixteenth on every relocation for
    /// no reason but the arithmetic in the middle.
    ///
    /// So a message that arrived is put out again unchanged and nothing is recomputed. What
    /// travels is the byte, which is the whole meaning of passing a clock on.
    ///
    /// <see cref="Ticks"/> and <see cref="Halt"/> serve both halves as they are: a tick carries
    /// nothing that could be recomputed and a stop carries nothing either.
    /// </remarks>
    void Begin();

    /// <summary>Sends a plain continue, exactly as it arrived.</summary>
    /// <inheritdoc cref="Begin"/>
    void Resume();

    /// <summary>
    /// Sends a song position pointer holding exactly the position that arrived.
    /// </summary>
    /// <inheritdoc cref="Begin"/>
    /// <param name="at">The position in sixteenth notes, as the message carried it.</param>
    void Place(int at);
}
