using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using JingleBox2.Machines.Ui.Interfaces;
using JingleBox2.Machines.Ui;

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
    /// <summary>Stepping, clamping and reading a typed number. Holds nothing, so one is enough.</summary>
    private readonly INumericInput _number = new NumericInput();

    /// <summary>
    /// The names the template gives its three parts.
    /// </summary>
    /// <remarks>
    /// Written out as literals rather than assembled from anything, so the same string appears
    /// in the XAML and in the source and either can be found from the other by searching.
    /// </remarks>
    public const string TextBoxPart = "PART_TextBox";

    /// <inheritdoc cref="TextBoxPart"/>
    public const string UpPart = "PART_Up";

    /// <inheritdoc cref="TextBoxPart"/>
    public const string DownPart = "PART_Down";

    /// <summary>
    /// Backs <see cref="Value"/>, the number itself.
    /// </summary>
    /// <remarks>
    /// Two way, since the whole purpose of the control is to be typed into, and it is the only
    /// place the value lives: <see cref="Text"/> is a picture of it and is put back from it
    /// whenever the two could have drifted apart.
    /// </remarks>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumberField, double>(
            nameof(Value), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Backs <see cref="Minimum"/>, which typed text is held to as well as stepping.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(Minimum));

    /// <summary>
    /// Backs <see cref="Maximum"/>.
    /// </summary>
    /// <remarks>
    /// A hundred rather than one, unlike the drawn controls: this is for a count or a tempo,
    /// which is what a number field is reached for, and nought to one is a thing you turn.
    /// </remarks>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(Maximum), 100);

    /// <summary>Backs <see cref="SmallStep"/>, a whole unit, since these usually count things.</summary>
    public static readonly StyledProperty<double> SmallStepProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(SmallStep), 1);

    /// <summary>Backs <see cref="LargeStep"/>, ten of them.</summary>
    public static readonly StyledProperty<double> LargeStepProperty =
        AvaloniaProperty.Register<NumberField, double>(nameof(LargeStep), 10);

    /// <summary>Backs <see cref="Format"/>: whole numbers, unless a panel asks for decimals.</summary>
    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(Format), "0");

    /// <summary>Backs <see cref="Text"/>, what the box is showing.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(Text), "0");

    /// <summary>The number, which is the one place it is held.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The lowest it may be, whether it is stepped or typed there.</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <inheritdoc cref="MaximumProperty"/>
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

    /// <summary>The numeric format the box is written with, and read back through.</summary>
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

    /// <summary>
    /// The three parts out of the template, kept so their handlers can be taken off again.
    /// </summary>
    /// <remarks>
    /// A template can be applied more than once to the same control, and each application hands
    /// over fresh parts; without holding the old ones there would be no way to unsubscribe from
    /// them and the handlers would pile up.
    /// </remarks>
    private TextBox? _textBox;

    /// <inheritdoc cref="_textBox"/>
    private Button? _up;

    /// <inheritdoc cref="_textBox"/>
    private Button? _down;

    /// <summary>
    /// True while the box is being written from the value, so the write does not come back round.
    /// </summary>
    /// <remarks>
    /// Setting the text sets the property, which is watched, which would show the value again.
    /// </remarks>
    private bool _syncing;

    /// <summary>
    /// Picks the three parts out of the template and wires them.
    /// </summary>
    /// <remarks>
    /// The old parts are unwired first, since this runs again every time a template is applied.
    /// </remarks>
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

    /// <summary>Takes the handlers back off whatever parts are currently held.</summary>
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

    /// <summary>
    /// Writes the box out again whenever anything that decides what it should say has moved.
    /// </summary>
    /// <remarks>
    /// The ends are in the list as well as the value, because they are what a typed number is
    /// held to: a range narrowed under a field that is already showing a number outside it would
    /// otherwise leave the box saying something the control would refuse.
    /// </remarks>
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

    /// <summary>
    /// One notch of the wheel is one step.
    /// </summary>
    /// <remarks>
    /// The base is deliberately not called and the event is marked handled: a wheel over a
    /// number changes the number rather than scrolling whatever the field happens to sit inside.
    /// </remarks>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        double delta = e.Delta.Y > 0 ? 1 : e.Delta.Y < 0 ? -1 : 0;
        if (delta == 0) return;

        StepBy(delta, e.KeyModifiers);
        e.Handled = true;
    }

    /// <summary>
    /// The step buttons, which are always a small step.
    /// </summary>
    /// <remarks>
    /// Shift is not read here: a button carries no modifier worth the surprise, and the wheel
    /// and the arrow keys are both to hand for a large one.
    /// </remarks>
    private void OnUpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        StepBy(1, KeyModifiers.None);

    /// <inheritdoc cref="OnUpClick"/>
    private void OnDownClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        StepBy(-1, KeyModifiers.None);

    /// <summary>
    /// Selects the whole number when the box takes the focus.
    /// </summary>
    /// <remarks>
    /// So that typing replaces the value rather than appending to it. Clicking into a field
    /// holding 120 and typing 90 should give 90, not 12090.
    /// </remarks>
    private void OnTextGotFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        _textBox?.SelectAll();

    /// <summary>Reads whatever was typed when the box is left, which is a commit like any other.</summary>
    private void OnTextLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        CommitTypedText();

    /// <summary>
    /// Arrows step it, enter takes what was typed, escape puts the real value back.
    /// </summary>
    /// <remarks>
    /// Escape is what makes the box safe to type into: the text is not the value until it is
    /// committed, so an edit part way through can always be abandoned.
    /// </remarks>
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

    /// <summary>One step either way, large if shift is held, held inside the ends.</summary>
    private void StepBy(double direction, KeyModifiers modifiers)
    {
        double step = modifiers.HasFlag(KeyModifiers.Shift) ? LargeStep : SmallStep;
        Commit(_number.Step(Value, direction, step, Minimum, Maximum));
    }

    /// <summary>
    /// Reads the box, keeping the value it already had when what is in there is not a number.
    /// </summary>
    /// <remarks>
    /// A stray keystroke should not wipe a tempo, which is why the fallback is the current value
    /// rather than nought. See <see cref="INumericInput.Parse"/>.
    /// </remarks>
    private void CommitTypedText() =>
        Commit(_number.Parse(_textBox?.Text ?? Text, Value, Minimum, Maximum));

    /// <summary>
    /// Takes a new value, or puts the old one back when it turns out not to be new.
    /// </summary>
    /// <remarks>
    /// The unchanged case still writes the box out, because it may be mid-edit: somebody who
    /// typed "12x" over a 12 and pressed enter gets 12 back, and a box left saying "12x" would
    /// be the control claiming a value it does not hold.
    /// </remarks>
    private void Commit(double value)
    {
        if (value == Value)
        {
            ShowValue();
            return;
        }

        Value = value;
    }

    /// <summary>
    /// Writes the value into the box, in both the property and the part.
    /// </summary>
    /// <remarks>
    /// The box is set as well as the property because a text box holds its own text and a
    /// binding pushed from here would not reach one that is mid-edit.
    /// </remarks>
    private void ShowValue()
    {
        if (_syncing) return;

        _syncing = true;
        Text = _number.Format(Value, Format);
        if (_textBox != null) _textBox.Text = Text;
        _syncing = false;
    }
}
