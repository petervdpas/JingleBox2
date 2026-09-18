using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JingleBox2.Diagnostics.Interfaces;
using JingleBox2.Diagnostics.Records;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
/// <remarks>
/// Asked of Windows itself: <c>GetSystemTimes</c> for the processors, <c>GlobalMemoryStatusEx</c>
/// for the memory, and a snapshot of every process for who started whom. Windows keeps its swap
/// in the page file and does not give a figure that means what swap means elsewhere, so none is
/// said.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsCounters : ISystemCounters
{
    /// <inheritdoc/>
    /// <remarks>
    /// The whole from <c>GetSystemTimes</c> and each thread from the performance figures Windows
    /// keeps per processor. In both, kernel time includes the idle time, so the whole is kernel
    /// and user together.
    /// </remarks>
    public ProcessorTimes? Processors()
    {
        if (!GetSystemTimes(out long idle, out long kernel, out long user)) return null;

        var whole = new CoreTime(kernel + user - idle, kernel + user);

        return new ProcessorTimes(whole, Cores());
    }

    /// <summary>Each processor thread's time, or none where Windows would not say.</summary>
    private static CoreTime[] Cores()
    {
        int count = Environment.ProcessorCount;
        var figures = new CorePerformance[count];
        int size = Marshal.SizeOf<CorePerformance>() * count;

        if (NtQuerySystemInformation(ProcessorPerformance, figures, size, out _) != 0) return [];

        var cores = new CoreTime[count];

        for (int at = 0; at < count; at++)
        {
            long total = figures[at].KernelTime + figures[at].UserTime;
            cores[at] = new CoreTime(total - figures[at].IdleTime, total);
        }

        return cores;
    }

    /// <summary>Which question <c>NtQuerySystemInformation</c> is asked: each processor's times.</summary>
    private const int ProcessorPerformance = 8;

    /// <summary>What Windows answers for one processor, in hundreds of nanoseconds.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CorePerformance
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public uint InterruptCount;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int question, [Out] CorePerformance[] answer, int size, out int written);

    /// <inheritdoc/>
    public MemoryFigures? Memory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };

        if (!GlobalMemoryStatusEx(ref status)) return null;

        return new MemoryFigures((long)status.TotalPhysical, (long)status.AvailablePhysical, 0, 0);
    }

    /// <inheritdoc/>
    public IReadOnlyList<int> Family(int root)
    {
        var children = new Dictionary<int, List<int>>();
        var snapshot = CreateToolhelp32Snapshot(SnapProcesses, 0);

        if (snapshot == InvalidHandle) return new[] { root };

        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };

            for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                int pid = (int)entry.ProcessId;
                int parent = (int)entry.ParentProcessId;

                if (parent <= 0 || parent == pid) continue;

                if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<int>();

                list.Add(pid);
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return LinuxCounters.Walk(root, children);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Not said: Windows keeps these per disc behind a device query for each, which is more than a
    /// card that is looked at now and then is worth.
    /// </remarks>
    public bool Disks(out long read, out long written)
    {
        read = 0;
        written = 0;

        return false;
    }

    /// <inheritdoc/>
    /// <remarks>Where Windows keeps it, under the first processor in the hardware description.</remarks>
    public string? ProcessorName()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");

            return (key?.GetValue("ProcessorNameString") as string)?.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public string SystemName() => RuntimeInformation.OSDescription;

    private const uint SnapProcesses = 0x00000002;

    private static readonly nint InvalidHandle = -1;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(nint snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(nint snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);
}
