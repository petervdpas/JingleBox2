using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JingleBox2.Rack.Ui.Records;
using JingleBox2.Rack.Ui.Interfaces;

namespace JingleBox2.Rack.Ui;

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
    /// <summary>How a value's name is written on a panel. Holds nothing, so one is enough.</summary>
    private readonly INaming _naming = new Naming();

    /// <summary>The air between the recess and the words that say what its positions mean.</summary>
    private const double LabelGap = 7;

    /// <summary>
    /// The air under the name at the top.
    /// </summary>
    /// <remarks>
    /// The same a knob leaves under its own name and a fader under its. These three stand beside
    /// each other in rows all over the app, and one of them leaving less read as crammed.
    /// </remarks>
    private const double TitleGap = 4;

    /// <summary>Backs <see cref="Label"/>, the name printed over the top.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Switch, string?>(nameof(Label));

    /// <summary>
    /// Backs <see cref="SlotWidth"/>: how wide the recess is, and everything else on the switch
    /// is sized from it.
    /// </summary>
    /// <remarks>
    /// The handle is worked out from this rather than set beside it, so a switch made wider
    /// cannot end up with a cap that no longer fills its well.
    /// </remarks>
    public static readonly StyledProperty<double> SlotWidthProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(SlotWidth), 21.0);

    /// <summary>
    /// Backs <see cref="SlotHeight"/>: how far the handle travels when the positions stack
    /// around it.
    /// </summary>
    /// <remarks>
    /// Only read by the stacked shape. With the positions listed alongside, the recess runs the
    /// whole height the switch was given, since it has to reach every position in the list.
    /// </remarks>
    public static readonly StyledProperty<double> SlotHeightProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(SlotHeight), 26.0);

    /// <summary>
    /// Backs <see cref="FontSize"/>, the size the positions are written at.
    /// </summary>
    /// <remarks>
    /// Small, because there are two or three of these words on a control the width of a knob,
    /// and they are read once to learn the switch rather than every time it is thrown.
    /// </remarks>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(FontSize), 8.5);

    /// <summary>
    /// Backs <see cref="TitleSize"/>, the size of the name over the top.
    /// </summary>
    /// <remarks>
    /// Larger than the positions under it: the name is what the eye runs along when it is
    /// looking for a control, and the positions are what it reads once it has found one.
    /// </remarks>
    public static readonly StyledProperty<double> TitleSizeProperty =
        AvaloniaProperty.Register<Switch, double>(nameof(TitleSize), 10.0);

    /// <summary>
    /// Backs <see cref="TitleLines"/>: how many lines of room the name gets, used or not.
    /// </summary>
    /// <remarks>
    /// A row of controls whose names are different lengths is a row whose handles sit at
    /// different heights, because a name that folds onto two lines pushes what is under it down
    /// and a short one does not. Reserving the same room for every name in a row puts them all
    /// back on one line. The same rule a knob follows.
    /// </remarks>
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

    /// <summary>
    /// Backs <see cref="IsChecked"/>, which is the whole of the state for a plain on and off.
    /// </summary>
    /// <remarks>
    /// Two way, because a switch is thrown by hand and whatever it is bound to has to hear about
    /// it. Ignored entirely once <see cref="ItemsSource"/> is set: the position then lives in
    /// <see cref="SelectedItem"/>, and two places holding it would eventually disagree.
    /// </remarks>
    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<Switch, bool>(nameof(IsChecked), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Backs <see cref="OnLabel"/>, the upper position of a plain on and off.</summary>
    public static readonly StyledProperty<string> OnLabelProperty =
        AvaloniaProperty.Register<Switch, string>(nameof(OnLabel), "On");

    /// <summary>
    /// Backs <see cref="OffLabel"/>, the lower one.
    /// </summary>
    /// <remarks>
    /// On and off is what a switch nobody has worded says, which is true of every switch ever
    /// made and useful on none of them. A machine that means something else says so.
    /// </remarks>
    public static readonly StyledProperty<string> OffLabelProperty =
        AvaloniaProperty.Register<Switch, string>(nameof(OffLabel), "Off");

    /// <summary>
    /// Backs <see cref="ItemsSource"/>: the positions, when they are more than an on and an off.
    /// </summary>
    /// <remarks>
    /// The same pair of properties a combo box takes, deliberately, so one can be swapped for
    /// the other without touching the binding underneath.
    /// </remarks>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Switch, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Backs <see cref="SelectedItem"/>, which position of the list the handle is in.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<Switch, object?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>
    /// Says which properties change the picture and which change the size, and makes a switch
    /// take the keyboard.
    /// </summary>
    /// <remarks>
    /// Focusable is overridden rather than set in the constructor so that a style can still take
    /// it back off a switch that is only there to be read.
    /// </remarks>
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

    /// <summary>What the switch is called, printed over the top of it.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="SlotWidthProperty"/>
    public double SlotWidth
    {
        get => GetValue(SlotWidthProperty);
        set => SetValue(SlotWidthProperty, value);
    }

    /// <inheritdoc cref="SlotHeightProperty"/>
    public double SlotHeight
    {
        get => GetValue(SlotHeightProperty);
        set => SetValue(SlotHeightProperty, value);
    }

    /// <inheritdoc cref="FontSizeProperty"/>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <inheritdoc cref="TitleSizeProperty"/>
    public double TitleSize
    {
        get => GetValue(TitleSizeProperty);
        set => SetValue(TitleSizeProperty, value);
    }

    /// <inheritdoc cref="TitleLinesProperty"/>
    public int TitleLines
    {
        get => GetValue(TitleLinesProperty);
        set => SetValue(TitleLinesProperty, value);
    }

    /// <inheritdoc cref="HeadRoomProperty"/>
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

    /// <summary>
    /// How deep the handle is along its travel, also taken from the recess width.
    /// </summary>
    /// <remarks>
    /// A little over half the width, so the cap reads as a handle lying across the well rather
    /// than as a square plug filling it.
    /// </remarks>
    private double HandleHeight => Math.Max(5, SlotWidth * 0.52);

    /// <inheritdoc cref="IsCheckedProperty"/>
    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>What the upper position is called, on a plain on and off.</summary>
    public string OnLabel
    {
        get => GetValue(OnLabelProperty);
        set => SetValue(OnLabelProperty, value);
    }

    /// <summary>And the lower one.</summary>
    public string OffLabel
    {
        get => GetValue(OffLabelProperty);
        set => SetValue(OffLabelProperty, value);
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Which of <see cref="ItemsSource"/> the handle is in, and what a throw writes.</summary>
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

    /// <summary>
    /// Which position the handle is in. Zero is the top.
    /// </summary>
    /// <remarks>
    /// A selection that matches none of the positions reads as the top rather than as nothing,
    /// so a switch bound to a value it has never heard of still draws a handle somewhere.
    /// </remarks>
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

    /// <summary>
    /// Puts the handle in a position, writing to whichever of the two bindings this switch uses.
    /// </summary>
    /// <remarks>
    /// Held inside the list rather than refused, since the position is worked out from where a
    /// click landed and a click near an end rounds past it.
    /// </remarks>
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

    /// <summary>
    /// Room for the name, the recess, and the positions in whichever of the two arrangements
    /// this switch is drawn in.
    /// </summary>
    /// <remarks>
    /// Stacked, the height is the taller of what the words and the travel need and what the
    /// scribe line asks for, since a switch pushed down to stand level with the knobs beside it
    /// is taller than one that follows its own name.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        _room = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;

        var positions = Positions;

        double widest = 0;
        double lines = 0;

        foreach (var position in positions)
        {
            var text = Text(_naming.Of(position), FontSize, Brushes.Black);
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

    /// <summary>Paints the name, then the switch in whichever arrangement its positions call for.</summary>
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

    /// <summary>
    /// Name on top, one answer above the handle and the other below it.
    /// </summary>
    /// <remarks>
    /// The recess goes on the scribe line when there is one, and the word above it is fitted
    /// into whatever room that leaves rather than pushing the recess down: the whole point of
    /// the scribe line is that everything in the row stands on it.
    ///
    /// The handle is drawn at whichever end of its travel is on, three pixels in from that end,
    /// which is what keeps it inside the well's rounded cap.
    /// </remarks>
    private void RenderStacked(DrawingContext context, ThemePalette palette, IReadOnlyList<object> positions, double top)
    {
        var upper = Text(_naming.Of(positions[0]), FontSize, Index == 0 ? palette.TextBrush : palette.MutedBrush);
        var lower = Text(_naming.Of(positions[1]), FontSize, Index == 1 ? palette.TextBrush : palette.MutedBrush);

        double middle = Bounds.Width / 2;

        context.DrawText(upper, new Point(middle - upper.Width / 2, top));

        double slotTop = top + upper.Height + LabelGap;
        var slot = new Rect(middle - SlotWidth / 2, slotTop, SlotWidth, SlotHeight);

        Well(context, palette, slot);

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

            var text = Text(_naming.Of(positions[index]), FontSize,
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
                new GradientStop(ThemePalette.Shade(palette.Surface, -0.45), 0),
                new GradientStop(ThemePalette.Shade(palette.Surface, -0.18), 0.5),
                new GradientStop(ThemePalette.Shade(palette.Surface, -0.45), 1)
            }
        };

        context.DrawRectangle(well, new Pen(new SolidColorBrush(ThemePalette.Shade(palette.Surface, -0.6)), 1),
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
                new GradientStop(ThemePalette.Shade(palette.Surface, 0.30), 0),
                new GradientStop(ThemePalette.Shade(palette.Surface, 0.16), 1)
            }
        };

        context.DrawRectangle(metal,
            new Pen(new SolidColorBrush(IsFocused ? palette.Accent : ThemePalette.Shade(palette.Surface, -0.3)), IsFocused ? 1.4 : 1),
            handle, 3, 3);

        double grip = handle.Center.Y;
        context.DrawLine(
            new Pen(new SolidColorBrush(ThemePalette.Shade(palette.Surface, -0.25)), 1),
            new Point(handle.Left + 3, grip), new Point(handle.Right - 3, grip));
    }

    /// <summary>
    /// Throws the switch.
    /// </summary>
    /// <remarks>
    /// A two position switch is thrown wherever it is clicked: there is nothing to aim at, and
    /// asking somebody to hit a nine pixel label is not a switch, it is a test. With the
    /// positions listed alongside there is something to aim at, so a click out on the names goes
    /// to the nearest one and a click on the recess itself steps to the next.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();

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

    /// <summary>
    /// One notch of the wheel is one position, up the list rather than down it.
    /// </summary>
    /// <remarks>
    /// The event is marked handled: over a switch the wheel throws the switch rather than
    /// scrolling the panel it sits in, and a panel that scrolled underneath the hand would take
    /// the switch out from under it.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        Step(e.Delta.Y > 0 ? -1 : 1);
        e.Handled = true;
    }

    /// <summary>
    /// Arrows walk the positions; space and enter step to the next.
    /// </summary>
    /// <remarks>
    /// Up and left both go one way and down and right the other, because a switch stacks its
    /// positions in one arrangement and lists them in the other, and which pair somebody reaches
    /// for follows whichever they are looking at.
    ///
    /// A key this does not answer is left unhandled, so it carries on out to the panel.
    /// </remarks>
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

    /// <summary>A piece of text laid out with no width limit, for a position name, which never folds.</summary>
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

    /// <summary>
    /// How wide the name may be: whatever the layout offered, or nothing at all when it offered
    /// no limit.
    /// </summary>
    /// <remarks>
    /// Taken during the measure and used during it, since that is the only moment anything tells
    /// a control how much room it is being given.
    /// </remarks>
    private double _room = double.PositiveInfinity;
}
