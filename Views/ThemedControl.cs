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
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

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
