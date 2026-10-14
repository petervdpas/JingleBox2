using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using ManagedBass.Wasapi;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// On Linux PipeWire is asked through its own tool, <c>pw-metadata</c>, which prints the settings
/// the server is running with; a second is allowed, and a machine without PipeWire answers that
/// it does not know. On Windows the default output's shared-mode rate and default period are read
/// through WASAPI. Elsewhere, and wherever asking fails, the answer is nothing.
/// </remarks>
public sealed partial class SoundServerClock : ISoundServerClock
{
    /// <summary>What the real server said, shared by every instance that asks it.</summary>
    private static readonly Kept Shared = new(Ask);

    /// <summary>The answer this instance reads: the shared one, or its own where it was handed a server to ask.</summary>
    private readonly Kept _kept;

    /// <summary>How long the server's tool is given to answer.</summary>
    private const int AskMilliseconds = 1000;

    /// <summary>Reads the real server, or the one handed in.</summary>
    /// <param name="ask">
    /// How to ask a server, for a test that has none. Left out, the real one is asked and the
    /// answer is shared with every other instance in the process.
    /// </param>
    public SoundServerClock(Func<ServerClock?>? ask = null)
    {
        _kept = ask == null ? Shared : new Kept(ask);
    }

    /// <inheritdoc/>
    public ServerClock? Read() => _kept.Read();

    /// <inheritdoc/>
    public ServerClock? ReadAgain() => _kept.ReadAgain();

    /// <summary>An answer asked for once and kept until somebody asks again.</summary>
    /// <param name="ask">How the server is asked.</param>
    private sealed class Kept(Func<ServerClock?> ask)
    {
        /// <summary>Guards the answer, since the tracker and the settings page read it from different moments.</summary>
        private readonly object _gate = new();

        /// <summary>Whether the server has been asked at all.</summary>
        private bool _asked;

        /// <summary>What it said.</summary>
        private ServerClock? _answer;

        /// <summary>The kept answer, asking the first time.</summary>
        public ServerClock? Read()
        {
            lock (_gate)
            {
                if (!_asked) Take();

                return _answer;
            }
        }

        /// <summary>Asks again and keeps what comes back.</summary>
        public ServerClock? ReadAgain()
        {
            lock (_gate)
            {
                Take();

                return _answer;
            }
        }

        /// <summary>Asks and keeps the answer. A server that cannot be asked is nothing, never a fault.</summary>
        private void Take()
        {
            try { _answer = ask(); }
            catch (Exception) { _answer = null; }

            _asked = true;
        }
    }

    /// <inheritdoc/>
    public ServerClock? FromPipeWire(string settings)
    {
        int? rate = null, quantum = null, forcedRate = null, forcedQuantum = null;

        foreach (Match line in Setting().Matches(settings ?? ""))
        {
            if (!int.TryParse(line.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                continue;

            switch (line.Groups["key"].Value)
            {
                case "clock.rate": rate = value; break;
                case "clock.quantum": quantum = value; break;
                case "clock.force-rate": forcedRate = value; break;
                case "clock.force-quantum": forcedQuantum = value; break;
            }
        }

        int finalRate = forcedRate is > 0 ? forcedRate.Value : rate ?? 0;
        int finalQuantum = forcedQuantum is > 0 ? forcedQuantum.Value : quantum ?? 0;

        return finalRate > 0 && finalQuantum > 0 ? new ServerClock(finalRate, finalQuantum) : null;
    }

    /// <summary>One <c>key:'...' value:'...'</c> pair as PipeWire prints it.</summary>
    [GeneratedRegex(@"key:'(?<key>[^']*)'\s+value:'(?<value>[^']*)'")]
    private static partial Regex Setting();

    /// <summary>Asks whatever this platform has.</summary>
    private static ServerClock? Ask()
    {
        try
        {
            if (OperatingSystem.IsLinux()) return AskPipeWire();
            if (OperatingSystem.IsWindows()) return AskWindows();
        }
        catch (Exception)
        {
        }

        return null;
    }

    /// <summary>Runs PipeWire's own tool and reads what it prints.</summary>
    private static ServerClock? AskPipeWire()
    {
        var start = new ProcessStartInfo("pw-metadata", "-n settings 0")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var tool = Process.Start(start);
        if (tool == null) return null;

        var said = tool.StandardOutput.ReadToEndAsync();

        if (!tool.WaitForExit(AskMilliseconds))
        {
            try { tool.Kill(); } catch (Exception) { }
            return null;
        }

        return new SoundServerClock().FromPipeWire(said.Result);
    }

    /// <summary>Reads the default output's shared-mode rate and period through WASAPI.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static ServerClock? AskWindows()
    {
        for (int index = 0; index < BassWasapi.DeviceCount; index++)
        {
            if (!BassWasapi.GetDeviceInfo(index, out var info)) continue;
            if (!info.IsDefault || info.IsInput || info.IsLoopback || !info.IsEnabled) continue;

            int frames = (int)Math.Round(info.DefaultUpdatePeriod * info.MixFrequency);

            return info.MixFrequency > 0 && frames > 0 ? new ServerClock(info.MixFrequency, frames) : null;
        }

        return null;
    }
}
