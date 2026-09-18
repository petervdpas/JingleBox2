using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Controls.Records;
using JingleBox2.Diagnostics.Interfaces;
using JingleBox2.Diagnostics.Records;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// The computer's load on SETTINGS, System, the way a system monitor shows it: every processor
/// thread over the last minute, the memory and the swap, the network and the discs, with how
/// much of the processors and the memory is JingleBox2 and its plugins.
/// </summary>
/// <remarks>
/// Read only while the card is on screen. The page asks for it with <see cref="Watch"/> when the
/// card appears and lets it go with <see cref="Stop"/>, so a program that is not being looked at
/// reads nothing at all.
///
/// The reading is done off the drawing thread and handed back to it, since walking the processes
/// takes a millisecond or two and the drawing thread has a song to show.
/// </remarks>
/// <param name="load">Where the figures come from.</param>
public sealed partial class SystemLoadViewModel(ISystemLoad load) : ObservableObject
{
    /// <summary>How long between the footer's glances.</summary>
    private static readonly TimeSpan GlanceEvery = TimeSpan.FromSeconds(2);

    /// <summary>The footer's ticking, while the footer shows the load.</summary>
    private Timer? _glancing;

    /// <summary>How busy the processors are, nought to one, for the footer.</summary>
    [ObservableProperty] private double glanceCpu;

    /// <summary>How full the memory is, nought to one, for the footer.</summary>
    [ObservableProperty] private double glanceMemory;

    /// <summary>
    /// Starts the footer's glance at the processors and the memory, every two seconds until it is
    /// stopped. Asking twice is the same as asking once.
    /// </summary>
    /// <remarks>
    /// Runs whatever page is showing, unlike the card, for as long as the footer shows the load.
    /// It costs two small file reads every two seconds and walks no processes: see
    /// <see cref="ISystemLoad.Glance"/>.
    /// </remarks>
    public void Glance()
    {
        if (_glancing != null) return;

        _glancing = new Timer(_ => Glimpse(), null, TimeSpan.Zero, GlanceEvery);
    }

    /// <summary>Stops the footer's glance, for a footer that no longer shows the load.</summary>
    public void StopGlancing()
    {
        _glancing?.Dispose();
        _glancing = null;
    }

    /// <summary>Takes one glance off the drawing thread and puts it in the footer.</summary>
    private void Glimpse()
    {
        SystemGlance? now = null;

        try
        {
            now = load.Glance();
        }
        catch (Exception)
        {
            /* A glance the system would not give leaves the footer where it was. */
        }

        if (now is not { } glance) return;

        Dispatcher.UIThread.Post(() =>
        {
            GlanceCpu = glance.Cpu;
            GlanceMemory = glance.Memory;
        });
    }

    /// <summary>How many readings a chart holds, one a second, which is the minute it spans.</summary>
    public const int Seconds = 61;

    /// <summary>How long between readings.</summary>
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The colours the processor threads are drawn in, in turn, starting again past the eighth.
    /// </summary>
    private static readonly Color[] CoreColours =
    [
        Color.Parse("#e01b24"), Color.Parse("#ff7800"), Color.Parse("#f6d32d"), Color.Parse("#33d17a"),
        Color.Parse("#26a269"), Color.Parse("#62a0ea"), Color.Parse("#1c71d8"), Color.Parse("#813d9c"),
    ];

    /// <summary>JingleBox2's own line, in white and thicker, so it stands out of the threads.</summary>
    public static readonly Color OwnColour = Color.Parse("#ffffff");

    /// <summary>The memory's line and disc.</summary>
    public static readonly Color MemoryColour = Color.Parse("#e01b24");

    /// <summary>The swap's line and disc.</summary>
    public static readonly Color SwapColour = Color.Parse("#33d17a");

    /// <summary>What comes in: received from the network, read from the discs.</summary>
    public static readonly Color InColour = Color.Parse("#3584e4");

    /// <summary>What goes out: sent to the network, written to the discs.</summary>
    public static readonly Color OutColour = Color.Parse("#ff7800");

    /// <summary>The ticking, while the card is on screen.</summary>
    private Timer? _timer;

    /// <summary>Set while a reading is under way, so a slow one is not stacked on by the next.</summary>
    private int _reading;

    /// <summary>Each thread's history, then JingleBox2's.</summary>
    private double[][] _cores = [];

    /// <summary>JingleBox2's share of the processors.</summary>
    private double[] _own = Empty();

    /// <summary>The memory in use, the swap in use, and JingleBox2's memory, as shares of their wholes.</summary>
    private double[] _memory = Empty(), _swap = Empty(), _ownMemory = Empty();

