using System;
using System.IO;

namespace JingleBox2.Tracker;

/// <summary>
/// Writes the recordings a song plays as names that survive the folder moving, and reads them
/// back as the paths this machine actually has.
/// </summary>
/// <remarks>
/// A song stores a full path for every recording an instrument plays, and almost all of them
/// are inside the application's own folder: a machine's presets, or a take off the shelf. That
/// folder is somewhere different on every machine and under a different name on every platform,
/// so a song saved here and opened anywhere else finds nothing, and so does a song opened after
/// the account was renamed. Nothing reports it. The instruments are simply silent.
///
/// So anything under the application folder is written as <c>{app}/</c> and what follows, with
/// forward slashes, and put back together on the way in. A path outside that folder is left
/// exactly as it was: it is somewhere the user chose, and guessing at it would be worse than
/// keeping it.
///
/// There is no ambiguity to guard against. A path that really begins <c>{app}/</c> is a
/// relative one, and every path a song holds is absolute.
/// </remarks>
public static class SongPaths
{
    private const string Token = "{app}/";

    /// <summary>A path as a song should hold it.</summary>
    public static string Pack(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        string root = Config.AppFolder.Path();

        if (path.Length <= root.Length + 1) return path;
        if (!path.AsSpan(0, root.Length).Equals(root, FilePaths.Comparison)) return path;
        if (path[root.Length] != Path.DirectorySeparatorChar && path[root.Length] != '/') return path;

        return Token + path.Substring(root.Length + 1).Replace('\\', '/');
    }

    /// <summary>The path that name means on this machine.</summary>
    public static string Unpack(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(Token, StringComparison.Ordinal))
            return path ?? "";

        string rest = path.Substring(Token.Length).Replace('/', Path.DirectorySeparatorChar);

        return Path.Combine(Config.AppFolder.Path(), rest);
    }

    /// <summary>Everything one instrument plays, written the portable way.</summary>
    public static void PackInto(TrackerInstrument instrument) => Walk(instrument, Pack);

    /// <summary>Everything one instrument plays, read back as real paths.</summary>
    public static void UnpackInto(TrackerInstrument instrument) => Walk(instrument, Unpack);

    /// <summary>
    /// Both directions over every recording an instrument names.
    /// </summary>
    /// <remarks>
    /// One walk rather than two, so a kind of instrument that grows a second file cannot be
    /// packed and then not unpacked. A chopped instrument names the same file on all sixteen
    /// of its pieces, and each is converted on its own: they agree because they started equal
    /// and the conversion is the same one, not because anything here is holding them together.
    /// </remarks>
    private static void Walk(TrackerInstrument instrument, Func<string, string> convert)
    {
        if (instrument == null) return;

        instrument.FilePath = convert(instrument.FilePath);

        if (instrument.Kit != null)
            foreach (var pad in instrument.Kit.Pads)
                pad.FilePath = convert(pad.FilePath);

        if (instrument.Zones != null)
            foreach (var zone in instrument.Zones.Zones)
                zone.FilePath = convert(zone.FilePath);
    }
}
