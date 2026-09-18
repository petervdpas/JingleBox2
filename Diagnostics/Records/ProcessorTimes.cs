using System.Collections.Generic;

namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// The processors' time at one moment: all of them together, and each on its own.
/// </summary>
/// <param name="Whole">All the processors together.</param>
/// <param name="Cores">Each processor thread, in the order the system numbers them.</param>
public readonly record struct ProcessorTimes(CoreTime Whole, IReadOnlyList<CoreTime> Cores);
