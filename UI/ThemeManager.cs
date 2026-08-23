using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.UI;

public static class ThemeManager
{
    /// <summary>
    /// The theme files, written out. Every one of these strings appears here literally so that
    /// a search for a theme's resource finds it, and so a renamed or deleted .axaml is a
    /// compile-time-visible edit here rather than a resource that quietly fails to load.
    /// </summary>
    private const string BaseUri = "avares://JingleBox2/Themes/Base.axaml";
    private const string DarkUri = "avares://JingleBox2/Themes/Dark.axaml";
    private const string NeonUri = "avares://JingleBox2/Themes/Neon.axaml";
    private const string IndustrialUri = "avares://JingleBox2/Themes/Industrial.axaml";
    private const string LightUri = "avares://JingleBox2/Themes/Light.axaml";

    /// <summary>The base sheet every theme is layered on top of.</summary>
    public const string BaseSheet = BaseUri;

    /// <summary>
    /// Which file each theme is, in the order the picker offers them. The set is closed: a
    /// theme exists because it is named here, not because a file happens to be beside the others.
    /// </summary>
    private static readonly Dictionary<string, string> Sheets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Dark"] = DarkUri,
            ["Neon"] = NeonUri,
            ["Industrial"] = IndustrialUri,
            ["Light"] = LightUri
        };

    /// <summary>The themes there are, in picker order.</summary>
    public static IReadOnlyList<string> Names { get; } = Sheets.Keys.ToArray();

    public const string Default = "Dark";

    public static string CurrentTheme { get; private set; } = Default;

    /// <summary>
    /// The name this one really is, or <see cref="Default"/> when it is nothing we have.
    /// </summary>
    public static string Resolve(string? themeName)
    {
        var wanted = themeName?.Trim();

        if (string.IsNullOrEmpty(wanted)) return Default;

        return Names.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase))
               ?? Default;
    }

    /// <summary>The file a theme is, or the default's file when it is nothing we have.</summary>
    public static string SheetFor(string? themeName) => Sheets[Resolve(themeName)];

    public static void Apply(string themeName)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("Application not ready");

        var resolved = Resolve(themeName);

        // ThemeVariant drives Fluent defaults (Light/Dark behaviors)
        app.RequestedThemeVariant = resolved.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        app.Resources.MergedDictionaries.Clear();

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri(BaseSheet)
        });

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri(SheetFor(resolved))
        });

        CurrentTheme = resolved;
    }
}
