using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Plugins;
using System;
using System.Globalization;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// One plugin parameter behind a knob. The plugin owns the value; this only carries a knob's
/// move to it and asks the plugin how to word what came back.
/// </summary>
/// <remarks>
/// The reading comes from the plugin rather than from the number: a compressor's threshold is
/// "-18.0 dB", not -18, and only the plugin knows which. Values are queued, not written, so a
/// drag never reaches into the audio thread.
///
/// It is also the one place a plugin's value is put into words, and everything that prints one
/// goes through here even when it wants nothing else. The chain strip builds one of these per
/// reading and throws it away again: wording is real work, since a VST3 parameter is nought to
/// one whatever it means, and doing it a second time somewhere else is how the strip came to
/// print 0.5000 where the plugin's own window printed 0.5.
/// </remarks>
public sealed class PluginParameterViewModel : ObservableObject
{
    /// <summary>The plugin, which is where the value really lives.</summary>
    private readonly IPluginParameters _effect;

    /// <summary>What this parameter is: its range, its unit, and whether it is a reading.</summary>
    private readonly PluginParameter _parameter;

    /// <summary>Told when this moves, so whatever owns the chain knows it has something to save.</summary>
    private readonly Action? _changed;

    /// <summary>The last value sent or taken, so a knob has something to draw between blocks.</summary>
    private double _value;

    /// <summary>When this was last moved here, so a poll does not fight a hand on the knob.</summary>
    private long _movedAt;

    /// <summary>
    /// Wraps one parameter of a loaded plugin, reading where it stands now.
    /// </summary>
    /// <param name="effect">The loaded plugin the value is read from and written back to.</param>
    /// <param name="parameter">Which of its parameters this is, carrying the name, the range and the stepping.</param>
    /// <param name="changed">
    /// Optional, and left out by anything that only wants the wording. A caller that is printing
    /// a value has nothing to save, and telling it about a move it caused by asking would be a
    /// chain that reports itself changed every time it is drawn.
    /// </param>
    public PluginParameterViewModel(IPluginParameters effect, PluginParameter parameter, Action? changed = null)
    {
        _effect = effect;
        _parameter = parameter;
        _changed = changed;
        _value = effect.ValueOf(parameter.Id);
    }

    /// <summary>What the plugin calls it, which is the label on the knob.</summary>
    public string Name => _parameter.Name;

    /// <summary>The bottom of the range the plugin declared, which is often not nought.</summary>
    public double Minimum => _parameter.Minimum;

    /// <summary>And the top of it.</summary>
    public double Maximum => _parameter.Maximum;

    /// <summary>Where the plugin says it should sit, used when a value arrives as nonsense.</summary>
    public double Default => _parameter.Default;

    /// <summary>The plugin reporting rather than listening: shown as a reading, not a knob.</summary>
    public bool IsReadOnly => _parameter.IsReadOnly;

    /// <summary>True when only whole positions mean anything, so the wording drops the decimals.</summary>
    public bool IsStepped => _parameter.IsStepped;

    /// <summary>A stepped parameter with two positions is an on and an off, not a dial.</summary>
    public bool IsSwitch => _parameter.IsSwitch;

    /// <summary>
    /// The same parameter as a tick box, for the ones that are one.
    /// </summary>
    /// <remarks>
    /// On is read as being at the top of the range rather than as any particular number, since a
    /// two-position parameter is not obliged to be nought and one.
    /// </remarks>
    public bool IsOn
    {
        get => Value >= Maximum - 0.0001;
        set
        {
            Value = value ? Maximum : Minimum;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Where the knob is, and setting it is what moves the plugin.
    /// </summary>
    /// <remarks>
    /// Clamped to the range and defaulted where a control hands over nonsense, because a plugin
    /// given a value outside what it declared is a plugin doing whatever it likes. A move that
    /// changes nothing is dropped rather than sent, which matters on a drag: the same value
    /// arrives many times over.
    ///
    /// Only something somebody set is worth saving. A reading moves on its own, and treating
    /// those as edits leaves a song that can never be saved because it is always about to need
    /// saving again.
    /// </remarks>
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

            if (!IsReadOnly) _changed?.Invoke();
        }
    }

