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

    IEnumerable<OutputDevice> GetOutputDevices();
    void SetOutputDevice(int deviceId);

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
}
