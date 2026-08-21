using JingleBox2.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Audio;

public interface IWaveformService
{
    WaveformData AnalyzeFile(string filePath);

    /// <summary>Duration of a recording, read from its headers alone.</summary>
    TimeSpan GetDuration(string filePath);

    /// <summary>
    /// Rewrites the file to contain only the frames in [startFrame, endFrame). Destructive:
    /// the original audio outside the region is gone once this returns.
    /// </summary>
    void TrimFile(string filePath, long startFrame, long endFrame);

    /// <summary>
    /// Lifts the whole file so its loudest moment sits on the target, in dBFS. Destructive,
    /// like the trim. Returns how far it moved in decibels, which is zero when the recording
    /// was already there or has nothing in it to lift.
    /// </summary>
    double NormalizeFile(string filePath, double targetDecibels);
}

public sealed class WaveformService : IWaveformService
{
    private const int PixelWidth = 5000;

    public WaveformData AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = WavFile.Read(filePath);

        return new WaveformData
        {
            PeakData = ExtractPeaks(samples, info.Channels, PixelWidth),
            SampleRate = info.SampleRate,
            Channels = info.Channels,
            TotalSamples = info.FrameCount
        };
    }

    public TimeSpan GetDuration(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var info = WavFile.ReadInfo(filePath);
        return TimeSpan.FromSeconds((double)info.FrameCount / info.SampleRate);
    }

    public void TrimFile(string filePath, long startFrame, long endFrame)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = WavFile.Read(filePath);

        startFrame = Math.Clamp(startFrame, 0, info.FrameCount);
        endFrame = Math.Clamp(endFrame, startFrame, info.FrameCount);

        long frames = endFrame - startFrame;
        if (frames <= 0)
            throw new InvalidOperationException("The trim region is empty.");

        if (startFrame == 0 && endFrame == info.FrameCount)
            return; // nothing to cut, leave the file untouched

        var trimmed = new short[frames * info.Channels];
        Array.Copy(samples, startFrame * info.Channels, trimmed, 0, trimmed.Length);

        Write(filePath, trimmed, info, ".trim.tmp");
    }

    public double NormalizeFile(string filePath, double targetDecibels)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = WavFile.Read(filePath);

        double peak = Normalization.PeakOf(samples);
        double gain = Normalization.GainFor(peak, targetDecibels);

        // Already where it should be, or nothing but silence: the file is not worth rewriting.
        if (Math.Abs(gain - 1) < 0.001) return 0;

        Normalization.Apply(samples, gain);
        Write(filePath, samples, info, ".norm.tmp");

        return Normalization.ToDecibels(gain);
    }

    /// <summary>
    /// Writes over a recording through a sibling file, so a failure part way through leaves
    /// the original where it was rather than half of it.
    /// </summary>
    private static void Write(string filePath, short[] samples, WavFile.Info info, string suffix)
    {
        string tempPath = filePath + suffix;

        try
        {
            WavFile.Write(tempPath, samples, info.SampleRate, info.Channels);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    private static float[] ExtractPeaks(short[] samples, int channels, int pixelWidth)
    {
        if (samples.Length == 0) return Array.Empty<float>();

        int framesPerPixel = Math.Max(1, samples.Length / channels / pixelWidth);
        var peaks = new List<float>(pixelWidth);

        for (int pixel = 0; pixel < pixelWidth; pixel++)
        {
            int start = pixel * framesPerPixel * channels;
            int end = Math.Min(start + framesPerPixel * channels, samples.Length);

            float maxPeak = 0;
            for (int i = start; i < end; i++)
            {
                // Widen before Abs: Math.Abs(short.MinValue) throws OverflowException.
                float normalized = Math.Abs((int)samples[i]) / 32768f;
                if (normalized > maxPeak)
                    maxPeak = normalized;
            }

            peaks.Add(maxPeak);
        }

        return peaks.ToArray();
    }
}
