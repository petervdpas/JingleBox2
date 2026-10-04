namespace JingleBox2.Sync.Interfaces;

/// <summary>
/// Where a pass of the transport begins on an Ableton Link timeline, and what quantum it is
/// lined up against.
/// </summary>
/// <remarks>
/// The arithmetic on its own, out of the clock thread, so it can be asked without a network, a
/// clock or a song.
///
/// **Alone, a pass starts at once; with anybody else in the session it waits for the next
/// quantum.** That is Link's own rule for a quantised launch, said by its documentation in as many
/// words: with no other peers a program is free to start whenever it likes, and with peers it
/// waits until the session's phase comes round, so bar one lands with everybody else's bar one.
/// Live behaves the same, which is the behaviour anybody coming from it expects.
/// </remarks>
public interface IAbletonLinkLines
{
    /// <summary>A quantum held to something usable: one beat to sixteen, and a bar of four where it is nonsense.</summary>
    /// <param name="asked">What the settings say.</param>
    double Quantum(double asked);

    /// <summary>The beat a pass begins on.</summary>
    /// <param name="beatNow">The beat at the moment the first line would otherwise sound.</param>
    /// <param name="quantum">How many beats line up between peers.</param>
    /// <param name="peers">How many other programs are in the session.</param>
    /// <returns>
    /// <paramref name="beatNow"/> itself when alone, otherwise the next whole quantum at or after
    /// it. Not a number where the beat it was handed is not one, so the caller can fall back to
    /// its own clock rather than wait on nothing.
    /// </returns>
    double StartBeat(double beatNow, double quantum, int peers);
}
