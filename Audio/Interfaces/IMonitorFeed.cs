namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What is coming in on the input, heard through the desk while it comes in.
/// </summary>
/// <remarks>
/// **This is the input channel of a mixing desk and nothing more exotic.** What arrives from the
/// capture is put on a bus of its own, that bus is a source on the output like the pads and the
/// take preview already are, and the recording chain hangs on it as an insert. So a microphone
/// through a pitch effect is heard as the pitched thing, which is what an insert on a desk's
/// input channel has always meant.
///
/// **The capture thread only ever copies.** It hands the block over and returns; the chain runs
/// where the bus is pulled, which is the same thread a pad's chain already runs on. That is
/// deliberate and it is the whole reason this is a push stream rather than an effect on the
/// capture callback: a plugin crossing costs a fixed amount per block, and paying it on the
/// thread that fills a take is paying it where a late block is a hole in the only copy of a
/// performance. The take is written from the captured bytes and nothing on this path can reach
/// them.
///
/// **What it cannot do is be quick.** What is heard is a capture buffer plus an output buffer
/// late, which on an ordinary machine is tens of milliseconds. A desk avoids that by not going
/// near a computer, and the only thing that moves it here is the buffer sizes in SETTINGS.
///
/// Off unless somebody says so, and not remembered between runs: the ordinary source is what an
/// output is playing, and playing that back into the output is a loop. A switch that came back on
/// at the next start would make one before anybody had asked for anything.
/// </remarks>
public interface IMonitorFeed
{
    /// <summary>Whether there is a stream to push to.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// The chain what is coming in is heard through, or nothing to hear it as it arrives.
    /// </summary>
    /// <remarks>
    /// The same chain a take is written through, so what is heard while somebody sets a level is
    /// what the take will hold. Read on the audio thread and written by whoever edits the chain,
    /// which is one reference either way and needs no lock.
    /// </remarks>
    Plugins.Interfaces.IAudioInsert? Insert { get; set; }

    /// <summary>
    /// Opens the path at whatever the capture is running at.
    /// </summary>
    /// <remarks>
    /// Whatever was open is closed first, since one capture is one path. The stream is made
    /// stereo whatever arrived, because that is what the effect on it and the bus under it both
    /// deal in, and at the capture's own rate, since the bus resamples and guessing here would
    /// be a monitor playing sharp.
    /// </remarks>
    /// <param name="rate">The capture's rate.</param>
    /// <param name="channels">How wide the capture is.</param>
    /// <returns>False where the stream would not open, which is said in the log.</returns>
    bool Open(int rate, int channels);

    /// <summary>
    /// Hands a captured block over to be heard.
    /// </summary>
    /// <remarks>
    /// Does nothing where the path is not open, so the capture callback can call it without
    /// asking first. What is already waiting is bounded: a bus source that is paused, which is
    /// what somebody else's solo does to it, is not pulled at all, and a queue nobody empties
    /// would grow for as long as the input is open.
    /// </remarks>
    /// <param name="data">The captured bytes, 16 bit, as they went into the take.</param>
    /// <param name="bytes">How many of them are real.</param>
    void Push(byte[] data, int bytes);

    /// <summary>Takes the path down, and does nothing twice.</summary>
    void Close();

    /// <summary>
    /// Whether what is on the recorder's bus is heard on the master.
    /// </summary>
    /// <remarks>
    /// **The bus runs whether or not anybody is listening, and this decides whether it is heard.**
    /// It was the path being opened and closed that did both jobs, which was enough while the
    /// capture was the only thing on that bus. It is not any more: a source patched across from
    /// the desk lives there too, and a channel nobody pulls is a channel that stops, so the bus
    /// has to keep running with the switch off.
    ///
    /// Silence rather than a pause, deliberately. What is on the bus goes on being rendered; what
    /// stops is anybody hearing it, which is what Hear it says.
    /// </remarks>
    bool Heard { get; set; }

    /// <summary>
    /// Said the moment what is being listened to has started to ring.
    /// </summary>
    /// <remarks>
    /// **Acoustic feedback, which is the one loop nothing about the wiring can see**: the signal
    /// leaves through a speaker and comes back through a microphone, and the only evidence is in
    /// the audio. Watched here rather than anywhere else because this is the only path in the
    /// application where that can happen: a delay at the top of its feedback knob is somebody's
    /// sound and must never be caught by this.
    ///
    /// Raised on the thread the capture arrives on, so whoever answers it is expected to get
    /// itself somewhere else before touching anything that draws.
    ///
    /// Said once and then latched. Setting <see cref="Heard"/> or <see cref="HearsTheRoom"/> true
    /// is what arms it again, both of them being the deliberate act of asking to listen after
    /// having been told why it stopped.
    /// </remarks>
    event System.Action? Rang;

    /// <summary>
    /// Whether what is being listened to could pick a room up, which is the only thing that rings.
    /// </summary>
    /// <remarks>
    /// **The gate that makes the ring detector possible rather than merely careful.** Acoustic
    /// feedback needs a microphone: something has to hear the speakers. A capture device might;
    /// what an output is playing and what a program is playing cannot, because neither has ever
    /// been near the air in the room. So on those two the question is not asked at all rather
    /// than asked and answered no.
    ///
    /// **Structure beats a threshold, and this was arrived at the expensive way.** Every attempt
    /// to separate a ring from music by its shape alone was a number that had to be tuned, and
    /// each one that was tuned was wrong on somebody's material: a low swell off a second card
    /// read as a lone tone five times in twenty seconds. None of that judgement was needed, since
    /// nothing that source carries can ever come back through the air.
    ///
    /// False unless something says otherwise, so a path nobody has described is quiet rather than
    /// opinionated.
    /// </remarks>
    bool HearsTheRoom { get; set; }
}
