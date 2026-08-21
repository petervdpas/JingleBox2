using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using System;
using System.Globalization;

namespace JingleBox2.ViewModels;

/// <summary>
/// The instrument currently open in the editor. A sample and a synth share a name and a
/// level; the rest of the page shows whichever half applies.
/// </summary>
public sealed class InstrumentEditorViewModel : ObservableObject
{
    private readonly TrackerInstrument _instrument;
    private readonly Action _changed;

    public InstrumentEditorViewModel(int index, TrackerInstrument instrument, Action changed)
    {
        Index = index;
        _instrument = instrument;
        _changed = changed;

        if (instrument.IsSynth)
            Patch = new SynthPatchViewModel(instrument.Patch, changed);
    }

    public int Index { get; }

    public TrackerInstrument Instrument => _instrument;

    /// <summary>Null for a sample instrument, which has no patch to edit.</summary>
    public SynthPatchViewModel? Patch { get; }

    public bool IsSynth => _instrument.IsSynth;

    public bool IsSample => !IsSynth;

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);

    public string KindText => IsSynth ? "Synth" : "Sample";

    public string SourceText => IsSynth ? "Generated, no file." : _instrument.FilePath;

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

    public double Volume
    {
        get => _instrument.Volume;
        set
        {
            double clamped = Math.Clamp(double.IsNaN(value) ? 0 : value, 0, 1);
            if (Math.Abs(_instrument.Volume - clamped) < 0.0001) return;

            _instrument.Volume = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeText));
            _changed();
        }
    }

    public string VolumeText => "Level " + _instrument.Volume.ToString("0.00", CultureInfo.InvariantCulture);

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

    public bool Loop
    {
        get => _instrument.Loop;
        set
        {
            if (_instrument.Loop == value) return;

            _instrument.Loop = value;
            OnPropertyChanged();
            _changed();
        }
    }
}
