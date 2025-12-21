using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;

namespace JingleBox2.UI;

public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "Dark";

    public static void Apply(string themeName)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("Application not ready");

        // ThemeVariant drives Fluent defaults (Light/Dark behaviors)
        app.RequestedThemeVariant = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        app.Resources.MergedDictionaries.Clear();

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri("avares://JingleBox2/themes/Base.axaml")
        });

        app.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://JingleBox2"))
        {
            Source = new Uri($"avares://JingleBox2/themes/{themeName}.axaml")
        });

        CurrentTheme = themeName;
    }
}
