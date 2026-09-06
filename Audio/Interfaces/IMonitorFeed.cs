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
/// Off unless somebody says so, and not remembered between runs, for the reason
/// <see cref="ViewModels.Interfaces.IInputSource.TakeAside"/> is not: the ordinary source is
/// what an output is playing, and playing that back into the output is a loop. A switch that
/// came back on at the next start would make one before anybody had asked for anything.
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
}
