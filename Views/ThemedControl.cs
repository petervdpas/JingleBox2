using Avalonia;
using Avalonia.Controls;
using System;

namespace JingleBox2.Views;

/// <summary>
/// A control that paints itself and has to be told when the theme moves under it.
/// </summary>
/// <remarks>
/// Styles repaint a templated control on a theme swap. A control that draws in
/// <c>Render</c> hears nothing, so without this it keeps the colours it was last painted
/// with: a knob stays green in a magenta theme until something else happens to invalidate it.
/// </remarks>
public abstract class ThemedControl : Control
{
    /// <summary>
    /// How much of itself a control shows when it cannot be used.
    /// </summary>
    /// <remarks>
    /// Faint enough to read as out of reach, solid enough to still be read. A control that has
    /// nothing to do right now is greyed rather than taken away, because a panel that grows
    /// and shrinks depending on what is running is a different panel each time you look at it.
    /// </remarks>
    private const double DisabledOpacity = 0.32;

    /// <summary>
    /// Greys the control out when it, or anything it sits inside, has been disabled.
    /// </summary>
    /// <remarks>
    /// A templated control gets this from its theme. One that paints itself in <c>Render</c>
    /// draws exactly the same thing whether it is enabled or not, so it has to be dimmed here
    /// or a dead knob looks like a live one.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEffectivelyEnabledProperty)
            Opacity = IsEffectivelyEnabled ? 1 : DisabledOpacity;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Whatever it is attached under may already be disabled, and nothing changed to say so.
        Opacity = IsEffectivelyEnabled ? 1 : DisabledOpacity;

        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;

        InvalidateVisual();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => InvalidateVisual();
}
