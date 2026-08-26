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
        // The machine decides which patch there is to edit. The mono synth keeps its own; every
        // other kind of ours plays from the older one.
        if (instrument.IsMonoSynth)
        {
            instrument.MonoSynth ??= new MonoSynthPatch();
            MonoSynth = new MonoSynthPatchViewModel(instrument.MonoSynth, changed);
        }

        if (instrument.IsSampler)
        {
            instrument.Zones ??= ZoneMap.Empty();
            instrument.Zones.Clamp();

            instrument.Sampler ??= new SamplerPatch();
            instrument.Sampler.Clamp();

            Zones = new ZoneMapViewModel(instrument.Zones, Sounded(changed), tap);

            Sampler = new SamplerPatchViewModel(instrument.Sampler, changed);

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

        if (instrument.IsKit)
        {
            // How many pads there are is the machine's to say, declared as buttons on its panel.
            // A machine that is not installed as a project says nothing, and the kit keeps
            // whatever size it was read at.
            int declared = Tracker.Machines.MachineProjects.For(Machine.For(instrument.Kind).SlotId)
                is { } project
                ? Tracker.Machines.MachinePresetFile.Buttons(project).Count
                : 0;

            instrument.Kit ??= DrumKit.Empty(declared > 0 ? declared : DrumKit.PadCount);
            instrument.Kit.Clamp(declared);

            Kit = new DrumKitViewModel(instrument.Kit, Sounded(changed), tap);

            Slices = Cutting(
                waveforms, instrument.Kit.Pads.Count,
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

        // A generated wave has no file to read the shape of, and nothing below this line is about
        // anything else. The machine's own face is, though: it has one whether or not there is a
        // recording behind it, so it is asked for either way.
        if (instrument.IsSynth)
        {
            Describe(waveforms);

            return;
        }

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

        // A knob on a described panel is a knob: it changes the instrument, the song is dirty,
        // and whatever else is showing the same setting has to hear about it.
        void Moved()
        {
            _changed();

            SayAgain();
        }

        var shelf = new Tracker.Machines.TakeLibrary(Recordings, waveforms);

        if (IsSample)
        {
            Values = new Tracker.Machines.RecordingValues(_instrument, shelf) { Changed = Moved };
        }
        else if (IsKit && Kit is { } kit)
        {
            Values = new Tracker.Machines.KitValues(kit) { Changed = Moved };

            MachinePads = new Tracker.Machines.KitPads(kit);
            MachineSlices = Slices;

            // Every setting the panel shows is about the pad in hand, so picking a different one
            // moves all of them at once without anything on the panel being touched. The panel
            // is told to read itself again rather than being rebuilt: rebuilding would answer
            // this and would also throw away the pad grid the press just landed on.
            kit.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(DrumKitViewModel.Selected)) SayAgain();
            };
        }
        else if (IsSynth && Patch is { } voice)
        {
            var settings = new Tracker.Machines.SynthValues(voice, _instrument) { Changed = Moved };

            Values = settings;

            // The picture of the wave, drawn out of the same engine that makes the sound.
            MachineScope = new Tracker.Machines.SynthScope(voice);
        }
        else if (IsMonoSynth && MonoSynth is { } mono)
        {
            Values = new Tracker.Machines.MonoSynthValues(mono) { Changed = Moved };
        }
        else if (IsSampler && Zones is { } zones && Sampler is { } filter)
        {
            Values = new Tracker.Machines.SamplerValues(zones, filter) { Changed = Moved };

            MachineZones = new Tracker.Machines.SamplerZones(zones);
            MachineSlices = Slices;

            // The same as the kit: half the panel is about the zone in hand, so picking another
            // one moves all of it without anything on the panel being touched.
            zones.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ZoneMapViewModel.Selected)) SayAgain();
            };
        }
        else
        {
            return;
        }

        Described = new MachineFace(face, project.Parameters, project.Folder);

        MachineTakes = shelf;
    }

    /// <summary>The kit behind the pads, on a machine that has any.</summary>
    public IMachinePads? MachinePads { get; private set; }

    /// <summary>The map behind the zones, on a machine that lays recordings across the keyboard.</summary>
    public IMachineZones? MachineZones { get; private set; }

    /// <summary>The shape it is making, on a machine that generates its sound.</summary>
    public IMachineScope? MachineScope { get; private set; }

    /// <summary>The recording being cut into pieces, on a machine that fills itself from one.</summary>
    public IMachineSlices? MachineSlices { get; private set; }

    /// <summary>
    /// Bumped when everything the described panel shows may have moved.
    /// </summary>
    /// <remarks>
    /// A count rather than an event, because the panel takes it as a plain binding and there is
    /// nothing to wire up or take down. What it means is "read yourself again", which is the
    /// answer to a setting being written where the panel could not see it happen.
    /// </remarks>
    public int PanelReread { get; private set; }

    /// <summary>
    /// Everything the described panel shows may have moved, so it should read itself again.
    /// </summary>
    /// <remarks>
    /// Public because the page can move a setting the panel could not: putting a recording on
    /// the machine happens in a dialog, and the panel has no way of knowing the dialog closed.
    /// </remarks>
    public void SaidAgain() => SayAgain();

    private void SayAgain()
    {
        PanelReread++;

        OnPropertyChanged(nameof(PanelReread));

        Moved();
    }

    /// <summary>The machine's own face, or nothing when it is drawn by hand.</summary>
    /// <remarks>
    /// Replaced rather than edited when the sound underneath changes, since the panel redraws on
    /// being handed a different machine and the machine itself has not changed: the same face,
    /// with a different recording behind it.
    /// </remarks>
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

    /// <summary>
    /// True when the machine draws its own keyboard, so the page should not add one.
    /// </summary>
    /// <remarks>
    /// The keyboard used to be the same on every panel and stood at the foot of all of them. It
    /// is not the same on a kit: which keys have drums on them and which one is in hand are
    /// things only the machine's own keyboard can show, so where a machine draws one, the shared
    /// keyboard would be a second keyboard saying less.
    ///
    /// Asked of the description rather than of which machine this is, so a machine somebody else
    /// built gets the same answer for the same reason.
    /// </remarks>
    public bool DescribesKeys => Described?.Panel.Root is { } root && Holds(root, MachineElementKinds.Keys);

    /// <summary>
    /// True when the keyboard at the foot of the panel is the one to show.
    /// </summary>
    /// <remarks>
    /// It is there unless the machine's own description already put one on the panel, which a
    /// kit does: hitting a key and watching its pad answer is one glance, so a kit's keyboard
    /// belongs beside its pads rather than at the foot of the page.
    /// </remarks>
    public bool ShowsSharedKeys => !DescribesKeys;

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

    /// <summary>The mono synth's own patch, when that is the machine. Null on every other.</summary>
    public MonoSynthPatchViewModel? MonoSynth { get; }

    /// <summary>BongaBong's kit, when that is the machine. Null on every other.</summary>
    public DrumKitViewModel? Kit { get; }

    public bool IsKit => _instrument.IsKit;

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

    /// <summary>The sampler's map, when that is the machine. Null on every other.</summary>
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

    /// <summary>The sampler's filter and envelopes, when that is the machine.</summary>
    public SamplerPatchViewModel? Sampler { get; }

    public bool IsSampler => _instrument.IsSampler;

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
        MonoSynth?.RefreshAll();
        Kit?.Refresh();
        Zones?.Refresh();
        Sampler?.RefreshAll();

        // A take landing on the Recording machine is a different file, and the picture was read
        // once when this was built. Without this it goes on saying the old one is missing.
        Reread();

        // And a panel drawn from the machine's own description was built from the settings as
        // they were. Nothing about the machine has changed, so nothing tells it to draw again:
        // the same face has to be handed over as a new one for the picture to catch up with the
        // recording that has just landed on it.
        if (Described is { } face)
        {
            Described = face.Again();

            // Said by name as well as in the sweep below. A compiled binding is told which
            // property to watch, and the panel is the one thing on this page that redraws from
            // scratch: it has to hear it plainly rather than in a list of everything.
            OnPropertyChanged(nameof(Described));
        }

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

    /// <summary>
    /// Which machine this is, by the id its settings are stored under.
    /// </summary>
    /// <remarks>
    /// For anything pointing at one of its parameters. A hardware knob is pointed at Zampler's
    /// cutoff rather than at this instrument's, so the machine is what the mapping names and
    /// the name on the front is not it: that can be reworded, and the id never is.
    /// </remarks>
    public string MachineId => Machine.For(_instrument.Kind).SlotId;

    public bool IsMonoSynth => _instrument.IsMonoSynth;

    public bool IsSynth => _instrument.IsSynth;

    public bool IsPlugin => _instrument.IsPlugin;

    public bool IsSample => !IsSynth && !IsPlugin && !IsMonoSynth && !IsKit && !IsSampler;

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
        _instrument.PluginState = _plugin.SaveState();
    }

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);

    public string KindText => IsSynth ? "Synth" : IsPlugin ? "Plugin" : "Sample";

    /// <summary>
    /// The file this instrument plays, for the one kind that has one.
    /// </summary>
    /// <remarks>
    /// Empty where there is nothing to name, and the line is then not drawn at all rather than
    /// drawn saying so. A generated sound having no file is not news: the panel is covered in
    /// oscillator controls, which is the same information and better put.
    ///
    /// A plugin only, now that a machine describes its own panel. The recording behind a
    /// described machine is one of its own settings and the machine says where it wants it
    /// said, which for the Recording machine is under the picker that chose it. Said here as
    /// well it was said twice, and the host's copy was always above the whole panel, since the
    /// host hands the machine one block and cannot reach inside it. A plugin has no described
    /// panel to put it on, so that one stays.
    /// </remarks>
    public string SourceText => IsPlugin ? PluginText : "";

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
        if (Tracker.FilePaths.Same(_instrument.FilePath, _drawn)) return;

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
