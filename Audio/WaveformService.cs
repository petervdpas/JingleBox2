using JingleBox2.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace JingleBox2.Audio;

public interface IWaveformService
{
    WaveformData AnalyzeFile(string filePath);
}

public sealed class WaveformService : IWaveformService
{
    public WaveformData AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var (pcmData, sampleRate, channels) = ReadWavFile(filePath);

        int pixelWidth = 5000;
        var peakData = ExtractPeaks(pcmData, channels, pixelWidth);

        return new WaveformData
        {
            PeakData = peakData,
            SampleRate = sampleRate,
            Channels = channels,
            TotalSamples = pcmData.Length / (channels * 2)
        };
    }

    private (short[] pcmData, int sampleRate, int channels) ReadWavFile(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        // Read RIFF header
        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidOperationException("Invalid WAV file");

        int riffSize = reader.ReadInt32();

        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidOperationException("Invalid WAV file");

        // Find fmt chunk
        int sampleRate = 0;
        int channels = 0;

        while (fs.Position < fs.Length)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();
            long chunkStart = fs.Position;

            if (chunkId == "fmt ")
            {
                reader.ReadUInt16(); // audio format (should be 1 for PCM)
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // byte rate
                reader.ReadUInt16(); // block align
                ushort bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                byte[] rawData = reader.ReadBytes(chunkSize);
                short[] pcmData = new short[chunkSize / 2];
                Buffer.BlockCopy(rawData, 0, pcmData, 0, chunkSize);
                return (pcmData, sampleRate, channels);
            }

            fs.Seek(chunkStart + chunkSize, SeekOrigin.Begin);
        }

        throw new InvalidOperationException("Invalid WAV file: missing data chunk");
    }

    private float[] ExtractPeaks(short[] pcmData, int channels, int pixelWidth)
    {
        if (pcmData.Length == 0) return new float[0];

        int samplesPerPixel = Math.Max(1, pcmData.Length / channels / pixelWidth);
        var peaks = new List<float>();

        for (int pixel = 0; pixel < pixelWidth; pixel++)
        {
            int startSample = pixel * samplesPerPixel * channels;
            int endSample = Math.Min(startSample + samplesPerPixel * channels, pcmData.Length);

            float maxPeak = 0;
            for (int i = startSample; i < endSample; i++)
            {
                float normalized = Math.Abs((int)pcmData[i]) / 32768f;
                if (normalized > maxPeak)
                    maxPeak = normalized;
            }

            peaks.Add(maxPeak);
        }

        return peaks.ToArray();
    }
}