    /// <summary>The network's and the discs' rates, in bytes a second.</summary>
    private double[] _received = Empty(), _sent = Empty(), _read = Empty(), _written = Empty();

    /// <summary>Every thread's line, then JingleBox2's on top.</summary>
    [ObservableProperty] private IReadOnlyList<ChartLine> cpuLines = [];

    /// <summary>The key under the processors' chart.</summary>
    [ObservableProperty] private IReadOnlyList<ChartKey> cpuKey = [];

    /// <summary>The memory's line, the swap's, and JingleBox2's memory.</summary>
    [ObservableProperty] private IReadOnlyList<ChartLine> memoryLines = [];

    /// <summary>What comes in and goes out over the network.</summary>
    [ObservableProperty] private IReadOnlyList<ChartLine> networkLines = [];

    /// <summary>The network chart's top value, in bytes a second, and its levels in words.</summary>
    [ObservableProperty] private double networkTop = 1024;

    /// <inheritdoc cref="networkTop"/>
    [ObservableProperty] private IReadOnlyList<string> networkLevels = Levels(1024);

    /// <summary>What is read from and written to the discs.</summary>
    [ObservableProperty] private IReadOnlyList<ChartLine> diskLines = [];

    /// <summary>The discs chart's top value, in bytes a second, and its levels in words.</summary>
    [ObservableProperty] private double diskTop = 1024;

    /// <inheritdoc cref="diskTop"/>
    [ObservableProperty] private IReadOnlyList<string> diskLevels = Levels(1024);

    /// <summary>The processor, how many threads it has, and the memory, in words.</summary>
    [ObservableProperty] private string computerText = "";

    /// <summary>The operating system, in its own words.</summary>
    [ObservableProperty] private string systemText = "";

    /// <summary>How much of the memory is in use, nought to one, for its disc.</summary>
    [ObservableProperty] private double memoryShare;

    /// <summary>The memory in use, as bytes, a share and the whole.</summary>
    [ObservableProperty] private string memoryText = "";

    /// <summary>What is holding copies of files, and nothing where the system does not say.</summary>
    [ObservableProperty] private string cacheText = "";

    /// <summary>JingleBox2's memory, as bytes and a share of the whole.</summary>
    [ObservableProperty] private string ownMemoryText = "";

    /// <summary>How much of the swap is in use, nought to one, for its disc.</summary>
    [ObservableProperty] private double swapShare;

    /// <summary>The swap in use, as bytes, a share and the whole.</summary>
    [ObservableProperty] private string swapText = "";

    /// <summary>Whether there is swap to show.</summary>
    [ObservableProperty] private bool hasSwap;

    /// <summary>Whether the network could be read.</summary>
    [ObservableProperty] private bool hasNetwork;

    /// <summary>What is coming in over the network now.</summary>
    [ObservableProperty] private string receiving = "";

    /// <summary>All that has come in since the computer started.</summary>
    [ObservableProperty] private string totalReceived = "";

    /// <summary>What is going out over the network now.</summary>
    [ObservableProperty] private string sending = "";

    /// <summary>All that has gone out since the computer started.</summary>
    [ObservableProperty] private string totalSent = "";

    /// <summary>Whether the discs could be read.</summary>
    [ObservableProperty] private bool hasDisks;

    /// <summary>What is being read from the discs now.</summary>
    [ObservableProperty] private string reading = "";

    /// <summary>All that has been read since the computer started.</summary>
    [ObservableProperty] private string totalRead = "";

    /// <summary>What is being written to the discs now.</summary>
    [ObservableProperty] private string writing = "";

    /// <summary>All that has been written since the computer started.</summary>
    [ObservableProperty] private string totalWritten = "";

    /// <summary>What "JingleBox2" is counting, in words.</summary>
    [ObservableProperty] private string familyText = "";

    /// <summary>Whether this system can be asked at all.</summary>
    [ObservableProperty] private bool measured = true;

    /// <summary>The memory's colour, for the disc beside its figures.</summary>
    public Color MemoryDisc => MemoryColour;

    /// <summary>The swap's colour, for the disc beside its figures.</summary>
    public Color SwapDisc => SwapColour;

    /// <summary>What comes in, for the arrows beside its figures.</summary>
    public IBrush InBrush { get; } = new SolidColorBrush(InColour);

    /// <summary>What goes out, for the arrows beside its figures.</summary>
    public IBrush OutBrush { get; } = new SolidColorBrush(OutColour);

    /// <summary>Starts reading once a second, from now. Asking twice is the same as asking once.</summary>
    public void Watch()
    {
        if (_timer != null) return;

        _timer = new Timer(_ => Take(), null, TimeSpan.Zero, Every);
    }

