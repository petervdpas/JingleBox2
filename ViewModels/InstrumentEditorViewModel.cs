using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.Tracker.Synth;
using JingleBox2.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        IWaveformService? waveforms = null,
        IInstrumentAudition? audition = null,
        ObservableCollection<Recording>? recordings = null)
    {
        Index = index;
        _instrument = instrument;
        _changed = changed;

        Recordings = recordings ?? new ObservableCollection<Recording>();

        if (instrument.IsPlugin)
        {
            OpenPlugin(audition);
            return;
        }

        // Both kinds run through the same voice now, so both have a patch to edit: a sample
        // has an envelope, a filter and modulation exactly as a generated wave does. Only the
        // oscillator half of it is meaningless for a recording, and the page hides that.
        // The machine decides which patch there is to edit. Ouroboros keeps its own; every
        // other kind of ours plays from the older one.
        if (instrument.IsOuroboros)
        {
            instrument.Ouroboros ??= new OuroborosPatch();
            Ouroboros = new OuroborosPatchViewModel(instrument.Ouroboros, changed);
        }

        if (instrument.IsZampler)
        {
            instrument.Zones ??= ZoneMap.Empty();
            instrument.Zones.Clamp();

            instrument.Zampler ??= new ZamplerPatch();
            instrument.Zampler.Clamp();

            Zones = new ZoneMapViewModel(
                instrument.Zones, Sounded(changed),
                note => audition?.Audition(instrument, note, TrackerCell.NoVolume));

            Zampler = new ZamplerPatchViewModel(instrument.Zampler, changed);

            Slices = Cutting(
                waveforms, ZoneMap.MaxZones,
                (path, points) =>
                {
                    instrument.Zones.Reslice(path, points);
                    Zones.Resliced();
                },
                at => instrument.Zones.Zones.ElementAtOrDefault(at)?.Shape,
                changed);

            FollowSound();
        }

        if (instrument.IsBongaBong)
        {
            instrument.Kit ??= DrumKit.Empty();
            instrument.Kit.Clamp();

            Kit = new DrumKitViewModel(
                instrument.Kit, Sounded(changed),
                note => audition?.Audition(instrument, note, TrackerCell.NoVolume));

            Slices = Cutting(
                waveforms, DrumKit.PadCount,
                (path, points) =>
                {
                    instrument.Kit.Reslice(path, points);
                    Kit.Resliced();
                },
                at => instrument.Kit.Pads.ElementAtOrDefault(at)?.Shape,
                changed);

            FollowSound();
        }

        Patch = new SynthPatchViewModel(instrument.Patch, changed);

        if (instrument.IsSynth) return;

        instrument.EnsureShape();
        ReadWaveform(waveforms);
    }

    public int Index { get; }

    public TrackerInstrument Instrument => _instrument;

    /// <summary>The voice settings, which both kinds of instrument have.</summary>
    public SynthPatchViewModel? Patch { get; }

    /// <summary>Ouroboros's own patch, when that is the machine. Null on every other.</summary>
    public OuroborosPatchViewModel? Ouroboros { get; }

    /// <summary>BongaBong's kit, when that is the machine. Null on every other.</summary>
    public DrumKitViewModel? Kit { get; }

    public bool IsBongaBong => _instrument.IsBongaBong;

    /// <summary>
    /// Your own takes, offered to the machines that put recordings on things.
    /// </summary>
    /// <remarks>
    /// The RECORD tab is the sampler's input. On the machine Zampler is named for, sampling and
    /// playing were one box: you sampled into it and put the result on the keyboard, and there
    /// was no step in between called finding the file. This is that step removed.
    ///
    /// It is the same list the RECORD tab shows, live, so a take made a moment ago is on a pad
    /// without anything being refreshed.
    /// </remarks>
    public ObservableCollection<Recording> Recordings { get; }

    /// <summary>
    /// Brings recordings in from the disc and puts them on the shelf of takes.
    /// </summary>
    /// <remarks>
    /// Copied in rather than pointed at, so a song never depends on a folder somebody else is
    /// free to tidy. What comes back are the paths as they now are, ready to go straight onto
    /// pads or zones.
    /// </remarks>
    public IReadOnlyList<string> Import(IEnumerable<string> paths)
    {
        var taken = RecordingImport.Take(paths);

        foreach (var recording in taken) Recordings.Add(recording);

        return taken.Select(r => r.FilePath).ToList();
    }

    /// <summary>Zampler's map, when that is the machine. Null on every other.</summary>
    public ZoneMapViewModel? Zones { get; }

    /// <summary>
    /// The take being cut into pieces, on the machines that hold pieces. Null on every other.
    /// </summary>
    /// <remarks>
    /// Both machines get the same one. What differs is how many pieces it will cut and what
    /// happens to them afterwards, and both of those are settled where it is made.
    /// </remarks>
    public SliceEditorViewModel? Slices { get; }

    public bool IsSlicing => Slices != null;

    /// <summary>
    /// Wraps a machine's change callback so the chop editor hears about it too.
    /// </summary>
    /// <remarks>
    /// A recording arrives on a machine in several ways: one take onto one zone, a folder of
    /// them at once, a preset landing. All of them end in the same callback, so following it is
    /// following all of them, and there is no list of entry points to keep up to date.
    /// </remarks>
    private Action Sounded(Action changed) => () =>
    {
        changed();
        FollowSound();
    };

    /// <summary>
    /// Points the chop editor at the recording the machine is holding, if it is holding one.
    /// </summary>
    /// <remarks>
    /// One recording shared by every piece is what a chopped machine is, and it is also what a
    /// machine with a single sample on it looks like before it has been chopped. Which is why
    /// there is no second place to load a take: chopping divides what is already there.
    ///
    /// A real multisample, several different recordings across the keyboard, has no one
    /// recording to show, so the editor puts itself away rather than picking one of them.
    /// </remarks>
    private void FollowSound()
    {
        if (Slices == null) return;

        if (Zones != null) Slices.Follow(Zones.Map.SlicedFile, Points(Zones.Map.IsSliced, Zones.Map.SlicePoints()));
        else if (Kit != null) Slices.Follow(Kit.Kit.SlicedFile, Points(Kit.Kit.IsSliced, Kit.Kit.SlicePoints()));
    }

    private static IReadOnlyList<double>? Points(bool sliced, IReadOnlyList<double> points) =>
        sliced ? points : null;

    /// <summary>
    /// Makes the slice editor and keeps the picture and the settings pointing at the same
    /// piece, whichever of the two was used to choose it.
    /// </summary>
    private SliceEditorViewModel Cutting(
        IWaveformService? waveforms,
        int maxSlices,
        Action<string, IReadOnlyList<double>> apply,
        Func<int, SampleShape?> windowFor,
        Action changed)
    {
        var slices = new SliceEditorViewModel(waveforms, maxSlices, apply, windowFor, changed);

        slices.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SliceEditorViewModel.SelectedSlice)) return;
            if (slices.SelectedSlice < 0) return;

            Zones?.SelectAt(slices.SelectedSlice);
            Kit?.SelectAt(slices.SelectedSlice);
        };

        // And the other way about. The map and the picture are two views of the same pieces,
        // and two views that disagree about which piece is in hand are worse than one view.
        if (Zones != null)
        {
            Zones.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ZoneMapViewModel.Selected)) return;
                if (Zones.Selected == null) return;

                int at = Zones.Zones.IndexOf(Zones.Selected);

                if (at >= 0) slices.SelectedSlice = at;
            };
        }

        if (Kit != null)
        {
            Kit.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(DrumKitViewModel.Selected)) return;
                if (Kit.Selected == null) return;

                int at = Kit.Pads.IndexOf(Kit.Selected);

                if (at >= 0) slices.SelectedSlice = at;
            };
        }

        return slices;
    }

    /// <summary>Zampler's filter and envelopes, when that is the machine.</summary>
    public ZamplerPatchViewModel? Zampler { get; }

    public bool IsZampler => _instrument.IsZampler;

    /// <summary>
    /// A preset has landed on the instrument: everything the panel shows may have moved.
    /// </summary>
    /// <remarks>
    /// The patches were written into rather than replaced, so the panel is still bound to the
    /// right objects and only has to be told to read them again.
    /// </remarks>
    public void Reloaded()
    {
        Patch?.RefreshAll();
        Ouroboros?.RefreshAll();
        Kit?.Refresh();
        Zones?.Refresh();
        Zampler?.RefreshAll();

        // The whole sound has been replaced, which is a different recording as surely as
        // dropping one on a zone is. The change callbacks the machines carry do not fire for
        // this, because nothing went through them: the instrument was written into from
        // outside.
        FollowSound();

        OnPropertyChanged(string.Empty);

        _changed();
    }

    /// <summary>What the machine is called, so the panel can say which one this is.</summary>
    public string MachineName => _instrument.Machine.Name;

    public bool IsOuroboros => _instrument.IsOuroboros;

    /// <summary>
    /// True for the machines that share the older voice: a recording and the older synth.
    /// </summary>
    /// <remarks>
    /// A plugin does its own envelope and filter, and Ouroboros brings its own panel, so
    /// neither shows the shared one. Without this both panels are drawn at once.
    /// </remarks>
    /// <summary>
    /// True for the machines that still share the general voice editor rather than having a
    /// front panel of their own.
    /// </summary>
    /// <remarks>
    /// Only the sampler now. OddSkilla and Ouroboros each have a panel of their own and a
    /// plugin has its own interface, so what is left is the one that is not a machine yet, and
    /// it keeps the shared editor until Zampler is built to replace it.
    /// </remarks>
    public bool HasCommonVoice => IsSample;

    public bool IsSynth => _instrument.IsSynth;

    public bool IsPlugin => _instrument.IsPlugin;

    public bool IsSample => !IsSynth && !IsPlugin && !IsOuroboros && !IsBongaBong && !IsZampler;

    /// <summary>The plugin's own knobs, when this instrument is a plugin.</summary>
    public PluginControlsViewModel? PluginPanel { get; private set; }

    public bool HasPluginPanel => PluginPanel != null;

    /// <summary>Said plainly when the plugin named by the instrument is not here to open.</summary>
    public string PluginProblem { get; private set; } = "";

    public bool HasPluginProblem => !string.IsNullOrWhiteSpace(PluginProblem);

    /// <summary>What plugin this instrument is, for the page to name.</summary>
    public string PluginText =>
        string.IsNullOrWhiteSpace(_instrument.PluginName) ? _instrument.PluginPath : _instrument.PluginName;

    /// <summary>
    /// Opens the plugin behind this instrument and builds its knobs.
    /// </summary>
    /// <remarks>
    /// A knob moved here changes the running plugin, and the patch is read back out of it
    /// afterwards. That is the only way round: a Serum sound is wavetables and samples as much
    /// as knob positions, and only the plugin can hand those over.
    /// </remarks>
    private void OpenPlugin(IInstrumentAudition? audition)
    {
        if (audition == null)
        {
            PluginProblem = "No audio engine to open this plugin in.";
            return;
        }

        var plugin = audition.PluginFor(_instrument);

        if (plugin == null)
        {
            PluginProblem = string.IsNullOrWhiteSpace(PluginText)
                ? "This instrument has no plugin set."
                : $"'{PluginText}' would not open. It may not be installed on this machine.";
            return;
        }

        _plugin = plugin;

        // Not prepared here. The plugin's interface is opened when its window is, because
        // Serum wants 1190 by 740 and Vital 1400 by 820, and neither belongs inside a page.
        PluginPanel = new PluginControlsViewModel(plugin, KeepPatch);
    }

    /// <summary>The plugin this editor is showing, when it is showing one.</summary>
    private Audio.Plugins.IPluginInstrument? _plugin;

    /// <summary>Set when a knob has moved and the patch has not been read back yet.</summary>
    private bool _patchStale;

    /// <summary>
    /// A knob moved. The patch is not read out here: asking a plugin for its state means
    /// asking it to serialise everything it holds, which for Vital is a couple of hundred
    /// kilobytes, and doing that on every degree of a knob turn would make the knob stutter.
    /// It is read once the turning stops, in <see cref="SyncPluginState"/>.
    /// </summary>
    private void KeepPatch()
    {
        _patchStale = true;
        _changed();
    }

    /// <summary>
    /// Puts the plugin's interface away, for an instrument being left. The plugin itself
    /// carries on: it is still what the tracker plays.
    /// </summary>
    public void ClosePlugin()
    {
        SyncPluginState();

        Closing?.Invoke();
        PluginPanel?.Close();
    }

    /// <summary>
    /// Raised when this instrument is being left, so anything showing its plugin can put
    /// itself away. A view model reaching into a window would be worse than one event.
    /// </summary>
    public event Action? Closing;

    /// <summary>
    /// Takes the sound back out of the plugin and onto the instrument, so it is what gets
    /// written to the library file. Called before a save rather than on every move.
    /// </summary>
    public void SyncPluginState()
    {
        if (!_patchStale || _plugin == null) return;

        _patchStale = false;
        _instrument.StateBytes = _plugin.SaveState();
    }

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);

    public string KindText => IsSynth ? "Synth" : IsPlugin ? "Plugin" : "Sample";

    public string SourceText => IsSynth ? "Generated, no file." : IsPlugin ? PluginText : _instrument.FilePath;

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
            if (IsSynth || IsPlugin) return "";
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
