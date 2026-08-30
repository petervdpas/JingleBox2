using JingleBox2.Audio.Records;
using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class WaveformService : IWaveformService
{
    /// <summary>Reading and writing WAV files. Holds nothing, so one serves the whole object.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>The peak normalisation rules. Holds nothing, so one serves the whole object.</summary>
    private readonly INormalization _levels = new Normalization();

    /// <summary>
    /// How many columns a picture holds, whatever the recording's length. Wide enough that a
    /// waveform drawn across a full screen is reading real peaks rather than an interpolation.
    /// </summary>
    private const int PixelWidth = 5000;

    /// <inheritdoc/>
    public WaveformData AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = _wav.Read(filePath);

        return new WaveformData
        {
            PeakData = ExtractPeaks(samples, info.Channels, PixelWidth),
            SampleRate = info.SampleRate,
            Channels = info.Channels,
            TotalSamples = info.FrameCount
        };
    }

    /// <inheritdoc/>
    public TimeSpan GetDuration(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var info = _wav.ReadInfo(filePath);
        return TimeSpan.FromSeconds((double)info.FrameCount / info.SampleRate);
    }

    /// <inheritdoc/>
    public long GetFrameCount(string filePath)
    {
        if (!File.Exists(filePath)) return 0;

        return _wav.ReadInfo(filePath).FrameCount;
    }

    /// <inheritdoc/>
    public void TrimFile(string filePath, long startFrame, long endFrame)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = _wav.Read(filePath);

        startFrame = Math.Clamp(startFrame, 0, info.FrameCount);
        endFrame = Math.Clamp(endFrame, startFrame, info.FrameCount);

        long frames = endFrame - startFrame;
        if (frames <= 0)
            throw new InvalidOperationException("The trim region is empty.");

        if (startFrame == 0 && endFrame == info.FrameCount)
            return;

        var trimmed = new short[frames * info.Channels];
        Array.Copy(samples, startFrame * info.Channels, trimmed, 0, trimmed.Length);

        Write(filePath, trimmed, info, ".trim.tmp");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Read whole, zeroed in place and written back through the same temporary file the trim
    /// uses, so a failure part way leaves the take as it was.
    /// </remarks>
    public void SilenceFile(string filePath, long startFrame, long endFrame)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = _wav.Read(filePath);

        startFrame = Math.Clamp(startFrame, 0, info.FrameCount);
        endFrame = Math.Clamp(endFrame, startFrame, info.FrameCount);

        long frames = endFrame - startFrame;
        if (frames <= 0)
            throw new InvalidOperationException("There is nothing selected to silence.");

        Array.Clear(samples, (int)(startFrame * info.Channels), (int)(frames * info.Channels));

        Write(filePath, samples, info, ".silence.tmp");
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A recording already on the target, or holding nothing but silence, is left where it is
    /// rather than rewritten to say the same thing.
    /// </remarks>
    public double NormalizeFile(string filePath, double targetDecibels)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (samples, info) = _wav.Read(filePath);

        double peak = _levels.PeakOf(samples);
        double gain = _levels.GainFor(peak, targetDecibels);

        if (Math.Abs(gain - 1) < 0.001) return 0;

        _levels.Apply(samples, gain);
        Write(filePath, samples, info, ".norm.tmp");

        return _levels.ToDecibels(gain);
    }

    /// <summary>
    /// Writes over a recording through a sibling file, so a failure part way through leaves
    /// the original where it was rather than half of it.
    /// </summary>
    private void Write(string filePath, short[] samples, WavInfo info, string suffix)
    {
        string tempPath = filePath + suffix;

        try
        {
            _wav.Write(tempPath, samples, info.SampleRate, info.Channels);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    /// <summary>The loudest sample under each column of the picture.</summary>
    /// <remarks>
    /// Each column's stretch is worked out from the column rather than by stepping a fixed number
    /// of frames, so the columns cover the whole recording and the last one really is the end of
    /// it. A fixed step throws away whatever the division left over, which puts every position
    /// read off the picture out by that much, worst at the end where the error has had the whole
    /// file to build up. A recording shorter than the picture is wide still gets a frame per
    /// column, so a short take is drawn across the width instead of squeezed into the left. Each
    /// sample is widened to an int before Abs, since Abs(short.MinValue) throws.
    /// </remarks>
    private static float[] ExtractPeaks(short[] samples, int channels, int pixelWidth)
    {
        if (samples.Length == 0) return Array.Empty<float>();

        long frames = samples.Length / channels;
        var peaks = new List<float>(pixelWidth);

        for (int pixel = 0; pixel < pixelWidth; pixel++)
        {
            long from = frames * pixel / pixelWidth;
            long to = frames * (pixel + 1) / pixelWidth;

            if (to <= from) to = Math.Min(frames, from + 1);

            int start = (int)(from * channels);
            int end = (int)Math.Min(to * channels, samples.Length);

            float maxPeak = 0;
            for (int i = start; i < end; i++)
            {
                float normalized = Math.Abs((int)samples[i]) / 32768f;
                if (normalized > maxPeak)
                    maxPeak = normalized;
            }

            peaks.Add(maxPeak);
        }

        return peaks.ToArray();
    }
}
