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
using JingleBox2.Machines;

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

    /// <param name="play">
    /// How the panel plays a note: through it, a pad or a zone tapped here is the same note as
    /// one played on the keyboard, so the keyboard moves to it and lights it. Without one, a tap
    /// still sounds, through the audition alone, and nothing on screen moves.
    /// </param>
    public InstrumentEditorViewModel(
        int index,
        TrackerInstrument instrument,
        Action changed,
        IWaveformService? waveforms = null,
        IInstrumentAudition? audition = null,
        ObservableCollection<Recording>? recordings = null,
        Action<Note>? play = null)
    {
        Index = index;
        _instrument = instrument;
        _changed = changed;

        // A tap on a pad or a zone is a note played on this panel, and the panel is what knows
        // the keyboard is there.
        Action<Note> tap = play ?? (note => audition?.Audition(instrument, note, TrackerCell.NoVolume));

        Recordings = recordings ?? new ObservableCollection<Recording>();

        // In front of every picker that offers a take: with a shelf of a hundred, the useful
        // question is which of the beds, not which of the hundred.
        Takes = new TakeFilter(Recordings);

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

            Zones = new ZoneMapViewModel(instrument.Zones, Sounded(changed), tap);

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

            // Picking another zone is not a change to the machine, so it does not come through
            // the change callback, and without this the picture would stay on the zone before.
            Zones.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ZoneMapViewModel.Selected)) FollowSound();
            };

            FollowSound();
        }

        if (instrument.IsBongaBong)
        {
            instrument.Kit ??= DrumKit.Empty();
            instrument.Kit.Clamp();

            Kit = new DrumKitViewModel(instrument.Kit, Sounded(changed), tap);

            Slices = Cutting(
                waveforms, DrumKit.PadCount,
                (path, points) =>
                {
                    instrument.Kit.Reslice(path, points);
                    Kit.Resliced();
                },
                at => instrument.Kit.Pads.ElementAtOrDefault(at)?.Shape,
                changed);

            // The same for the pad in hand.
            Kit.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DrumKitViewModel.Selected)) FollowSound();
            };

            FollowSound();
        }

        Patch = new SynthPatchViewModel(instrument.Patch, changed);

        if (instrument.IsSynth) return;

        instrument.EnsureShape();
        ReadWaveform(waveforms);

        Describe(waveforms);
    }

    /// <summary>
    /// Finds the machine's own face, if this installation has it and this build can drive it.
    /// </summary>
    /// <remarks>
    /// Two things have to be true. The machine has to be installed with a panel laid out, which
    /// is what <see cref="MachineProjects.PanelFor"/> answers; and this build has to know how to
    /// turn that machine's parameters into an instrument's settings, which is what the values
    /// are. A machine with a face and nobody to read it draws knobs that turn nothing, so the
    /// panel written by hand is shown instead and nothing is lost.
    ///
    /// The recording machine is the only one with an adapter today. The others keep the panel
    /// they have always had until each one has been converted, which is the point of asking
    /// rather than assuming: converting a machine is finished when its knobs move an instrument,
    /// not when its file exists.
    /// </remarks>
    private void Describe(IWaveformService? waveforms)
    {
        string id = Machine.For(_instrument.Kind).SlotId;

        if (Tracker.Machines.MachineProjects.PanelFor(id) is not { } face) return;

        if (Tracker.Machines.MachineProjects.For(id) is not { } project) return;

        if (!IsSample) return;

        var shape = project.Parameters;

        var shelf = new Tracker.Machines.TakeLibrary(Recordings, waveforms);

        var values = new Tracker.Machines.RecordingValues(_instrument, shelf)
        {
            // A knob on a described panel is a knob: it changes the instrument, the song is
            // dirty, and whatever else is showing the same setting has to hear about it.
            Changed = () =>
            {
                _changed();

                Moved();
            }
        };

        Described = new MachineFace(face, shape, project.Folder);

        Values = values;
        MachineTakes = shelf;
    }

    /// <summary>The machine's own face, or nothing when it is drawn by hand.</summary>
    public MachineFace? Described { get; private set; }

    /// <summary>Where that face reads and writes, which is this instrument.</summary>
    public IMachineValues? Values { get; private set; }

    /// <summary>Where it looks up the recording it names.</summary>
    public IMachineTakes? MachineTakes { get; private set; }

    /// <summary>True when the panel comes off the machine rather than out of this program.</summary>
    public bool IsDescribed => Described != null;

    /// <summary>
    /// True when the machine describes its own picker, so the page should not add one.
    /// </summary>
    /// <remarks>
    /// The page fills in what a machine does not say. Where the machine puts a picker on its own
    /// panel, a second one in the header is the same control twice, showing the same list, one
    /// of which is in the wrong place.
    /// </remarks>
    public bool DescribesPreset => Described?.Panel.Root is { } root && Holds(root, MachineElementKinds.Preset);

    private static bool Holds(MachineElement element, string kind)
    {
        if (element.Element == kind) return true;

        foreach (var child in element.Children)
            if (Holds(child, kind)) return true;

        return false;
    }

    /// <summary>Everything the page reads off the instrument, after a described panel moved one.</summary>
    /// <remarks>
    /// A described panel writes straight to the instrument, so the properties this class hands
    /// out are all suspect at once and there is no telling which. The header alone reads four of
    /// them, and the source line is the one somebody notices.
    /// </remarks>
    private void Moved()
    {
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(SampleText));
        OnPropertyChanged(nameof(BaseNoteText));
        OnPropertyChanged(nameof(Playhead));
    }

    public int Index { get; }

    public TrackerInstrument Instrument => _instrument;

    /// <summary>The machine's own theme: its colour and how far it is carried.</summary>
    public MachineTheme Theme => Machine.For(_instrument.Kind).Theme;

    /// <summary>Its colour on its own, for the band across the top of the panel.</summary>
    public string Colour => Theme.Accent;

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

    /// <summary>The same shelf, narrowed to a category. What the take pickers actually show.</summary>
    public TakeFilter Takes { get; }

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
    /// False for a machine's own slot on the shelf, which keeps the machine's name.
    /// </summary>
    /// <remarks>
    /// A rack's boxes are called what they are called. To have a Zampler called something else,
    /// duplicate it: the copy is yours and is named by you.
    ///
    /// A plugin is the same, for a different reason: it is called whatever the VST3 or CLAP
    /// says it is called. Naming it something else would mean two names for one plugin, and the
    /// one that matters is the plugin's, since that is what has to be found again when a song is
    /// opened on another machine.
    /// </remarks>
    public bool CanRename => !Machine.IsSlot(_instrument.Id) && !_instrument.IsPlugin;

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
    /// Points the chop editor at the recording the machine is holding, or failing that at the
    /// one on the piece in hand.
    /// </summary>
    /// <remarks>
    /// One recording shared by every piece is what a chopped machine is, and it is also what a
    /// machine with a single sample on it looks like before it has been chopped. Which is why
    /// there is no second place to load a take: chopping divides what is already there.
    ///
    /// When the pieces do not agree on one recording there is nothing whole to read cuts back
    /// off, but there is still something to chop: whatever is on the zone or pad you have
    /// picked. Showing that is the difference between a machine that says "put a recording on
    /// me" at somebody who has just put a recording on it, and one that shows them the
    /// recording they put there. The cuts stay hidden until the machine really is one file cut
    /// up, since a map of different recordings has no cuts to read.
    /// </remarks>
    private void FollowSound()
    {
        if (Slices == null) return;

        if (Zones != null)
        {
            string whole = Zones.Map.SlicedFile;

            Slices.Follow(
                whole.Length > 0 ? whole : Zones.Selected?.Zone.FilePath ?? "",
                Points(Zones.Map.IsSliced, Zones.Map.SlicePoints()));
        }
        else if (Kit != null)
        {
            string whole = Kit.Kit.SlicedFile;

            Slices.Follow(
                whole.Length > 0 ? whole : Kit.Selected?.Pad.FilePath ?? "",
                Points(Kit.Kit.IsSliced, Kit.Kit.SlicePoints()));
        }
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

        // A take landing on the Recording machine is a different file, and the picture was read
        // once when this was built. Without this it goes on saying the old one is missing.
        Reread();

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

    /// <summary>True when the voice is the one written out in XAML rather than the machine's own.</summary>
    public bool ShowsWrittenVoice => HasCommonVoice && !IsDescribed;

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
    /// written to the rack file. Called before a save rather than on every move.
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

    /// <summary>
    /// Where the sound has got to in the whole file, as a fraction of it, or -1 for nothing
    /// playing. The same number the chop editor's cursor runs on, for the panel that shows one
    /// picture rather than pieces.
    /// </summary>
    public double Playhead
    {
        get => _playhead;
        set
        {
            if (_playhead.Equals(value)) return;

            _playhead = value;
            OnPropertyChanged();
        }
    }

    private double _playhead = -1;

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
    /// <summary>The service that read the picture, kept so it can be read again.</summary>
    private IWaveformService? _waveforms;

    /// <summary>Reads the picture again when the file underneath has changed.</summary>
    private void Reread()
    {
        if (_instrument.IsSynth || _instrument.IsPlugin) return;
        if (_instrument.FilePath == _drawn) return;

        _waveform = null;
        _sampleProblem = null;

        Peaks = null;

        OnPropertyChanged(nameof(SampleText));
        OnPropertyChanged(nameof(SourceText));

        ReadWaveform(_waveforms);
    }

    /// <summary>Which file the picture on show was read from.</summary>
    private string _drawn = "";

    private void ReadWaveform(IWaveformService? waveforms)
    {
        _waveforms = waveforms;

        if (waveforms == null) return;

        string path = _instrument.FilePath;

        _drawn = path;

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

    /// <summary>
    /// Whether a key played on this panel stops the note before it. Off, the notes pile up,
    /// which is what a keyboard does; on, the machine plays one thing at a time, which is what
    /// a long recording wants.
    /// </summary>
    public bool OneVoice
    {
        get => _instrument.OneVoice;
        set
        {
            if (_instrument.OneVoice == value) return;

            _instrument.OneVoice = value;
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
