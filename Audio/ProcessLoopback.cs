using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through WASAPI's per-process loopback, which is <c>ActivateAudioInterfaceAsync</c> with the
/// activation params saying which process tree to take. That is Windows 10 build 20348 and
/// later, so this reports itself unavailable off Windows and on anything older, and the recorder
/// then keeps to devices and outputs.
///
/// **NAudio does the activation and this does everything else.** What it gives back is a stream
/// of whatever format was asked for, in blocks, on its own thread; what everything above meets
/// is sixteen bit interleaved, so <see cref="ISixteenBit"/> stands between the two. The format
/// cannot be negotiated the ordinary way here, because the virtual device a process loopback
/// activates has no mix format to ask for: what arrives is what was requested or nothing at all.
/// </remarks>
[SupportedOSPlatform("windows10.0.20348.0")]
public sealed class ProcessLoopback : IProgramCapture
{
    /// <summary>What the capture is asked for, since the device cannot be asked what it is.</summary>
    /// <remarks>
    /// The rate everything here is written around, and floats because that is what NAudio's
    /// process loopback path is built to ask for: the ordinary conversion flag is not offered on
    /// that virtual device, so a format it refuses is a capture that does not start at all.
    /// </remarks>
    private const int AskedRate = 44100;

    /// <inheritdoc cref="AskedRate"/>
    private const int AskedChannels = 2;

    /// <summary>Brings each block to the one shape everything above meets.</summary>
    private readonly ISixteenBit _down = new SixteenBit();

    /// <summary>Held while the capture is started or stopped, which can come from either thread.</summary>
    private readonly object _lock = new();

    /// <summary>The capture, while one is running.</summary>
    private WasapiRecorder? _capture;

    /// <summary>What each block is handed to.</summary>
    private Action<byte[]>? _onAudio;

    /// <summary>What the blocks are made of, read off the capture once it exists.</summary>
    private CaptureFormat _format;

    /// <inheritdoc/>
    /// <remarks>
    /// True, and it says nothing about the machine: **this class only exists where the machine
    /// can run it**, since the activation is a Windows call with a build behind it and the
    /// compiler is told so. What decides is <see cref="AudioCapture"/>, which is the one place
    /// that asks the machine and hands back this or the empty one.
    ///
    /// Whether a particular program can actually be captured is still <see cref="Start"/>'s
    /// answer, since the only honest way to know that is to have asked.
    /// </remarks>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public int SampleRate => _format.SampleRate;

    /// <inheritdoc/>
    public int Channels => _format.Channels;

    /// <inheritdoc/>
    public bool IsRunning
    {
        get { lock (_lock) return _capture != null; }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Read off the sessions every output holds, since a program appears there while it is
    /// playing and nowhere else. One program can hold several sessions, so each is named once;
    /// this application's own is left out, which is the one source that can be built by accident
    /// and is a feedback loop when it is.
    /// </remarks>
    public IReadOnlyList<AudioProgram> Programs()
    {
        var found = new List<AudioProgram>();

        if (!IsAvailable) return found;

        var seen = new HashSet<int>();
        int own = Environment.ProcessId;

        try
        {
            using var devices = new MMDeviceEnumerator();

            foreach (var device in devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                using (device)
                {
                    var sessions = device.AudioSessionManager.Sessions;

                    for (int index = 0; index < sessions.Count; index++)
                    {
                        int id = (int)sessions[index].GetProcessID;

                        if (id <= 0 || id == own || !seen.Add(id)) continue;

                        if (Named(id) is { } name) found.Add(new AudioProgram(id, name));
                    }
                }
            }
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Audio, () => "programs: the sessions could not be read: " + bad.Message);
        }

        return found;
    }

    /// <summary>What a process is called, or nothing where it has gone since it was listed.</summary>
    /// <remarks>
    /// A process that ends between the session being read and this being asked is the ordinary
    /// case rather than a fault, and the answer is to leave it out of the list.
    /// </remarks>
    private static string? Named(int id)
    {
        try
        {
            using var program = Process.GetProcessById(id);

            return string.IsNullOrWhiteSpace(program.ProcessName) ? null : program.ProcessName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public bool Start(int processId, Action<byte[]> onAudio)
    {
        if (!IsAvailable || processId <= 0 || onAudio == null) return false;

        Stop();

        lock (_lock)
        {
            try
            {
                var capture = new WasapiRecorderBuilder()
                    .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
                    .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(AskedRate, AskedChannels))
                    .WithMmcssThreadPriority("Pro Audio")
                    .BuildAsync()
                    .GetAwaiter()
                    .GetResult();

                _format = new CaptureFormat(
                    capture.WaveFormat.SampleRate,
                    capture.WaveFormat.Channels,
                    capture.WaveFormat.BitsPerSample,
                    capture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat);

                _onAudio = onAudio;

                capture.DataAvailable += Arrived;
                capture.StartRecording();

                _capture = capture;

                Log.Write(LogArea.Audio, () =>
                    "programs: capturing " + processId + " at " + _format.SampleRate + " Hz, " +
                    _format.Channels + " channels, " + _format.Bits + (_format.Floats ? " bit float" : " bit"));

                return true;
            }
            catch (Exception bad)
            {
                Log.Write(LogArea.Audio, () => "programs: " + processId + " would not be captured: " + bad.Message);

                _capture = null;
                _onAudio = null;

                return false;
            }
        }
    }

    /// <summary>
    /// One block, brought to sixteen bit and handed on.
    /// </summary>
    /// <remarks>
    /// The span is the capture's own buffer and is not ours to keep, so it is copied before
    /// anything else looks at it. A block flagged silent is passed on as it is rather than
    /// skipped: what arrives while a program is quiet is a real stretch of silence, and dropping
    /// it would make a take shorter than the performance.
    /// </remarks>
    private void Arrived(ReadOnlySpan<byte> block, AudioClientBufferFlags flags, long position, long clock)
    {
        Action<byte[]>? onAudio;

        lock (_lock) onAudio = _onAudio;

        if (onAudio == null || block.Length == 0) return;

        var sixteen = _down.Down(block.ToArray(), block.Length, _format);

        if (sixteen.Length > 0) onAudio(sixteen);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        WasapiRecorder? capture;

        lock (_lock)
        {
            capture = _capture;

            _capture = null;
            _onAudio = null;
        }

        if (capture == null) return;

        try
        {
            capture.DataAvailable -= Arrived;
            capture.StopRecording();
            capture.Dispose();
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Audio, () => "programs: the capture would not stop: " + bad.Message);
        }
    }

    /// <inheritdoc/>
    public void Dispose() => Stop();
}
