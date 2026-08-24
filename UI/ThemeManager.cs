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

    /// <summary>The base sheet every theme is layered on top of.</summary>
    public const string BaseSheet = BaseUri;

    /// <summary>A theme: the file it is, and whether its room is a lit one.</summary>
    /// <remarks>
    /// Whether it is lit is written down rather than worked out from the background colour, so
    /// that adding a theme makes somebody say which kind it is. It is not decoration: Fluent
    /// draws its own parts from the variant, the popups, the scrollbars, the text boxes, and a
    /// pale sheet under the dark variant gets dark popups over a white page.
    /// </remarks>
    private readonly record struct Sheet(string Uri, bool Lit);

    /// <summary>
    /// Which file each theme is, in the order the picker offers them. The set is closed: a
    /// theme exists because it is named here, not because a file happens to be beside the others.
    /// </summary>
    /// <remarks>
    /// In pairs, dark then light, because that is how somebody looks for one: they know the
    /// colours they want and then which room they are sitting in. The plain pair comes first
    /// and is called nothing else, having no colour to be named after.
    /// </remarks>
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

        var found = Names.FirstOrDefault(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));

        if (found != null) return found;

        return WasCalled.TryGetValue(wanted, out var now) ? now : Default;
    }

    /// <summary>The file a theme is, or the default's file when it is nothing we have.</summary>
    public static string SheetFor(string? themeName) => Sheets[Resolve(themeName)].Uri;

    public static void Apply(string themeName)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("Application not ready");

        var resolved = Resolve(themeName);
        var sheet = Sheets[resolved];

        // ThemeVariant drives Fluent defaults (Light/Dark behaviors)
        app.RequestedThemeVariant = sheet.Lit ? ThemeVariant.Light : ThemeVariant.Dark;

        app.Resources.MergedDictionaries.Clear();

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri(BaseSheet)
        });

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri(sheet.Uri)
        });

        CurrentTheme = resolved;

        Changed?.Invoke();
    }

    /// <summary>
    /// The theme has been swapped.
    /// </summary>
    /// <remarks>
    /// For anything holding a colour it worked out from the theme's own: a machine panel is
    /// painted in its machine's shade of the theme's surface, and that has to be mixed again
    /// against the new one. Avalonia's own resource change reaches everything bound to a
    /// resource; this reaches the few things that read one and then did arithmetic on it.
    /// </remarks>
    public static event Action? Changed;
}
