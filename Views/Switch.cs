using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// A panel switch: a metal handle that sits in one of two or three positions, with what each
/// position means written beside it.
/// </summary>
/// <remarks>
/// A drop-down is the wrong shape for something with two answers. On a machine's front panel
/// the positions are all visible at once and the handle says which one you are on without being
/// opened, which is the whole reason hardware uses a toggle here: you read it at a glance and
/// you throw it without aiming.
///
/// Two ways to use it, because a panel has both kinds. <see cref="IsChecked"/> for a plain on
/// and off, and <see cref="ItemsSource"/> with <see cref="SelectedItem"/> for a switch whose
/// positions are the values of an enum, which is the same pair a combo box takes so one can be
/// swapped for the other without touching the binding.
/// </remarks>
public class Switch : ThemedControl
{
    private const double LabelGap = 7;
    private const double TitleGap = 4;

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Switch, string?>(nameof(Label));

    /// <summary>How wide the recess is. Everything else on the switch is sized from it.</summary>
    public static readonly StyledProperty<double> SlotWidthProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(SlotWidth), 21.0);

    /// <summary>How far the handle travels, when the positions stack around it.</summary>
    public static readonly StyledProperty<double> SlotHeightProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(SlotHeight), 26.0);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(FontSize), 8.5);

    /// <summary>The name over the top. Smaller than the positions by default.</summary>
    public static readonly StyledProperty<double> TitleSizeProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(TitleSize), 10.0);

    /// <summary>How many lines of room the name gets, so a row of these lines up.</summary>
    public static readonly StyledProperty<int> TitleLinesProperty =
        AvaloniaProperty.Register<Switch, int>(nameof(TitleLines), 1);

    /// <summary>
    /// How far down the switch itself starts, so it stands on the same line as the knobs
    /// beside it. Zero lets it follow its own name.
    /// </summary>
    /// <remarks>
    /// A switch carries a word above its handle and a knob does not, so left alone the two sit
    /// at different heights in the same row. Telling both what to reserve puts them on one
    /// line, which is what a panel does with a scribe line.
    /// </remarks>
    public static readonly StyledProperty<double> HeadRoomProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(HeadRoom));

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<Switch, bool>(nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>What the two positions are called when this is a plain on and off.</summary>
    public static readonly StyledProperty<string> OnLabelProperty =
        AvaloniaProperty.Register<Switch, string>(nameof(OnLabel), "On");

    public static readonly StyledProperty<string> OffLabelProperty =
        AvaloniaProperty.Register<Switch, string>(nameof(OffLabel), "Off");

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Switch, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<Switch, object?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    static Switch()
    {
        AffectsRender<Switch>(
            LabelProperty, IsCheckedProperty, OnLabelProperty, OffLabelProperty,
            ItemsSourceProperty, SelectedItemProperty);

        AffectsMeasure<Switch>(
            LabelProperty, OnLabelProperty, OffLabelProperty, ItemsSourceProperty,
            SlotWidthProperty, SlotHeightProperty, FontSizeProperty, TitleSizeProperty, TitleLinesProperty, HeadRoomProperty);

        AffectsRender<Switch>(SlotWidthProperty, SlotHeightProperty, FontSizeProperty, TitleSizeProperty, TitleLinesProperty, HeadRoomProperty);

        FocusableProperty.OverrideDefaultValue<Switch>(true);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double SlotWidth
    {
        get => GetValue(SlotWidthProperty);
        set => SetValue(SlotWidthProperty, value);
    }

    public double SlotHeight
    {
        get => GetValue(SlotHeightProperty);
        set => SetValue(SlotHeightProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public double TitleSize
    {
        get => GetValue(TitleSizeProperty);
        set => SetValue(TitleSizeProperty, value);
    }

    public int TitleLines
    {
        get => GetValue(TitleLinesProperty);
        set => SetValue(TitleLinesProperty, value);
    }

    public double HeadRoom
    {
        get => GetValue(HeadRoomProperty);
        set => SetValue(HeadRoomProperty, value);
    }

    /// <summary>The room the name is given, used or not.</summary>
    private double TitleRoom(FormattedText title) =>
        Math.Max(title.Height, TitleSize * 1.35 * Math.Max(1, TitleLines));

    /// <summary>The handle, sized from the recess it sits in so the two cannot drift apart.</summary>
    private double HandleWidth => Math.Max(6, SlotWidth - 6);

    private double HandleHeight => Math.Max(5, SlotWidth * 0.52);

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string OnLabel
    {
        get => GetValue(OnLabelProperty);
        set => SetValue(OnLabelProperty, value);
    }

    public string OffLabel
    {
        get => GetValue(OffLabelProperty);
        set => SetValue(OffLabelProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>The positions, top to bottom, however this switch was set up.</summary>
    private IReadOnlyList<object> Positions
    {
        get
        {
            if (ItemsSource is IEnumerable items)
            {
                var list = items.Cast<object>().ToList();
                if (list.Count > 0) return list;
            }

            return new object[] { OnLabel, OffLabel };
        }
    }

    /// <summary>Which position the handle is in. Zero is the top.</summary>
    private int Index
    {
        get
        {
            if (ItemsSource == null) return IsChecked ? 0 : 1;

            var positions = Positions;
            for (int index = 0; index < positions.Count; index++)
            {
                if (Equals(positions[index], SelectedItem)) return index;
            }

            return 0;
        }
    }

    private void Move(int index)
    {
        var positions = Positions;
        if (positions.Count == 0) return;

        index = Math.Clamp(index, 0, positions.Count - 1);

        if (ItemsSource == null) IsChecked = index == 0;
        else SelectedItem = positions[index];
    }

    /// <summary>Round to the next position, so a click is always a change.</summary>
    private void Step(int delta)
    {
        int count = Positions.Count;
        if (count == 0) return;

        Move((Index + delta % count + count) % count);
    }

    /// <summary>
    /// True when the positions stack around the handle rather than sitting beside it.
    /// </summary>
    /// <remarks>
    /// The way a panel prints a two position toggle: the name on top, one answer above the
    /// handle and the other below it. Narrow enough to stand in a row of knobs, which is the
    /// point. Three positions will not stack that way, so those keep their names alongside.
    /// </remarks>
    private bool Stacked => Positions.Count == 2;

    protected override Size MeasureOverride(Size availableSize)
    {
        _room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;

        var positions = Positions;

        double widest = 0;
        double lines = 0;

        foreach (var position in positions)
        {
            var text = Text(Naming.Of(position), FontSize, Brushes.Black);
            widest = Math.Max(widest, text.Width);
            lines += text.Height;
        }

        double width;
        double height;

        if (Stacked)
        {
            width = Math.Max(widest, SlotWidth);
            height = Math.Max(lines + SlotHeight + LabelGap * 2,
                              HeadRoom + SlotHeight + LabelGap + lines / 2);
        }
        else
        {
            height = Math.Max(HandleHeight * positions.Count + 6, lines);
            width = SlotWidth + LabelGap + widest;
        }

        if (!string.IsNullOrEmpty(Label))
        {
            var title = Text(Label, TitleSize, Brushes.Black, _room);
            width = Math.Max(width, title.Width);
            height += TitleRoom(title) + TitleGap;
        }

        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);
        var positions = Positions;
        if (positions.Count == 0) return;

        double top = 0;

        if (!string.IsNullOrEmpty(Label))
        {
            var title = Text(Label, TitleSize, palette.MutedBrush, Bounds.Width);
            context.DrawText(title, new Point((Bounds.Width - title.Width) / 2, 0));
            top = TitleRoom(title) + TitleGap;
        }

        if (Stacked) RenderStacked(context, palette, positions, top);
        else RenderBeside(context, palette, positions, top);
    }

    /// <summary>Name on top, one answer above the handle and the other below it.</summary>
    private void RenderStacked(DrawingContext context, ThemePalette palette, IReadOnlyList<object> positions, double top)
    {
        var upper = Text(Naming.Of(positions[0]), FontSize, Index == 0 ? palette.TextBrush : palette.MutedBrush);
        var lower = Text(Naming.Of(positions[1]), FontSize, Index == 1 ? palette.TextBrush : palette.MutedBrush);

        double middle = Bounds.Width / 2;

        // The slot goes on the scribe line when there is one, and the word above it is fitted
        // into whatever room that leaves rather than pushing the slot down.
        context.DrawText(upper, new Point(middle - upper.Width / 2, top));

        double slotTop = top + upper.Height + LabelGap;
        var slot = new Rect(middle - SlotWidth / 2, slotTop, SlotWidth, SlotHeight);

        Well(context, palette, slot);

        // The handle sits at whichever end is on.
        double handleY = Index == 0 ? slot.Top + 3 : slot.Bottom - HandleHeight - 3;
        Handle(context, palette, new Rect(middle - HandleWidth / 2, handleY, HandleWidth, HandleHeight));

        context.DrawText(lower, new Point(middle - lower.Width / 2, slot.Bottom + LabelGap));
    }

    /// <summary>The positions listed alongside, for a switch with more than two.</summary>
    private void RenderBeside(DrawingContext context, ThemePalette palette, IReadOnlyList<object> positions, double top)
    {
        double slotHeight = Bounds.Height - top;
        var slot = new Rect(0, top, SlotWidth, slotHeight);

        Well(context, palette, slot);

        double span = slotHeight - HandleHeight - 6;
        double step = positions.Count > 1 ? span / (positions.Count - 1) : 0;

        for (int index = 0; index < positions.Count; index++)
        {
            double centre = top + 3 + HandleHeight / 2 + step * index;

            var text = Text(Naming.Of(positions[index]), FontSize,
                index == Index ? palette.TextBrush : palette.MutedBrush);

            context.DrawText(text, new Point(SlotWidth + LabelGap, centre - text.Height / 2));
        }

        double handleCentre = top + 3 + HandleHeight / 2 + step * Index;

        Handle(context, palette, new Rect(
            (SlotWidth - HandleWidth) / 2, handleCentre - HandleHeight / 2, HandleWidth, HandleHeight));
    }

    /// <summary>The recess the handle sits in, darker than the panel around it.</summary>
    private void Well(DrawingContext context, ThemePalette palette, Rect slot)
    {
        var well = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Shade(palette.Surface, -0.45), 0),
                new GradientStop(Shade(palette.Surface, -0.18), 0.5),
                new GradientStop(Shade(palette.Surface, -0.45), 1)
            }
        };

        context.DrawRectangle(well, new Pen(new SolidColorBrush(Shade(palette.Surface, -0.6)), 1),
            slot, slot.Width / 2, slot.Width / 2);
    }

    /// <summary>The cap, lit from above so it stands out of the recess.</summary>
    private void Handle(DrawingContext context, ThemePalette palette, Rect handle)
    {
        var metal = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Shade(palette.Surface, 0.30), 0),
                new GradientStop(Shade(palette.Surface, 0.16), 1)
            }
        };

        context.DrawRectangle(metal,
            new Pen(new SolidColorBrush(IsFocused ? palette.Accent : Shade(palette.Surface, -0.3)), IsFocused ? 1.4 : 1),
            handle, 3, 3);

        double grip = handle.Center.Y;
        context.DrawLine(
            new Pen(new SolidColorBrush(Shade(palette.Surface, -0.25)), 1),
            new Point(handle.Left + 3, grip), new Point(handle.Right - 3, grip));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();

        // A two position switch is thrown wherever it is clicked: there is nothing to aim at,
        // and asking somebody to hit a nine pixel label is not a switch, it is a test.
        if (Stacked)
        {
            Step(1);
            e.Handled = true;
            return;
        }

        var positions = Positions;
        double top = string.IsNullOrEmpty(Label) ? 0 : TitleSize * 1.4 + TitleGap;
        double span = Bounds.Height - top - HandleHeight - 6;
        double step = positions.Count > 1 ? span / (positions.Count - 1) : 0;

        if (step > 0 && e.GetPosition(this).X > SlotWidth)
        {
            int nearest = (int)Math.Round((e.GetPosition(this).Y - top - 3 - HandleHeight / 2) / step);
            Move(nearest);
        }
        else
        {
            Step(1);
        }

        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        Step(e.Delta.Y > 0 ? -1 : 1);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Up or Key.Left: Step(-1); e.Handled = true; break;
            case Key.Down or Key.Right: Step(1); e.Handled = true; break;
            case Key.Space or Key.Enter: Step(1); e.Handled = true; break;
        }
    }

    private FormattedText Text(string? text, double size, IBrush brush) =>
        Text(text, size, brush, double.PositiveInfinity);

    /// <summary>
    /// The same, folded to a width, so a long name sits over its own switch and not over the
    /// knob beside it. A panel prints a long name on two short lines for the same reason.
    /// </summary>
    private FormattedText Text(string? text, double size, IBrush brush, double maxWidth)
    {
        var built = new FormattedText(
            text ?? "", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily.Default), size, brush);

        if (!double.IsInfinity(maxWidth) && maxWidth > 1) built.MaxTextWidth = maxWidth;

        return built;
    }

    /// <summary>How wide the name may be: whatever the layout offered.</summary>
    private double _room = double.PositiveInfinity;

    /// <summary>Lighter or darker than a colour, for the light falling on a moulded part.</summary>
    private static Color Shade(Color colour, double amount)
    {
        double Mix(byte channel) => amount >= 0
            ? channel + (255 - channel) * amount
            : channel * (1 + amount);

        return Color.FromRgb((byte)Mix(colour.R), (byte)Mix(colour.G), (byte)Mix(colour.B));
    }
}

