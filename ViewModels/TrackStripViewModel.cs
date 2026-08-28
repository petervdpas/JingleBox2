using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using JingleBox2.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// One channel strip on the mixer. Writes straight into the song's <see cref="TrackMix"/>,
/// and reports back so what is already sounding follows the fader.
/// </summary>
public sealed class TrackStripViewModel : ObservableObject
{
    /// <summary>
    /// The song's own settings for this track, written into rather than copied out of.
    /// </summary>
    /// <remarks>
    /// A strip that held its own numbers would leave the mixer and the mix disagreeing until
    /// something saved, and the mix is read on the audio thread while the fader is moving.
    /// </remarks>
    private readonly TrackMix _strip;

    /// <summary>
    /// Told after anything on the strip moves, so the song knows it has something to save and
    /// whatever is already sounding follows the fader.
    /// </summary>
    private readonly Action _changed;

    /// <summary>
    /// Builds a strip over one track's settings, or over the master when the track is -1.
    /// </summary>
    /// <remarks>
    /// Nothing keys the master and nothing is keyed off it, so it is given no ducking sources at
    /// all: everything has already been summed by the time the master is reached.
    /// </remarks>
    public TrackStripViewModel(int track, TrackMix strip, string instrumentName, int trackCount, Action changed)
    {
        Track = track;
        _strip = strip;
        instrument = instrumentName;
        _changed = changed;

        DuckKeys = track < 0 ? Array.Empty<DuckKey>() : BuildKeys(track, trackCount);
    }

    /// <summary>
    /// What this strip's side chain can listen to: any other track, or nothing. A strip is
    /// not offered itself, since a track keying itself is a gate rather than a duck.
    /// </summary>
    public IReadOnlyList<DuckKey> DuckKeys { get; }

    /// <summary>
    /// Every other track, and None at the top. The strip's own track is left out, since a track
    /// keying itself is a gate rather than a duck.
    /// </summary>
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

    /// <summary>Which track this strip is, or -1 for the master, which is not a track.</summary>
    public int Track { get; }

    /// <summary>Backing field for <see cref="IsSelected"/>.</summary>
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

    /// <summary>Backing field for <see cref="EffectName"/>.</summary>
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

    /// <summary>Whether there is a tag to draw at all.</summary>
    public bool HasEffect => EffectName.Length > 0;

    /// <summary>True for the strip the whole mix goes through, which is not a track.</summary>
    public bool IsMaster => Track < 0;

    /// <summary>
    /// What is written at the top of the strip: MASTER, or the track in the same two-digit form
    /// the pattern header and the instrument badges use.
    /// </summary>
    public string Label => IsMaster
        ? "MASTER"
        : "TR-" + (Track + 1).ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Backing field for <see cref="InstrumentName"/>.</summary>
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

    /// <summary>
    /// The track's level as an amplitude, held inside the range the mix allows.
    /// </summary>
    /// <remarks>
    /// This is what is stored and what the engine multiplies by. The fader shows
    /// <see cref="VolumeDecibels"/>, and both are announced whenever either moves, or a knob
    /// pointed at one would leave the other reading the old value.
    /// </remarks>
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

    /// <summary>Where the track sits, -1 hard left to 1 hard right, nought in the middle.</summary>
    public double Pan
    {
        get => _strip.Pan;
        set => Set(v => _strip.Pan = v, _strip.Pan, value, -1, 1, nameof(Pan));
    }

    /// <summary>
    /// Whether the track is silenced. Solo elsewhere silences it too, and does not touch this:
    /// what a strip is muted to is a setting, and what it can be heard through is the mix.
    /// </summary>
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

    /// <summary>Backing field for <see cref="Left"/>.</summary>
    private double left;

    /// <summary>Backing field for <see cref="Right"/>.</summary>
    private double right;

    /// <summary>
    /// What the track is sounding right now on the left, for the strip's meter.
    /// </summary>
    /// <remarks>
    /// Written from outside by whatever is polling the mixer, rather than read here: the meters
    /// are polled while anything is sounding, which is a rule about the whole mix and not about
    /// one strip. The master's reading is a peak off the last buffer and goes stale, so it says
    /// nothing once it is older than the mixer's hold; a track's is worked out from the voices
    /// that are sounding and falls on its own.
    /// </remarks>
    public double Left
    {
        get => left;
        set => SetProperty(ref left, value);
    }

    /// <summary>And the right, written the same way.</summary>
    public double Right
    {
        get => right;
        set => SetProperty(ref right, value);
    }

    /// <summary>
    /// Whether this track is soloed. The master has no solo, since soloing everything is what
    /// it is already doing.
    /// </summary>
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

    /// <summary>
    /// How long the duck takes to let go after the key track stops, in milliseconds.
    /// </summary>
    /// <remarks>
    /// The one value on the strip that had no name for a link to point at until the strip was
    /// gone over control by control; see <c>Midi/MixLinks.cs</c>.
    /// </remarks>
    public double DuckReleaseMs
    {
        get => _strip.DuckReleaseMs;
        set => Set(v => _strip.DuckReleaseMs = v, _strip.DuckReleaseMs, value,
            TrackMix.MinDuckReleaseMs, TrackMix.MaxDuckReleaseMs, nameof(DuckReleaseMs));
    }

    /// <summary>
    /// Which track this strip ducks to, as the picker's own row rather than as a number.
    /// </summary>
    /// <remarks>
    /// Read back off <see cref="DuckKeys"/> each time rather than kept, so a key naming a track
    /// that no longer exists reads as None instead of as a row nothing matches. A knob cannot
    /// be pointed at this: it names a track rather than a value, the same reason a take picker
    /// cannot be pointed at.
    /// </remarks>
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

    /// <summary>
    /// Writes one number into the mix, held inside its range, and only when it really moved.
    /// </summary>
    /// <remarks>
    /// A value that is not a number is read as the floor rather than being written through: a
    /// NaN reaching the mix is silence at best and a stuck strip at worst, and it can arrive
    /// from an empty text box. The threshold below which nothing is announced is a tenth of a
    /// thousandth, which is finer than any of these values is drawn or heard, and it is what
    /// stops a fader dragged across its travel announcing the same number a hundred times.
    /// </remarks>
    /// <param name="assign">Puts the value into the mix, which is the only thing that knows where it goes.</param>
    /// <param name="current">Where the mix stands now, so a value arriving as itself announces nothing.</param>
    /// <param name="value">What is being asked for, before it has been bounded.</param>
    /// <param name="min">The bottom of the value's own range, and where a NaN lands.</param>
    /// <param name="max">The top of the value's own range.</param>
    /// <param name="changed">
    /// Every name that now reads differently, since one number can be two properties: a level
    /// is an amplitude and a reading in decibels.
    /// </param>
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
