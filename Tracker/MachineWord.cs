using System;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class MachineWord : IMachineWord
{
    /// <summary>What a song made on Windows says.</summary>
    public const string Windows = "windows";

    /// <summary>What a song made on a Mac says.</summary>
    public const string Mac = "macos";

    /// <summary>What a song made anywhere else says, which here is Linux.</summary>
    public const string Linux = "linux";

    /// <inheritdoc/>
    /// <remarks>
    /// Asked of the runtime each time rather than kept, since it costs nothing and a field would
    /// be the same answer written down twice.
    /// </remarks>
    public string Here =>
        OperatingSystem.IsWindows() ? Windows
        : OperatingSystem.IsMacOS() ? Mac
        : Linux;

    /// <inheritdoc/>
    /// <remarks>
    /// A song that does not say is read as having been made here, which is what every song
    /// written before this field existed has to mean: those songs have been opened on this
    /// machine with their paths looked at all along, and a change that stopped looking at them
    /// would lose recordings that are found today.
    /// </remarks>
    public bool Travelled(string? made) =>
        !string.IsNullOrWhiteSpace(made)
        && !string.Equals(made, Here, StringComparison.OrdinalIgnoreCase);
}
