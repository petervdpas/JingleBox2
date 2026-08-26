using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace JingleBox2.Tracker;

/// <summary>
/// Reads and writes songs, one file per song, alongside the recordings.
/// </summary>
/// <remarks>
/// Separate files rather than entries in config.json: a song is a document the user names,
/// copies, and can hand to someone else, and a pad refers to one by path.
///
/// A song file is a zip, and what is in it is this:
/// <code>
/// song.json      the patterns, the order, the mix, the instruments
/// state/00.bin   what a plugin instrument saved, as the plugin handed it over
/// </code>
///
/// One file with the patches inside it was the obvious thing and it was the wrong thing. A
/// plugin's state is the bulk of a song by a wide margin: of one song here, 348 KB, the music
/// is 781 bytes and one synth's patch is 331 KB of it, base64, which is a third larger than the
/// bytes it stands for and has to be encoded on the way out and decoded on the way in. Worse,
/// it was in the same document as the patterns, and a document is all or nothing: a patch that
/// came back damaged from a plugin did not cost the patch, it cost the song.
///
/// Kept apart, a patch is read as the bytes it is, straight into the plugin that wants it, and
/// a patch that will not read costs that instrument its sound and nothing else. song.json stays
/// small enough to read in a text editor and to parse before anything heavy is touched, which
/// is what lets the plugins a song needs be started while the rest of it is still loading.
/// </remarks>
public sealed class SongStore : ISampleUsage
{
    public const string Extension = ".jibx";

    /// <summary>What songs were called when a song was one JSON file.</summary>
    private const string OldExtension = ".json";

    /// <summary>What a converted one is left called, so it is kept but no longer found.</summary>
    private const string RetiredExtension = ".json.old";

    private const string SongEntry = "song.json";
    private const string StateFolder = "state/";
    private const string StateExtension = ".bin";

    // Defaults are written out rather than skipped. A synth patch is full of settings whose
    // zero is a real choice (no attack, no sustain, no vibrato), and skipping them would let
    // the property initializers put their own values back on load.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SongsDirectory { get; }

    public SongStore(string appName = "JingleBox2")
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SongsDirectory = Path.Combine(baseDir, appName, "songs");
        Directory.CreateDirectory(SongsDirectory);

