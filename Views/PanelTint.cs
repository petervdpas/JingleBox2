using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using JingleBox2.Views.Records;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
public sealed class PanelTint : IPanelTint
{
    /// <summary>
    /// The theme keys a panel's own resources stand in front of, every one declared rather than
    /// built out of a variable, so a key can be found from either end by searching for it.
    /// </summary>
    private const string BackgroundKey = "Color.Background";

    /// <inheritdoc cref="BackgroundKey"/>
    private const string SurfaceKey = "Color.Surface";

    /// <inheritdoc cref="BackgroundKey"/>
    private const string BorderKey = "Color.Border";

    /// <inheritdoc cref="BackgroundKey"/>
    private const string AccentKey = "Color.Accent";

    /// <inheritdoc cref="BackgroundKey"/>
    private const string TextKey = "Color.TextPrimary";

    /// <inheritdoc cref="BackgroundKey"/>
    private const string MutedKey = "Color.TextMuted";

    /// <summary>
    /// And the brush beside each colour, because a control bound to the theme takes the brush
    /// and one that paints itself reads the colour. Both have to be put back or half the panel
    /// would follow the machine and half would follow the application.
    /// </summary>
    private const string BackgroundBrushKey = "BgBrush";

    /// <inheritdoc cref="BackgroundBrushKey"/>
    private const string SurfaceBrushKey = "SurfaceBrush";

    /// <inheritdoc cref="BackgroundBrushKey"/>
    private const string BorderBrushKey = "BorderBrush";

    /// <inheritdoc cref="BackgroundBrushKey"/>
    private const string AccentBrushKey = "AccentBrush";

    /// <inheritdoc cref="BackgroundBrushKey"/>
    private const string TextBrushKey = "TextPrimaryBrush";

    /// <inheritdoc cref="BackgroundBrushKey"/>
    private const string MutedBrushKey = "TextMutedBrush";

    /// <inheritdoc/>
    public void Apply(Control panel, Rack.SoundDevices.Faces.Records.PanelTheme? machine)
    {
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

    /// <inheritdoc/>
    public void Repaint(Control panel, Rack.SoundDevices.Faces.Records.PanelTheme? machine)
    {
        Apply(panel, machine);

        foreach (var inside in panel.GetVisualDescendants()) inside.InvalidateVisual();
    }

    /// <inheritdoc/>
    public PanelShades? Shades(Rack.SoundDevices.Faces.Records.PanelTheme? machine)
    {
        if (machine == null) return null;

        if (!Hue(machine.Accent, out var hue)) return null;

        var face = Mix(hue, Colors.Black, machine.Face);

        var ink = Light(face) ? Color.FromRgb(0x14, 0x16, 0x1A) : Colors.White;

        return new PanelShades(
            face,
            Mix(hue, Colors.Black, machine.Panel),
            Mix(hue, Colors.White, machine.Edge),
            Mix(hue, Colors.White, machine.Mark),
            ink,
            Mix(ink, face, 0.42));
    }

    /// <inheritdoc/>
    public string Hex(Color colour) =>
        "#" + colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2");

    /// <inheritdoc/>
    /// <inheritdoc/>
    public IBrush Wash(string? colour, double amount) =>
        Hue(colour, out var hue) ? new SolidColorBrush(hue, amount) : Brushes.Transparent;

    /// <inheritdoc/>
    public bool Hue(string? colour, out Color hue)
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
    private bool Light(Color colour) =>
        (0.2126 * colour.R + 0.7152 * colour.G + 0.0722 * colour.B) / 255.0 > 0.5;

    /// <summary>Takes every key back off the panel, so nothing of a machine's is left behind.</summary>
    private void Clear(Control panel)
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

    /// <summary>One shade, as both the colour and the brush, since the panel is read both ways.</summary>
    private void Set(Control panel, string colourKey, string brushKey, Color colour)
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
    private Color Mix(Color from, Color to, double amount) => Color.FromArgb(
        from.A,
        Blend(from.R, to.R, amount),
        Blend(from.G, to.G, amount),
        Blend(from.B, to.B, amount));

    /// <summary>One channel of that mix, rounded and held inside a byte.</summary>
    private byte Blend(byte from, byte to, double amount) =>
        (byte)Math.Clamp(Math.Round(from + (to - from) * amount), 0, 255);
}
