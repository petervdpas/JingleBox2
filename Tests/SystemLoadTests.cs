using System;
using System.Collections.Generic;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Interfaces;
using JingleBox2.Diagnostics.Records;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// How busy the computer is and how much of it is this program, as SETTINGS, System shows it.
/// </summary>
/// <remarks>
/// The readings of the kernel's files are put questions to with the text of real ones, and the
/// arithmetic with a computer made up for the purpose, since the real one is never the same twice.
/// </remarks>
public class SystemLoadTests
{
    /// <summary>The first lines of a real <c>/proc/stat</c>.</summary>
    private const string Stat =
        "cpu  3357 0 4313 1362393 2000 0 20 0 0 0\n" +
        "cpu0 1393 0 1666 1350000 0 0 0 0 0 0\n";

    /// <summary>Idle and waiting on a disc are the time not busy, and the guest columns are not counted twice.</summary>
    [Fact]
    public void Busy_is_everything_but_idle_and_waiting()
    {
        var times = LinuxCounters.ReadProcessors(Stat);

        Assert.NotNull(times);
        Assert.Equal(3357 + 4313 + 20, times.Value.Whole.Busy);
        Assert.Equal(3357 + 4313 + 1362393 + 2000 + 20, times.Value.Whole.Total);
    }

    /// <summary>Every line after the first is one thread of its own.</summary>
    [Fact]
    public void Each_core_is_read_from_its_own_line()
    {
        var times = LinuxCounters.ReadProcessors(Stat + "cpu1 10 0 10 80 0 0 0 0 0 0\nintr 5 6\n");

        Assert.NotNull(times);
        Assert.Equal(2, times.Value.Cores.Count);
        Assert.Equal(new CoreTime(1393 + 1666, 1393 + 1666 + 1350000), times.Value.Cores[0]);
        Assert.Equal(new CoreTime(20, 100), times.Value.Cores[1]);
    }

    /// <summary>A file that is not the one expected says so rather than answering nonsense.</summary>
    [Fact]
    public void A_strange_stat_file_is_refused()
    {
        Assert.Null(LinuxCounters.ReadProcessors("intr 1 2 3\n"));
        Assert.Null(LinuxCounters.ReadProcessors(""));
    }

    /// <summary>The memory in use is what is not available, which leaves the file cache out of it.</summary>
    [Fact]
    public void Memory_is_read_in_bytes()
    {
        const string meminfo =
            "MemTotal:        7851204 kB\n" +
            "MemFree:          812000 kB\n" +
            "MemAvailable:    2617000 kB\n" +
            "SwapTotal:       5193724 kB\n" +
            "SwapFree:        1497000 kB\n";

        var memory = LinuxCounters.ReadMemory(meminfo);

        Assert.NotNull(memory);
        Assert.Equal(7851204L * 1024, memory.Value.Total);
        Assert.Equal(2617000L * 1024, memory.Value.Available);
        Assert.Equal(5193724L * 1024, memory.Value.SwapTotal);
        Assert.Equal(1497000L * 1024, memory.Value.SwapFree);
    }

    /// <summary>Only the discs themselves are counted, in bytes, and not their partitions or loop devices.</summary>
    [Fact]
    public void Disks_are_counted_once_each()
    {
        const string diskstats =
            " 259       0 nvme0n1 1544211 616706 100 501978 1025856 2445263 300 15687094 0 431872 16211965\n" +
            " 259       2 nvme0n1p2 1543380 615887 99 501689 1025853 2445263 299 15687048 0 483788 16188737\n" +
            "   7       0 loop0 286 0 1756 13 0 0 0 0 0 16 13\n";

        var discs = new HashSet<string> { "nvme0n1", "loop0" };

        Assert.True(LinuxCounters.ReadDisks(diskstats, discs, out long read, out long written));
        Assert.Equal(100L * 512, read);
        Assert.Equal(300L * 512, written);
    }

    /// <summary>The cache is the buffers, the cached files and what the kernel can take back, together.</summary>
    [Fact]
    public void The_cache_is_read_with_the_memory()
    {
        var memory = LinuxCounters.ReadMemory(
            "MemTotal: 1000 kB\nMemAvailable: 500 kB\nBuffers: 10 kB\nCached: 200 kB\nSReclaimable: 30 kB\n");

        Assert.NotNull(memory);
        Assert.Equal(240L * 1024, memory.Value.Cache);
    }

