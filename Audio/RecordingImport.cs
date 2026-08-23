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
    /// WAV and nothing else, because WAV is what a machine can play. A pad goes through BASS
    /// and will happily take an mp3; an instrument is read into memory by <c>SampleStore</c>,
    /// which decodes WAV alone. Offering the others here would let one be picked, copied in and
    /// put on a zone, where it would then sit silently with nothing to say why.
    /// </remarks>
    public static readonly string[] Kinds = { ".wav" };

    /// <summary>Where JingleBox keeps its recordings.</summary>
    public static string Directory =>
        System.IO.Path.Combine(Config.AppFolder.Path(), "recordings");

    /// <summary>True when this is something worth offering to bring in.</summary>
    public static bool Playable(string path) =>
        Kinds.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Copies each file in and hands back what they became. Files already ours are left where
    /// they are and reported as themselves, so importing twice does not make two.
    /// </summary>
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
                string landed = Path.GetDirectoryName(path) == home ? path : Copy(path, home);

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
                // One file that will not copy is one file, not a failed import.
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
    /// </remarks>
    private static string Copy(string path, string home)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        string suffix = Path.GetExtension(path);

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
    /// </remarks>
    private static void Convert(string path, string wanted)
    {
        WavFile.Stored stored;

        try
        {
            stored = WavFile.StoredAs(path);
        }
        catch (Exception)
        {
            // Not readable as a WAV at all: copied as it is, and it will report itself missing
            // or unplayable later, which is a truer thing to say than a conversion failure here.
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
    public static string Describe(string path)
    {
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
