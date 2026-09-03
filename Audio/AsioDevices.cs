using System;
using System.Collections.Generic;
using JingleBox2.Audio.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using ManagedBass.Asio;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Every call into the library is guarded, because the library may not be there at all: on Linux
/// there is no <c>bassasio</c>, and on Windows it is a file somebody has to have put beside the
/// program. A missing native library throws when the first call is made rather than when the
/// assembly loads, so the only honest way to find out is to ask it something and see.
///
/// Asked once and remembered, since the answer cannot change while the program runs and the
/// question costs a thrown exception the first time it is no.
/// </remarks>
public sealed class AsioDevices : IAsioDevices
{
    /// <summary>The first pair of outputs, which is where a stereo mix goes.</summary>
    private const int FirstPair = 0;

    /// <summary>Guards the driver, which one thread may open while another is closing.</summary>
    private readonly object _lock = new();

    /// <summary>Whether the library answered, or nothing until it has been asked.</summary>
    private bool? _present;

    /// <summary>Why it did not, in words.</summary>
    private string _missing = "";

    /// <summary>Which driver is open, or minus one.</summary>
    private int _open = -1;

    /// <inheritdoc/>
    public bool Present
    {
        get
        {
            lock (_lock)
            {
                if (_present is { } already) return already;

                try
                {
                    _ = BassAsio.DeviceCount;
                    _present = true;
                    _missing = "";
                }
                catch (Exception ex)
                {
                    _present = false;
                    _missing = OperatingSystem.IsWindows()
                        ? "bassasio.dll is not beside the program, so no ASIO driver can be reached."
                        : "ASIO is a Windows standard, so there is none on this system.";

                    Log.Write(LogArea.Audio, () => "asio: not available, " + ex.GetType().Name);
                }

                return _present.Value;
            }
        }
    }

    /// <inheritdoc/>
    public string Missing
    {
        get
        {
            _ = Present;

            lock (_lock) return _missing;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AudioOutput> Devices
    {
        get
        {
            if (!Present) return Array.Empty<AudioOutput>();

            var numbering = new AudioOutputs();
            var found = new List<AudioOutput>();

            try
            {
                for (int index = 0; index < BassAsio.DeviceCount; index++)
                {
                    if (!BassAsio.GetDeviceInfo(index, out var info)) continue;

                    found.Add(new AudioOutput(
                        numbering.Numbered(AudioOutputKind.Asio, index),
                        info.Name ?? ("ASIO " + index),
                        AudioOutputKind.Asio));
                }
            }
            catch (Exception ex)
            {
                Log.Fault(LogArea.Audio, "the ASIO drivers could not be listed", ex);
                return found;
            }

            Log.Write(LogArea.Audio, () => "asio: " + found.Count + " drivers");

            return found;
        }
    }

    /// <inheritdoc/>
    public int Latency
    {
        get
        {
            lock (_lock)
            {
                if (_open < 0) return 0;

                try
                {
                    return Math.Max(0, BassAsio.GetLatency(false));
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool Open(int index, int stream, int rate, int frames)
    {
        if (!Present || index < 0 || stream == 0 || rate <= 0) return false;

        lock (_lock)
        {
            CloseLocked();

            try
            {
                if (!BassAsio.Init(index, AsioInitFlags.Thread))
                {
                    Said("the driver would not open", index);
                    return false;
                }

                _open = index;

                if (!BassAsio.CheckRate(rate))
                {
                    Said("the card will not run at " + rate + " Hz", index);
                    CloseLocked();
                    return false;
                }

                BassAsio.Rate = rate;

                if (!BassAsio.ChannelEnableBass(false, FirstPair, stream, true))
                {
                    Said("the card would not take the mix", index);
                    CloseLocked();
                    return false;
                }

                if (!BassAsio.Start(frames > 0 ? frames : 0, 0))
                {
                    Said("the driver would not start", index);
                    CloseLocked();
                    return false;
                }

                Log.Write(LogArea.Audio, () =>
                    "asio: driver " + index + " is running at " + rate + " Hz, "
                    + (frames > 0 ? frames + " frames a block" : "the driver's own block")
                    + ", " + Latency + " frames behind");

                return true;
            }
            catch (Exception ex)
            {
                Log.Fault(LogArea.Audio, "the ASIO driver could not be opened", ex);
                CloseLocked();
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_lock) CloseLocked();
    }

    /// <summary>Lets the driver go, with the lock already held.</summary>
    /// <remarks>
    /// Stop before free, and both guarded: a driver that has already gone away throws rather than
    /// answering, and this runs on the way out of an output change where nothing may throw.
    /// </remarks>
    private void CloseLocked()
    {
        if (_open < 0) return;

        _open = -1;

        try
        {
            BassAsio.Stop();
            BassAsio.Free();

            Log.Write(LogArea.Audio, "asio: the driver has been let go");
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Audio, "the ASIO driver would not let go", ex);
        }
    }

    /// <summary>Writes down a refusal, with what the library said about it.</summary>
    /// <param name="what">What could not be done.</param>
    /// <param name="index">Which driver it was.</param>
    private static void Said(string what, int index) =>
        Log.Write(LogArea.Audio, () =>
            "asio: driver " + index + ", " + what + ": " + BassAsio.LastError);
}
