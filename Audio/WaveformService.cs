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

        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidOperationException("Invalid WAV file");

        reader.ReadInt32();

        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidOperationException("Invalid WAV file");

        if (new string(reader.ReadChars(4)) != "fmt ")
            throw new InvalidOperationException("Invalid WAV file: missing fmt chunk");

        int fmtSize = reader.ReadInt32();
        ushort audioFormat = reader.ReadUInt16();
        ushort channels = reader.ReadUInt16();
        int sampleRate = reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadUInt16();
        ushort bitsPerSample = reader.ReadUInt16();

        fs.Seek(8 + 4 + fmtSize, SeekOrigin.Current);

        string dataChunkId;
        while ((dataChunkId = new string(reader.ReadChars(4))) != "data")
        {
            int chunkSize = reader.ReadInt32();
            fs.Seek(chunkSize, SeekOrigin.Current);
        }

        int dataSize = reader.ReadInt32();
        byte[] rawData = reader.ReadBytes(dataSize);

        short[] pcmData = new short[dataSize / 2];
        Buffer.BlockCopy(rawData, 0, pcmData, 0, dataSize);

        return (pcmData, sampleRate, (int)channels);
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
                float normalized = Math.Abs(pcmData[i]) / 32768f;
                if (normalized > maxPeak)
                    maxPeak = normalized;
            }

            peaks.Add(maxPeak);
        }

        return peaks.ToArray();
    }
}
