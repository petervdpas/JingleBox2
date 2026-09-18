using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JingleBox2.Diagnostics.Interfaces;
using JingleBox2.Diagnostics.Records;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
/// <remarks>
/// Read from the files the kernel keeps under <c>/proc</c>: <c>stat</c> for the processors,
/// <c>meminfo</c> for the memory, and each process's own <c>stat</c> for who started it. Plain
/// text, and the reading of each is a method on its own so it can be put a question to without a
/// kernel.
/// </remarks>
public sealed class LinuxCounters : ISystemCounters
{
    /// <inheritdoc/>
    public ProcessorTimes? Processors()
    {
        try
        {
            return ReadProcessors(File.ReadAllText("/proc/stat"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public MemoryFigures? Memory()
    {
        try
        {
            return ReadMemory(File.ReadAllText("/proc/meminfo"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Asked of each process's own list of the processes it started, which the kernel keeps per
    /// thread, so only this program's family is read and not every process on the computer. A
    /// kernel built without those lists gets the long way round instead: every process looked at
    /// once, to learn who started it.
    /// </remarks>
    public IReadOnlyList<int> Family(int root)
    {
        if (!File.Exists("/proc/" + root.ToString(CultureInfo.InvariantCulture) + "/task/"
                         + root.ToString(CultureInfo.InvariantCulture) + "/children"))
            return Everyone(root);

        var family = new List<int> { root };

        for (int at = 0; at < family.Count; at++)
        {
            foreach (int child in ChildrenOf(family[at]))
                if (!family.Contains(child)) family.Add(child);
        }

        return family;
    }

    /// <summary>The processes one process started, from the lists its threads keep.</summary>
    /// <param name="pid">The process.</param>
    private static List<int> ChildrenOf(int pid)
    {
        var children = new List<int>();

        try
        {
            foreach (var task in Directory.EnumerateDirectories("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/task"))
            {
                string text;

                try
                {
                    text = File.ReadAllText(Path.Combine(task, "children"));
                }
                catch (Exception)
                {
                    continue;
                }

                children.AddRange(ReadChildren(text));
            }
        }
        catch (Exception)
        {
            /* The process ended while it was being read, and started nothing that is still ours. */
        }

        return children;
    }

    /// <summary>The family the long way: every process on the computer asked who started it.</summary>
    /// <param name="root">Where to start.</param>
    private static IReadOnlyList<int> Everyone(int root)
    {
        var children = new Dictionary<int, List<int>>();

        try
        {
            foreach (var folder in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
                    continue;

                int parent;

                try
                {
                    parent = ReadParent(File.ReadAllText(Path.Combine(folder, "stat")));
                }
                catch (Exception)
                {
                    continue;
                }

                if (parent <= 0) continue;

                if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<int>();

                list.Add(pid);
            }
        }
        catch (Exception)
        {
            return new[] { root };
        }

        return Walk(root, children);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// From <c>/proc/diskstats</c>, counting only the devices <c>/sys/block</c> lists as discs and
    /// leaving out the loop, memory and mapped devices, which are other discs' bytes again.
    /// </remarks>
    public bool Disks(out long read, out long written)
    {
        read = 0;
        written = 0;

        try
        {
            var discs = new HashSet<string>(StringComparer.Ordinal);

            foreach (var folder in Directory.EnumerateDirectories("/sys/block"))
                discs.Add(Path.GetFileName(folder));

            return ReadDisks(File.ReadAllText("/proc/diskstats"), discs, out read, out written);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The bytes read and written, from <c>/proc/diskstats</c>, over the discs named.</summary>
    /// <remarks>
    /// Each line is a device's numbers, its name and then its counts; the sectors read are the
    /// third count and the sectors written the seventh. A sector here is always 512 bytes,
    /// whatever the disc's own sectors are.
    /// </remarks>
    /// <param name="text">What the file says.</param>
    /// <param name="discs">The devices that are discs.</param>
    /// <param name="read">The bytes read.</param>
    /// <param name="written">The bytes written.</param>
    /// <returns>False where no disc was found in it.</returns>
    public static bool ReadDisks(string text, IReadOnlySet<string> discs, out long read, out long written)
    {
        read = 0;
        written = 0;

        bool any = false;

        foreach (var line in text.Split('\n'))
        {
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 10) continue;

            string name = fields[2];

            if (!discs.Contains(name) || name.StartsWith("loop", StringComparison.Ordinal)
                || name.StartsWith("ram", StringComparison.Ordinal) || name.StartsWith("zram", StringComparison.Ordinal)
                || name.StartsWith("dm-", StringComparison.Ordinal) || name.StartsWith("md", StringComparison.Ordinal))
                continue;

            if (!long.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out long sectorsRead)
                || !long.TryParse(fields[9], NumberStyles.None, CultureInfo.InvariantCulture, out long sectorsWritten))
                continue;

            read += sectorsRead * 512;
            written += sectorsWritten * 512;
            any = true;
        }

        return any;
    }

    /// <inheritdoc/>
    /// <remarks>The first model name in <c>/proc/cpuinfo</c>, since every core says the same one.</remarks>
    public string? ProcessorName()
    {
        try
        {
            return ReadProcessorName(File.ReadAllText("/proc/cpuinfo"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The distribution's own name for itself from <c>/etc/os-release</c>, and the kernel's
    /// version after it, since the two are what anybody asking about a Linux machine wants.
    /// </remarks>
    public string SystemName()
    {
        string kernel = Environment.OSVersion.Version.ToString(3);

        try
        {
            string? name = ReadReleaseName(File.ReadAllText("/etc/os-release"));

            if (name != null) return name + ", kernel " + kernel;
        }
        catch (Exception)
        {
            /* No release file is a Linux that does not say which one it is. */
        }

        return "Linux, kernel " + kernel;
    }

    /// <summary>The processes named in one thread's <c>children</c> file.</summary>
    /// <param name="text">What the file says: numbers with spaces between them.</param>
    public static IEnumerable<int> ReadChildren(string text)
    {
        foreach (var field in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            if (int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
                yield return pid;
    }

    /// <summary>The first model name in <c>/proc/cpuinfo</c>, or nothing where there is none.</summary>
    /// <param name="text">What the file says.</param>
    public static string? ReadProcessorName(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            int colon = line.IndexOf(':');

            if (colon > 0 && line.Substring(0, colon).Trim() == "model name")
                return string.Join(' ', line.Substring(colon + 1).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return null;
    }

    /// <summary>The pretty name in <c>/etc/os-release</c>, or nothing where there is none.</summary>
    /// <param name="text">What the file says, a name and a quoted value to a line.</param>
    public static string? ReadReleaseName(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (!line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal)) continue;

            string value = line.Substring("PRETTY_NAME=".Length).Trim().Trim('"');

            return value.Length > 0 ? value : null;
        }

        return null;
    }

    /// <summary>A process and everything under it, from a table of who started whom.</summary>
    /// <param name="root">Where to start.</param>
    /// <param name="children">Each process's own children.</param>
    public static IReadOnlyList<int> Walk(int root, IReadOnlyDictionary<int, List<int>> children)
    {
        var family = new List<int> { root };

        for (int at = 0; at < family.Count; at++)
        {
            if (!children.TryGetValue(family[at], out var under)) continue;

            foreach (int child in under)
                if (!family.Contains(child)) family.Add(child);
        }

        return family;
    }

    /// <summary>The processors' time, from the <c>cpu</c> lines of <c>/proc/stat</c>.</summary>
    /// <remarks>
    /// The first line is all of them together and each <c>cpu</c> line after it is one thread,
    /// numbered from nought. A thread taken offline has no line, so the cores are the ones there.
    /// </remarks>
    /// <param name="text">What the file says.</param>
    /// <returns>Nothing where the first line is not there or not as expected.</returns>
    public static ProcessorTimes? ReadProcessors(string text)
    {
        CoreTime? whole = null;
        var cores = new List<CoreTime>();

        foreach (var line in text.Split('\n'))
        {
            if (!line.StartsWith("cpu", StringComparison.Ordinal)) continue;

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (ReadTime(fields) is not { } time) continue;

            if (fields[0] == "cpu") whole = time;
            else cores.Add(time);
        }

        return whole is { } all ? new ProcessorTimes(all, cores) : null;
    }

    /// <summary>One <c>cpu</c> line's busy time and all its time.</summary>
    /// <remarks>
    /// The line is the name and then the time spent each way. Idle and waiting on a disc are the
    /// time not busy; the guest columns are already inside user and nice, so they are left out of
    /// the whole rather than counted twice.
    /// </remarks>
    /// <param name="fields">The line, split at its spaces.</param>
    private static CoreTime? ReadTime(string[] fields)
    {
        if (fields.Length < 5) return null;

        long idle = 0, total = 0;
        int counted = Math.Min(fields.Length - 1, 8);

        for (int at = 1; at <= counted; at++)
        {
            if (!long.TryParse(fields[at], NumberStyles.None, CultureInfo.InvariantCulture, out long value))
                return null;

            total += value;

            if (at == 4 || at == 5) idle += value;
        }

        return total > 0 ? new CoreTime(total - idle, total) : null;
    }

    /// <summary>The memory and the swap, from <c>/proc/meminfo</c>.</summary>
    /// <remarks>
    /// Available rather than free: free leaves out the file cache the kernel hands back the moment
    /// anything asks, and a computer with most of its memory in cache is not a full one. The
    /// cache is said as well, as the buffers, the cached files and the kernel's own reclaimable
    /// memory together, which is the figure other system monitors give.
    /// </remarks>
    /// <param name="text">What the file says, a name, a number and kB to a line.</param>
    public static MemoryFigures? ReadMemory(string text)
    {
        long total = -1, available = -1, swapTotal = 0, swapFree = 0, cache = 0;

        foreach (var line in text.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;

            string name = line.Substring(0, colon);
            var rest = line.Substring(colon + 1).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (rest.Length == 0 || !long.TryParse(rest[0], NumberStyles.None, CultureInfo.InvariantCulture, out long kb))
                continue;

            long bytes = kb * 1024;

            switch (name)
            {
                case "MemTotal": total = bytes; break;
                case "MemAvailable": available = bytes; break;
                case "SwapTotal": swapTotal = bytes; break;
                case "SwapFree": swapFree = bytes; break;
                case "Buffers":
                case "Cached":
                case "SReclaimable": cache += bytes; break;
            }
        }

        if (total <= 0 || available < 0) return null;

        return new MemoryFigures(total, available, swapTotal, swapFree, cache);
    }

    /// <summary>Which process started this one, from its own <c>stat</c> file.</summary>
    /// <remarks>
    /// Counted from the last closing bracket, since the name before it is in brackets and may
    /// have spaces and brackets of its own in it.
    /// </remarks>
    /// <param name="text">What the file says.</param>
    /// <returns>The parent, or nought where it cannot be read.</returns>
    public static int ReadParent(string text)
    {
        int close = text.LastIndexOf(')');
        if (close < 0) return 0;

        var fields = text.Substring(close + 1).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return fields.Length >= 2 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int parent)
            ? parent
            : 0;
    }
}