    /// <summary>Stops reading. The charts keep what they had, and carry on from there next time.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Takes one reading off the drawing thread and hands it to <see cref="Show"/>.</summary>
    private void Take()
    {
        if (Interlocked.Exchange(ref _reading, 1) == 1) return;

        LoadReading? now = null;
        MachineFacts? facts = null;

        try
        {
            if (ComputerText.Length == 0) facts = load.Facts();

            now = load.Read();
        }
        catch (Exception)
        {
            /* A figure the system would not give is a card that says so, not a fault. */
        }
        finally
        {
            Interlocked.Exchange(ref _reading, 0);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (facts is { } known) Describe(known);

            Show(now);
        });
    }

    /// <summary>Puts what the computer is on the card, which is done once.</summary>
    /// <param name="facts">What the computer is.</param>
    private void Describe(MachineFacts facts)
    {
        string threads = facts.Threads.ToString(CultureInfo.InvariantCulture) + " threads";
        string memory = facts.Memory > 0 ? ", " + Size(facts.Memory) + " memory" : "";

        ComputerText = (facts.Processor is { Length: > 0 } name ? name + ", " : "") + threads + memory;
        SystemText = facts.System;
    }

    /// <summary>Puts a reading on the card.</summary>
    /// <remarks>
    /// Every chart is given new lines rather than having the old ones changed underneath it,
    /// since a new value is what tells it to draw again.
    /// </remarks>
    /// <param name="reading">The figures, or nothing where the system would not say.</param>
    private void Show(LoadReading? reading)
    {
        if (reading is not { } now)
        {
            Measured = false;
            return;
        }

        Measured = true;

        ShowProcessors(now);
        ShowMemory(now);
        ShowNetwork(now.Network);
        ShowDisks(now.Disks);

        FamilyText = now.OwnProcesses <= 1
            ? "JingleBox2 is one process, with no plugin running."
            : "JingleBox2 is " + now.OwnProcesses.ToString(CultureInfo.InvariantCulture)
              + " processes: the program itself and the plugins it has started, with whatever they started in turn.";
    }

    /// <summary>Moves every thread's line and JingleBox2's on by the reading.</summary>
    /// <remarks>
    /// The first reading has no threads in it, since busy is measured between two, so it moves
    /// nothing on. A different number of threads starts the lines again.
    /// </remarks>
    private void ShowProcessors(LoadReading now)
    {
        if (now.Cores.Count == 0) return;

        if (_cores.Length != now.Cores.Count)
        {
            _cores = new double[now.Cores.Count][];

            for (int at = 0; at < _cores.Length; at++) _cores[at] = Empty();
        }

        var lines = new List<ChartLine>(_cores.Length + 1);
        var key = new List<ChartKey>(_cores.Length + 1);

        for (int at = 0; at < _cores.Length; at++)
        {
            _cores[at] = Shifted(_cores[at], now.Cores[at]);

            var colour = CoreColours[at % CoreColours.Length];

            lines.Add(new ChartLine(_cores[at], colour));
            key.Add(new ChartKey("CPU" + (at + 1).ToString(CultureInfo.InvariantCulture), Percent(now.Cores[at]),
                new SolidColorBrush(colour)));
        }

        double own = Math.Min(now.OwnCpu, now.MachineCpu);

        _own = Shifted(_own, own);

        lines.Add(new ChartLine(_own, OwnColour, 2.5));
        key.Add(new ChartKey("JingleBox2", Percent(own) + " of all", new SolidColorBrush(OwnColour)));

        CpuLines = lines;
        CpuKey = key;
    }

    /// <summary>Moves the memory's lines on, and says the memory and the swap in words.</summary>
    private void ShowMemory(LoadReading now)
    {
        var memory = now.Memory;
        long used = memory.Total - memory.Available;

        double share = memory.Total > 0 ? (double)used / memory.Total : 0;
        double own = memory.Total > 0 ? Math.Min((double)now.OwnMemory / memory.Total, share) : 0;
        double swap = memory.SwapTotal > 0 ? (double)(memory.SwapTotal - memory.SwapFree) / memory.SwapTotal : 0;

        _memory = Shifted(_memory, share);
        _ownMemory = Shifted(_ownMemory, own);

        HasSwap = memory.SwapTotal > 0;

        var lines = new List<ChartLine> { new(_memory, MemoryColour) };

        if (HasSwap)
        {
            _swap = Shifted(_swap, swap);
            lines.Add(new ChartLine(_swap, SwapColour));
        }

        lines.Add(new ChartLine(_ownMemory, OwnColour, 2.5));

        MemoryLines = lines;

        MemoryShare = share;
        MemoryText = Size(used) + " (" + Percent(share) + ") of " + Size(memory.Total);
        CacheText = memory.Cache > 0 ? "Cache " + Size(memory.Cache) : "";
        OwnMemoryText = "JingleBox2 " + Size(now.OwnMemory) + " (" + Percent(own) + ")";

        SwapShare = swap;
        SwapText = HasSwap ? Size(memory.SwapTotal - memory.SwapFree) + " (" + Percent(swap) + ") of " + Size(memory.SwapTotal) : "";
    }

    /// <summary>Moves the network's lines on, and says it in words.</summary>
    private void ShowNetwork(TrafficFigures? network)
    {
        HasNetwork = network != null;

        if (network is not { } now) return;

        _received = Shifted(_received, now.InRate);
        _sent = Shifted(_sent, now.OutRate);

        NetworkTop = Ceiling(_received, _sent);
        NetworkLevels = Levels(NetworkTop);
        NetworkLines = [new ChartLine(_received, InColour), new ChartLine(_sent, OutColour)];

        Receiving = Rate(now.InRate);
        TotalReceived = Size(now.InTotal);
        Sending = Rate(now.OutRate);
        TotalSent = Size(now.OutTotal);
    }

    /// <summary>Moves the discs' lines on, and says them in words.</summary>
    private void ShowDisks(TrafficFigures? disks)
    {
        HasDisks = disks != null;

        if (disks is not { } now) return;

        _read = Shifted(_read, now.InRate);
        _written = Shifted(_written, now.OutRate);

        DiskTop = Ceiling(_read, _written);
        DiskLevels = Levels(DiskTop);
        DiskLines = [new ChartLine(_read, InColour), new ChartLine(_written, OutColour)];

        Reading = Rate(now.InRate);
        TotalRead = Size(now.InTotal);
        Writing = Rate(now.OutRate);
        TotalWritten = Size(now.OutTotal);
    }

    /// <summary>A history with no readings in it yet.</summary>
    private static double[] Empty()
    {
        var history = new double[Seconds];

        Array.Fill(history, double.NaN);

        return history;
    }

    /// <summary>The same history a second on: the oldest reading dropped and the new one added.</summary>
    /// <param name="history">What there was.</param>
    /// <param name="value">The reading to add.</param>
    private static double[] Shifted(double[] history, double value)
    {
        var next = new double[history.Length];

        Array.Copy(history, 1, next, 0, history.Length - 1);
        next[^1] = value;

        return next;
    }

    /// <summary>
    /// A top for a chart of rates that the highest reading fits under: one, two or five of a
    /// binary unit, and never less than a kilobyte a second, so a quiet minute is a flat line
    /// rather than noise blown up to fill the chart.
    /// </summary>
    private static double Ceiling(double[] one, double[] other)
    {
        double highest = 0;

        foreach (double value in one) if (!double.IsNaN(value)) highest = Math.Max(highest, value);
        foreach (double value in other) if (!double.IsNaN(value)) highest = Math.Max(highest, value);

        for (double unit = 1024; unit < 1L << 40; unit *= 1024)
            foreach (int many in Steps)
                if (many * unit >= highest) return many * unit;

        return highest;
    }

    /// <summary>The counts of a unit a rate chart's top may be.</summary>
    private static readonly int[] Steps = [1, 2, 5, 10, 20, 50, 100, 200, 500];

    /// <summary>A rate chart's five levels in words, from the top down.</summary>
    private static IReadOnlyList<string> Levels(double top) =>
        [Rate(top), Rate(top * 0.8), Rate(top * 0.6), Rate(top * 0.4), Rate(top * 0.2)];

    /// <summary>A share as percent, to one place.</summary>
    private static string Percent(double share) =>
        (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A number of bytes a second, the way a system monitor says it.</summary>
    private static string Rate(double bytes) =>
        bytes < 1024 ? bytes.ToString("0", CultureInfo.InvariantCulture) + " bytes/s"
        : bytes < 1 << 20 ? (bytes / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " KiB/s"
        : bytes < 1 << 30 ? (bytes / (1 << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB/s"
        : (bytes / (1 << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GiB/s";

    /// <summary>A number of bytes the way a person reads it.</summary>
    private static string Size(long bytes) =>
        bytes >= 1L << 30 ? (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.InvariantCulture) + " GiB"
        : bytes >= 1L << 20 ? (bytes / (double)(1L << 20)).ToString("0.0", CultureInfo.InvariantCulture) + " MiB"
        : (bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture) + " KiB";
}
