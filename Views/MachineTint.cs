using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Linq;
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

        if (Shades(machine) is not { } shade) return;

        Set(panel, BackgroundKey, BackgroundBrushKey, shade.Face);
        Set(panel, SurfaceKey, SurfaceBrushKey, shade.Panel);
        Set(panel, BorderKey, BorderBrushKey, shade.Edge);
        Set(panel, AccentKey, AccentBrushKey, shade.Mark);
        Set(panel, TextKey, TextBrushKey, shade.Ink);
        Set(panel, MutedKey, MutedBrushKey, shade.Muted);
    }

    /// <summary>
    /// The same, and every drawn control inside it told to draw itself again.
    /// </summary>
    /// <remarks>
    /// A control bound to the theme's brushes hears a resource change on its own. One that
    /// paints itself does not: it reads the colours once per render, and nothing has asked it
    /// to render. That is invisible while a machine is only ever tinted as it opens, and it is
    /// the whole of the feedback while somebody is moving the colour about, so the panel is
    /// told outright.
    /// </remarks>
    public static void Repaint(Control panel, Machines.MachineTheme? machine)
    {
        Apply(panel, machine);

        foreach (var inside in panel.GetVisualDescendants()) inside.InvalidateVisual();
    }

    /// <summary>
    /// What a machine's theme comes to, once the distances have been worked out from its colour.
    /// </summary>
    /// <remarks>
    /// Here rather than inside <see cref="Apply"/> because two things want the answer and only
    /// one of them is a panel: somebody setting the distances has to be shown what they do, and
    /// a preview drawn from a second copy of the arithmetic is a preview of something else.
    /// </remarks>
    public static MachineShades? Shades(Machines.MachineTheme? machine)
    {
        if (machine == null) return null;

        if (!Hue(machine.Accent, out var hue)) return null;

        var face = Mix(hue, Colors.Black, machine.Face);

        // Whatever the face turned out to be, the lettering has to be readable on it. A pale
        // machine gets dark lettering the same way a dark one gets pale.
        var ink = Light(face) ? Color.FromRgb(0x14, 0x16, 0x1A) : Colors.White;

        return new MachineShades(
            face,
            Mix(hue, Colors.Black, machine.Panel),
            Mix(hue, Colors.White, machine.Edge),
            Mix(hue, Colors.White, machine.Mark),
            ink,
            Mix(ink, face, 0.42));
    }

    /// <summary>A colour written the way a machine writes one down.</summary>
    public static string Hex(Color colour) =>
        "#" + colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2");

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

    /// <summary>One colour with that much of another mixed into it.</summary>
    /// <remarks>
    /// The whole of the recipe. A machine's face is its colour with black mixed in, and a row on
    /// a list is whatever the list stands on with the machine's colour mixed in: the same sum
    /// read from either end.
    /// </remarks>
    private static Color Mix(Color from, Color to, double amount) => Color.FromArgb(
        from.A,
        Blend(from.R, to.R, amount),
        Blend(from.G, to.G, amount),
        Blend(from.B, to.B, amount));

    private static byte Blend(byte from, byte to, double amount) =>
        (byte)Math.Clamp(Math.Round(from + (to - from) * amount), 0, 255);
}
