using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using System;
using System.Globalization;

namespace JingleBox2.ViewModels;

/// <summary>
/// One channel strip on the mixer. Writes straight into the song's <see cref="TrackMix"/>,
/// and reports back so what is already sounding follows the fader.
/// </summary>
public sealed class TrackStripViewModel : ObservableObject
{
    private readonly TrackMix _strip;
    private readonly Action _changed;

    public TrackStripViewModel(int track, TrackMix strip, string instrumentName, Action changed)
    {
        Track = track;
        _strip = strip;
        instrument = instrumentName;
        _changed = changed;
    }

    public int Track { get; }

    /// <summary>The same two-digit form the pattern header and the instrument badges use.</summary>
    public string Label => "TR-" + (Track + 1).ToString("00", CultureInfo.InvariantCulture);

    private string instrument;

    /// <summary>Settable, so renaming an instrument does not mean rebuilding the whole mixer.</summary>
    public string InstrumentName
    {
        get => instrument;
        set
        {
            if (instrument == value) return;

            instrument = value;
            OnPropertyChanged();
        }
    }

    public double Volume
    {
        get => _strip.Volume;
        set => Set(v => _strip.Volume = v, _strip.Volume, value, TrackMix.MinVolume, TrackMix.MaxVolume, nameof(Volume));
    }

    public double Pan
    {
        get => _strip.Pan;
        set => Set(v => _strip.Pan = v, _strip.Pan, value, -1, 1, nameof(Pan));
    }

    public bool Mute
    {
        get => _strip.Mute;
        set
        {
            if (_strip.Mute == value) return;

            _strip.Mute = value;
            OnPropertyChanged();
            _changed();
        }
    }

    private double left;
    private double right;

    /// <summary>What the track is sounding right now, for the strip's meter.</summary>
    public double Left
    {
        get => left;
        set => SetProperty(ref left, value);
    }

    public double Right
    {
        get => right;
        set => SetProperty(ref right, value);
    }

    public bool Solo
    {
        get => _strip.Solo;
        set
        {
            if (_strip.Solo == value) return;

            _strip.Solo = value;
            OnPropertyChanged();
            _changed();
        }
    }

    private void Set(Action<double> assign, double current, double value, double min, double max, string name)
    {
        double clamped = double.IsNaN(value) ? min : Math.Clamp(value, min, max);
        if (Math.Abs(current - clamped) < 0.0001) return;

        assign(clamped);
        OnPropertyChanged(name);
        _changed();
    }
}
