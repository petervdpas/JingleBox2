using System.Collections.Generic;
using JingleBox2.Tracker.Commands.Records;

namespace JingleBox2.Tracker.Commands.Interfaces;

/// <summary>Something a command started that goes on past the end of its line, such as a long glide.</summary>
/// <remarks>
/// Asked once a line, before the line's own cells, for as long as it says it is still going. A
/// line that writes anything into the same note column takes over from it: what was carried
/// stops where it had got to and the new cell is played from there.
/// </remarks>
public interface ICarry
{
    /// <summary>Writes this line's share, and says whether there is more after it.</summary>
    /// <param name="voice">The column it is going on in.</param>
    /// <param name="ticks">How many ticks a line has.</param>
    /// <param name="into">Where the events go, each with its tick.</param>
    /// <returns>True while it still has lines to go.</returns>
    bool Go(VoiceState voice, int ticks, ICollection<TickEvent> into);
}