    /// <summary>Without a total or an available figure there is nothing to say about the memory.</summary>
    [Fact]
    public void Memory_without_its_figures_is_nothing()
    {
        Assert.Null(LinuxCounters.ReadMemory("MemFree: 100 kB\n"));
    }

    /// <summary>A process name with spaces and brackets in it does not move the parent along.</summary>
    [Fact]
    public void The_parent_is_read_past_an_awkward_name()
    {
        Assert.Equal(1234, LinuxCounters.ReadParent("5678 (Web Content (2)) S 1234 5678 5678 0 -1"));
        Assert.Equal(0, LinuxCounters.ReadParent("nonsense"));
    }

    /// <summary>The children file is numbers with spaces between them, and a space after the last.</summary>
    [Fact]
    public void Children_are_read_from_their_list()
    {
        Assert.Equal(new[] { 12, 34 }, LinuxCounters.ReadChildren("12 34 "));
        Assert.Empty(LinuxCounters.ReadChildren(""));
    }

    /// <summary>The family is every generation down, and nobody outside it.</summary>
    [Fact]
    public void The_family_goes_all_the_way_down()
    {
        var children = new Dictionary<int, List<int>>
        {
            [1] = new() { 2, 3 },
            [3] = new() { 4 },
            [9] = new() { 10 },
        };

        Assert.Equal(new[] { 1, 2, 3, 4 }, LinuxCounters.Walk(1, children));
    }

    /// <summary>The processor's name and the system's are read from their files.</summary>
    [Fact]
    public void Names_are_read_from_their_files()
    {
        Assert.Equal("Intel(R) Core(TM) i5-8265U CPU @ 1.60GHz",
            LinuxCounters.ReadProcessorName("processor\t: 0\nmodel name\t: Intel(R) Core(TM) i5-8265U CPU @ 1.60GHz\n"));

        Assert.Equal("Debian GNU/Linux 13 (trixie)",
            LinuxCounters.ReadReleaseName("NAME=\"Debian GNU/Linux\"\nPRETTY_NAME=\"Debian GNU/Linux 13 (trixie)\"\n"));
    }

    /// <summary>
    /// The first reading has nothing to measure busy against, and the second measures the stretch
    /// between them.
    /// </summary>
    [Fact]
    public void Busy_is_measured_between_two_readings()
    {
        var computer = new MadeUpComputer();
        var load = new SystemLoad(computer, Environment.ProcessId);

        var first = load.Read();

        Assert.NotNull(first);
        Assert.Equal(0, first.Value.MachineCpu);

        computer.Busy += 25;
        computer.Total += 100;
        computer.CoreBusy += 40;
        computer.CoreTotal += 50;

        var second = load.Read();

        Assert.NotNull(second);
        Assert.Equal(0.25, second.Value.MachineCpu, 3);
        Assert.Equal(new[] { 0.8 }, second.Value.Cores);
        Assert.Equal(1, second.Value.OwnProcesses);
        Assert.True(second.Value.OwnMemory > 0);
    }

    /// <summary>A system that cannot be asked answers nothing rather than noughts that look like figures.</summary>
    [Fact]
    public void A_system_that_cannot_be_asked_says_nothing()
    {
        Assert.Null(new SystemLoad(null, Environment.ProcessId).Read());
    }

    /// <summary>A computer whose figures the test decides, with this process as the whole family.</summary>
    private sealed class MadeUpComputer : ISystemCounters
    {
        public long Busy = 100;

        public long Total = 1000;

        public long CoreBusy = 10;

        public long CoreTotal = 100;

        public ProcessorTimes? Processors() =>
            new ProcessorTimes(new CoreTime(Busy, Total), new[] { new CoreTime(CoreBusy, CoreTotal) });

        public MemoryFigures? Memory() => new MemoryFigures(8L << 30, 3L << 30, 0, 0);

        public IReadOnlyList<int> Family(int root) => new[] { root };

        public bool Disks(out long read, out long written)
        {
            read = 0;
            written = 0;
            return false;
        }

        public string? ProcessorName() => "Made up";

        public string SystemName() => "Made up";
    }
}