/// <summary>How a value's name is written on a panel.</summary>
public static class Naming
{
    /// <summary>
    /// The words a panel prints in capitals whatever else is done to them.
    /// </summary>
    /// <remarks>
    /// Spelled out rather than guessed at from the length or the vowels. The set is small, it
    /// is the set this application actually uses, and a rule clever enough to find them would
    /// also find words that are not acronyms at all.
    /// </remarks>
    private static readonly string[] Acronyms = { "LFO", "VCO", "VCF", "VCA", "EG", "PW" };

    /// <summary>
    /// An enum's name with the words spaced out: LowPass reads as "Low pass", which is what a
    /// panel would have printed on it. Acronyms keep their capitals.
    /// </summary>
    public static string Of(object? value)
    {
        string raw = value?.ToString() ?? "";
        if (raw.Length == 0) return raw;

        foreach (var acronym in Acronyms)
        {
            if (string.Equals(raw, acronym, StringComparison.OrdinalIgnoreCase)) return acronym;
        }

        var text = new System.Text.StringBuilder(raw.Length + 4);

        for (int index = 0; index < raw.Length; index++)
        {
            char letter = raw[index];

            if (index > 0 && char.IsUpper(letter))
            {
                text.Append(' ');
                text.Append(char.ToLowerInvariant(letter));
            }
            else
            {
                text.Append(letter);
            }
        }

        return text.ToString();
    }
}
