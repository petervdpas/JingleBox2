using System;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class SongPaths(IFilePaths? paths = null, IAppFolder? folder = null) : ISongPaths
{

    /// <summary>The rule itself, which knows nothing about instruments.</summary>
    private readonly IPortablePath _portable = new PortablePath(paths, folder);

    /// <inheritdoc/>
    public string Pack(string path) => _portable.Pack(path);

    /// <inheritdoc/>
    public string Unpack(string path) => _portable.Unpack(path);

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
