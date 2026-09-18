using System.Collections.Generic;

namespace JingleBox2.Diagnostics.Records;

/// <summary>
/// How busy the computer is and how much of that is this program, at one moment.
/// </summary>
/// <param name="MachineCpu">How busy all the processors are together, nought to one.</param>
/// <param name="OwnCpu">
/// How much of all the processors this program and every process it started are using, nought
/// to one, out of the same whole as <paramref name="MachineCpu"/>.
/// </param>
/// <param name="Cores">How busy each processor thread is on its own, nought to one.</param>
/// <param name="Memory">What the computer's memory stands at.</param>
/// <param name="OwnMemory">
/// The memory this program and the processes it started are holding, in bytes. Library code two
/// processes share is counted in each of them, so this is a little more than they would give back
/// by closing.
/// </param>
/// <param name="OwnProcesses">How many processes that is, this one included.</param>
/// <param name="Network">What is going in and out over the network, or nothing where it cannot be read.</param>
/// <param name="Disks">What is being read from and written to the discs, or nothing where it cannot be read.</param>
public readonly record struct LoadReading(
    double MachineCpu, double OwnCpu, IReadOnlyList<double> Cores, MemoryFigures Memory, long OwnMemory,
    int OwnProcesses, TrafficFigures? Network = null, TrafficFigures? Disks = null);
