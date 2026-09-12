using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Audio.Plugins;

/// <inheritdoc/>
/// <remarks>
/// Over the settings themselves, which is where the two lists live, so turning something over is
/// written down by whoever owns the settings rather than here. Told how to say the settings moved
/// rather than reaching for a store, which is what lets the whole of this be put a question to
/// without a disc.
/// </remarks>
public sealed class PluginSwitches : IPluginSwitches
{
    /// <summary>The folders that are not walked, as they are written down.</summary>
    private readonly List<string> _places;

    /// <summary>The plugins that are not offered, by id.</summary>
    private readonly List<string> _plugins;

    /// <summary>How to say the settings have moved, or nothing where they are not kept.</summary>
    private readonly Action? _moved;

    /// <param name="places">The folders switched off, which is the settings' own list.</param>
    /// <param name="plugins">The plugins switched off, by id, which is the settings' own list.</param>
    /// <param name="moved">
    /// How to say the settings changed. Left out, nothing is written down, which is what a test
    /// and a picker shown with no settings behind it want.
    /// </param>
    public PluginSwitches(List<string>? places = null, List<string>? plugins = null, Action? moved = null)
    {
        _places = places ?? new List<string>();
        _plugins = plugins ?? new List<string>();
        _moved = moved;
    }

    /// <summary>The word a format is written under, which is what a settings line and a scan use.</summary>
    /// <param name="format">The standard.</param>
    public static string Word(PluginFormat format) => format == PluginFormat.Clap ? "clap" : "vst3";

    /// <summary>One place as one line, which is how it is stored and how a scan is told.</summary>
    /// <param name="place">The folder and the format.</param>
    public static string Said(PluginPlace place) => Word(place.Format) + ":" + place.Path;

    /// <summary>
    /// A line read back, or nothing where this build has no word for it.
    /// </summary>
    /// <remarks>
    /// Nothing rather than a guess, since a line naming a standard this build has never heard of
    /// is a folder somebody switched off in a later version, and reading it as one of ours would
    /// switch the wrong folder off.
    /// </remarks>
    /// <param name="said">The line.</param>
    public static PluginPlace? Read(string? said)
    {
        if (said is null) return null;

        int colon = said.IndexOf(':');
        if (colon <= 0 || colon == said.Length - 1) return null;

        string word = said[..colon];
        string path = said[(colon + 1)..];

        if (string.Equals(word, Word(PluginFormat.Clap), StringComparison.OrdinalIgnoreCase))
            return new PluginPlace(path, PluginFormat.Clap);

        if (string.Equals(word, Word(PluginFormat.Vst3), StringComparison.OrdinalIgnoreCase))
            return new PluginPlace(path, PluginFormat.Vst3);

        return null;
    }

    /// <inheritdoc/>
    public bool Wanted(PluginPlace? place) =>
        place is null || !_places.Any(one => Same(one, place));

    /// <inheritdoc/>
    public bool Wanted(PluginInfo? plugin) =>
        plugin is null
        || plugin.Id.Length == 0
        || !_plugins.Any(one => string.Equals(one, plugin.Id, StringComparison.Ordinal));

    /// <inheritdoc/>
    public bool Turn(PluginPlace? place, bool on)
    {
        if (place is null) return false;

        bool moved = on
            ? _places.RemoveAll(one => Same(one, place)) > 0
            : Wanted(place) && Added(Said(place));

        if (moved) _moved?.Invoke();

        return moved;
    }

    /// <inheritdoc/>
    public bool Turn(PluginInfo? plugin, bool on)
    {
        if (plugin is null || plugin.Id.Length == 0) return false;

        bool moved = on
            ? _plugins.RemoveAll(one => string.Equals(one, plugin.Id, StringComparison.Ordinal)) > 0
            : Wanted(plugin) && Added(plugin.Id, _plugins);

        if (moved) _moved?.Invoke();

        return moved;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PluginPlace> Off =>
        _places.Select(Read).Where(one => one is not null).Select(one => one!).ToList();

    /// <summary>Writes a line down, for a list that keeps one entry per thing.</summary>
    /// <param name="said">The line to add.</param>
    /// <param name="into">Which list, defaulting to the folders'.</param>
    private bool Added(string said, List<string>? into = null)
    {
        (into ?? _places).Add(said);

        return true;
    }

    /// <summary>Whether a stored line is about that place, path and format both.</summary>
    /// <remarks>
    /// The path without regard to case, which is right on Windows and merely cautious here, and
    /// the format exactly, since those two words are ours.
    /// </remarks>
    /// <param name="said">A line as it is stored.</param>
    /// <param name="place">The place being asked about.</param>
    private static bool Same(string said, PluginPlace place) =>
        Read(said) is { } one
        && one.Format == place.Format
        && string.Equals(one.Path, place.Path, StringComparison.OrdinalIgnoreCase);
}
