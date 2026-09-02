using JingleBox2.Audio;
using JingleBox2.Audio.Records;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Rack.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;

namespace JingleBox2.SoundDevices.SoundMachines;

/// <summary>
/// The shelf of recordings, answering the questions a machine panel asks about one.
/// </summary>
/// <remarks>
/// A panel drawn from a machine file has a picture of a take on it and a line under the
/// picture, and it has no business knowing where recordings are kept, what a WAV header looks
/// like or which service reads one. It asks for a take by name and gets back a shape and a
/// line of text.
///
/// A take is named by its file, because that is what an instrument stores and what a song
/// carries: a position in the list would move the next time something above it was deleted. A
/// bare name is understood as well, since that is what a picker shows and what somebody typing
/// into a machine file would write, and it is looked up on the shelf.
/// </remarks>
public sealed class TakeLibrary : IMachineTakes
{
    /// <summary>Reading and writing WAV files. Holds nothing, so one serves the whole object.</summary>
    private readonly IWavFile _wav = new WavFile();

    /// <summary>Whether two paths are one file, by this machine's rules.</summary>
    private readonly IFilePaths _paths = new FilePaths();

    /// <summary>What the panel says when the file a take points at is not there any more.</summary>
    /// <remarks>The instrument editor's words, so the same fault reads the same in both places.</remarks>
    public const string MissingText = "The file this instrument plays is missing.";

    /// <summary>And when it is there but nothing can be made of it.</summary>
    public const string UnreadableText = "The file could not be read.";

    /// <summary>The application's recordings, or nothing when there is no shelf to look on.</summary>
    private readonly IReadOnlyList<Recording>? _shelf;

    /// <summary>What turns a file into a picture, or nothing, in which case there are no pictures.</summary>
    private readonly IWaveformService? _waveforms;

    /// <summary>What has been read, by file, so a panel drawn again does not read the disc again.</summary>
    private readonly Dictionary<string, Entry> _read = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What guards <see cref="_read"/>.
    /// </summary>
    /// <remarks>
    /// Two panels can be drawing at once and a preview can be being filled in behind them, so
    /// the table itself is locked. What it holds is not: reading a long take takes a moment, and
    /// doing it inside the lock would make the second panel wait on the first.
    /// </remarks>
    private readonly object _gate = new();