        BringOldSongsAcross();
    }

    /// <summary>
    /// Songs written before a song was a container, turned into one, once each.
    /// </summary>
    /// <remarks>
    /// The old file is a song.json with the patches left inside it, which is exactly what this
    /// reads out of the container, so bringing one across is reading it and saving it. Done
    /// here rather than offered, because a song nobody converts is a song that has quietly
    /// disappeared from the list.
    ///
    /// The original is kept, renamed so it is not found again. Anything that goes wrong with
    /// one song is that song's own business: the rest still come across, and one that will not
    /// read is left alone under its own name for somebody to look at.
    /// </remarks>
    private void BringOldSongsAcross()
    {
        string[] old;

        try { old = Directory.GetFiles(SongsDirectory, "*" + OldExtension); }
        catch (Exception) { return; }

        foreach (string path in old)
        {
            string wanted = Path.ChangeExtension(path, Extension);

            try
            {
                // Already brought across, and the container is the one that counts. Converting
                // again would put the old work back on top of the new.
                if (File.Exists(wanted)) continue;

                var document = JsonSerializer.Deserialize<SongDocument>(File.ReadAllText(path), JsonOptions);
                var song = document?.ToSong();
                if (song == null) continue;

                song.Normalize();
                Save(song, wanted);

                File.Move(path, Path.ChangeExtension(path, null) + RetiredExtension, overwrite: true);
            }
            catch (Exception)
            {
            }
        }
    }

    public string PathFor(string songName) =>
        Path.Combine(SongsDirectory, songName + Extension);

    public IReadOnlyList<string> List() =>
        Directory.Exists(SongsDirectory)
            ? Directory.GetFiles(SongsDirectory, "*" + Extension).OrderBy(p => p).ToArray()
            : Array.Empty<string>();

    /// <summary>Saved songs as name, path and what they say about themselves, for a picker.</summary>
    public IReadOnlyList<SongFile> ListSongs() =>
        List()
            .Select(path => new SongFile(Path.GetFileNameWithoutExtension(path), path, DescriptionIn(path)))
            .ToArray();

    /// <summary>
    /// What one song says about itself, without loading the song.
    /// </summary>
    /// <remarks>
    /// Read rather than remembered anywhere else, so the description a song carries is the one
    /// the list shows even when the file was written by another machine or edited by hand.
    /// A song that will not parse simply has nothing to say, since this is a list to read and
    /// not the load that has to report a broken file.
    /// </remarks>
    private static string DescriptionIn(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var container = new ZipArchive(file, ZipArchiveMode.Read);

            var entry = container.GetEntry(SongEntry);
            if (entry == null) return "";

            using var reading = entry.Open();
            using var document = JsonDocument.Parse(reading);

            return document.RootElement.TryGetProperty(nameof(SongDocument.Description), out var said)
                   && said.ValueKind == JsonValueKind.String
                ? said.GetString() ?? ""
                : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    public bool Exists(string songName) => File.Exists(PathFor(songName));

    public void Delete(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    /// <summary>
    /// The instruments in saved songs that play a recording, said as "instrument in song".
    /// </summary>
    /// <remarks>
    /// Songs are asked as well as the rack, because a recording is deleted from RECORD and the
    /// rack is not the only thing built on one. A take nothing on the rack uses but three songs
    /// do read as free, and deleting it emptied those three tracks with nothing said. Nothing
    /// reported it afterwards either: the songs still opened, and three instruments were simply
    /// silent.
    ///
    /// Only song.json is read, never the patches and never the audio, so asking this about
    /// every song in the folder costs a few milliseconds however large the songs are.
    /// </remarks>
    public IReadOnlyList<string> InstrumentsUsing(string filePath)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(filePath)) return names;

        foreach (string path in List())
        {
            var (song, instruments) = Playing(path);
            if (instruments.Count == 0) continue;

            foreach (string name in SampleUsage.By(instruments, filePath))
                names.Add(name + " in '" + song + "'");
        }

        return names;
    }

    /// <summary>
    /// Points every saved song playing one recording at another, for a take that was renamed.
    /// </summary>
    /// <remarks>
    /// Only song.json is rewritten. The patches and any recordings the song carries are left
    /// exactly as they are rather than read out and written back, so renaming a take does not
    /// mean rebuilding a forty megabyte file. The whole thing still lands in one move, because
    /// half a rewritten song is not a song.
    /// </remarks>
    public int Repoint(string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return 0;

        int moved = 0;

        foreach (string path in List())
        {
            try
            {
                if (Repointed(path, from, to)) moved++;
            }
            catch (Exception)
            {
                // A song that will not open is not one this rename can fix.
            }
        }

        return moved;
    }

    /// <summary>
    /// What each song was holding when it was last read, and when that was.
    /// </summary>
    /// <remarks>
    /// RECORD marks every take on the shelf with what plays it, and asks once per take. Without
    /// this, a shelf of fifty takes and a folder of ten songs opened a hundred and fifty zip
    /// files every time the rack changed, and it got worse with every song ever written. A song
    /// is read once and read again when it is written, which is what the timestamp is for.
    /// </remarks>
    private readonly Dictionary<string, (DateTime Written, string Song, IReadOnlyList<TrackerInstrument> Instruments)> _read =
        new(FilePaths.Comparer);

    private readonly object _readLock = new();

    /// <summary>One song's name and the instruments it holds, without opening anything heavy.</summary>
    private (string Song, IReadOnlyList<TrackerInstrument> Instruments) Playing(string path)
    {
        DateTime written;

        try { written = File.GetLastWriteTimeUtc(path); }
        catch (Exception) { return ("", Array.Empty<TrackerInstrument>()); }

        lock (_readLock)
        {
            if (_read.TryGetValue(path, out var held) && held.Written == written)
                return (held.Song, held.Instruments);
        }

        var found = Held(path);

        lock (_readLock) _read[path] = (written, found.Song, found.Instruments);

        return found;
    }

    /// <summary>The same, read off the disc rather than remembered.</summary>
    private static (string Song, IReadOnlyList<TrackerInstrument> Instruments) Held(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var container = new ZipArchive(file, ZipArchiveMode.Read);

            var document = Written(container);
            if (document == null) return ("", Array.Empty<TrackerInstrument>());

            foreach (var instrument in document.Instruments) SongPaths.UnpackInto(instrument);

            return (Path.GetFileNameWithoutExtension(path), document.Instruments);
        }
        catch (Exception)
        {
            return ("", Array.Empty<TrackerInstrument>());
        }
    }

    /// <summary>Rewrites one song's song.json if it plays that recording. True when it did.</summary>
    private static bool Repointed(string path, string from, string to)
    {
        SongDocument? document;

        using (var file = File.OpenRead(path))
        using (var container = new ZipArchive(file, ZipArchiveMode.Read))
            document = Written(container);

        if (document == null) return false;

        bool moved = false;

        foreach (var instrument in document.Instruments)
        {
            SongPaths.UnpackInto(instrument);
            if (SampleUsage.Repoint(instrument, from, to)) moved = true;
            SongPaths.PackInto(instrument);
        }

        if (!moved) return false;

        // Beside it and moved on top, so what is there is the whole old song or the whole new
        // one. Updating the file where it lies would leave neither if anything went wrong.
        string writing = path + ".writing";

        try
        {
            File.Copy(path, writing, overwrite: true);

            using (var file = File.Open(writing, FileMode.Open, FileAccess.ReadWrite))
            using (var container = new ZipArchive(file, ZipArchiveMode.Update))
            {
                container.GetEntry(SongEntry)?.Delete();

                var entry = container.CreateEntry(SongEntry, CompressionLevel.Optimal);
                using var said = entry.Open();
                JsonSerializer.Serialize(said, document, JsonOptions);
            }

            File.Move(writing, path, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            try { if (File.Exists(writing)) File.Delete(writing); } catch (Exception) { }
            throw;
        }
    }

    /// <summary>What song.json says, and nothing else out of the container.</summary>
    private static SongDocument? Written(ZipArchive container)
    {
        var entry = container.GetEntry(SongEntry);
        if (entry == null) return null;

        using var reading = entry.Open();
        return JsonSerializer.Deserialize<SongDocument>(reading, JsonOptions);
    }

    /// <summary>What one instrument's patch is called inside the file.</summary>
    /// <remarks>
    /// By its place in the list rather than by its name or its id. A name is the user's and can
    /// be anything a file name cannot be, an id would have to be trusted to be one; the list is
    /// written and read in the same order by the same code, and the number is what the reader
    /// already has in its hand.
    /// </remarks>
    private static string StateName(int index) =>
        StateFolder + index.ToString("00", CultureInfo.InvariantCulture) + StateExtension;

    /// <summary>
    /// Writes a song, and when asked, the recordings it plays along with it.
    /// </summary>
    /// <remarks>
    /// Packing is the deliberate act, not the default. An ordinary save names its recordings,
    /// which is what keeps it in milliseconds and what keeps the twenty second keep from
    /// writing tens of megabytes behind your back. See <see cref="SongSamples"/> for what
    /// travels and what is left named.
    /// </remarks>
    public void Save(Song song, string filePath, bool withSamples = false)
    {
        song.Normalize();

        // Read off the song itself, while its paths are still this machine's.
        var carrying = withSamples ? SongSamples.Wanted(song) : Array.Empty<string>();

        var document = SongDocument.From(song);
        var states = document.TakeStatesOut();

        Config.SafeFile.Write(filePath, stream =>
        {
            using var container = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

            var entry = container.CreateEntry(SongEntry, CompressionLevel.Optimal);
            using (var writing = entry.Open())
                JsonSerializer.Serialize(writing, document, JsonOptions);

            // Compressed quickly rather than well. A patch is the one thing here big enough
            // for the difference to be felt, and it is felt on every save.
            foreach (var (index, bytes) in states)
            {
                var patch = container.CreateEntry(StateName(index), CompressionLevel.Fastest);
                using var writing = patch.Open();
                writing.Write(bytes, 0, bytes.Length);
            }

            SongSamples.Write(container, carrying);
        });
    }

    /// <summary>Loads a song, or null when the file is missing or not a song.</summary>
    public Song? Load(string filePath) => Load(filePath, out _);

    /// <summary>
    /// The same, saying what recordings the song brought with it.
    /// </summary>
    /// <remarks>
    /// A packed song puts its recordings on the shelf as it opens, so the shelf has to be told
    /// to look again. What comes back is what was not already there: opening the same packed
    /// song twice adds nothing the second time.
    /// </remarks>
    public Song? Load(string filePath, out IReadOnlyList<string> arrived)
    {
        arrived = Array.Empty<string>();

        try
        {
            if (!File.Exists(filePath)) return null;

            using var file = File.OpenRead(filePath);
            using var container = new ZipArchive(file, ZipArchiveMode.Read);

            var entry = container.GetEntry(SongEntry);
            if (entry == null) return null;

            SongDocument? document;

            using (var reading = entry.Open())
                document = JsonSerializer.Deserialize<SongDocument>(reading, JsonOptions);

            if (document == null) return null;

            PutStatesBack(container, document);

            var song = document.ToSong();
            arrived = SongSamples.Read(container, song);

            song.Normalize();
            return song;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Gives each plugin instrument its patch back.
    /// </summary>
    /// <remarks>
    /// One at a time, and one that will not come out is passed over. This is the whole point of
    /// keeping them out of the document: an instrument with no patch opens on the plugin's own
    /// defaults, where a document that will not parse is a song nobody has any more.
    /// </remarks>
    private static void PutStatesBack(ZipArchive container, SongDocument document)
    {
        for (int index = 0; index < document.Instruments.Count; index++)
        {
            var entry = container.GetEntry(StateName(index));
            if (entry == null) continue;

            try
            {
                var bytes = new byte[entry.Length];

                using var reading = entry.Open();
                reading.ReadExactly(bytes);

                document.Instruments[index].PluginState = bytes;
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>
    /// What song.json holds. Patterns serialize as one string per row rather than an object per
    /// cell, which keeps a 64 line file readable in a text editor and small on disk.
    /// </summary>
    private sealed class SongDocument
    {
        /// <summary>2 since the patches moved out of here and into the container.</summary>
        public int Version { get; set; } = 2;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double Bpm { get; set; } = TrackerTiming.DefaultBpm;
        public int LinesPerBeat { get; set; } = TrackerTiming.DefaultLinesPerBeat;
        public int KeyboardOctave { get; set; } = 4;
        public int TrackCount { get; set; } = Song.DefaultTrackCount;
        public List<int> Order { get; set; } = new();
        public List<int> TrackInstruments { get; set; } = new();
        public List<TrackMix> Mix { get; set; } = new();

        /// <summary>This song's own controller layout. See <see cref="Song.Controls"/>.</summary>
        public List<Midi.ControlMapping> Controls { get; set; } = new();
        public List<TrackerInstrument> Instruments { get; set; } = new();
        public List<PatternDocument> Patterns { get; set; } = new();

        public static SongDocument From(Song song) => new()
        {
            Name = song.Name,
            Description = song.Description,
            Bpm = song.Bpm,
            LinesPerBeat = song.LinesPerBeat,
            KeyboardOctave = song.KeyboardOctave,
            TrackCount = song.TrackCount,
            Order = new List<int>(song.Order),
            TrackInstruments = new List<int>(song.TrackInstruments),
            Mix = song.Mix.Select(m => m.Clone()).ToList(),
            Controls = song.Controls.Select(Midi.ControlMapping.Copy).ToList(),
            Instruments = song.Instruments.Select(Written).ToList(),
            Patterns = song.Patterns.Select(PatternDocument.From).ToList()
        };

        /// <summary>One instrument as the file should hold it: a copy, with portable paths.</summary>
        private static TrackerInstrument Written(TrackerInstrument instrument)
        {
            var copy = instrument.Clone();
            SongPaths.PackInto(copy);
            return copy;
        }

        /// <summary>
        /// Lifts every patch out of the document, to be written beside it.
        /// </summary>
        /// <remarks>
        /// Safe to empty what it takes, because the instruments here are already copies: the
        /// song being saved keeps its patches and goes on playing them.
        /// </remarks>
        public List<(int Index, byte[] Bytes)> TakeStatesOut()
        {
            var states = new List<(int Index, byte[] Bytes)>();

            for (int index = 0; index < Instruments.Count; index++)
            {
                var bytes = Instruments[index].PluginState;
                if (bytes == null || bytes.Length == 0) continue;

                states.Add((index, bytes));
                Instruments[index].PluginState = Array.Empty<byte>();
            }

            return states;
        }

        /// <summary>One instrument as this machine has it: a copy, with real paths.</summary>
        private static TrackerInstrument Read(TrackerInstrument instrument)
        {
            var copy = instrument.Clone();
            SongPaths.UnpackInto(copy);
            return copy;
        }

        public Song ToSong()
        {
            var song = new Song
            {
                Name = Name,
                Description = Description,
                Bpm = Bpm,
                LinesPerBeat = LinesPerBeat,
                KeyboardOctave = KeyboardOctave,
                TrackCount = TrackCount,
                Order = new List<int>(Order),
                TrackInstruments = new List<int>(TrackInstruments),
                Mix = Mix.Select(m => m.Clone()).ToList(),
                Controls = Controls.Select(Midi.ControlMapping.Copy).ToList(),
                Instruments = Instruments.Select(Read).ToList()
            };

            song.Patterns = Patterns.Select(p => p.ToPattern(TrackCount)).ToList();
            return song;
        }
    }

    private sealed class PatternDocument
    {
        public string Name { get; set; } = "";
        public int Lines { get; set; } = Pattern.DefaultLines;

        /// <summary>One entry per used cell, as "line:track:cell". Blank cells are not stored.</summary>
        public List<string> Cells { get; set; } = new();

        public static PatternDocument From(Pattern pattern)
        {
            var document = new PatternDocument { Name = pattern.Name, Lines = pattern.Lines };

            for (int line = 0; line < pattern.Lines; line++)
                for (int track = 0; track < pattern.TrackCount; track++)
                {
                    var cell = pattern[line, track];
                    if (cell.IsEmpty) continue;

                    document.Cells.Add($"{line}:{track}:{TrackerCellText.Write(cell)}");
                }

            return document;
        }

        public Pattern ToPattern(int trackCount)
        {
            var pattern = new Pattern(Lines, trackCount) { Name = Name };

            foreach (var entry in Cells)
            {
                var parts = entry.Split(':', 3);
                if (parts.Length != 3) continue;
                if (!int.TryParse(parts[0], out int line)) continue;
                if (!int.TryParse(parts[1], out int track)) continue;
                if (!pattern.Contains(line, track)) continue;
                if (!TrackerCellText.TryRead(parts[2], out var cell)) continue;

                pattern[line, track] = cell;
            }

            return pattern;
        }
    }
}
