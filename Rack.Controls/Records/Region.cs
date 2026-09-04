namespace JingleBox2.Rack.Controls.Records;

/// <summary>
/// A stretch of a recording, as two fractions of the whole of it.
/// </summary>
/// <remarks>
/// Both ends together, because every rule about one of them is a rule about the other: how far
/// a handle may travel is decided by where its partner is, and a region drawn out from nothing
/// arrives as a pair. Handing them about singly is how one of them comes to be moved without
/// the other being consulted.
/// </remarks>
/// <param name="Start">Where it begins, nought to one.</param>
/// <param name="End">Where it ends, nought to one, and never below the start.</param>
public readonly record struct Region(double Start, double End);
