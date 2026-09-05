using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using JingleBox2.Tracker.Enums;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// Songs written before a song was a container are brought across on the way in, and the
/// document is read and written through <c>System.Text.Json</c> against a shape of its own
/// rather than against <see cref="Song"/>, so what is on disc can stay still while the model
/// moves.
/// </remarks>
public sealed class SongStore : ISongStore
{
    /// <summary>Recordings written so a song survives its folder moving.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISongPaths Portable = new SongPaths();

    /// <summary>A cell as it reads on screen and in the file.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ITrackerCellText CellText = new TrackerCellText();

    /// <summary>The recordings a packed song carries inside it.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISongSamples Carried = new SongSamples();

    /// <summary>
    /// Says whether the song just read came from a different kind of machine, and writes it down.
    /// </summary>
    /// <remarks>
    /// The paths in a song are the one thing in it that does not travel, and everything that has
    /// to know is downstream of here: what a plugin is looked up by, and what a reader of the log
    /// needs when a recording or a plugin does not turn up. Said out loud rather than only acted
    /// on, since **a song quietly behaving differently because of where it was made is worse than
    /// one that says so**.
    /// </remarks>
    /// <param name="song">The song that was just read.</param>
    private static void Travelled(Song song)
    {
        bool travelled = Machine.Travelled(song.MadeOn);

        Audio.Plugins.SongOrigin.Wants(travelled);

        if (!travelled) return;

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () =>
            $"'{song.Name}' was written on {song.MadeOn} and this is {Machine.Here}: " +
            "the paths in it are not compared here, so plugins and recordings are found by name");
    }

    /// <summary>
    /// Which kind of machine this one is, stamped into every song written here.
    /// </summary>
    /// <remarks>
    /// Holds nothing, so one serves them all. **Written on every save rather than kept from where
    /// the song began**, since what anybody wants to know is whether the paths in the file that
    /// is in front of them could mean anything on this computer, and those paths were written by
    /// whoever saved it last.
    /// </remarks>
    private static readonly Interfaces.IMachineWord Machine = new MachineWord();

    /// <summary>Which instruments play a given recording.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly ISampleUsers Usage = new SampleUsers();

    /// <summary>What the volume column means, and how a song written on the old scale is read.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IVolumeScale Volumes = new VolumeScale();

    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    /// <remarks>
    /// Static because the cache below is keyed by path and a field initializer cannot read an
    /// instance field. It holds nothing of its own, so one shared between stores is one rule
    /// rather than one piece of state.
    /// </remarks>
    private static readonly IFilePaths _paths = new FilePaths();

    /// <summary>What a song file is called. A zip, whatever the extension says.</summary>
    public const string Extension = ".jibx";

    /// <summary>What songs were called when a song was one JSON file.</summary>
    private const string OldExtension = ".json";

    /// <summary>What a converted one is left called, so it is kept but no longer found.</summary>
    private const string RetiredExtension = ".json.old";

    /// <summary>The document itself, which is the only entry a song must have.</summary>
    private const string SongEntry = "song.json";

    /// <summary>Where the plugins' own patches sit, apart from the document.</summary>
    private const string StateFolder = "state/";

    /// <summary>What a patch is called, since it is bytes a plugin handed over and not text.</summary>
    private const string StateExtension = ".bin";

    /// <summary>
    /// Indented, and with defaults written out rather than skipped.
    /// </summary>
    /// <remarks>
    /// A synth patch is full of settings whose zero is a real choice: no attack, no sustain, no
    /// vibrato. Skipping them would leave the property initialisers to put their own values back
    /// on load, so a patch saved with the attack at nought would open with whatever a new one
    /// has.
    /// </remarks>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc/>
    public string SongsDirectory { get; }

    /// <summary>
    /// Points at the songs folder under the application's own folder, making it if it is not
    /// there, and brings any song written before this format across.
    /// </summary>
    /// <param name="appName">
    /// Which application folder, so a test can be pointed at one of its own.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="files">How a file is written whole, defaulted to the real one.</param>
    public SongStore(string appName = AppFolder.AppName, IAppFolder? folder = null, ISafeFile? files = null)
    {
        _files = files ?? new SafeFile();

        SongsDirectory = Path.Combine((folder ?? new AppFolder()).Path(appName), "songs");
        Directory.CreateDirectory(SongsDirectory);

        BringOldSongsAcross();
    }

    /// <summary>How a file is written whole, so a song save cannot leave half a zip.</summary>
    private readonly ISafeFile _files;

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
    ///
    /// A song already brought across is left alone. The container is the one that counts, and
    /// converting again would put the old work back on top of the new.
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

    /// <inheritdoc/>
    public string PathFor(string songName) =>
        Path.Combine(SongsDirectory, songName + Extension);

    /// <inheritdoc/>
    public IReadOnlyList<string> List() =>
        Directory.Exists(SongsDirectory)
            ? Directory.GetFiles(SongsDirectory, "*" + Extension).OrderBy(p => p).ToArray()
            : Array.Empty<string>();

    /// <inheritdoc/>
    public IReadOnlyList<SongFile> ListSongs() =>
        List()
            .Select(path => new SongFile(
                Path.GetFileNameWithoutExtension(path), path, DescriptionIn(path), SavedAt(path)))
            .ToArray();

    /// <summary>
    /// When a song was last written, by the file's own clock.
    /// </summary>
    /// <remarks>
    /// The file rather than anything inside it. A song does not record when it was saved and
    /// should not have to: the thing that knows is the file system, it is right even for a song
    /// copied in from somewhere, and it costs no reading of the file at all.
    ///
    /// A file that will not answer has no date rather than a made-up one, the same bargain the
    /// description makes: this is a list to read, not the load that has to report a broken file.
    /// </remarks>
    private static DateTime SavedAt(string path)
    {
        try { return File.GetLastWriteTime(path); }
        catch (Exception) { return default; }
    }

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

    /// <inheritdoc/>
    public bool Exists(string songName) => File.Exists(PathFor(songName));

    /// <inheritdoc/>
    public void Delete(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Said as "instrument in song", because a name on its own would not tell anybody which of
    /// their songs is about to go quiet.
    ///
    /// Songs are asked as well as the rack, because a recording is deleted from RECORD and the
    /// rack is not the only thing built on one. A take nothing on the rack uses but three songs
    /// do read as free, and deleting it emptied those three tracks with nothing said. Nothing
    /// reported it afterwards either: the songs still opened, and three instruments were simply
    /// silent.
    ///
    /// Only song.json is read, never the patches and never the audio, and the answer is cached
    /// per song by its write time, so asking this about every song in the folder costs a few
    /// milliseconds however large the songs are.
    /// </remarks>
    public IReadOnlyList<string> InstrumentsUsing(string filePath)
    {
        var names = new List<string>();
        if (string.IsNullOrWhiteSpace(filePath)) return names;

        foreach (string path in List())
        {
            var (song, instruments) = Playing(path);
            if (instruments.Count == 0) continue;

            foreach (string name in Usage.By(instruments, filePath))
                names.Add(name + " in '" + song + "'");
        }

        return names;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Only song.json is rewritten. The patches and any recordings the song carries are left
    /// exactly as they are rather than read out and written back, so renaming a take does not
    /// mean rebuilding a forty megabyte file. The whole thing still lands in one move, because
    /// half a rewritten song is not a song.
    ///
    /// A song that will not open is not one this rename can fix, so it is passed over and the
    /// rest still move.
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
        new(_paths.Comparer);

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

            foreach (var instrument in document.Instruments) Portable.UnpackInto(instrument);

            return (Path.GetFileNameWithoutExtension(path), document.Instruments);
        }
        catch (Exception)
        {
            return ("", Array.Empty<TrackerInstrument>());
        }
    }

    /// <summary>Rewrites one song's song.json if it plays that recording. True when it did.</summary>
    /// <remarks>
    /// Written beside the file and moved on top, so what is there is the whole old song or the
    /// whole new one. Updating the container where it lies would leave neither if anything went
    /// wrong part way, and a half rewritten song is worse than one that was never touched.
    /// </remarks>
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
            Portable.UnpackInto(instrument);
            if (Usage.Repoint(instrument, from, to)) moved = true;
            Portable.PackInto(instrument);
        }

        if (!moved) return false;

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

    /// <summary>
    /// The song as its own file would hold it, and back again.
    /// </summary>
    /// <remarks>
    /// For a history to keep a step in. Through the same reader and writer a save goes through,
    /// deliberately: those two are already trusted with people's work and already know what
    /// belongs in a song and what does not, so a step written this way cannot disagree with what
    /// saving would produce. Writing a second copier beside them is how the two drift apart, and
    /// the way that fails is an undo that silently drops whatever the second one forgot.
    ///
    /// Not what a song is stored as on disc: that is a container with the plugins' own patches
    /// beside the document, and those are megabytes and do not change when somebody removes an
    /// instrument. This is the document alone.
    /// </remarks>
    public static string Copy(Song song) =>
        song is null ? "" : JsonSerializer.Serialize(SongDocument.From(song), JsonOptions);

    /// <summary>Reads one back. Null when it will not read, which costs the step and nothing else.</summary>
    public static Song? Uncopy(string said)
    {
        if (string.IsNullOrWhiteSpace(said)) return null;

        try
        {
            return JsonSerializer.Deserialize<SongDocument>(said, JsonOptions)?.ToSong();
        }
        catch (Exception bad)
        {
            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Tracker, () => "history: a step will not read back: " + bad.Message);

            return null;
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

    /// <summary>What one effect's patch is called inside the file, by the strip it is on.</summary>
    /// <remarks>
    /// By the track it sits on and its place in that track's chain, for the same reason an
    /// instrument's goes by its place in the list: both numbers are what the reader has in its
    /// hand as it walks the same lists the writer walked. The "t" keeps the two kinds apart in
    /// a folder somebody may well open.
    ///
    /// The master is track minus one and gets a name of its own, "m" and the device, rather than
    /// a number. It is not a track, and numbering it would make it one the day somebody adds a
    /// thirty-third.
    /// </remarks>
    private static string ChainStateName(int track, int device) =>
        StateFolder
        + (track < 0 ? "m" : "t" + track.ToString("00", CultureInfo.InvariantCulture) + "-")
        + device.ToString("00", CultureInfo.InvariantCulture) + StateExtension;

    /// <inheritdoc/>
    /// <remarks>
    /// What the song carries is read off the song itself while its paths are still this
    /// machine's, before the document is built and its paths made portable.
    ///
    /// The document goes in compressed well, since it is text and it is small. The patches go in
    /// compressed quickly rather than well: a patch is the one thing here big enough for the
    /// difference to be felt, and it is felt on every save.
    ///
    /// The whole container is written through <see cref="Files.SafeFile"/>, so a save that goes
    /// wrong part way costs nothing: the song that was there is still there.
    /// </remarks>
    public void Save(Song song, string filePath, bool withSamples = false)
    {
        song.Normalize();

        var carrying = withSamples ? Carried.Wanted(song) : Array.Empty<string>();

        var document = SongDocument.From(song);
        var states = document.TakeStatesOut();
        var patches = document.TakeChainStatesOut();

        _files.Write(filePath, stream =>
        {
            using var container = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

            var entry = container.CreateEntry(SongEntry, CompressionLevel.Optimal);
            using (var writing = entry.Open())
                JsonSerializer.Serialize(writing, document, JsonOptions);

            foreach (var (index, bytes) in states)
            {
                var patch = container.CreateEntry(StateName(index), CompressionLevel.Fastest);
                using var writing = patch.Open();
                writing.Write(bytes, 0, bytes.Length);
            }

            foreach (var (track, device, bytes) in patches)
            {
                var patch = container.CreateEntry(ChainStateName(track, device), CompressionLevel.Fastest);
                using var writing = patch.Open();
                writing.Write(bytes, 0, bytes.Length);
            }

            Carried.Write(container, carrying);
        });
    }

    /// <inheritdoc/>
    public Song? Load(string filePath) => Load(filePath, out _);

    /// <inheritdoc/>
    /// <remarks>
    /// The reading is a real one through <see cref="Load(string)"/> rather than a look at the
    /// name, since a file called <c>.jibx</c> is not a song and the list is not the place to find
    /// that out. It costs opening the archive twice, once here and once when it is really opened,
    /// which happens once per import and never on a path anybody is waiting on.
    ///
    /// The recordings it carries are deliberately not unpacked here. Copying the file is the
    /// whole of the import; what it holds arrives when it is opened, which is the one path that
    /// already does it and is the same path a song already in the folder takes.
    /// </remarks>
    public string? Import(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

            if (Load(filePath) == null) return null;

            string wanted = Free(Path.GetFileNameWithoutExtension(filePath));

            File.Copy(filePath, wanted);

            return wanted;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// A path in the songs folder under that name, or under the first number that is not taken.
    /// </summary>
    /// <remarks>
    /// Stops at a hundred rather than counting for ever, since a folder holding a hundred songs
    /// of one name is somebody's fault rather than something to work around, and a loop with no
    /// end on it is worse than a refusal.
    /// </remarks>
    /// <param name="name">What the file arriving is called.</param>
    /// <returns>Where it may be written.</returns>
    private string Free(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "song";

        string path = PathFor(name);

        for (int at = 2; File.Exists(path) && at < 100; at++) path = PathFor(name + " (" + at + ")");

        return path;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Null for anything that will not read, whatever the reason: a missing file, something that
    /// is not a zip, a zip with no document in it, or a document that will not parse. There is
    /// nothing useful to tell the caller apart here, since none of them is a song.
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
            arrived = Carried.Read(container, song);

            song.Normalize();

            Travelled(song);

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
            var bytes = Lump(container, StateName(index));
            if (bytes != null) document.Instruments[index].PluginState = bytes;
        }

        for (int track = 0; track < document.Mix.Count; track++)
            PutChainBack(container, document.Mix[track], track);

        PutChainBack(container, document.Master, MasterStrip);
    }

    /// <summary>The patches of one strip's effects, put back where the plugins will look.</summary>
    private static void PutChainBack(ZipArchive container, TrackMix? strip, int track)
    {
        var devices = strip?.Plugins?.Devices;
        if (devices == null) return;

        for (int device = 0; device < devices.Count; device++)
        {
            var bytes = Lump(container, ChainStateName(track, device));
            if (bytes != null) devices[device].State = bytes;
        }
    }

    /// <summary>The master, which is a strip without being a track. See ChainStateName.</summary>
    private const int MasterStrip = -1;

    /// <summary>One patch out of the container, or null when it is not there or will not read.</summary>
    private static byte[]? Lump(ZipArchive container, string name)
    {
        var entry = container.GetEntry(name);
        if (entry == null) return null;

        try
        {
            var bytes = new byte[entry.Length];

            using var reading = entry.Open();
            reading.ReadExactly(bytes);

            return bytes;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// What song.json holds. Patterns serialize as one string per row rather than an object per
    /// cell, which keeps a 64 line file readable in a text editor and small on disk.
    /// </summary>
    /// <remarks>
    /// A shape of its own rather than <see cref="Song"/> itself, so what is on disc can stay
    /// still while the model moves. The properties that are simply the song's own are named after
    /// them and mean what they mean there; the ones that differ say so.
    /// </remarks>
    private sealed class SongDocument
    {
        /// <summary>
        /// What this build writes: 4 since patterns were named from nought.
        /// </summary>
        /// <remarks>
        /// 3 was the volume column widening to 0x80, 2 was the patches moving out of the
        /// document and into the container, and 1 is every song written before any of it.
        /// </remarks>
        public const int Current = 4;

        /// <summary>
        /// What wrote the song being read, which decides what its numbers mean.
        /// </summary>
        /// <remarks>
        /// 1 rather than <see cref="Current"/> when the file does not say, deliberately. A song
        /// with no version in it is older than the field, so reading it as current would skip
        /// every conversion the file needs and do it silently.
        /// </remarks>
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        /// <inheritdoc cref="Song.MadeOn"/>
        public string MadeOn { get; set; } = "";

        public double Bpm { get; set; } = TrackerTiming.DefaultBpm;
        public int LinesPerBeat { get; set; } = TrackerTiming.DefaultLinesPerBeat;

        /// <summary>
        /// Whether the song plays its order or loops one pattern. See <see cref="Song.PlayMode"/>.
        /// </summary>
        /// <remarks>
        /// Absent in a song written before it was part of one, which reads back as Pattern and
        /// is what every one of those songs opened as.
        /// </remarks>
        public TrackerPlayMode PlayMode { get; set; } = TrackerPlayMode.Pattern;

        /// <summary>
        /// Whether the song comes round at the end. See <see cref="Song.Looping"/>.
        /// </summary>
        /// <remarks>
        /// True when the file does not say, which is what the transport did before anything
        /// could set it, so an older song plays exactly as it did.
        /// </remarks>
        public bool Looping { get; set; } = true;
        public int KeyboardOctave { get; set; } = 4;
        public int TrackCount { get; set; } = Song.DefaultTrackCount;

        /// <summary>
        /// How many note columns each track shows. Absent in a song written before they existed,
        /// which reads back as one apiece and is exactly what that song played.
        /// </summary>
        public List<int> NoteColumns { get; set; } = new();

        public List<int> Order { get; set; } = new();

        /// <summary>
        /// The loop range over the order, either end, or -1 apiece for none.
        /// </summary>
        /// <remarks>
        /// Absent in a song written before the range existed, which reads back as no range and
        /// is exactly what that song had.
        /// </remarks>
        public int LoopFrom { get; set; } = Song.NoLoop;

        /// <inheritdoc cref="LoopFrom"/>
        public int LoopTo { get; set; } = Song.NoLoop;
        public List<int> TrackInstruments { get; set; } = new();
        public List<TrackMix> Mix { get; set; } = new();

        /// <summary>
        /// The master strip. A song written before this existed reads back with a fresh one,
        /// which is unity and no effect, so it sounds exactly as it did.
        /// </summary>
        public TrackMix Master { get; set; } = new();

        /// <summary>This song's own controller layout. See <see cref="Song.Controls"/>.</summary>
        public List<Midi.ControlMapping> Controls { get; set; } = new();
        public List<TrackerInstrument> Instruments { get; set; } = new();
        public List<PatternDocument> Patterns { get; set; } = new();

        /// <summary>
        /// The document for a song, with everything copied rather than shared.
        /// </summary>
        /// <remarks>
        /// Copies throughout, so lifting the patches out of it afterwards cannot reach the song
        /// that is still being played, and so a save cannot be affected by an edit made while it
        /// is running.
        /// </remarks>
        public static SongDocument From(Song song) => new()
        {
            Version = Current,
            Name = song.Name,
            Description = song.Description,
            MadeOn = Machine.Here,
            Bpm = song.Bpm,
            LinesPerBeat = song.LinesPerBeat,
            PlayMode = song.PlayMode,
            Looping = song.Looping,
            KeyboardOctave = song.KeyboardOctave,
            TrackCount = song.TrackCount,
            NoteColumns = new List<int>(song.NoteColumns),
            Order = new List<int>(song.Order),
            LoopFrom = song.LoopFrom,
            LoopTo = song.LoopTo,
            TrackInstruments = new List<int>(song.TrackInstruments),
            Mix = song.Mix.Select(m => m.Clone()).ToList(),
            Master = song.Master.Clone(),
            Controls = song.Controls.Select(Midi.ControlMapping.Copy).ToList(),
            Instruments = song.Instruments.Select(Written).ToList(),
            Patterns = song.Patterns.Select(PatternDocument.From).ToList()
        };

        /// <summary>
        /// Names every pattern after its own place in the song.
        /// </summary>
        /// <remarks>
        /// They were named from one while the order counts slots from nought, so the two columns
        /// of the order list were permanently one apart and a fresh song read "slot 00 plays
        /// pattern 01". Nothing looks a pattern up by name, so renaming them all costs nothing
        /// and cannot break a song: the order holds indexes and always did.
        ///
        /// Every pattern, not only the ones the order plays, since one that has fallen out of
        /// the order can be pointed back at and would otherwise come back wearing the old
        /// numbering.
        /// </remarks>
        private static void Renumber(Song song)
        {
            for (int at = 0; at < song.Patterns.Count; at++)
                song.Patterns[at].Name = Song.Named(at);
        }

        /// <summary>One instrument as the file should hold it: a copy, with portable paths.</summary>
        private static TrackerInstrument Written(TrackerInstrument instrument)
        {
            var copy = instrument.Clone();
            Portable.PackInto(copy);
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

        /// <summary>
        /// The same for every effect on every track, which is where a Serum on a track keeps
        /// its preset.
        /// </summary>
        /// <remarks>
        /// Out here for exactly the reason the instruments' patches are. One effect's lump is
        /// bigger than the whole of the music, it does not change when somebody moves a note,
        /// and a document is all or nothing: a patch that came back damaged used to be able to
        /// cost the song.
        ///
        /// Safe to empty what it takes, because the mix here is already a copy.
        /// </remarks>
        public List<(int Track, int Device, byte[] Bytes)> TakeChainStatesOut()
        {
            var states = new List<(int Track, int Device, byte[] Bytes)>();

            for (int track = 0; track < Mix.Count; track++) Take(Mix[track], track);

            Take(Master, MasterStrip);

            return states;

            void Take(TrackMix? strip, int track)
            {
                var devices = strip?.Plugins?.Devices;
                if (devices == null) return;

                for (int device = 0; device < devices.Count; device++)
                {
                    var bytes = devices[device].State;
                    if (bytes == null || bytes.Length == 0) continue;

                    states.Add((track, device, bytes));
                    devices[device].State = Array.Empty<byte>();
                }
            }
        }

        /// <summary>One instrument as this machine has it: a copy, with real paths.</summary>
        private static TrackerInstrument Read(TrackerInstrument instrument)
        {
            var copy = instrument.Clone();
            Portable.UnpackInto(copy);
            return copy;
        }

        /// <summary>The song this document describes, with real paths and its own copies.</summary>
        /// <remarks>
        /// The patterns are read last and given the song's track count, since a pattern's width
        /// is the song's and is not stored per pattern.
        ///
        /// And then brought onto this build's volume scale where the file was written on the
        /// old one, which is <see cref="IVolumeScale"/>. After the patterns rather than while
        /// they are being read, because it is a fact about the song and not about a cell: every
        /// pattern in one file was written by the same build.
        /// </remarks>
        public Song ToSong()
        {
            var song = new Song
            {
                Name = Name,
                Description = Description,
                MadeOn = MadeOn,
                Bpm = Bpm,
                LinesPerBeat = LinesPerBeat,
                PlayMode = PlayMode,
                Looping = Looping,
                KeyboardOctave = KeyboardOctave,
                TrackCount = TrackCount,
                NoteColumns = new List<int>(NoteColumns),
                Order = new List<int>(Order),
                LoopFrom = LoopFrom,
                LoopTo = LoopTo,
                TrackInstruments = new List<int>(TrackInstruments),
                Mix = Mix.Select(m => m.Clone()).ToList(),
                Master = (Master ?? new TrackMix()).Clone(),
                Controls = Controls.Select(Midi.ControlMapping.Copy).ToList(),
                Instruments = Instruments.Select(Read).ToList()
            };

            song.Patterns = Patterns.Select(p => p.ToPattern(TrackCount, NoteColumns)).ToList();

            if (Version < 3) Volumes.Widen(song);

            if (Version < 4) Renumber(song);

            return song;
        }
    }

    /// <summary>
    /// One pattern as the file holds it: its shape, the cells that hold something, and its lanes.
    /// </summary>
    /// <remarks>
    /// The track count is not here. Every pattern in a song is the song's width, so storing it
    /// per pattern would be storing the same number once per pattern and giving a hand-edited
    /// file a way to disagree with itself.
    /// </remarks>
    private sealed class PatternDocument
    {
        /// <summary>What the order list calls it.</summary>
        public string Name { get; set; } = "";

        /// <summary>How many steps it has, which is the one part of its shape it does own.</summary>
        public int Lines { get; set; } = Pattern.DefaultLines;

        /// <summary>
        /// One entry per used cell, as "line:track:cell". Blank cells are not stored.
        /// </summary>
        /// <remarks>
        /// A note column past the first is written as "line:track:column:cell", and the first
        /// column keeps the three-part form it always had. Not for tidiness: a build that
        /// predates note columns splits this into three and reads whatever it finds in the
        /// third field as a cell, so writing the column number into every entry would mean an
        /// older copy of the application opening the song and finding every cell unreadable.
        /// This way it reads what it can play and quietly leaves behind what it cannot, which
        /// is the same bargain the rest of this format makes.
        ///
        /// The cell text is space separated and never holds a colon, so counting the fields is
        /// enough to tell the two forms apart.
        /// </remarks>
        public List<string> Cells { get; set; } = new();

        /// <summary>One entry per automated parameter. Empty for almost every pattern.</summary>
        public List<LaneDocument> Lanes { get; set; } = new();

        /// <summary>The document for a pattern, skipping every cell that holds nothing.</summary>
        public static PatternDocument From(Pattern pattern)
        {
            var document = new PatternDocument { Name = pattern.Name, Lines = pattern.Lines };

            for (int line = 0; line < pattern.Lines; line++)
            {
                for (int track = 0; track < pattern.TrackCount; track++)
                {
                    for (int column = 0; column < pattern.ColumnsOn(track); column++)
                    {
                        var cell = pattern[line, track, column];
                        if (cell.IsEmpty) continue;

                        document.Cells.Add(column == 0
                            ? $"{line}:{track}:{CellText.Write(cell)}"
                            : $"{line}:{track}:{column}:{CellText.Write(cell)}");
                    }
                }
            }

            foreach (var lane in pattern.Lanes)
                document.Lanes.Add(LaneDocument.From(lane));

            return document;
        }

        /// <summary>
        /// The pattern this describes, at the song's width.
        /// </summary>
        /// <remarks>
        /// The lanes go in before the cells, since a lane's points are fitted to the pattern's
        /// length as it is added. A lane for a track this song no longer has is dropped, with the
        /// master let through because it is a strip rather than one of the tracks. A cell entry
        /// that will not parse, or that names a cell outside the pattern, is passed over rather
        /// than costing the song.
        /// </remarks>
        public Pattern ToPattern(int trackCount, IReadOnlyList<int>? columns = null)
        {
            var pattern = new Pattern(Lines, trackCount) { Name = Name };

            pattern.SetColumns(columns);

            foreach (var lane in Lanes)
                if (lane.ToLane() is { } made && (made.IsMaster || made.Track < trackCount))
                    pattern.Lane(made);

            foreach (var entry in Cells)
            {
                var parts = entry.Split(':', 4);
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[0], out int line)) continue;
                if (!int.TryParse(parts[1], out int track)) continue;

                int column = 0;
                string written = parts[2];

                if (parts.Length == 4 && int.TryParse(parts[2], out int said))
                {
                    column = said;
                    written = parts[3];
                }

                if (!pattern.Contains(line, track, column)) continue;
                if (!CellText.TryRead(written, out var cell)) continue;

                pattern[line, track, column] = cell;
            }

            return pattern;
        }
    }

    /// <summary>
    /// One automation lane as the file holds it.
    /// </summary>
    /// <remarks>
    /// Named fields for the header and one compact string per point, which is the split the two
    /// halves ask for. What a lane is about is half a dozen unlike things, three of which are
    /// only read for one kind of destination, and packing those into one string would mean a
    /// format with optional fields and a plugin id that must never contain the separator. The
    /// points are the opposite: hundreds of one identical shape, where a line each would make a
    /// recorded sweep a page long and a diff unreadable.
    ///
    /// The enums are written as numbers, which is what a settings file here already does, so
    /// adding a play mode or a kind on the end leaves what is stored meaning what it meant.
    /// </remarks>
    private sealed class LaneDocument
    {
        /// <summary>Which strip, where minus one is the master rather than a track.</summary>
        public int Track { get; set; }

        /// <summary>What kind of thing is moved: the instrument, an insert, or the strip.</summary>
        public Midi.Enums.ControlKind Kind { get; set; } = Midi.Enums.ControlKind.SoundDevice;

        /// <summary>How it gets from one point to the next.</summary>
        public AutomationPlay Play { get; set; } = AutomationPlay.Lines;

        /// <summary>The machine by its slot id, read only for an instrument parameter.</summary>
        public string Machine { get; set; } = "";

        /// <summary>Which of its parameters, by the key it is stored under rather than its name.</summary>
        public string Key { get; set; } = "";

        /// <summary>The plugin by the id the scanner gave it, read only for an insert.</summary>
        public string Plugin { get; set; } = "";

        /// <summary>Which insert, for a chain where the plugin is not named.</summary>
        public int Slot { get; set; }

        /// <summary>Which parameter of it, as the plugin numbers them.</summary>
        public uint Parameter { get; set; }

        /// <summary>Which strip control, read only for a lane about the mix.</summary>
        public Midi.Enums.MixControl Mix { get; set; } = Midi.Enums.MixControl.Volume;

        /// <summary>One entry per point, as "time=value". The time is in lines.</summary>
        public List<string> Points { get; set; } = new();

        /// <summary>The document for one lane, its points written out one string apiece.</summary>
        public static LaneDocument From(AutomationLane lane)
        {
            var document = new LaneDocument
            {
                Track = lane.Track,
                Kind = lane.Kind,
                Play = lane.Play,
                Machine = lane.Machine,
                Key = lane.Key,
                Plugin = lane.Plugin,
                Slot = lane.Slot,
                Parameter = lane.Parameter,
                Mix = lane.Mix
            };

            foreach (var point in lane.Points)
                document.Points.Add(Said(point.Time) + "=" + Said(point.Value));

            return document;
        }

        /// <summary>
        /// The lane this describes, or null when it describes nothing that can be one.
        /// </summary>
        /// <remarks>
        /// A kind that cannot be automated and a track below nought are both refused. The one
        /// exception down there is the master, which is a strip without being a track; anything
        /// else negative is a hand-edited file.
        ///
        /// A point that will not parse is passed over rather than costing the lane, and the
        /// points go in through <see cref="AutomationLane.TakePoints"/>, which sorts them and
        /// keeps one per time whatever order the file happened to hold them in.
        /// </remarks>
        public AutomationLane? ToLane()
        {
            if (!AutomationLane.Automatable(Kind)) return null;

            if (Track < 0 && Track != TrackerPlayer.MasterStrip) return null;

            var lane = new AutomationLane
            {
                Track = Track,
                Kind = Kind,
                Play = Play,
                Machine = Machine,
                Key = Key,
                Plugin = Plugin,
                Slot = Slot,
                Parameter = Parameter,
                Mix = Mix
            };

            var points = new List<AutomationPoint>();

            foreach (var entry in Points)
            {
                int at = entry.IndexOf('=');
                if (at <= 0) continue;

                if (!double.TryParse(entry[..at], NumberStyles.Float,
                                     CultureInfo.InvariantCulture, out double time)) continue;

                if (!double.TryParse(entry[(at + 1)..], NumberStyles.Float,
                                     CultureInfo.InvariantCulture, out double value)) continue;

                points.Add(new AutomationPoint(time, value));
            }

            lane.TakePoints(points);

            return lane;
        }

        /// <summary>
        /// A number as the file should hold it.
        /// </summary>
        /// <remarks>
        /// Six places, which is finer than any controller can send: the most a MIDI message
        /// carries is fourteen bits, and one part in sixteen thousand is five places. Invariant,
        /// so a song written in a country that spells a decimal point with a comma opens
        /// everywhere else.
        /// </remarks>
        private static string Said(double value) =>
            value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
