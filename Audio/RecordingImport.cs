using JingleBox2.Audio.Records;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class RecordingImport : IRecordingImport
{
    /// <summary>Where the application keeps its things, which the shelf sits under.</summary>
    private readonly Files.Interfaces.IAppFolder _folder = new Files.AppFolder();

    /// <summary>Reading and writing WAV files, which is what everything here ends as.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>Turning everything that is not a WAV into one, at the door.</summary>
    private readonly IAudioDecode _decode = new AudioDecode();

    /// <inheritdoc/>
    public string[] Kinds =>
        new[] { WavKind }.Concat(_decode.Kinds).ToArray();

    /// <summary>What the shelf holds, and what everything written here is written as.</summary>
    private const string WavKind = ".wav";

    /// <inheritdoc/>
    public string Directory =>
        System.IO.Path.Combine(_folder.Path(), "recordings");

    /// <inheritdoc/>
    public bool Playable(string path) =>
        Kinds.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool Converts(string path)
    {
        if (_decode.Handles(path)) return true;

        try
        {
            return !_wav.StoredAs(path).IsOurs;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Recording> Take(IEnumerable<string> paths)
    {
        var taken = new List<Recording>();
        if (paths == null) return taken;

        string home = Directory;
        System.IO.Directory.CreateDirectory(home);

        foreach (string path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !Playable(path)) continue;

            try
            {
                bool already = Path.GetDirectoryName(path) == home && !Converts(path);

                string landed = already ? path : Copy(path, home);

                taken.Add(new Recording
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = Path.GetFileNameWithoutExtension(landed),
                    FilePath = landed,
                    DurationMs = 0,
                    CreatedAt = DateTime.Now
                });
            }
            catch (Exception)
            {
            }
        }

        return taken;
    }

    /// <summary>
    /// Copies one file in under a name nothing else there has.
    /// </summary>
    /// <remarks>
    /// Never overwrites. Two different kits can each have a "kick.wav" and neither should
    /// silently become the other.
    ///
    /// Always written as a WAV whatever came in. What lands here is what the machines read, and a
    /// file called .mp3 holding a WAV would be a lie that something downstream eventually acts on.
    /// </remarks>
    /// <param name="path">The file being brought in.</param>
    /// <param name="home">The recordings folder.</param>
    /// <returns>Where it landed.</returns>
    private string Copy(string path, string home)
    {
        string stem = Path.GetFileNameWithoutExtension(path);

        const string suffix = WavKind;

        string wanted = Path.Combine(home, stem + suffix);
        int at = 2;

        while (File.Exists(wanted))
        {
            wanted = Path.Combine(home, stem + " " + at.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix);
            at++;
        }

        Convert(path, wanted);

        return wanted;
    }

    /// <summary>
    /// Puts one file on the shelf as the sixteen-bit WAV everything here works in, copying it
    /// unchanged when that is already what it is.
    /// </summary>
    /// <remarks>
    /// The shelf holds one format, and this is where that becomes true. A sample folder is full
    /// of 24-bit and float files, and letting one through would mean the trim and the normalise
    /// quietly rewriting it as sixteen bits later, at a moment nobody connected to importing it.
    /// Converting here happens once, at the door, where it can be said out loud.
    ///
    /// Copied byte for byte when it is already ours, so importing a recording this app made
    /// gives back the same file and not a re-encoding of it.
    ///
    /// This is the only place in the program that meets an mp3, and the file it writes is the
    /// only thing anything else will ever see of it. Something that cannot be read as a WAV at all
    /// is copied as it is, and will report itself missing or unplayable later, which is a truer
    /// thing to say than a conversion failure here.
    /// </remarks>
    /// <param name="path">The file being brought in.</param>
    /// <param name="wanted">Where it is to land.</param>
    /// <exception cref="InvalidOperationException">It could not be decoded.</exception>
    private void Convert(string path, string wanted)
    {
        if (_decode.Handles(path))
        {
            if (_decode.Read(path) is not { } decoded)
                throw new InvalidOperationException(_decode.Trouble(path));

            _wav.Write(wanted, decoded.Samples, decoded.SampleRate, decoded.Channels);

            return;
        }

        WavStored stored;

        try
        {
            stored = _wav.StoredAs(path);
        }
        catch (Exception)
        {
            File.Copy(path, wanted);
            return;
        }

        if (stored.IsOurs)
        {
            File.Copy(path, wanted);
            return;
        }

        var (samples, info) = _wav.Read(path);

        _wav.Write(wanted, samples, info.SampleRate, info.Channels);
    }

    /// <inheritdoc/>
    public string Describe(string path)
    {
        if (_decode.Handles(path)) return Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

        try
        {
            return _wav.StoredAs(path).ToString();
        }
        catch (Exception)
        {
            return "unreadable";
        }
    }
}
