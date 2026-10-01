using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Commands.Records;

/// <summary>One event and the tick of its line it happens on.</summary>
/// <param name="Tick">Counting from nought, which is the moment the line begins.</param>
/// <param name="Event">What happens.</param>
public readonly record struct TickEvent(int Tick, TrackerEvent Event);
