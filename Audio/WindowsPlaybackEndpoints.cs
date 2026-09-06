using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using NAudio.CoreAudioApi;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through the system's own endpoint enumerator, which is where the ids come from that Windows
/// wants when it is told where a program should play.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsPlaybackEndpoints : IPlaybackEndpoints
{
    /// <inheritdoc/>
    /// <remarks>
    /// Active ones only. An endpoint that is unplugged or switched off is a place nothing can be
    /// sent to, and offering one would be offering a way to make a program silent for a reason
    /// nobody could see.
    /// </remarks>
    public IReadOnlyList<AudioEndpoint> Outputs()
    {
        var outputs = new List<AudioEndpoint>();

        try
        {
            using var devices = new MMDeviceEnumerator();

            foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device) outputs.Add(new AudioEndpoint(device.ID, device.FriendlyName));
            }
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Audio, () => "outputs: the endpoints could not be read: " + bad.Message);
        }

        return outputs;
    }
}
