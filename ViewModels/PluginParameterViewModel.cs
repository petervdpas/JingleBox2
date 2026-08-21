using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Plugins;
using System;

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

    private double _value;

    /// <summary>When this was last moved here, so a poll does not fight a hand on the knob.</summary>
    private long _movedAt;

    public PluginParameterViewModel(ClapEffect effect, ClapParameter parameter)
    {
        _effect = effect;
        _parameter = parameter;
        _value = effect.ValueOf(parameter.Id);
    }

    public string Name => _parameter.Name;

    public double Minimum => _parameter.Minimum;

    public double Maximum => _parameter.Maximum;

    public double Default => _parameter.Default;

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

    /// <summary>The plugin's own words for the current value, for the knob's reading.</summary>
    public string Text
    {
        get
        {
            string worded = _effect.TextFor(_parameter.Id, _value);
            return string.IsNullOrEmpty(worded) ? _value.ToString("0.00") : worded;
        }
    }

    /// <summary>
    /// How far the knob turns. Plugins declare their own ranges, some of them tiny, so the
    /// step is worked out from the range rather than being a number that suits one plugin.
    /// </summary>
    public double SmallStep => Span / 200;

    public double LargeStep => Span / 20;

    private double Span => Math.Max(0.0001, Maximum - Minimum);
}
