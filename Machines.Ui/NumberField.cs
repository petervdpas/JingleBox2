using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// A compact numeric field: a value you can type, plus a pair of stacked step buttons that
/// together match the height of the input. Scrolling over it and the arrow keys step it too.
/// </summary>
/// <remarks>
/// A templated control rather than a NumericUpDown so the buttons can be small: the built-in
/// spinner reserves more width for its buttons than the number itself needs.
/// </remarks>
public class NumberField : TemplatedControl
{
    public const string TextBoxPart = "PART_TextBox";
    public const string UpPart = "PART_Up";
    public const string DownPart = "PART_Down";

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumberField, double>(
            nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(Minimum));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(SmallStep), 1);

    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(LargeStep), 10);

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(Format), "0");

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(Text), "0");

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>One button press, wheel notch, or arrow key.</summary>
    public double SmallStep
    {
        get => GetValue(SmallStepProperty);
        set => SetValue(SmallStepProperty, value);
    }

    /// <summary>The same, with Shift held.</summary>
    public double LargeStep
    {
        get => GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
    }

    public string Format
    {
        get => GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>What the text box shows. Bound by the template; not usually set directly.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private TextBox? _textBox;
    private Button? _up;
    private Button? _down;
    private bool _syncing;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        Detach();

        _textBox = e.NameScope.Find<TextBox>(TextBoxPart);
        _up = e.NameScope.Find<Button>(UpPart);
        _down = e.NameScope.Find<Button>(DownPart);

        if (_textBox != null)
        {
            _textBox.LostFocus += OnTextLostFocus;
            _textBox.KeyDown += OnTextKeyDown;
            _textBox.GotFocus += OnTextGotFocus;
        }

        if (_up != null) _up.Click += OnUpClick;
        if (_down != null) _down.Click += OnDownClick;

        ShowValue();
    }

    private void Detach()
    {
        if (_textBox != null)
        {
            _textBox.LostFocus -= OnTextLostFocus;
            _textBox.KeyDown -= OnTextKeyDown;
            _textBox.GotFocus -= OnTextGotFocus;
        }

        if (_up != null) _up.Click -= OnUpClick;
        if (_down != null) _down.Click -= OnDownClick;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty ||
            change.Property == FormatProperty ||
            change.Property == MinimumProperty ||
            change.Property == MaximumProperty)
        {
            ShowValue();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        // Deliberately not calling the base: a wheel over a number changes the number rather
        // than scrolling whatever the field happens to sit inside.
        double delta = e.Delta.Y > 0 ? 1 : e.Delta.Y < 0 ? -1 : 0;
        if (delta == 0) return;

        StepBy(delta, e.KeyModifiers);
        e.Handled = true;
    }

    private void OnUpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        StepBy(1, KeyModifiers.None);

    private void OnDownClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        StepBy(-1, KeyModifiers.None);

    private void OnTextGotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        _textBox?.SelectAll(); // typing replaces the value instead of appending to it

    private void OnTextLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        CommitTypedText();

    private void OnTextKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                StepBy(1, e.KeyModifiers);
                e.Handled = true;
                return;

            case Key.Down:
                StepBy(-1, e.KeyModifiers);
                e.Handled = true;
                return;

            case Key.Enter:
                CommitTypedText();
                e.Handled = true;
                return;

            case Key.Escape:
                ShowValue();
                e.Handled = true;
                return;
        }
    }

    private void StepBy(double direction, KeyModifiers modifiers)
    {
        double step = modifiers.HasFlag(KeyModifiers.Shift) ? LargeStep : SmallStep;
        Commit(NumericInput.Step(Value, direction, step, Minimum, Maximum));
    }

    private void CommitTypedText() =>
        Commit(NumericInput.Parse(_textBox?.Text ?? Text, Value, Minimum, Maximum));

    private void Commit(double value)
    {
        if (value == Value)
        {
            ShowValue(); // the text may still be mid-edit, so put the real value back
            return;
        }

        Value = value;
    }

    private void ShowValue()
    {
        if (_syncing) return;

        _syncing = true;
        Text = NumericInput.Format(Value, Format);
        if (_textBox != null) _textBox.Text = Text;
        _syncing = false;
    }
}
