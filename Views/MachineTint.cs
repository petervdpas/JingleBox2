using Avalonia.Controls;
using Avalonia.Media;
using System;
using JingleBox2.Machines;
using JingleBox2.Machines.Ui;

namespace JingleBox2.Views;

/// <summary>
/// Paints a panel in the machine's own colour.
/// </summary>
/// <remarks>
/// A rack device is one colour all over: the chassis, the legends, the marks on the knobs.
/// Doing that here means putting the machine's colour where the theme's is, on the panel
/// itself, so everything inside it, drawn controls reading <see cref="ThemePalette"/> and
/// borders bound to the theme's brushes alike, reads the machine's shade instead of the
/// application's without knowing anything has changed.
///
/// The lettering follows the face rather than the theme: a pale machine gets dark lettering
/// and a dark one gets pale, so a panel is readable wherever it is standing.
/// </remarks>
public static class MachineTint
{
    // Declared, not built from a variable, so the keys stay greppable.
    private const string BackgroundKey = "Color.Background";
    private const string SurfaceKey = "Color.Surface";
    private const string BorderKey = "Color.Border";
    private const string AccentKey = "Color.Accent";
    private const string TextKey = "Color.TextPrimary";
    private const string MutedKey = "Color.TextMuted";

    private const string BackgroundBrushKey = "BgBrush";
    private const string SurfaceBrushKey = "SurfaceBrush";
    private const string BorderBrushKey = "BorderBrush";
    private const string AccentBrushKey = "AccentBrush";
    private const string TextBrushKey = "TextPrimaryBrush";
    private const string MutedBrushKey = "TextMutedBrush";

    /// <summary>
    /// Puts the machine's shades on the panel, or takes them off again when there is no
    /// machine to show.
    /// </summary>
    public static void Apply(Control panel, Machines.MachineTheme? machine)
    {
        // Off first, so what shows through is the application's own and not the last machine's.
        Clear(panel);

        if (machine == null) return;

        if (!Hue(machine.Accent, out var hue)) return;

        var face = Mix(hue, Colors.Black, machine.Face);
        var group = Mix(hue, Colors.Black, machine.Panel);

        // Whatever the face turned out to be, the lettering has to be readable on it. A pale
        // machine gets dark lettering the same way a dark one gets pale.
        var ink = Light(face) ? Color.FromRgb(0x14, 0x16, 0x1A) : Colors.White;

        Set(panel, BackgroundKey, BackgroundBrushKey, face);
        Set(panel, SurfaceKey, SurfaceBrushKey, group);
        Set(panel, BorderKey, BorderBrushKey, Mix(hue, Colors.White, machine.Edge));
        Set(panel, AccentKey, AccentBrushKey, Mix(hue, Colors.White, machine.Mark));
        Set(panel, TextKey, TextBrushKey, ink);
        Set(panel, MutedKey, MutedBrushKey, Mix(ink, face, 0.42));
    }

    /// <summary>The colour a machine is painted in, or nothing when it does not say.</summary>
    public static bool Hue(string? colour, out Color hue)
    {
        hue = default;

        if (string.IsNullOrWhiteSpace(colour)) return false;

        try
        {
            hue = Color.Parse(colour);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>True when a colour is pale enough to need dark lettering on it.</summary>
    private static bool Light(Color colour) =>
        (0.2126 * colour.R + 0.7152 * colour.G + 0.0722 * colour.B) / 255.0 > 0.5;

    private static void Clear(Control panel)
    {
        foreach (string key in new[]
                 {
                     BackgroundKey, SurfaceKey, BorderKey, AccentKey, TextKey, MutedKey,
                     BackgroundBrushKey, SurfaceBrushKey, BorderBrushKey, AccentBrushKey,
                     TextBrushKey, MutedBrushKey
                 })
        {
            panel.Resources.Remove(key);
        }
    }

    private static void Set(Control panel, string colourKey, string brushKey, Color colour)
    {
        panel.Resources[colourKey] = colour;
        panel.Resources[brushKey] = new SolidColorBrush(colour);
    }

    /// <summary>The theme's colour with that much of the machine's mixed into it.</summary>
    private static Color Mix(Color theme, Color machine, double amount) => Color.FromArgb(
        theme.A,
        Blend(theme.R, machine.R, amount),
        Blend(theme.G, machine.G, amount),
        Blend(theme.B, machine.B, amount));

    private static byte Blend(byte from, byte to, double amount) =>
        (byte)Math.Clamp(Math.Round(from + (to - from) * amount), 0, 255);
}
