using System;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class ThemeCatalogue : IThemeCatalogue
{
    /// <summary>
    /// The theme files, written out. Every one of these strings appears here literally so that
    /// a search for a theme's resource finds it, and so a renamed or deleted .axaml is a
    /// compile-time-visible edit here rather than a resource that quietly fails to load.
    /// </summary>
    private const string BaseUri = "avares://JingleBox2/Themes/Base.axaml";

    private const string DarkUri = "avares://JingleBox2/Themes/Dark.axaml";
    private const string LightUri = "avares://JingleBox2/Themes/Light.axaml";

    private const string NeonDarkUri = "avares://JingleBox2/Themes/NeonDark.axaml";
    private const string NeonLightUri = "avares://JingleBox2/Themes/NeonLight.axaml";

    private const string IndustrialDarkUri = "avares://JingleBox2/Themes/IndustrialDark.axaml";
    private const string IndustrialLightUri = "avares://JingleBox2/Themes/IndustrialLight.axaml";

    private const string OrchidDarkUri = "avares://JingleBox2/Themes/OrchidDark.axaml";
    private const string OrchidLightUri = "avares://JingleBox2/Themes/OrchidLight.axaml";

    private const string CitrusDarkUri = "avares://JingleBox2/Themes/CitrusDark.axaml";
    private const string CitrusLightUri = "avares://JingleBox2/Themes/CitrusLight.axaml";

    private const string EmberDarkUri = "avares://JingleBox2/Themes/EmberDark.axaml";
    private const string EmberLightUri = "avares://JingleBox2/Themes/EmberLight.axaml";

    /// <inheritdoc cref="IThemeCatalogue.BaseSheet"/>
    public const string Base = BaseUri;

    /// <inheritdoc/>
    string IThemeCatalogue.BaseSheet => Base;

    /// <inheritdoc cref="IThemeCatalogue.Default"/>
    public const string Plain = "Dark";

    /// <inheritdoc/>
    string IThemeCatalogue.Default => Plain;

    /// <summary>A theme: the file it is, and whether its room is a lit one.</summary>
    private readonly record struct Sheet(string Uri, bool Lit);

    /// <summary>Which file each theme is, in the order the picker offers them.</summary>
    private static readonly Dictionary<string, Sheet> Sheets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dark"] = new(DarkUri, false),
            ["Light"] = new(LightUri, true),

            ["Neon Dark"] = new(NeonDarkUri, false),
            ["Neon Light"] = new(NeonLightUri, true),

            ["Industrial Dark"] = new(IndustrialDarkUri, false),
            ["Industrial Light"] = new(IndustrialLightUri, true),

            ["Orchid Dark"] = new(OrchidDarkUri, false),
            ["Orchid Light"] = new(OrchidLightUri, true),

            ["Citrus Dark"] = new(CitrusDarkUri, false),
            ["Citrus Light"] = new(CitrusLightUri, true),

            ["Ember Dark"] = new(EmberDarkUri, false),
            ["Ember Light"] = new(EmberLightUri, true)
        };

    /// <summary>
    /// What a theme used to be called, and what it is now.
    /// </summary>
    /// <remarks>
    /// A name is saved in config.json, so the coloured themes gaining a second half cannot be
    /// allowed to turn somebody's chosen theme back into the default the next time they open
    /// the app. Every one of these was the whole family before it was the dark half of it.
    /// </remarks>
    private static readonly Dictionary<string, string> WasCalled =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Neon"] = "Neon Dark",
            ["Industrial"] = "Industrial Dark",
            ["Orchid"] = "Orchid Dark",
            ["Citrus"] = "Citrus Dark",
            ["Ember"] = "Ember Dark"
        };

    /// <inheritdoc cref="IThemeCatalogue.Names"/>
    public static IReadOnlyList<string> Every { get; } = Sheets.Keys.ToArray();

    /// <inheritdoc/>
    IReadOnlyList<string> IThemeCatalogue.Names => Every;

    /// <inheritdoc/>
    public string Resolve(string? themeName)
    {
        var wanted = themeName?.Trim();

        if (string.IsNullOrEmpty(wanted)) return Plain;

        var found = Every.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));

        if (found != null) return found;

        return WasCalled.TryGetValue(wanted, out var now) ? now : Plain;
    }

    /// <inheritdoc/>
    public string SheetFor(string? themeName) => Sheets[Resolve(themeName)].Uri;

    /// <inheritdoc/>
    public bool IsLit(string? themeName) => Sheets[Resolve(themeName)].Lit;
}
