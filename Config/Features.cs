using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JingleBox2.Config.Enums;
using JingleBox2.Config.Interfaces;
using JingleBox2.Config.Records;

namespace JingleBox2.Config;

/// <inheritdoc/>
/// <remarks>
/// The catalogue is written out here, one line a part with its word, its name and what switching
/// it off does, so the settings cards, the file and the help all read the same list. The switches
/// themselves are an array read with a volatile read, since the clock and the audio path ask and
/// a lock there would be a wait on the drawing thread.
/// </remarks>
public sealed class Features : IFeatures
{
    /// <summary>Every switchable part.</summary>
    private static readonly FeatureInfo[] Catalogue =
    [
        new(Feature.PatternAutomation, FeaturePage.Tracker, "pattern-automation", "Pattern automation",
            "The automation under the pattern. Off, it is not shown, and a pattern's lanes are kept in the song but not played or recorded."),
        new(Feature.CommandEditor, FeaturePage.Tracker, "command-editor", "Command editor",
            "Command... on the pattern's right click menu. Off, it is gone from the menu; a command can still be typed into the cell."),
        new(Feature.NeighbourPatterns, FeaturePage.Tracker, "neighbour-patterns", "Neighbouring patterns",
            "The faded patterns before and after the one you are on, in song mode. Off, the room is left empty and they are not drawn."),
        new(Feature.ChainReadings, FeaturePage.Tracker, "chain-readings", "Chain readings",
            "The first controls and where they stand, printed on each block of the instrument/effect chain and the mixer's chains. Off, they are not read, which saves asking every plugin's process."),
        new(Feature.SongAutomation, FeaturePage.Mixer, "song-automation", "Song automation",
            "The automation on the mixer: levels, pans and the tempo over the whole song. Off, it is not shown, and the song's lanes are kept but not played or recorded, so the song plays at its own tempo."),
        new(Feature.SideChain, FeaturePage.Mixer, "side-chain", "Side chain",
            "The Duck from row at the foot of each track's strip. Off, the row is gone and no track is ducked, so nothing is worked out for it while the song plays."),
        new(Feature.Patchbay, FeaturePage.Mixer, "patchbay", "Patchbay",
            "The Patchbay tab beside the desk. Off, the tab is gone and its picture is neither built nor kept moving. The audio still goes where it is patched.")
    ];

    /// <summary>Where the parts switched off are written down, or nothing to keep them for this run.</summary>
    private readonly AppConfig? _config;

    /// <summary>Says the settings moved, so they are written to disc.</summary>
    private readonly Action? _moved;

    /// <summary>Whether each part is on, by its number.</summary>
    private readonly bool[] _on;

    /// <summary>Reads which parts the settings have switched off.</summary>
    /// <param name="config">The settings, or nothing for everything on and kept for this run only.</param>
    /// <param name="moved">Called when a switch is written into the settings.</param>
    public Features(AppConfig? config = null, Action? moved = null)
    {
        _config = config;
        _moved = moved;
        _on = new bool[Catalogue.Length];

        var off = config?.FeaturesOff ?? [];

        foreach (var info in Catalogue) _on[(int)info.Feature] = !off.Contains(info.Word);
    }

    /// <inheritdoc/>
    public IReadOnlyList<FeatureInfo> All => Catalogue;

    /// <inheritdoc/>
    public IReadOnlyList<FeatureInfo> On(FeaturePage page) => Catalogue.Where(info => info.Page == page).ToArray();

    /// <inheritdoc/>
    public bool IsOn(Feature feature) =>
        (int)feature >= 0 && (int)feature < _on.Length && Volatile.Read(ref _on[(int)feature]);

    /// <inheritdoc/>
    public void Set(Feature feature, bool on)
    {
        if ((int)feature < 0 || (int)feature >= _on.Length) return;
        if (IsOn(feature) == on) return;

        Volatile.Write(ref _on[(int)feature], on);

        if (_config is not null)
        {
            string word = Catalogue.First(info => info.Feature == feature).Word;
            var off = _config.FeaturesOff ??= [];

            off.Remove(word);
            if (!on) off.Add(word);

            _moved?.Invoke();
        }

        Changed?.Invoke(feature);
    }

    /// <inheritdoc/>
    public event Action<Feature>? Changed;
}
