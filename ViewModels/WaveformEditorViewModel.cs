using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Models;
using System;

namespace JingleBox2.ViewModels;

public sealed partial class WaveformEditorViewModel : ObservableObject
{
    private readonly WaveformData _waveformData;

    [ObservableProperty] private long trimStartSample;
    [ObservableProperty] private long trimEndSample;
    [ObservableProperty] private bool isDraggingStart;
    [ObservableProperty] private bool isDraggingEnd;

    public long TotalSamples => _waveformData.TotalSamples;
    public float[] PeakData => _waveformData.PeakData;
    public int SampleRate => _waveformData.SampleRate;

    public double TrimStartPercent => TotalSamples > 0 ? (double)TrimStartSample / TotalSamples * 100 : 0;
    public double TrimEndPercent => TotalSamples > 0 ? (double)TrimEndSample / TotalSamples * 100 : 100;

    public WaveformEditorViewModel(WaveformData waveformData)
    {
        _waveformData = waveformData;
        TrimEndSample = waveformData.TotalSamples;
    }

    public void SetTrimStart(double percent)
    {
        long sample = (long)(percent / 100 * TotalSamples);
        sample = Math.Max(0, Math.Min(sample, TrimEndSample - 1));
        TrimStartSample = sample;
        OnPropertyChanged(nameof(TrimStartPercent));
    }

    public void SetTrimEnd(double percent)
    {
        long sample = (long)(percent / 100 * TotalSamples);
        sample = Math.Max(TrimStartSample + 1, Math.Min(sample, TotalSamples));
        TrimEndSample = sample;
        OnPropertyChanged(nameof(TrimEndPercent));
    }

    public TimeSpan GetTrimStartTime() => TimeSpan.FromSeconds((double)TrimStartSample / SampleRate);
    public TimeSpan GetTrimEndTime() => TimeSpan.FromSeconds((double)TrimEndSample / SampleRate);
    public TimeSpan GetTrimDuration() => TimeSpan.FromSeconds((double)(TrimEndSample - TrimStartSample) / SampleRate);
}