    /// <summary>How long a knob is left alone after being moved before polling touches it.</summary>
    private const long SettleMilliseconds = 500;

    /// <summary>
    /// Takes a value the plugin set itself, in its own window. Shown, not sent back: the
    /// plugin is the one that moved it and telling it so again would be an argument.
    /// </summary>
    /// <remarks>
    /// How the move arrives depends on the standard. VST3 reports it the moment a knob is
    /// touched, through the host's own handler; CLAP only hands it back at the end of a block,
    /// which is why a CLAP plugin with its window open is polled as well.
    /// </remarks>
    public void Adopt(double value)
    {
        double clamped = double.IsNaN(value) ? _parameter.Default : Math.Clamp(value, Minimum, Maximum);
        if (Math.Abs(_value - clamped) < 0.000001) return;

        _value = clamped;
        _movedAt = Environment.TickCount64;

        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(Text));
    }

    /// <summary>Which parameter this is, for matching up a move the plugin reported.</summary>
    public uint Id => _parameter.Id;

    /// <summary>
    /// Takes the value back from the plugin. Some parameters are the plugin talking rather
    /// than listening: a gain reduction or an output level is a meter, and without this it
    /// would sit at whatever it read when the effect was loaded.
    /// </summary>
    /// <remarks>
    /// A knob that was just moved is left alone for <see cref="SettleMilliseconds"/>. The plugin
    /// only hears about a move on its next block, so reading it back too early would drag the
    /// knob backwards under the hand that is moving it.
    /// </remarks>
    public void Refresh()
    {
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
    /// <remarks>
    /// A VST3 parameter is nought to one whatever it actually means, so for those the plugin's
    /// wording is the only thing that says anything at all: printing 0.53 for a filter cutoff
    /// would be worse than printing nothing.
    /// </remarks>
    public string Text
    {
        get
        {
            string worded = _effect.TextFor(_parameter.Id, _value);

            if (_parameter.Normalized) return Plain(worded);

            return Worthwhile(worded) ? worded : Number(_value);
        }
    }

    /// <summary>
    /// A normalized parameter as the plugin words it, tidied. Plugins that hand back a real
    /// unit are left alone; the many that hand back "50.000000" are cut down to "50" and given
    /// the unit the plugin declared separately.
    /// </summary>
    private string Plain(string worded)
    {
        if (string.IsNullOrWhiteSpace(worded)) return Number(_value);
        if (Worthwhile(worded)) return worded;

        if (!double.TryParse(worded, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain))
            return worded;

        string text = Math.Abs(plain) >= 100
            ? plain.ToString("0.#", CultureInfo.InvariantCulture)
            : plain.ToString("0.##", CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(_parameter.Units) ? text : text + " " + _parameter.Units;
    }

    /// <summary>True when the plugin's wording carries something the bare number does not.</summary>
    /// <remarks>
    /// A letter or a percent sign means a unit or a name, and is worth keeping. Digits, signs
    /// and separators are the number this class can print better itself.
    /// </remarks>
    private static bool Worthwhile(string worded)
    {
        if (string.IsNullOrWhiteSpace(worded)) return false;

        foreach (char character in worded)
        {
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

    /// <summary>The same, for a page of movement rather than a nudge.</summary>
    public double LargeStep => Span / 20;

    /// <summary>
    /// The width of the range, never nought.
    /// </summary>
    /// <remarks>
    /// A plugin declaring a parameter whose ends are the same number is not a fault worth
    /// refusing over, but it would make both steps nought and leave a knob that cannot be moved
    /// at all.
    /// </remarks>
    private double Span => Math.Max(0.0001, Maximum - Minimum);
}
