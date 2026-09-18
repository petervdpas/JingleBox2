using System.Collections.Generic;
using JingleBox2.Diagnostics.Records;

namespace JingleBox2.Diagnostics.Interfaces;

/// <summary>
/// The three things about the whole computer that .NET does not say for itself: how busy the
/// processors are, where the memory stands, and which processes a process started.
/// </summary>
/// <remarks>
/// Each process's own time and memory are .NET's to answer and are not asked here. These are the
/// part that is a different question on every system, so each system answers them in its own
/// words and <see cref="ISystemLoad"/> puts the answers together the same way everywhere.
/// </remarks>
public interface ISystemCounters
{
    /// <summary>
    /// How much time the processors have spent busy and in all, together and one by one, or
    /// nothing where the system would not say.
    /// </summary>
    ProcessorTimes? Processors();

    /// <summary>Where the memory stands now, or nothing where the system would not say.</summary>
    MemoryFigures? Memory();

    /// <summary>
    /// That process and every process it started, and every process those started, however deep.
    /// </summary>
    /// <remarks>
    /// Deep because a plugin's own process starts processes of its own: a plugin drawing its face
    /// in a web view has a browser engine behind it, and that is this program's cost as much as
    /// the plugin is.
    ///
    /// The dearest of the three to ask, so it is asked every few readings rather than every one:
    /// a plugin is started now and then, not every second.
    /// </remarks>
    /// <param name="root">The process to start from.</param>
    IReadOnlyList<int> Family(int root);

    /// <summary>
    /// All the bytes read from and written to the discs since the computer started, or nothing
    /// where the system would not say.
    /// </summary>
    /// <remarks>
    /// The discs themselves, not the partitions on them or the devices made out of them, since
    /// each of those is the same bytes counted again.
    /// </remarks>
    /// <param name="read">The bytes read.</param>
    /// <param name="written">The bytes written.</param>
    bool Disks(out long read, out long written);

    /// <summary>The processor's own name, or nothing where the system would not say.</summary>
    string? ProcessorName();

    /// <summary>The operating system and its version, in the words it uses itself.</summary>
    string SystemName();
}
