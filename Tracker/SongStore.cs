using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JingleBox2.Tracker;

/// <summary>
/// Reads and writes songs as JSON files, one per song, alongside the recordings.
/// Separate files rather than entries in config.json: a song is a document the user names,
/// copies, and can hand to someone else, and a pad refers to one by path.
/// </summary>
public sealed class SongStore
{
    public const string Extension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public string SongsDirectory { get; }

    public SongStore(string appName = "JingleBox2")
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SongsDirectory = Path.Combine(baseDir, appName, "songs");
        Directory.CreateDirectory(SongsDirectory);
    }

    public string PathFor(string songName) =>
        Path.Combine(SongsDirectory, songName + Extension);

    public IReadOnlyList<string> List() =>
        Directory.Exists(SongsDirectory)
            ? Directory.GetFiles(SongsDirectory, "*" + Extension).OrderBy(p => p).ToArray()
            : Array.Empty<string>();

    /// <summary>Saved songs as name and path, ready for a picker.</summary>
    public IReadOnlyList<SongFile> ListSongs() =>
        List().Select(path => new SongFile(Path.GetFileNameWithoutExtension(path), path)).ToArray();

    public bool Exists(string songName) => File.Exists(PathFor(songName));

    public void Delete(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    public void Save(Song song, string filePath)
    {
        song.Normalize();

        var document = SongDocument.From(song);
        File.WriteAllText(filePath, JsonSerializer.Serialize(document, JsonOptions));
    }

    /// <summary>Loads a song, or null when the file is missing or not a song.</summary>
    public Song? Load(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            var document = JsonSerializer.Deserialize<SongDocument>(File.ReadAllText(filePath), JsonOptions);
            var song = document?.ToSong();
            song?.Normalize();
            return song;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The wire format. Patterns serialize as one string per row rather than an object per
    /// cell, which keeps a 64 line file readable in a text editor and small on disk.
    /// </summary>
    private sealed class SongDocument
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public double Bpm { get; set; } = TrackerTiming.DefaultBpm;
        public int LinesPerBeat { get; set; } = TrackerTiming.DefaultLinesPerBeat;
        public int TrackCount { get; set; } = Song.DefaultTrackCount;
        public List<int> Order { get; set; } = new();
        public List<int> TrackInstruments { get; set; } = new();
        public List<TrackerInstrument> Instruments { get; set; } = new();
        public List<PatternDocument> Patterns { get; set; } = new();

        public static SongDocument From(Song song) => new()
        {
            Name = song.Name,
            Bpm = song.Bpm,
            LinesPerBeat = song.LinesPerBeat,
            TrackCount = song.TrackCount,
            Order = new List<int>(song.Order),
            TrackInstruments = new List<int>(song.TrackInstruments),
            Instruments = song.Instruments.Select(i => i.Clone()).ToList(),
            Patterns = song.Patterns.Select(PatternDocument.From).ToList()
        };

        public Song ToSong()
        {
            var song = new Song
            {
                Name = Name,
                Bpm = Bpm,
                LinesPerBeat = LinesPerBeat,
                TrackCount = TrackCount,
                Order = new List<int>(Order),
                TrackInstruments = new List<int>(TrackInstruments),
                Instruments = Instruments.Select(i => i.Clone()).ToList()
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
