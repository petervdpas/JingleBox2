namespace JingleBox2.Audio.Records;

/// <summary>One program on this machine that is playing something.</summary>
/// <remarks>
/// A program appears here only while it holds an audio session, which is the same rule the
/// PipeWire side keeps: a browser with nothing playing is not in the list, and it turns up the
/// moment it makes a sound.
/// </remarks>
/// <param name="ProcessId">
/// What the system calls it, and the only thing a capture can be pointed at. It is not stable
/// across a restart of that program, which is why a route naming one is re-read rather than
/// remembered.
/// </param>
/// <param name="Name">What to call it on the page, which is the program's own name.</param>
public readonly record struct AudioProgram(int ProcessId, string Name);