    /// <param name="shelf">
    /// The recordings the app has, live: the same collection RECORD fills, so a take made
    /// while a panel is open can be put on it without anything being rebuilt.
    /// </param>
    /// <param name="waveforms">
    /// What reads a file into a picture. Without one there are no pictures, and a panel that
    /// wanted to draw one draws nothing rather than failing.
    /// </param>
    public TakeLibrary(IReadOnlyList<Recording>? shelf = null, IWaveformService? waveforms = null)
    {
        _shelf = shelf;
        _waveforms = waveforms;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Null covers everything that is not a picture: no take set, a take that cannot be found,
    /// a file that will not read, and no waveform service to read it with. The panel draws its
    /// empty picture for all four and the line from <see cref="Describe"/> says which it was.
    ///
    /// The reading happens outside the lock, since a long take takes a moment and two panels
    /// asking at once should not queue. The worst that comes of that is the same file being read
    /// twice, and the result is only kept if the entry it was read for is still the current one:
    /// a file rewritten while it was being read has a newer entry by now, and the old peaks are
    /// dropped rather than filed under it.
    /// </remarks>
    public float[]? Peaks(string take)
    {
        string? path = PathOf(take);
        if (path == null || _waveforms == null) return null;

        var entry = Fresh(path);
        if (entry == null) return null;

        if (entry.Peaks != null) return entry.Peaks;

        float[]? peaks;

        try
        {
            peaks = _waveforms.AnalyzeFile(path).PeakData;
        }
        catch (Exception)
        {
            return null;
        }

        lock (_gate)
        {
            if (_read.TryGetValue(path, out var current) && current == entry) entry.Peaks = peaks;
        }

        return peaks;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The name and not the technical line, because this is what somebody reads to know which
    /// recording is on the machine. A file that is not on the shelf is called by its file name
    /// without the extension, which is what it was called when it was dragged in.
    ///
    /// How long it is and what rate it was recorded at is a different question and a different
    /// line on the panel, and it is <see cref="Details"/>. The format has nowhere to put that
    /// line yet, so nothing asks for it.
    /// </remarks>
    public string Describe(string take)
    {
        if (string.IsNullOrWhiteSpace(take)) return "";

        foreach (var recording in _shelf ?? (IReadOnlyList<Recording>)Array.Empty<Recording>())
        {
            if (_paths.Same(recording.FilePath, take) ||
                string.Equals(recording.Name, take, StringComparison.Ordinal))
            {
                return recording.Name.Length > 0 ? recording.Name : Path.GetFileNameWithoutExtension(recording.FilePath);
            }
        }

        string? found = PathOf(take);

        return found == null ? MissingText : Path.GetFileNameWithoutExtension(found);
    }

    /// <summary>
    /// How long the take is, what rate it was recorded at, and whether it is in stereo.
    /// </summary>
    /// <remarks>
    /// The instrument editor's own line, word for word, so the take reads the same on a
    /// described panel as it does on the page it came from. Read out of the file's headers
    /// rather than by decoding it, so a picker can describe a shelf of a hundred takes without
    /// reading a hundred files.
    /// </remarks>
    public string Details(string take)
    {
        if (string.IsNullOrWhiteSpace(take)) return "";

        string? path = PathOf(take);
        if (path == null) return MissingText;

        var entry = Fresh(path);
        if (entry == null) return MissingText;

        if (entry.Text != null) return entry.Text;

        string text;

        try
        {
            var info = _wav.ReadInfo(path);

            double seconds = info.SampleRate > 0 ? (double)info.FrameCount / info.SampleRate : 0;
            string channels = info.Channels >= 2 ? "stereo" : "mono";

            text = $"{seconds:0.00} s, {info.SampleRate} Hz {channels}";
        }
        catch (Exception)
        {
            text = UnreadableText;
        }

        lock (_gate)
        {
            if (_read.TryGetValue(path, out var current) && current == entry) entry.Text = text;
        }

        return text;
    }

    /// <summary>
    /// Which file that take is, or null for one that is not there.
    /// </summary>
    /// <remarks>
    /// The path first, because that is what an instrument holds. Then the name, because that is
    /// what a picker shows and what a person writes. Then the path on its own terms, so a take
    /// that is not on the shelf, a sample sitting in a machine's own sounds folder, still draws.
    ///
    /// The last of those is asked of the file system, which throws for a name that is not a path
    /// at all: too long, or full of characters a file name cannot hold. That is nothing rather
    /// than a fault, since a machine file can name anything somebody typed.
    /// </remarks>
    private string? PathOf(string take)
    {
        if (string.IsNullOrWhiteSpace(take)) return null;

        if (_shelf != null)
        {
            var byPath = _shelf.FirstOrDefault(r =>
                string.Equals(r.FilePath, take, StringComparison.OrdinalIgnoreCase));

            if (byPath != null && File.Exists(byPath.FilePath)) return byPath.FilePath;

            var byName = _shelf.FirstOrDefault(r =>
                string.Equals(r.Name, take, StringComparison.OrdinalIgnoreCase));

            if (byName != null && File.Exists(byName.FilePath)) return byName.FilePath;
        }

        try
        {
            return File.Exists(take) ? take : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// What is known about that file, thrown away and started again if the file has changed
    /// underneath, or null when it has gone.
    /// </summary>
    /// <remarks>
    /// What is kept is what is expensive: the peaks, which mean decoding the whole recording,
    /// and the line of text, which means opening it. Both are kept against the file's date and
    /// its length, so a take that is trimmed, normalised or recorded over is read again the
    /// next time it is asked for, and one that is only renamed on the shelf is not.
    ///
    /// One entry per take that has actually been looked at, and they are not turned out: a
    /// shelf of a hundred takes all drawn is a couple of megabytes of peaks, and a panel that
    /// dropped them would read the file again every time you clicked back to it.
    /// </remarks>
    private Entry? Fresh(string path)
    {
        DateTime written;
        long length;

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists) return null;

            written = file.LastWriteTimeUtc;
            length = file.Length;
        }
        catch (Exception)
        {
            return null;
        }

        lock (_gate)
        {
            if (_read.TryGetValue(path, out var entry) && entry.Written == written && entry.Length == length)
                return entry;

            var made = new Entry(written, length);
            _read[path] = made;

            return made;
        }
    }

    /// <summary>One take as it was last read, and what the file looked like when it was.</summary>
    /// <param name="written">When the file was last written, as it was when this was made.</param>
    /// <param name="length">And how long it was, since a rewrite can leave the date alone.</param>
    private sealed class Entry(DateTime written, long length)
    {
        /// <summary>When the file was last written, as it was when this entry was made.</summary>
        public DateTime Written { get; } = written;

        /// <summary>And how long it was. Both have to match or the entry is thrown away.</summary>
        public long Length { get; } = length;

        /// <summary>The picture, once somebody has asked for it.</summary>
        public float[]? Peaks { get; set; }

        /// <summary>And the line under it, which is read from the headers alone.</summary>
        public string? Text { get; set; }
    }
}
