using JingleBox2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JingleBox2.Audio;

/// <summary>
/// Brings recordings in from anywhere on the disc by copying them into JingleBox's own.
/// </summary>
/// <remarks>
/// The machines only play recordings JingleBox holds, and this is the one door in. A sample
/// that lives in somebody's downloads folder is a song waiting to break: the folder gets tidied
/// and the kit goes silent, which is exactly what happened to the first kit built here. Copied
/// in, the file is ours, and a song depending on it depends on something that will still be
/// there.
///
/// It is also the Emulator's own arrangement, and where the word comes from: you loaded your
/// sounds onto the machine's disk, and after that the machine played from its disk.
/// </remarks>
public static class RecordingImport
{
    /// <summary>What can be brought in.</summary>
    /// <remarks>
    /// WAV first, because that is what the shelf holds, then everything the decoder can turn
    /// into one. A machine still only ever plays a WAV: an instrument is read into memory by
    /// <c>SampleStore</c>, which decodes WAV alone, and the shelf is what it reads from.
    ///
    /// So this is not a list of what a machine can play. It is a list of what can be made into
    /// something a machine can play, on the way in, once, before the file is on the shelf at
    /// all. What is offered follows what is really installed, so nothing is offered here that
    /// would then fail: see <see cref="BassPlugins"/>.
    /// </remarks>
    public static string[] Kinds =>
        new[] { WavKind }.Concat(AudioDecode.Kinds).ToArray();

    /// <summary>What the shelf holds, and what everything written here is written as.</summary>
    private const string WavKind = ".wav";

    /// <summary>Where JingleBox keeps its recordings.</summary>
    public static string Directory =>
        System.IO.Path.Combine(Config.AppFolder.Path(), "recordings");

    /// <summary>True when this is something worth offering to bring in.</summary>
    public static bool Playable(string path) =>
        Kinds.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>True when a file will be rewritten on the way in rather than copied.</summary>
    /// <remarks>
    /// For a panel that wants to say what it did with a file. The answer is the same one
    /// <see cref="Convert"/> acts on, asked without doing anything.
    ///
    /// Anything that is not a WAV is decoded and written out as one, whatever is inside it. A
    /// file that cannot be read as a WAV either is copied as it is, so nothing is converted.
    /// </remarks>
    /// <param name="path">The file, wherever it is.</param>
    public static bool Converts(string path)
    {
        if (AudioDecode.Handles(path)) return true;

        try
        {
            return !WavFile.StoredAs(path).IsOurs;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Copies each file in and hands back what they became. Files already ours are left where
    /// they are and reported as themselves, so importing twice does not make two.
    /// </summary>
    /// <remarks>
    /// Only a WAV already sitting on the shelf is left alone. Anything else is brought in even
    /// from that same folder, since an mp3 lying there is not on the shelf: nothing reads it.
    ///
    /// One file that will not copy is one file rather than a failed import, so the rest still
    /// arrive.
    /// </remarks>
    /// <param name="paths">The files, or null.</param>
    /// <returns>What each became, in the order they were given.</returns>
    public static IReadOnlyList<Recording> Take(IEnumerable<string> paths)
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
    private static string Copy(string path, string home)
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
    private static void Convert(string path, string wanted)
    {
        if (AudioDecode.Handles(path))
        {
            if (AudioDecode.Read(path) is not { } decoded)
                throw new InvalidOperationException(AudioDecode.Trouble(path));

            WavFile.Write(wanted, decoded.Samples, decoded.SampleRate, decoded.Channels);

            return;
        }

        WavFile.Stored stored;

        try
        {
            stored = WavFile.StoredAs(path);
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

        var (samples, info) = WavFile.Read(path);

        WavFile.Write(wanted, samples, info.SampleRate, info.Channels);
    }

    /// <summary>How a file is written, for a panel that wants to say what it did with it.</summary>
    /// <remarks>
    /// A compressed file has no bit depth worth reporting, so what it is called is what it is.
    /// </remarks>
    /// <param name="path">The file.</param>
    public static string Describe(string path)
    {
        if (AudioDecode.Handles(path)) return Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

        try
        {
            return WavFile.StoredAs(path).ToString();
        }
        catch (Exception)
        {
            return "unreadable";
        }
    }
}
