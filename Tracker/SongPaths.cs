using System;
using System.IO;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Config;
using JingleBox2.Config.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class SongPaths(IFilePaths? paths = null, IAppFolder? folder = null) : ISongPaths
{
    /// <summary>How this system decides a path is under the application folder.</summary>
    private readonly IFilePaths _paths = paths ?? new FilePaths();

    /// <summary>Where the application keeps its things, which is what a packed path stands in for.</summary>
    private readonly IAppFolder _folder = folder ?? new AppFolder();

    /// <summary>
    /// What stands in for the application folder. Forward slash, on every platform.
    /// </summary>
    /// <remarks>
    /// A separator has to be chosen and written down, because a song saved on Windows has to
    /// open on Linux. Forward slash, since that is the one both understand and the one a zip
    /// entry already uses.
    /// </remarks>
    private const string Token = "{app}/";

    /// <inheritdoc/>
    public string Pack(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        string root = _folder.Path();

        if (path.Length <= root.Length + 1) return path;
        if (!path.AsSpan(0, root.Length).Equals(root, _paths.Comparison)) return path;
        if (path[root.Length] != Path.DirectorySeparatorChar && path[root.Length] != '/') return path;

        return Token + path.Substring(root.Length + 1).Replace('\\', '/');
    }

    /// <inheritdoc/>
    public string Unpack(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(Token, StringComparison.Ordinal))
            return path ?? "";

        string rest = path.Substring(Token.Length).Replace('/', Path.DirectorySeparatorChar);

        return Path.Combine(_folder.Path(), rest);
    }

    /// <inheritdoc/>
    public void PackInto(TrackerInstrument instrument) => Walk(instrument, Pack);

    /// <inheritdoc/>
    public void UnpackInto(TrackerInstrument instrument) => Walk(instrument, Unpack);

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
