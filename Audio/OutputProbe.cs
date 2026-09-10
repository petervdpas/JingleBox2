using System;
using JingleBox2.Audio.Interfaces;
using ManagedBass;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through BASS, which is the only thing here that knows what an output is. The device the
/// calling thread was on is put back afterwards: BASS keeps the current device per thread and
/// initialising one moves it, so a probe that did not put it back would leave every call after it
/// on this thread pointed at whatever was asked about last.
/// </remarks>
public sealed class OutputProbe : IOutputProbe
{
    /// <summary>What to open it at, which only has to be something the device will take.</summary>
    private readonly int _rate;

    /// <summary>Takes the rate the application runs at.</summary>
    /// <param name="rate">Frames a second.</param>
    public OutputProbe(int rate) => _rate = rate;

    /// <inheritdoc/>
    public bool Opens(int device)
    {
        int held = Held();

        try
        {
            if (!Bass.Init(device, _rate, DeviceInitFlags.Stereo)) return Bass.LastError == Errors.Already;

            Bass.CurrentDevice = device;
            Bass.Free();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            Restore(held);
        }
    }

    /// <summary>Which device this thread was on, or nothing where it was on none.</summary>
    private static int Held()
    {
        try
        {
            return Bass.CurrentDevice;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>Puts the thread back on the device it was on, where there was one.</summary>
    /// <param name="device">What <see cref="Held"/> answered.</param>
    private static void Restore(int device)
    {
        if (device < 0) return;

        try
        {
            Bass.CurrentDevice = device;
        }
        catch (Exception)
        {
        }
    }
}
