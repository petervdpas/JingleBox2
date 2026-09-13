using System;
using System.Collections.Generic;
using System.IO;
using JingleBox2.Audio;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Tracker.Synth;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <param name="listener">What hears the drums, or the rules this application listens by.</param>
/// <param name="wav">What reads and writes the files, or the ordinary WAV reader.</param>
public sealed class DrumChopper(IDrumListener? listener = null, IWavFile? wav = null) : IDrumChopper
{
    /// <summary>How long each file fades in over, so a cut just before a hit does not click.</summary>
    private const double FadeInSeconds = 0.001;

    /// <summary>How long it fades out over, so a cut through what was still ringing does not either.</summary>
    private const double FadeOutSeconds = 0.008;

    /// <summary>What hears the drums.</summary>
    private readonly IDrumListener _listener = listener ?? new DrumListener();

    /// <summary>What reads and writes the files.</summary>
    private readonly IWavFile _wav = wav ?? new WavFile();

    /// <inheritdoc/>
    public string FolderFor(string recording, string chops)
    {
        string name = Path.GetFileNameWithoutExtension(recording ?? "");

        foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, ' ');

        name = name.Trim();

        if (name.Length == 0) name = "Chop";

        string folder = Path.Combine(chops, name);

        for (int number = 2; Directory.Exists(folder) || File.Exists(folder); number++)
            folder = Path.Combine(chops, name + " " + number);

        return folder;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ChoppedDrum> Chop(string recording, string folder, int pads)
    {
        if (string.IsNullOrWhiteSpace(recording) || string.IsNullOrWhiteSpace(folder) || pads <= 0 || !File.Exists(recording))
            return Array.Empty<ChoppedDrum>();

        short[] samples;
        int rate;
        int channels;

        try
        {
            var (read, info) = _wav.Read(recording);

            samples = read;
            rate = info.SampleRate;
            channels = Math.Max(1, info.Channels);
        }
        catch (Exception)
        {
            return Array.Empty<ChoppedDrum>();
        }

        var sample = new SampleData(samples, channels, rate);
        var kit = _listener.Kit(_listener.Listen(sample), pads);

        if (kit.Count == 0) return Array.Empty<ChoppedDrum>();

        Directory.CreateDirectory(folder);

        var chopped = new List<ChoppedDrum>(kit.Count);
        long frames = sample.FrameCount;

        for (int at = 0; at < kit.Count; at++)
        {
            var hit = kit[at];
            string name = _listener.NameOf(kit, at);

            long from = Math.Clamp((long)Math.Floor(hit.Start * frames), 0, frames - 1);
            long to = Math.Clamp((long)Math.Ceiling(hit.End * frames), from + 1, frames);

            var piece = new short[(to - from) * channels];

            Array.Copy(samples, from * channels, piece, 0, piece.Length);

            Fade(piece, channels, rate);

            string file = Path.Combine(folder, name + ".wav");

            _wav.Write(file, piece, rate, channels);

            chopped.Add(new ChoppedDrum(file, name, hit.Sound));
        }

        return chopped;
    }

    /// <summary>Fades a piece in over its first moment and out over its last few.</summary>
    private static void Fade(short[] piece, int channels, int rate)
    {
        long frames = piece.Length / channels;
        long rising = Math.Min(frames / 2, Math.Max(1, (long)(FadeInSeconds * rate)));
        long falling = Math.Min(frames / 2, Math.Max(1, (long)(FadeOutSeconds * rate)));

        for (long frame = 0; frame < rising; frame++)
            for (int c = 0; c < channels; c++)
                piece[(frame * channels) + c] = (short)(piece[(frame * channels) + c] * (frame / (double)rising));

        for (long frame = 0; frame < falling; frame++)
        {
            long at = frames - 1 - frame;

            for (int c = 0; c < channels; c++)
                piece[(at * channels) + c] = (short)(piece[(at * channels) + c] * (frame / (double)falling));
        }
    }
}
