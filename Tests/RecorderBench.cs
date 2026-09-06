using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Routing.Enums;
using JingleBox2.Audio.Routing.Interfaces;
using JingleBox2.Audio.Routing.Records;
using JingleBox2.Config;
using JingleBox2.ViewModels;

namespace JingleBox2.Tests;

/// <summary>
/// A RECORD page with nothing behind it, for the rules on it that are not about audio.
/// </summary>
/// <remarks>
/// The page takes six things and every one of them is a door onto hardware or a disc, so the
/// rules worth asking about, which source may be listened to and what a changed output does to a
/// source taken aside, could not be asked at all without this. Built once here rather than in
/// each test file, since two spellings of a double drift the same way two spellings of anything
/// else do.
/// </remarks>
public sealed class RecorderBench
{
    /// <summary>A source that is a program, which may be listened to.</summary>
    public static readonly AudioRoute Firefox = new("Firefox", "Firefox", AudioRouteKind.Application);

    /// <summary>What an output is playing, which may not: listening to it is a loop.</summary>
    public static readonly AudioRoute Speakers = new("Speakers.monitor", "Speakers", AudioRouteKind.Monitor);

    /// <summary>A routing that answers yes and writes down what it was asked.</summary>
    public sealed class Rewiring : IAudioRouting
    {
        /// <summary>How many times a source was taken off its own output.</summary>
        public int Aside { get; private set; }

        /// <summary>How many times whatever was taken aside was put back.</summary>
        public int Back { get; private set; }

        /// <inheritdoc/>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public IReadOnlyList<AudioRoute> GetRoutes() => new[] { Firefox, Speakers };

        /// <inheritdoc/>
        public AudioRoute? GetCurrentRoute() => Firefox;

        /// <inheritdoc/>
        public bool Connect(AudioRoute route) => true;

        /// <inheritdoc/>
        public bool CanTakeAside => true;

        /// <inheritdoc/>
        public bool TakeAside(AudioRoute route)
        {
            Aside++;

            return true;
        }

        /// <inheritdoc/>
        public void GiveBack() => Back++;
    }

    /// <summary>A recorder that captures nothing and remembers what it was told.</summary>
    public sealed class Deaf : IRecordingService
    {
        /// <inheritdoc/>
        public IReadOnlyList<string> GetInputDevices() => Array.Empty<string>();

        /// <inheritdoc/>
        public string? SelectedDevice { get; set; }

        /// <inheritdoc/>
        public void StartRecording() { }

        /// <inheritdoc/>
        public void StopRecording() { }

        /// <inheritdoc/>
        public bool IsRecording => false;

        /// <inheritdoc/>
        public void StartMonitoring() { }

        /// <inheritdoc/>
        public void StopMonitoring() { }

        /// <inheritdoc/>
        public bool IsMonitoring => false;

        /// <inheritdoc/>
        public string? LastStartWarning => null;

        /// <inheritdoc/>
        public double GainDb { get; set; }

        /// <inheritdoc/>
        public int Channels => 2;

        /// <inheritdoc/>
        public bool IsClipping => false;

        /// <inheritdoc/>
        public bool ClippedDuringTake => false;

        /// <inheritdoc/>
        public byte[] GetCapturedAudio() => Array.Empty<byte>();

        /// <inheritdoc/>
        public byte[] GetRecentRecordingData(int maxBytes) => Array.Empty<byte>();

        /// <inheritdoc/>
        public void ClearCapture() { }

        /// <inheritdoc/>
        public Task<SavedTake> WriteTakeAsync(string folder, string fileName, string cleanName) =>
            Task.FromResult(new SavedTake(string.Empty, null));

        /// <inheritdoc/>
        public JingleBox2.Audio.Plugins.Interfaces.IAudioInsert? Effect { get; set; }

        /// <inheritdoc/>
        public int SampleRate => 44100;

        /// <inheritdoc/>
        public int? LoopbackDevice { get; set; }

        /// <inheritdoc/>
        public IReadOnlyList<LoopbackDevice> GetLoopbackDevices() => Array.Empty<LoopbackDevice>();

        /// <inheritdoc/>
        public IReadOnlyList<AudioProgram> GetPrograms() => Array.Empty<AudioProgram>();

        /// <inheritdoc/>
        public int? LoopbackProgram { get; set; }

        /// <inheritdoc/>
        public void ReopenInput() { }

        /// <inheritdoc/>
        public void HearThrough(IMonitorFeed monitor) => Told = monitor;

        /// <summary>The path it was told about, so a test can say it was told once.</summary>
        public IMonitorFeed? Told { get; private set; }

        /// <inheritdoc/>
        public bool Hearing { get; set; }
    }

    /// <summary>A meter that reads nothing.</summary>
    public sealed class Flat : ILevelMeterService
    {
        /// <inheritdoc/>
        public float GetLevelFromBytes(byte[]? data) => 0f;

        /// <inheritdoc/>
        public float GetLevelFromHandle(int channelHandle) => 0f;

        /// <inheritdoc/>
        public StereoLevel GetStereoFromBytes(byte[]? data, int channels) => new(0f, 0f);

        /// <inheritdoc/>
        public StereoLevel GetStereoFromHandle(int channelHandle) => new(0f, 0f);
    }

    /// <summary>A waveform service nothing here asks anything of.</summary>
    public sealed class Blank : IWaveformService
    {
        /// <inheritdoc/>
        public WaveformData AnalyzeFile(string filePath) => new() { PeakData = Array.Empty<float>() };

        /// <inheritdoc/>
        public TimeSpan GetDuration(string filePath) => TimeSpan.Zero;

        /// <inheritdoc/>
        public long GetFrameCount(string filePath) => 0;

        /// <inheritdoc/>
        public void TrimFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public void SilenceFile(string filePath, long startFrame, long endFrame) { }

        /// <inheritdoc/>
        public double NormalizeFile(string filePath, double targetDecibels) => 0;
    }

    /// <summary>The recorder under the page, so a test can read what the page told it.</summary>
    public Deaf Recorder { get; } = new();

    /// <summary>The machine's wiring, so a test can count what was moved.</summary>
    public Rewiring Wiring { get; } = new();

    /// <summary>The page itself.</summary>
    public RecordViewModel Page { get; }

    /// <summary>Builds the page over the doubles.</summary>
    public RecorderBench() =>
        Page = new RecordViewModel(Recorder, new Flat(), new Blank(), new ConfigStore(), new AppConfig(), Wiring);
}
