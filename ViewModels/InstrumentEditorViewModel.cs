using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.UI;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// The instrument currently open in the editor. A sample and a synth share a name and a
/// level; the rest of the page shows whichever half applies.
/// </summary>
public sealed class InstrumentEditorViewModel : ObservableObject
{
    private readonly TrackerInstrument _instrument;
    private readonly Action _changed;

    private WaveformData? _waveform;
    private float[]? _peaks;

    public InstrumentEditorViewModel(
        int index,
        TrackerInstrument instrument,
        Action changed,
        IWaveformService? waveforms = null)
    {
        Index = index;
        _instrument = instrument;
        _changed = changed;

        // Both kinds run through the same voice now, so both have a patch to edit: a sample
        // has an envelope, a filter and modulation exactly as a generated wave does. Only the
        // oscillator half of it is meaningless for a recording, and the page hides that.
        Patch = new SynthPatchViewModel(instrument.Patch, changed);

        if (instrument.IsSynth) return;

        instrument.EnsureShape();
        ReadWaveform(waveforms);
    }

    public int Index { get; }

    public TrackerInstrument Instrument => _instrument;

    /// <summary>The voice settings, which both kinds of instrument have.</summary>
    public SynthPatchViewModel? Patch { get; }

    public bool IsSynth => _instrument.IsSynth;

    public bool IsSample => !IsSynth;

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);

    public string KindText => IsSynth ? "Synth" : "Sample";

    public string SourceText => IsSynth ? "Generated, no file." : _instrument.FilePath;

    /// <summary>
    /// The sample's shape, one value per pixel column, or null while it is being read. A synth
    /// never has any: there is no file to look at.
    /// </summary>
    public float[]? Peaks
    {
        get => _peaks;
        private set
        {
            _peaks = value;
            OnPropertyChanged();
        }
    }

    /// <summary>What the file turned out to be, for the line under the picture.</summary>
    public string SampleText
    {
        get
        {
            if (IsSynth) return "";
            if (_waveform == null) return _sampleProblem ?? "Reading the file...";

            double seconds = _waveform.SampleRate > 0
                ? (double)_waveform.TotalSamples / _waveform.SampleRate
                : 0;

            string channels = _waveform.Channels >= 2 ? "stereo" : "mono";

            return $"{seconds:0.00} s, {_waveform.SampleRate} Hz {channels}";
        }
    }

    private string? _sampleProblem;

    /// <summary>
    /// Reduces the file to peaks, off the UI thread: a long take takes a moment to read, and
    /// picking an instrument in the list should not wait for it.
    /// </summary>
    private void ReadWaveform(IWaveformService? waveforms)
    {
        if (waveforms == null) return;

        string path = _instrument.FilePath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _sampleProblem = "The file this instrument plays is missing.";
            return;
        }

        Task.Run(() =>
        {
            try
            {
                return waveforms.AnalyzeFile(path);
            }
            catch (Exception)
            {
                return null;
            }
        }).ContinueWith(read => Dispatcher.UIThread.Post(() =>
        {
            var data = read.Result;

            if (data == null)
            {
                _sampleProblem = "The file could not be read.";
                OnPropertyChanged(nameof(SampleText));
                return;
            }

            _waveform = data;
            Peaks = data.PeakData;
            OnPropertyChanged(nameof(SampleText));
        }));
    }

    public string Name
    {
        get => _instrument.Name;
        set
        {
            string name = value ?? "";
            if (_instrument.Name == name) return;

            _instrument.Name = name;
            OnPropertyChanged();
            _changed();
        }
    }

    /// <summary>Past unity is makeup gain: a quiet sample or a soft patch can be pushed up.</summary>
    public const double MaxVolume = 2.0;

    public double Volume
    {
        get => _instrument.Volume;
        set
        {
            double clamped = Math.Clamp(double.IsNaN(value) ? 0 : value, 0, MaxVolume);
            if (Math.Abs(_instrument.Volume - clamped) < 0.0001) return;

            _instrument.Volume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeDecibels));
            _changed();
        }
    }

    /// <summary>The same level as a fader reads it: decibels, with unity at zero.</summary>
    public double VolumeDecibels
    {
        get => GainScale.ToDecibels(_instrument.Volume);
        set => Volume = GainScale.ToAmplitude(value);
    }

    /// <summary>The pitch the file sounds at, which every other note is measured against.</summary>
    public double BaseNoteSemitone
    {
        get => _instrument.BaseNoteSemitone;
        set
        {
            int semitone = (int)Math.Round(Math.Clamp(value, Note.MinSemitone, Note.MaxSemitone));
            if (_instrument.BaseNoteSemitone == semitone) return;

            _instrument.BaseNoteSemitone = semitone;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BaseNoteText));
            _changed();
        }
    }

    public string BaseNoteText => _instrument.BaseNote.ToString();

    /// <summary>
    /// The part of the recording that plays, as fractions of the file. Fractions rather than
    /// frames so a trim or a re-record leaves the handles pointing at the same moment in the
    /// sound rather than at a stale offset.
    /// </summary>
    public double SampleStart
    {
        get => Shape.Start;
        set => SetPosition(v => Shape.Start = v, Shape.Start, value, nameof(SampleStart));
    }

    public double SampleEnd
    {
        get => Shape.End;
        set => SetPosition(v => Shape.End = v, Shape.End, value, nameof(SampleEnd));
    }

    public double LoopStart
    {
        get => Shape.LoopStart;
        set => SetPosition(v => Shape.LoopStart = v, Shape.LoopStart, value, nameof(LoopStart));
    }

    public double LoopEnd
    {
        get => Shape.LoopEnd;
        set => SetPosition(v => Shape.LoopEnd = v, Shape.LoopEnd, value, nameof(LoopEnd));
    }

    public SampleLoopMode[] LoopModes { get; } = Enum.GetValues<SampleLoopMode>();

    public SampleLoopMode LoopMode
    {
        get => Shape.LoopMode;
        set
        {
            if (Shape.LoopMode == value) return;

            Shape.LoopMode = value;

            // The old flag is kept saying the same thing, so a build without loop modes still
            // loops the instruments that should.
            _instrument.Loop = Shape.IsLooping;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLooping));
            OnPropertyChanged(nameof(Loop));
            _changed();
        }
    }

    public bool IsLooping => Shape.IsLooping;

    public bool Reverse
    {
        get => Shape.Reverse;
        set
        {
            if (Shape.Reverse == value) return;

            Shape.Reverse = value;
            OnPropertyChanged();
            _changed();
        }
    }

    /// <summary>The old loop flag, still on the instrument, now driven by the loop mode.</summary>
    public bool Loop => Shape.IsLooping;

    private SampleShape Shape
    {
        get
        {
            _instrument.Shape ??= new SampleShape();
            return _instrument.Shape;
        }
    }

    /// <summary>
    /// Writes one of the four positions and tells the view about all of them: moving the start
    /// past a loop point drags that point along, so the picture has to be told.
    /// </summary>
    private void SetPosition(Action<double> assign, double current, double value, string name)
    {
        double clamped = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
        if (Math.Abs(current - clamped) < 0.00001) return;

        assign(clamped);
        Shape.Clamp();

        OnPropertyChanged(name);
        OnPropertyChanged(nameof(SampleStart));
        OnPropertyChanged(nameof(SampleEnd));
        OnPropertyChanged(nameof(LoopStart));
        OnPropertyChanged(nameof(LoopEnd));

        _changed();
    }
}
