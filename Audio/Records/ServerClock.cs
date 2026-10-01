namespace JingleBox2.Audio.Records;

/// <summary>
/// The sound server's own cycle: the rate it runs at, and how many frames it moves at a time.
/// </summary>
/// <remarks>
/// A buffer shorter than two of these runs dry, since the server takes a whole cycle at once and
/// one cycle has to be ready while the other plays. PipeWire calls the frames a quantum; Windows
/// calls the same thing a device period.
/// </remarks>
/// <param name="Rate">The rate the server runs at, in Hz.</param>
/// <param name="Quantum">How many frames it takes at a time, at its own rate.</param>
public readonly record struct ServerClock(int Rate, int Quantum);
