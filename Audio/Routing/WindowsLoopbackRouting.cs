using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Routing.Records;

namespace JingleBox2.Audio.Routing;

/// <summary>
/// Choosing what to record on Windows: the capture devices, and what each output is playing.
/// </summary>
/// <remarks>
/// Windows has no graph to patch, so nothing is being rewired here. The list offers what the
/// system can capture, and picking one points the recorder at it: a device through BASS as
/// before, or an output through WASAPI loopback. One program on its own is not in reach this
/// way; that needs per-process loopback, which is a different piece of work.
///
/// Setting the recorder's loopback device reopens the capture, so a route picked here is heard
/// straight away rather than the next time the input happens to be opened.
/// </remarks>
public sealed class WindowsLoopbackRouting : IAudioRouting
{
    /// <summary>
    /// In front of an output's number, for a route that records what that output is playing.
    /// </summary>
    /// <remarks>
    /// Declared rather than composed from a variable, so both kinds of id stay greppable: a
    /// node string is written in one place and read in another, and an id built out of pieces
    /// is one nobody can search for.
    /// </remarks>
    private const string LoopbackPrefix = "loopback:";

    /// <summary>In front of a capture device's name, for a route that records that device.</summary>
    private const string DevicePrefix = "device:";

    /// <summary>The recorder, which is what is actually pointed somewhere: nothing is rewired here.</summary>
    private readonly IRecordingService _recording;

    /// <summary>Takes the recorder this will be pointing at devices and outputs.</summary>
    public WindowsLoopbackRouting(IRecordingService recording) => _recording = recording;

    /// <inheritdoc/>
    /// <remarks>
    /// Windows, and the system really offering loopback. No loopback devices means the add-on
    /// is missing or the system will not do it, and then this offers nothing the recorder's own
    /// device picker does not already, so it stands down rather than showing the same devices
    /// twice.
    /// </remarks>
    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;

            try
            {
                return _recording.GetLoopbackDevices().Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The capture devices first and then the outputs, which is the same reading order the
    /// PipeWire side produces. One running program on its own is not among them: that needs
    /// per-process loopback, which is a different piece of work.
    ///
    /// Anything that goes wrong reading the two lists comes back as no routes at all rather
    /// than half of them, since half a list is a page that looks complete and is not.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Asked of the recorder rather than remembered: a loopback output wins where one is set,
    /// and the selected capture device answers otherwise. The answer is matched back against
    /// the offered list, so the page marks the row it is already showing rather than a second
    /// route that merely says the same thing.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing is rewired: the two prefixes say which of the recorder's two ways of listening
    /// is meant, and the recorder is set accordingly. Pointing at a device clears the loopback
    /// first, or the recorder would go on taking the output it was given before.
    /// </remarks>
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
