using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace JingleBox2.Views;

/// <summary>
/// A slider of a stated width with its reading printed immediately after it.
/// </summary>
/// <remarks>
/// A control rather than three elements in a grid on every page that wants one, because the
/// arrangement is the whole difficulty and it went wrong three times when it was left to a
/// layout. A slider in a row wants to take the slack, and whatever else is in that row then
/// either floats away from it or gets squeezed off the edge: a reading belongs against the
/// control it is the reading of, at every window width, and that is a property of the pair
/// rather than of the page they happen to sit on.
///
/// So the pair is sized here and nothing stretches. <see cref="SliderWidth"/> is the slider and
/// the reading takes what it needs; the control asks for exactly the two of them and no more,
/// which is what stops a page giving it slack it would only have to give away again.
///
/// The reading is a string handed in rather than a number formatted here, because what a number
/// means is the caller's business: 2048 is frames and 46 ms and neither of those is something a
/// slider could work out. An empty one is no reading at all and the control is then a slider,
/// which is the other half of what it is for.
///
/// A <see cref="Panel"/> holding a real <see cref="Slider"/> rather than a control drawn from
/// nothing: dragging, the keyboard, the ticks and the theme are all already right, and none of
/// them is what this is about.
/// </remarks>
public class RangeField : Panel
{
    /// <summary>The slider itself, which is Avalonia's own.</summary>
    private readonly Slider _slider = new()
    {
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
        TickPlacement = TickPlacement.BottomRight,
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>What it is on, printed after it. Not shown when there is nothing to say.</summary>
    private readonly TextBlock _reading = new()
    {
        VerticalAlignment = VerticalAlignment.Center
    };

    /// <summary>Builds the pair and keeps the slider's value and this one's in step.</summary>
    /// <remarks>
    /// The slider is listened to rather than bound to, so a value written from outside and a
    /// value dragged both arrive here and neither can start a loop with the other.
    /// </remarks>
    public RangeField()
    {
        Children.Add(_slider);
        Children.Add(_reading);

        _slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty && !_settling) Value = _slider.Value;
        };
    }

    /// <summary>True while a value is being pushed into the slider, so it is not read back.</summary>
    private bool _settling;

    /// <summary>Backs <see cref="Minimum"/>: the left end of the range.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<RangeField, double>(nameof(Minimum));

    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Backs <see cref="Maximum"/>: the right end of the range.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<RangeField, double>(nameof(Maximum), 1d);

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Backs <see cref="Value"/>: where the slider is, two ways.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<RangeField, double>(
            nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Backs <see cref="Reading"/>: what the slider is on, in whatever words the caller uses.
    /// </summary>
    public static readonly StyledProperty<string> ReadingProperty =
        AvaloniaProperty.Register<RangeField, string>(nameof(Reading), "");

    /// <inheritdoc cref="ReadingProperty"/>
    /// <remarks>Empty is no reading, and the control is then a slider and nothing else.</remarks>
    public string Reading
    {
        get => GetValue(ReadingProperty);
        set => SetValue(ReadingProperty, value);
    }

    /// <summary>Backs <see cref="SliderWidth"/>: how wide the slider itself is drawn.</summary>
    public static readonly StyledProperty<double> SliderWidthProperty =
        AvaloniaProperty.Register<RangeField, double>(nameof(SliderWidth), 260d);

    /// <inheritdoc cref="SliderWidthProperty"/>
    /// <remarks>
    /// Stated rather than taken from the room available, which is the whole point of the
    /// control: a slider that grows with the window drags its reading away with it.
    /// </remarks>
    public double SliderWidth
    {
        get => GetValue(SliderWidthProperty);
        set => SetValue(SliderWidthProperty, value);
    }

    /// <summary>Backs <see cref="Gap"/>: the space between the slider and its reading.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<RangeField, double>(nameof(Gap), 10d);

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>Backs <see cref="Steps"/>: how far one notch of the slider moves it.</summary>
    public static readonly StyledProperty<double> StepsProperty =
        AvaloniaProperty.Register<RangeField, double>(nameof(Steps), 1d);

    /// <inheritdoc cref="StepsProperty"/>
    /// <remarks>
    /// One by default and snapped to, since the ranges this is used for are lists of choices
    /// rather than continuous. A step of nought turns the snapping off.
    /// </remarks>
    public double Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    /// <inheritdoc/>
    /// <remarks>Passes each of its own settings through to the two children it is made of.</remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MinimumProperty) _slider.Minimum = Minimum;
        else if (change.Property == MaximumProperty) _slider.Maximum = Maximum;
        else if (change.Property == ValueProperty) Settle();
        else if (change.Property == SliderWidthProperty) InvalidateMeasure();
        else if (change.Property == GapProperty) InvalidateMeasure();
        else if (change.Property == StepsProperty)
        {
            _slider.TickFrequency = Steps;
            _slider.IsSnapToTickEnabled = Steps > 0;
        }
        else if (change.Property == ReadingProperty)
        {
            _reading.Text = Reading;
            _reading.IsVisible = Reading.Length > 0;

            InvalidateMeasure();
        }
    }

    /// <summary>Puts the value into the slider without hearing it come back out.</summary>
    private void Settle()
    {
        _settling = true;

        try { _slider.Value = Value; }
        finally { _settling = false; }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Exactly the slider and the reading, and never the room offered. A control that asked for
    /// what it was given would be handing the slack back to the page it was put on, which is the
    /// arrangement this exists to replace.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = Math.Max(0, SliderWidth);

        _slider.Measure(new Size(width, availableSize.Height));

        double height = _slider.DesiredSize.Height;

        if (_reading.IsVisible)
        {
            _reading.Measure(new Size(double.PositiveInfinity, availableSize.Height));

            width += Math.Max(0, Gap) + _reading.DesiredSize.Width;
            height = Math.Max(height, _reading.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        double width = Math.Max(0, SliderWidth);

        _slider.Arrange(new Rect(0, 0, width, finalSize.Height));

        if (_reading.IsVisible)
        {
            double at = width + Math.Max(0, Gap);

            _reading.Arrange(new Rect(at, 0, Math.Max(0, finalSize.Width - at), finalSize.Height));
        }

        return finalSize;
    }
}
