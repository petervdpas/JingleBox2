namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// How much time a processor has spent busy and in all, since the computer started.
/// </summary>
/// <remarks>
/// In whatever unit the system counts in, the same one for both, since what is wanted is how the
/// two moved between two readings and never either figure on its own.
/// </remarks>
/// <param name="Busy">The time spent doing something.</param>
/// <param name="Total">All the time there has been, busy or not.</param>
public readonly record struct CoreTime(long Busy, long Total);
