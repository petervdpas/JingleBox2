// ===============================
// Audio/IAudioEngine.cs
// ===============================
using System;
using System.Collections.Generic;
using JingleBox2.Config;
using JingleBox2.Models;

namespace JingleBox2.Audio;

public enum PadPlaybackState
{
    Stopped,
    Playing,
    Error
}

public sealed record PadPlaybackChanged(
    int PadIndex,
    PadPlaybackState State,
    string? Message = null
);

public interface IAudioEngine : IDisposable
{
    int PadCount { get; }

    /// <summary>
    /// The loudest thing the pads are putting out, 0 to 1. Half of the main output meter; the
    /// tracker's own stream is the other half.
    /// </summary>
    float GetOutputLevel();

    IEnumerable<OutputDevice> GetOutputDevices();
    void SetOutputDevice(int deviceId);

    /// <summary>Brings BASS up on the current device. Other players share this one init.</summary>
    void EnsureInitialized();

    event EventHandler<PadPlaybackChanged>? PadPlaybackChanged;

    bool IsPadPlaying(int padIndex);
    double GetPadProgress(int padIndex);
    float GetPadLevel(int padIndex);
    float GetPadChannelVolume(int padIndex);

    void PlaySample(int padIndex, string filePath, float volume);
    void PlayStream(int padIndex, string url, float volume);

    void StopSample(int padIndex);

    void SetPadSource(int padIndex, PadSourceKind kind, string? source);
    void SetPadVolume(int padIndex, float volume);
    void SetPadLoop(int padIndex, bool loop);
    void SetPadFadeIn(int padIndex, double seconds);
    void SetPadFadeOut(int padIndex, double seconds);

    void Resize(int newPadCount);

    /// <summary>
    /// Puts an effect in a pad's path, or takes one off with null. The effect hears that pad
    /// and nothing else, and it stays with the pad across the next thing it plays.
    /// </summary>
    void SetPadInsert(int padIndex, Plugins.IAudioInsert? insert);

    /// <summary>What is on a pad, or null.</summary>
    Plugins.IAudioInsert? GetPadInsert(int padIndex);

    /// <summary>The rate a pad's audio is running at, for a plugin that has to match it.</summary>
    int PadSampleRate(int padIndex);
}
