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
    /// <summary>What the server said, asked the first time anybody wants it.</summary>
    private static readonly Lazy<ServerClock?> Asked = new(Ask);

    /// <summary>How long the server's tool is given to answer.</summary>
    private const int AskMilliseconds = 1000;

    /// <inheritdoc/>
    public ServerClock? Read() => Asked.Value;

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
