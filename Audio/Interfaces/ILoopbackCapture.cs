using System;

namespace JingleBox2.Audio.Interfaces;


/// <summary>
/// Listening to what an output is playing, rather than to something plugged in.
/// </summary>
/// <remarks>
/// A separate capture path from BASS's own recording, because loopback does not come through
/// Bass.RecordStart. Everything above it still meets sixteen bit interleaved audio, so the gain,
/// the clip detection, the meter and the WAV writer are unchanged and only where the audio comes
/// from is different.
///
/// Recording the output rather than an input is what lets somebody capture a stream, a browser or
/// another program without a virtual cable, a Stereo Mix or any extra hardware.
/// </remarks>
public interface ILoopbackCapture : IDisposable
{
    /// <summary>What the capture is actually running at, once it has started.</summary>
    int SampleRate { get; }

    /// <summary>How many channels the audio arrives in, which is always two here.</summary>
    int Channels { get; }

    /// <summary>Whether audio is arriving.</summary>
    bool IsRunning { get; }

    /// <summary>Starts listening to an output.</summary>
    /// <remarks>
    /// The audio arrives as sixteen bit interleaved stereo whatever the device mixes at, so the
    /// caller does not have to know where it came from. A capture already running is stopped
    /// first, since one of these listens to one output.
    /// </remarks>
    /// <param name="device">Which output, from <see cref="WasapiLoopback.GetDevices"/>.</param>
    /// <param name="onAudio">Called from the capture's own thread with each block.</param>
    /// <returns>False where this is not available or the device will not open.</returns>
    bool Start(int device, Action<byte[]> onAudio);

    /// <summary>Stops listening, and does nothing when nothing is being listened to.</summary>
    void Stop();
}
