using System;
using System.Runtime.InteropServices;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed partial class ClockResolution : IClockResolution
{
    /// <summary>The finest tick worth asking for, in milliseconds.</summary>
    /// <remarks>
    /// One, which is what the system's own multimedia timers are documented in and what every
    /// program that has to keep time on this platform asks for. Asking for less is not offered,
    /// and asking for more would be choosing a coarser clock than the default in places.
    /// </remarks>
    private const uint Finest = 1;

    /// <summary>What the system answers when it will not do it.</summary>
    private const uint Refused = 97;

    /// <summary>Asks the multimedia timer service for a tick of the given length.</summary>
    [LibraryImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static partial uint BeginPeriod(uint milliseconds);

    /// <summary>Whether the ask was made and granted, which is what <see cref="Said"/> reads.</summary>
    private bool _taken;

    /// <inheritdoc/>
    /// <remarks>
    /// Everything is caught, including the library not being there at all. A system without
    /// <c>winmm</c> is one that never needed this, and a process that would not start because it
    /// could not ask for a finer clock would be the worst possible trade.
    /// </remarks>
    public bool Take()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            _taken = BeginPeriod(Finest) != Refused;

            return _taken;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public string Said()
    {
        if (!OperatingSystem.IsWindows()) return "the system's own clock";

        return _taken
            ? "a " + Finest + " ms clock"
            : "the system's default clock, which rounds every wait up to about 15.6 ms";
    }
}
