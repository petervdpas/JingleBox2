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

        // Write to a sibling file first so a failure part-way cannot destroy the recording.
        string tempPath = filePath + ".trim.tmp";
        try
        {
            WavFile.Write(tempPath, trimmed, info.SampleRate, info.Channels);
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
