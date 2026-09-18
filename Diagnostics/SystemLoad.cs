using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using JingleBox2.Diagnostics.Interfaces;
using JingleBox2.Diagnostics.Records;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
/// <remarks>
/// The whole computer comes from <see cref="ISystemCounters"/> and each process of the family from
/// .NET, which answers that the same way everywhere. The family's share of the processors is its
/// processor time over the wall clock times the number of processors, which is the same whole the
/// computer's own figure is out of.
///
/// The family is found again every few readings rather than every one, and in between the same
/// processes are asked again: a plugin is started now and then, and one that ended in the meantime
/// is simply not there to answer.
///
/// One thread at a time: the view model reading it once a second is the only caller.
/// </remarks>
/// <param name="counters">The system's own figures, or nothing on a system that has none.</param>
/// <param name="root">The process whose family is this program, which is this process.</param>
public sealed class SystemLoad(ISystemCounters? counters, int root) : ISystemLoad
{
    /// <summary>The one for the system this is running on.</summary>
    public static SystemLoad ForThisMachine() => new(CountersForThisMachine(), Environment.ProcessId);

    /// <summary>The system's own figures for the system this is running on, or nothing on one that has none.</summary>
    public static ISystemCounters? CountersForThisMachine() =>
        OperatingSystem.IsLinux() ? new LinuxCounters()
        : OperatingSystem.IsWindows() ? new WindowsCounters()
        : null;

    /// <summary>The processors' time at the last reading, and nothing before one.</summary>
    private ProcessorTimes? _before;

    /// <summary>When the last reading was, on the stopwatch, and nought before one.</summary>
    private long _at;

    /// <summary>Each process's processor time at the last reading.</summary>
    private Dictionary<int, TimeSpan> _spent = new();

    /// <summary>How many readings the family is kept for before it is found again.</summary>
    private const int FamilyEvery = 5;

    /// <summary>The family as last found, and how many readings ago.</summary>
    private IReadOnlyList<int> _family = [];

    /// <inheritdoc cref="_family"/>
    private int _familyAge = FamilyEvery;

    /// <summary>What the computer is, once it has been asked.</summary>
    private MachineFacts? _facts;

    /// <inheritdoc/>
    public MachineFacts Facts()
    {
        if (_facts is { } known) return known;

        var facts = new MachineFacts(
            counters?.ProcessorName(),
            Environment.ProcessorCount,
            counters?.Memory()?.Total ?? 0,
            counters?.SystemName() ?? System.Runtime.InteropServices.RuntimeInformation.OSDescription);

        _facts = facts;

        return facts;
    }

    /// <inheritdoc/>
    public LoadReading? Read()
    {
        if (counters == null) return null;

        var memory = counters.Memory();

        if (memory == null) return null;

        long now = Stopwatch.GetTimestamp();

        double machine = 0;
        var cores = new double[0];

        if (counters.Processors() is { } times)
        {
            if (_before is { } before)
            {
                machine = Share(before.Whole, times.Whole);

                if (before.Cores.Count == times.Cores.Count)
                {
                    cores = new double[times.Cores.Count];

                    for (int at = 0; at < cores.Length; at++)
                        cores[at] = Share(before.Cores[at], times.Cores[at]);
                }
            }

            _before = times;
        }

        if (++_familyAge >= FamilyEvery)
        {
            _family = counters.Family(root);
            _familyAge = 0;
        }

        var family = _family;
        var spent = new Dictionary<int, TimeSpan>(family.Count);

        long held = 0;
        double used = 0;
        int counted = 0;

        foreach (int pid in family)
        {
            try
            {
                using var process = Process.GetProcessById(pid);

                var time = process.TotalProcessorTime;

                held += process.WorkingSet64;
                spent[pid] = time;
                counted++;

                /* A process not there last time was started since, so all of its time is new. */
                if (_at > 0) used += (time - (_spent.TryGetValue(pid, out var before) ? before : TimeSpan.Zero)).TotalSeconds;
            }
            catch (Exception)
            {
                /* Ended between being found and being asked, which is nothing to count. */
            }
        }

        double own = 0;
        double seconds = _at > 0 ? (double)(now - _at) / Stopwatch.Frequency : 0;

        if (seconds > 0) own = Math.Clamp(used / (seconds * Environment.ProcessorCount), 0, 1);

        TrafficFigures? network = NetworkTotals(out long received, out long sent)
            ? Traffic(ref _network, received, sent, seconds)
            : null;

        TrafficFigures? disks = counters.Disks(out long read, out long written)
            ? Traffic(ref _disks, read, written, seconds)
            : null;

        _spent = spent;
        _at = now;

        return new LoadReading(machine, own, cores, memory.Value, held, counted, network, disks);
    }

    /// <summary>The network's and the discs' totals at the last reading, and nothing before one.</summary>
    private (long In, long Out)? _network, _disks;

    /// <summary>
    /// Two totals as rates since the last reading, which is kept for the next.
    /// </summary>
    /// <remarks>
    /// A total that went down, which is a network card taken away or a disc unplugged, is a rate
    /// of nought for that reading rather than a very large negative one.
    /// </remarks>
    /// <param name="last">The totals at the last reading, replaced with these.</param>
    /// <param name="into">All the bytes in, or read.</param>
    /// <param name="outOf">All the bytes out, or written.</param>
    /// <param name="seconds">How long since the last reading, and nought where there was none.</param>
    private static TrafficFigures Traffic(ref (long In, long Out)? last, long into, long outOf, double seconds)
    {
        double inRate = 0, outRate = 0;

        if (last is { } before && seconds > 0)
        {
            inRate = Math.Max(0, into - before.In) / seconds;
            outRate = Math.Max(0, outOf - before.Out) / seconds;
        }

        last = (into, outOf);

        return new TrafficFigures(inRate, outRate, into, outOf);
    }

    /// <summary>All the bytes the network cards have received and sent, the loopback left out.</summary>
    /// <remarks>
    /// .NET's own answer, which is the same question on every system. The loopback is this computer
    /// talking to itself, which includes this program talking to its plugins, and is not network.
    /// </remarks>
    /// <param name="received">The bytes received.</param>
    /// <param name="sent">The bytes sent.</param>
    /// <returns>False where there is no network card to ask.</returns>
    private static bool NetworkTotals(out long received, out long sent)
    {
        received = 0;
        sent = 0;

        bool any = false;

        try
        {
            foreach (var card in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (card.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var figures = card.GetIPStatistics();

                received += figures.BytesReceived;
                sent += figures.BytesSent;
                any = true;
            }
        }
        catch (Exception)
        {
            return false;
        }

        return any;
    }

    /// <summary>How busy a processor was between two readings, nought to one.</summary>
    /// <param name="before">Its time at the first.</param>
    /// <param name="after">And at the second.</param>
    private static double Share(CoreTime before, CoreTime after) =>
        after.Total > before.Total
            ? Math.Clamp((double)(after.Busy - before.Busy) / (after.Total - before.Total), 0, 1)
            : 0;
}
