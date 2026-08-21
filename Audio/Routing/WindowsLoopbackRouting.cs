using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JingleBox2.Audio.Routing;

/// <summary>
/// Choosing what to record on Windows: the capture devices, and what each output is playing.
/// </summary>
/// <remarks>
/// Windows has no graph to patch, so nothing is being rewired here. The list offers what the
/// system can capture, and picking one points the recorder at it: a device through BASS as
/// before, or an output through WASAPI loopback. One program on its own is not in reach this
/// way; that needs per-process loopback, which is a different piece of work.
/// </remarks>
public sealed class WindowsLoopbackRouting : IAudioRouting
{
    // Declared, not composed from a variable, so the two kinds of id stay greppable.
    private const string LoopbackPrefix = "loopback:";
    private const string DevicePrefix = "device:";

    private readonly IRecordingService _recording;

    public WindowsLoopbackRouting(IRecordingService recording) => _recording = recording;

    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;

            try
            {
                // No loopback devices means the add-on is missing or the system will not do it,
                // and then this offers nothing the device picker does not already.
                return _recording.GetLoopbackDevices().Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public IReadOnlyList<AudioRoute> GetRoutes()
    {
        if (!IsAvailable) return Array.Empty<AudioRoute>();

        var routes = new List<AudioRoute>();

        try
        {
            foreach (var device in _recording.GetInputDevices())
                routes.Add(new AudioRoute(DevicePrefix + device, device, AudioRouteKind.Input));

            foreach (var output in _recording.GetLoopbackDevices())
            {
                routes.Add(new AudioRoute(
                    LoopbackPrefix + output.Index.ToString(CultureInfo.InvariantCulture),
                    output.Name,
                    AudioRouteKind.Monitor));
            }
        }
        catch (Exception)
        {
            return Array.Empty<AudioRoute>();
        }

        return routes;
    }

    public AudioRoute? GetCurrentRoute()
    {
        if (!IsAvailable) return null;

        var routes = GetRoutes();

        if (_recording.LoopbackDevice is int loopback)
        {
            string node = LoopbackPrefix + loopback.ToString(CultureInfo.InvariantCulture);
            return routes.FirstOrDefault(r => r.Node == node);
        }

        string? device = _recording.SelectedDevice;
        if (string.IsNullOrEmpty(device)) return null;

        return routes.FirstOrDefault(r => r.Node == DevicePrefix + device);
    }

    public bool Connect(AudioRoute route)
    {
        if (!IsAvailable || route == null) return false;

        try
        {
            if (route.Node.StartsWith(LoopbackPrefix, StringComparison.Ordinal))
            {
                string index = route.Node[LoopbackPrefix.Length..];
                if (!int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int device))
                    return false;

                // The setter reopens the capture, so the change is heard straight away.
                _recording.LoopbackDevice = device;
                return true;
            }

            if (!route.Node.StartsWith(DevicePrefix, StringComparison.Ordinal)) return false;

            _recording.LoopbackDevice = null;
            _recording.SelectedDevice = route.Node[DevicePrefix.Length..];
            _recording.ReopenInput();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
