using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Plugins;
using System;
using System.Globalization;

namespace JingleBox2.ViewModels;

/// <summary>
/// One plugin parameter behind a knob. The plugin owns the value; this only carries a knob's
/// move to it and asks the plugin how to word what came back.
/// </summary>
/// <remarks>
/// The reading comes from the plugin rather than from the number: a compressor's threshold is
/// "-18.0 dB", not -18, and only the plugin knows which. Values are queued, not written, so a
/// drag never reaches into the audio thread.
/// </remarks>
public sealed class PluginParameterViewModel : ObservableObject
{
    private readonly ClapEffect _effect;
    private readonly ClapParameter _parameter;

    /// <summary>Told when this moves, so whatever owns the chain knows it has something to save.</summary>
    private readonly Action? _changed;

    private double _value;

    /// <summary>When this was last moved here, so a poll does not fight a hand on the knob.</summary>
    private long _movedAt;

    public PluginParameterViewModel(ClapEffect effect, ClapParameter parameter, Action? changed = null)
    {
        _effect = effect;
        _parameter = parameter;
        _changed = changed;
        _value = effect.ValueOf(parameter.Id);
    }

    public string Name => _parameter.Name;

    public double Minimum => _parameter.Minimum;

    public double Maximum => _parameter.Maximum;

    public double Default => _parameter.Default;

    /// <summary>The plugin reporting rather than listening: shown as a reading, not a knob.</summary>
    public bool IsReadOnly => _parameter.IsReadOnly;

    public bool IsStepped => _parameter.IsStepped;

    /// <summary>A stepped parameter with two positions is an on and an off, not a dial.</summary>
    public bool IsSwitch => IsStepped && Math.Abs(Maximum - Minimum - 1) < 0.0001;

    /// <summary>The same parameter as a tick box, for the ones that are one.</summary>
    public bool IsOn
    {
        get => Value >= Maximum - 0.0001;
        set
        {
            Value = value ? Maximum : Minimum;
            OnPropertyChanged();
        }
    }

    public double Value
    {
        get => _value;
        set
        {
            double clamped = double.IsNaN(value) ? _parameter.Default : Math.Clamp(value, Minimum, Maximum);
            if (Math.Abs(_value - clamped) < 0.000001) return;

            _value = clamped;
            _movedAt = Environment.TickCount64;
            _effect.SetValue(_parameter.Id, clamped);

            OnPropertyChanged();
            OnPropertyChanged(nameof(Text));

            // A reading moves on its own; only something the user set is worth saving.
            if (!IsReadOnly) _changed?.Invoke();
        }
    }

    /// <summary>How long a knob is left alone after being moved before polling touches it.</summary>
    private const long SettleMilliseconds = 500;

    /// <summary>
    /// Takes the value back from the plugin. Some parameters are the plugin talking rather
    /// than listening: a gain reduction or an output level is a meter, and without this it
    /// would sit at whatever it read when the effect was loaded.
    /// </summary>
    public void Refresh()
    {
        // A knob that was just moved is left alone: the plugin only hears about the move on
        // its next block, and reading it back too early would drag the knob backwards.
        if (Environment.TickCount64 - _movedAt < SettleMilliseconds) return;

        double actual = _effect.ValueOf(_parameter.Id);
        if (Math.Abs(actual - _value) < 0.000001) return;

        _value = actual;

        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Text));
    }

    /// <summary>
    /// What the control reads. The plugin's own wording is used when it says something a
    /// number cannot, "-6.0 dB" or "Peak", and dropped when it is only the number again with
    /// six decimals after it, which is most of them.
    /// </summary>
    public string Text
    {
        get
        {
            string worded = _effect.TextFor(_parameter.Id, _value);
            return Worthwhile(worded) ? worded : Number(_value);
        }
    }

    /// <summary>True when the plugin's wording carries something the bare number does not.</summary>
    private static bool Worthwhile(string worded)
    {
        if (string.IsNullOrWhiteSpace(worded)) return false;

        foreach (char character in worded)
        {
            // A letter or a percent sign means a unit or a name; digits, signs and separators
            // are the number this class can print better itself.
            if (char.IsLetter(character) || character == '%') return true;
        }

        return false;
    }

    /// <summary>
    /// A number as anyone would write it: whole where the parameter is whole, two decimals
    /// where the range is wide enough not to need more, and no trailing zeros.
    /// </summary>
    private string Number(double value)
    {
        if (IsStepped) return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

        double span = Math.Abs(Maximum - Minimum);
        string text = span >= 100
            ? value.ToString("0.#", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

        return text;
    }

    /// <summary>
    /// How far the knob turns. Plugins declare their own ranges, some of them tiny, so the
    /// step is worked out from the range rather than being a number that suits one plugin.
    /// </summary>
    public double SmallStep => Span / 200;

    public double LargeStep => Span / 20;

    private double Span => Math.Max(0.0001, Maximum - Minimum);
}
