namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// What the computer is, which does not change while the program runs.
/// </summary>
/// <param name="Processor">The processor's own name, or nothing where the system would not say.</param>
/// <param name="Threads">How many processor threads there are, which is the whole the load is out of.</param>
/// <param name="Memory">All the memory there is, in bytes, and nought where the system would not say.</param>
/// <param name="System">The operating system and its version, in the words it uses itself.</param>
public readonly record struct MachineFacts(string? Processor, int Threads, long Memory, string System);
