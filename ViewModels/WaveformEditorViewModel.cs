using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Records;
using System;

namespace JingleBox2.ViewModels;

/// <summary>
/// One take's picture with two handles on it, saying where it starts and where it ends.
/// </summary>
/// <remarks>
/// The peak data is held rather than copied, so this is a view over a take and not a second copy
/// of one: several of these over the same recording all read the same array.
///
/// A position is kept in samples and offered in percentages, because those are the two rooms it
/// has to be true in at once. The audio is cut at a sample, and the picture is laid out across
/// whatever width it was given, so a handle that lived in pixels would move when the window was
/// resized and a handle that lived only in samples could not be drawn without the width.
///
/// Nothing reaches this at the moment: the window over a take is dragged on the picture itself,
/// on <c>WaveformView</c>. It is the same pair of decisions written down away from a control, so
/// they can be put a question to without one.
/// </remarks>
public sealed partial class WaveformEditorViewModel : ObservableObject
{
    /// <summary>The take being looked at: its peaks, its length and its rate.</summary>
    private readonly WaveformData _waveformData;

    /// <summary>Where the take is taken to start, counted in samples from its real beginning.</summary>
    [ObservableProperty] private long trimStartSample;

    /// <summary>And where it is taken to end, which is one past the last sample kept.</summary>
    [ObservableProperty] private long trimEndSample;

    /// <summary>True while a hand is on the start handle, so the picture can draw it held.</summary>
    [ObservableProperty] private bool isDraggingStart;

    /// <summary>And on the end handle.</summary>
    [ObservableProperty] private bool isDraggingEnd;

    /// <summary>How long the whole take is, whatever the handles say.</summary>
    public long TotalSamples => _waveformData.TotalSamples;

    /// <summary>The shape to draw, already reduced to peaks rather than every sample.</summary>
    public float[] PeakData => _waveformData.PeakData;

    /// <summary>The take's own rate, which is what turns a sample count into a time.</summary>
    public int SampleRate => _waveformData.SampleRate;

    /// <summary>Where the start handle sits across the picture, 0 to 100.</summary>
    /// <remarks>Nought for a take with no length, since there is nowhere for a handle to be.</remarks>
    public double TrimStartPercent => TotalSamples > 0 ? (double)TrimStartSample / TotalSamples * 100 : 0;

    /// <summary>And the end handle, which is at the far right of an untrimmed take.</summary>
    public double TrimEndPercent => TotalSamples > 0 ? (double)TrimEndSample / TotalSamples * 100 : 100;

    /// <summary>
    /// Opens a take with its handles at the two ends, so it starts saying "all of it".
    /// </summary>
    public WaveformEditorViewModel(WaveformData waveformData)
    {
        _waveformData = waveformData;
        TrimEndSample = waveformData.TotalSamples;
    }

    /// <summary>
    /// Moves the start handle to that fraction across the picture.
    /// </summary>
    /// <remarks>
    /// It is held short of the end handle rather than being allowed to pass it: a window whose
    /// two ends had crossed would be a negative length, and everything downstream of it reads
    /// that as a take of no length rather than as a mistake somebody could see.
    ///
    /// The percentage is what the picture reports, since only the picture knows how wide it was
    /// drawn; the sample is what everything else works in.
    /// </remarks>
    public void SetTrimStart(double percent)
    {
        long sample = (long)(percent / 100 * TotalSamples);
        sample = Math.Max(0, Math.Min(sample, TrimEndSample - 1));
        TrimStartSample = sample;
        OnPropertyChanged(nameof(TrimStartPercent));
    }

    /// <summary>
    /// Moves the end handle, held past the start one and no further than the take goes.
    /// </summary>
    public void SetTrimEnd(double percent)
    {
        long sample = (long)(percent / 100 * TotalSamples);
        sample = Math.Max(TrimStartSample + 1, Math.Min(sample, TotalSamples));
        TrimEndSample = sample;
        OnPropertyChanged(nameof(TrimEndPercent));
    }

    /// <summary>How far into the take the start handle is, in time rather than in samples.</summary>
    public TimeSpan GetTrimStartTime() => TimeSpan.FromSeconds((double)TrimStartSample / SampleRate);

    /// <summary>And the end handle, which is a time on the same clock.</summary>
    public TimeSpan GetTrimEndTime() => TimeSpan.FromSeconds((double)TrimEndSample / SampleRate);

    /// <summary>How long what is kept lasts, which is what a person reads off a trim.</summary>
    public TimeSpan GetTrimDuration() => TimeSpan.FromSeconds((double)(TrimEndSample - TrimStartSample) / SampleRate);
}
