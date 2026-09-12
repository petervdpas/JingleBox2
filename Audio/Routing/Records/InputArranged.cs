using JingleBox2.Audio.Routing.Enums;

namespace JingleBox2.Audio.Routing.Records;

/// <summary>
/// What came of making the machine match what the input is set to.
/// </summary>
/// <remarks>
/// Two answers rather than one, because a source is wired up in two moves and either can fail on
/// its own: the capture is pointed at it, and it is taken off its own output. A source that
/// arrives here but is still playing out of its own speakers is heard twice, a buffer apart,
/// and a source taken aside that never reached the capture is silence. The line somebody reads
/// needs both.
/// </remarks>
/// <param name="Aside">What came of taking it off its own output.</param>
/// <param name="Connected">Whether the capture is being fed by it.</param>
public readonly record struct InputArranged(InputAside Aside, bool Connected);
