using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Config.Enums;

namespace JingleBox2.Tests;

/// <summary>An audio engine that answers nothing and starts nothing.</summary>
/// <remarks>
/// The clock is what is under test, and it reaches the audio only to say the device should
/// be ready. A song with no instruments never asks for a voice, so nothing here is called
/// but <see cref="EnsureInitialized"/>.
/// </remarks>
internal sealed class QuietAudio : IAudioEngine
{
    /// <inheritdoc/>
    public int PadCount => 0;
    /// <inheritdoc/>
    public float GetOutputLevel() => 0f;
    /// <inheritdoc/>
    public IEnumerable<AudioOutput> GetOutputDevices() => Array.Empty<AudioOutput>();
    /// <inheritdoc/>
    public void SetOutputDevice(int deviceId) { }
    /// <inheritdoc/>
    public void EnsureInitialized() { }
    /// <inheritdoc/>
    public event EventHandler<PadPlaybackChanged>? PadPlaybackChanged { add { } remove { } }
    /// <inheritdoc/>
    public bool IsPadPlaying(int padIndex) => false;
    /// <inheritdoc/>
    public double GetPadProgress(int padIndex) => 0;
    /// <inheritdoc/>
    public float GetPadLevel(int padIndex) => 0f;
    /// <inheritdoc/>
    public float GetPadChannelVolume(int padIndex) => 0f;
    /// <inheritdoc/>
    public IOutputBus Output { get; } = new Nowhere();
    /// <inheritdoc/>
    public IOutputBus PadBus { get; } = new Nowhere();
    /// <inheritdoc/>
    public IOutputBus TakeBus { get; } = new Nowhere();

    /// <inheritdoc/>
    public JingleBox2.Audio.Interfaces.IRecordingSource Recordings { get; } = new NoRecordings();
    /// <inheritdoc/>
    public IOutputBus MonitorBus { get; } = new Nowhere();
    /// <inheritdoc/>
    public JingleBox2.Audio.Interfaces.IMonitorFeed Monitor { get; } = new JingleBox2.Audio.NoMonitorFeed();
    /// <inheritdoc/>
    public void PlaySample(int padIndex, string filePath, float volume) { }
    /// <inheritdoc/>
    public void PlayStream(int padIndex, string url, float volume) { }
    /// <inheritdoc/>
    public void StopSample(int padIndex) { }
    /// <inheritdoc/>
    public void SetPadSource(int padIndex, PadSourceKind kind, string? source) { }
    /// <inheritdoc/>
    public void SetPadVolume(int padIndex, float volume) { }
    /// <inheritdoc/>
    public void SetPadLoop(int padIndex, bool loop) { }
    /// <inheritdoc/>
    public void SetPadFadeIn(int padIndex, double seconds) { }
    /// <inheritdoc/>
    public void SetPadFadeOut(int padIndex, double seconds) { }
    /// <inheritdoc/>
    public void Resize(int newPadCount) { }
    /// <inheritdoc/>
    public void SetPadInsert(int padIndex, IAudioInsert? insert) { }
    /// <inheritdoc/>
    public IAudioInsert? GetPadInsert(int padIndex) => null;
    /// <inheritdoc/>
    public int PadSampleRate(int padIndex) => 48000;
    /// <inheritdoc/>
    public void Dispose() { }

    /// <summary>A bus that never opens, so nothing is ever summed into it.</summary>
    /// <remarks>
    /// The tracker asks its engine for one while it is being built, and what it does with the
    /// answer is decided by <see cref="IsOpen"/>: false is the arrangement this suite wants,
    /// which is a stream that plays itself on a machine that has no card to play it on.
    /// </remarks>
    private sealed class Nowhere : IOutputBus
    {
        /// <inheritdoc/>
        public bool Present => false;
        /// <inheritdoc/>
        public double Pan { get; set; }
        /// <inheritdoc/>
        public bool Mute { get; set; }
        /// <inheritdoc/>
        public int Handle => 0;
        /// <inheritdoc/>
        public int BufferMs { get; set; }
        /// <inheritdoc/>
        public (float Left, float Right) Reading => (0f, 0f);
        /// <inheritdoc/>
        public bool IsOpen => false;
        /// <inheritdoc/>
        public float Level { get; set; } = 1f;
        /// <inheritdoc/>
        public bool Open(int rate, int channels, bool pulled) => false;
        /// <inheritdoc/>
        public bool Add(int source) => false;
        /// <inheritdoc/>
        public void Remove(int source) { }
        /// <inheritdoc/>
        public void HearOnly(IReadOnlyCollection<int> sources) { }
        /// <inheritdoc/>
        /// <inheritdoc/>
        public int Sources => 0;

        public bool Holds(int source) => false;
        /// <inheritdoc/>
        public void Close() { }
        /// <inheritdoc/>
        public void Dispose() { }
    }
}
