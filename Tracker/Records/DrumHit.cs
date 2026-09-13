using JingleBox2.Tracker.Enums;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// One hit found in a recording: where it is, what it sounds like, and what it was judged by.
/// </summary>
/// <remarks>
/// Where it is, as fractions of the whole recording, because that is how a pad's window is kept:
/// a pad plays the one recording between two fractions, so a hit becomes a pad without anything
/// being cut out of the file.
/// </remarks>
/// <param name="Start">Where the hit begins, as a fraction of the recording.</param>
/// <param name="End">Where it has died away or the next hit begins, whichever is sooner.</param>
/// <param name="Sound">What it was judged to be.</param>
/// <param name="Low">How much of its energy is under a hundred and fifty cycles, nought to one.</param>
/// <param name="Middle">How much is between that and four thousand.</param>
/// <param name="High">How much is above four thousand.</param>
/// <param name="DecaySeconds">How long it takes to fall twenty decibels from its loudest.</param>
/// <param name="Noise">How noisy it is, as crossings of nought a second over its first moments.</param>
/// <param name="Peak">How loud it is at its loudest, nought to one.</param>
/// <param name="Clear">How long it rang before the next hit arrived, in seconds, which is how little else is on top of it.</param>
public sealed record DrumHit(
    double Start,
    double End,
    DrumSound Sound,
    double Low,
    double Middle,
    double High,
    double DecaySeconds,
    double Noise,
    double Peak,
    double Clear);
