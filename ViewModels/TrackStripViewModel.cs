using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using JingleBox2.UI;
using System;
using System.Collections.Generic;
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

    public TrackStripViewModel(int track, TrackMix strip, string instrumentName, int trackCount, Action changed)
    {
        Track = track;
        _strip = strip;
        instrument = instrumentName;
        _changed = changed;

        // Nothing keys the master and nothing is keyed off it: everything has already been
        // summed by the time it is reached.
        DuckKeys = track < 0 ? Array.Empty<DuckKey>() : BuildKeys(track, trackCount);
    }

    /// <summary>
    /// What this strip's side chain can listen to: any other track, or nothing. A strip is
    /// not offered itself, since a track keying itself is a gate rather than a duck.
    /// </summary>
    public IReadOnlyList<DuckKey> DuckKeys { get; }

    private static IReadOnlyList<DuckKey> BuildKeys(int track, int trackCount)
    {
        var keys = new List<DuckKey> { DuckKey.None };

        for (int other = 0; other < trackCount; other++)
        {
            if (other == track) continue;

            keys.Add(new DuckKey(other, "TR-" + (other + 1).ToString("00", CultureInfo.InvariantCulture)));
        }

        return keys;
    }

    public int Track { get; }

    private bool selected;

    /// <summary>True for the strip the effect panel is about.</summary>
    public bool IsSelected
    {
        get => selected;
        set
        {
            if (selected == value) return;

            selected = value;
            OnPropertyChanged();
        }
    }

    private string effect = "";

    /// <summary>The effect running on this track, or empty. Shown on the strip as a tag.</summary>
    public string EffectName
    {
        get => effect;
        set
        {
            if (effect == value) return;

            effect = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEffect));
        }
    }

    public bool HasEffect => EffectName.Length > 0;

    /// <summary>The same two-digit form the pattern header and the instrument badges use.</summary>
    /// <summary>True for the strip the whole mix goes through, which is not a track.</summary>
    public bool IsMaster => Track < 0;

    public string Label => IsMaster
        ? "MASTER"
        : "TR-" + (Track + 1).ToString("00", CultureInfo.InvariantCulture);

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
            OnPropertyChanged(nameof(InstrumentTip));
        }
    }

    /// <summary>
    /// What the strip's badge says when you hover it. The name lives here rather than on the
    /// strip: an instrument can be called anything, and the strips have to stay the same height
    /// as each other.
    /// </summary>
    public string InstrumentTip => string.IsNullOrWhiteSpace(instrument)
        ? Label + ": no instrument"
        : Label + ": " + instrument;

    public double Volume
    {
        get => _strip.Volume;
        set => Set(v => _strip.Volume = v, _strip.Volume, value, TrackMix.MinVolume, TrackMix.MaxVolume,
            nameof(Volume), nameof(VolumeDecibels));
    }

    /// <summary>
    /// The same level as the fader reads it. The strip stores an amplitude, because that is
    /// what the engine multiplies by; a desk is marked in decibels with unity at zero.
    /// </summary>
    public double VolumeDecibels
    {
        get => GainScale.ToDecibels(_strip.Volume);
        set => Volume = GainScale.ToAmplitude(value);
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

    /// <summary>
    /// How far this track is pushed down while the key track sounds. Zero is a strip with no
    /// side chain at all, whatever the key is set to.
    /// </summary>
    public double Duck
    {
        get => _strip.Duck;
        set => Set(v => _strip.Duck = v, _strip.Duck, value, TrackMix.MinDuck, TrackMix.MaxDuck,
            nameof(Duck));
    }

    public double DuckReleaseMs
    {
        get => _strip.DuckReleaseMs;
        set => Set(v => _strip.DuckReleaseMs = v, _strip.DuckReleaseMs, value,
            TrackMix.MinDuckReleaseMs, TrackMix.MaxDuckReleaseMs, nameof(DuckReleaseMs));
    }

    public DuckKey DuckKey
    {
        get
        {
            foreach (var key in DuckKeys)
            {
                if (key.Track == _strip.DuckFrom) return key;
            }

            return DuckKey.None;
        }
        set
        {
            int track = value?.Track ?? TrackMix.NoKey;
            if (_strip.DuckFrom == track) return;

            _strip.DuckFrom = track;

            OnPropertyChanged();
            _changed();
        }
    }

    private void Set(Action<double> assign, double current, double value, double min, double max, params string[] changed)
    {
        double clamped = double.IsNaN(value) ? min : Math.Clamp(value, min, max);
        if (Math.Abs(current - clamped) < 0.0001) return;

        assign(clamped);

        foreach (var name in changed)
            OnPropertyChanged(name);

        _changed();
    }
}

/// <summary>A track a side chain can listen to, as the mixer's picker shows it.</summary>
public sealed record DuckKey(int Track, string Label)
{
    public static readonly DuckKey None = new(TrackMix.NoKey, "None");

    public override string ToString() => Label;
}
