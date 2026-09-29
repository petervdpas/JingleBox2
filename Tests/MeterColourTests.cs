using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// Every theme says what its meters are lit in, all three colours of them.
/// </summary>
/// <remarks>
/// A theme that leaves one out is a meter lit in the fallback's green, orange or red in the middle
/// of a theme that chose otherwise, which nothing on the screen would explain.
/// </remarks>
public sealed class MeterColourTests
{
    /// <summary>The three keys, as the meter reads them.</summary>
    private static readonly string[] Keys =
    [
        "Color.MeterSafe",
        "Color.MeterWarn",
        "Color.MeterHot"
    ];

    /// <summary>Every theme but the one the rest are written over carries all three.</summary>
    [Fact]
    public void Every_theme_lights_its_own_meters()
    {
        string themes = Path.Combine(Sources(), "Themes");
        Assert.True(Directory.Exists(themes), "no Themes folder at " + themes);

        var files = Directory.GetFiles(themes, "*.axaml")
            .Where(file => Path.GetFileName(file) != "Base.axaml")
            .ToList();

        Assert.NotEmpty(files);

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);

            foreach (string key in Keys)
                Assert.True(text.Contains("x:Key=\"" + key + "\"", StringComparison.Ordinal),
                    Path.GetFileName(file) + " does not say " + key);
        }
    }

    /// <summary>
    /// No theme lights its meters in its own accent at ordinary levels.
    /// </summary>
    /// <remarks>
    /// Most of what is metered sits under the warning zone, so a safe colour that is the accent
    /// is a meter that reads as the old one-colour bar on every page, which is what happened on
    /// every theme but the plain pair.
    /// </remarks>
    [Fact]
    public void No_theme_meters_in_its_accent()
    {
        string themes = Path.Combine(Sources(), "Themes");

        foreach (string file in Directory.GetFiles(themes, "*.axaml").Where(f => Path.GetFileName(f) != "Base.axaml"))
        {
            string text = File.ReadAllText(file);

            Assert.NotEqual(Colour(text, "Color.Accent"), Colour(text, "Color.MeterSafe"));
        }
    }

    /// <summary>One colour out of a theme file, by its key, or nothing where it is not there.</summary>
    private static string? Colour(string text, string key)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text, "x:Key=\"" + System.Text.RegularExpressions.Regex.Escape(key) + "\">\\s*(#[0-9A-Fa-f]+)");

        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    /// <summary>Where the repository is, walked up from wherever the tests are running.</summary>
    private static string Sources()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at != null && !File.Exists(Path.Combine(at.FullName, "JingleBox2.csproj")))
            at = at.Parent;

        return at?.FullName ?? "";
    }
}
