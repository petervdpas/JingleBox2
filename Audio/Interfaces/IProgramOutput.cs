namespace JingleBox2.Audio.Interfaces;

/// <summary>Where one program on this machine plays out, said by this application.</summary>
/// <remarks>
/// **This is how a source is taken aside where there is no graph.** On PipeWire a source is
/// simply unplugged from the speakers; Windows has no such move, and what it has instead is a
/// per-program output, the one its own Volume mixer sets. Pointing a program at an output nobody
/// is listening to is the same thing said in Windows' words.
///
/// What it changes outlives this process, which is why the word in the system's own call is
/// persisted: a program left pointed at a silent output after this application has closed is a
/// program somebody will think is broken. Putting it back is <see cref="Release"/> and it is not
/// optional.
/// </remarks>
public interface IProgramOutput
{
    /// <summary>Whether this machine can be told where a program plays.</summary>
    bool CanPoint { get; }

    /// <summary>
    /// Sends one program's audio to one output.
    /// </summary>
    /// <remarks>
    /// The program tree is not followed here, unlike the capture: the system applies this per
    /// process, and a browser's tab processes inherit it because they are started afterwards by
    /// the one that was pointed. What is captured and what is pointed can therefore differ for a
    /// moment after a program starts a new process, which is the system's own behaviour.
    /// </remarks>
    /// <param name="processId">Which program.</param>
    /// <param name="endpoint">The output, by the system's own id for it.</param>
    /// <returns>False where the machine cannot, or the call was refused.</returns>
    bool Point(int processId, string endpoint);

    /// <summary>Gives a program its own choice of output back.</summary>
    /// <remarks>
    /// Which is what an empty endpoint means to the system: not silence, but no preference of
    /// ours, so the program follows the machine's default again.
    /// </remarks>
    /// <param name="processId">Which program.</param>
    bool Release(int processId);
}
