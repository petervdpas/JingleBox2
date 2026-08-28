using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <summary>
/// Which theme the application is wearing, and the swap that puts another one on.
/// </summary>
/// <remarks>
/// Static, and one of the three doors here that is. An application wears one theme, the
/// toolkit's resources are one set, and the things that mixed a colour of their own subscribe
/// from wherever they happen to be built, which is why the notice is an event rather than
/// something handed about. Nothing here decides anything: which themes there are, what file
/// each is and what a name out of a settings file really means is <see cref="IThemeCatalogue"/>,
/// which can be asked without an application being up.
///
/// Two sheets are merged, always: a base every theme is layered on, and the theme itself. So a
/// theme file says only what it changes, and a rule that is the same everywhere is written once.
/// </remarks>
public static class ThemeSwitch
{
    /// <summary>Which themes there are and what each one is. Holds nothing, so one is enough.</summary>
    private static readonly IThemeCatalogue Catalogue = new ThemeCatalogue();

    /// <inheritdoc cref="IThemeCatalogue.BaseSheet"/>
    public const string BaseSheet = ThemeCatalogue.Base;

    /// <inheritdoc cref="IThemeCatalogue.Default"/>
    public const string Default = ThemeCatalogue.Plain;

    /// <inheritdoc cref="IThemeCatalogue.Names"/>
    public static IReadOnlyList<string> Names => Catalogue.Names;

    /// <summary>Which theme is on now.</summary>
    public static string CurrentTheme { get; private set; } = Default;

    /// <inheritdoc cref="IThemeCatalogue.Resolve"/>
    /// <param name="themeName">What the settings say, or null.</param>
    public static string Resolve(string? themeName) => Catalogue.Resolve(themeName);

    /// <inheritdoc cref="IThemeCatalogue.SheetFor"/>
    /// <param name="themeName">What the settings say, or null.</param>
    public static string SheetFor(string? themeName) => Catalogue.SheetFor(themeName);

    /// <summary>Puts a theme on, and tells anything that had mixed a colour of its own.</summary>
    /// <remarks>
    /// The requested variant is set as well as the sheet, because Fluent draws its own parts from
    /// it: the popups, the scrollbars and the text boxes. A pale sheet under the dark variant gets
    /// dark popups over a white page.
    /// </remarks>
    /// <param name="themeName">Which theme, resolved through <see cref="Resolve"/>.</param>
    /// <exception cref="InvalidOperationException">The application is not up yet.</exception>
    public static void Apply(string themeName)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("Application not ready");

        var resolved = Catalogue.Resolve(themeName);

        app.RequestedThemeVariant = Catalogue.IsLit(resolved) ? ThemeVariant.Light : ThemeVariant.Dark;

        app.Resources.MergedDictionaries.Clear();

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri(Catalogue.BaseSheet)
        });

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri(Catalogue.SheetFor(resolved))
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
