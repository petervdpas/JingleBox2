using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Machines;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// One recording cut into pieces: its shape, where the cuts are, and which piece is in hand.
/// </summary>
/// <remarks>
/// Knows nothing about what takes the pieces. Zampler hands them stretches of keyboard and
/// BongaBong hands them single keys, and both are told the same thing by the same callback:
/// this file, cut at these points. Which is the only way the two panels can share a picture
/// and a set of gestures without one of them having to know what the other holds.
///
/// The cuts are not kept here either. They are read out of whatever holds the pieces when this
/// opens, and written back through the callback whenever they change, so the pieces stay the
/// one place they live.
/// </remarks>
public sealed partial class SliceEditorViewModel : ObservableObject, IMachineSlices
{
    /// <summary>What a fresh slicing aims for when nothing says otherwise.</summary>
    public const int DefaultPieces = 8;

    /// <summary>
    /// The three ways a piece can repeat, with what each is called on the panel. Written out
    /// rather than worked out from the enum, so the words on the switch are chosen here and
    /// the order is the order they appear in.
    /// </summary>
    /// <summary>How a recording can be divided, and what each is called on the panel.</summary>
    /// <remarks>
    /// Three, because a recording can be one of three things. Struck sounds have attacks to find.
    /// Spoken or played phrases have silences between them and no attacks worth ranking, because
    /// a word is several attacks and the quietest moment inside one is louder than the pause
    /// after it. And a loop that is neither is simply divided.
    /// </remarks>
    private static readonly string[] CutNames = { "Hits", "Gaps", "Even" };

    private static readonly (string Name, SampleLoopMode Mode)[] Loops =
    {
        ("Off", SampleLoopMode.None),
        ("Fwd", SampleLoopMode.Forward),
        ("Ping", SampleLoopMode.PingPong)
    };

    private readonly IWaveformService? _waveforms;
    private readonly Action<string, IReadOnlyList<double>> _apply;
    private readonly Func<int, SampleShape?>? _windowFor;
    private readonly Action? _changed;

    /// <summary>True while the points are being set from outside, so that is not an edit.</summary>
    private bool _settling;

    private double _seconds;

    /// <param name="apply">Told the file and the points whenever the cutting changes.</param>
    /// <param name="windowFor">
    /// The window of the piece at that place, for the loop handles. Whoever holds the pieces
    /// knows where they are; this only needs to reach the one being worked on.
    /// </param>
    /// <param name="changed">Told when a loop moved, which the cutting callback does not cover.</param>
    public SliceEditorViewModel(
        IWaveformService? waveforms,
        int maxSlices,
        Action<string, IReadOnlyList<double>> apply,
        Func<int, SampleShape?>? windowFor = null,
        Action? changed = null)
    {
        _waveforms = waveforms;
        _apply = apply;
        _windowFor = windowFor;
        _changed = changed;

        MaxSlices = Math.Clamp(maxSlices, 1, SampleSlicer.MaxSlices);
        Pieces = Math.Min(DefaultPieces, MaxSlices);

        Points.CollectionChanged += (_, _) => Edited();
    }

    /// <summary>Where the recording is cut. The control drags, adds to and removes from this.</summary>
    public ObservableCollection<double> Points { get; } = new();

    /// <summary>How many pieces whatever holds them has room for.</summary>
    public int MaxSlices { get; }

    /// <summary>The recording's shape, or null while it is being read.</summary>
    [ObservableProperty] private float[]? peaks;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    [NotifyPropertyChangedFor(nameof(TakeText))]
    private string filePath = "";

    /// <summary>Which piece the settings underneath are about, or -1 for none.</summary>
    [ObservableProperty] private int selectedSlice = -1;

    partial void OnSelectedSliceChanged(int value) => SaidAboutTheLoop();

    /// <summary>How many pieces the next slicing aims for.</summary>
    [ObservableProperty] private double pieces;

    /// <summary>Which of the three ways the next chop uses.</summary>
    [ObservableProperty] private string cutBy = CutNames[0];

    [ObservableProperty] private string status = "";

    /// <summary>
    /// Where the sound has got to in the recording, as a fraction of it, or -1 for silence.
    /// </summary>
    public double Playhead
    {
        get => _playhead;
        set
        {
            // Compared before it is stored: this is set forty times a second and every set that
            // changes nothing would still repaint the picture.
            if (Math.Abs(_playhead - value) < 1e-6) return;

            _playhead = value;
            OnPropertyChanged();
        }
    }

    private double _playhead = -1;

    /// <summary>What the cutting switch offers.</summary>
    public IReadOnlyList<string> CutOptions { get; } = CutNames;

    /// <summary>What the loop switch offers.</summary>
    public IReadOnlyList<string> LoopNames { get; } = Loops.Select(l => l.Name).ToList();

    /// <summary>The window of the piece being worked on, or null when none is.</summary>
    private SampleShape? Window => _windowFor?.Invoke(SelectedSlice);

    /// <summary>Whether the piece in hand repeats, and how.</summary>
    public string LoopName
    {
        get
        {
            var mode = Window?.LoopMode ?? SampleLoopMode.None;

            foreach (var loop in Loops)
                if (loop.Mode == mode) return loop.Name;

            return Loops[0].Name;
        }
        set
        {
            var window = Window;
            if (window == null) return;

            foreach (var loop in Loops)
            {
                if (loop.Name != value || window.LoopMode == loop.Mode) continue;

                window.LoopMode = loop.Mode;
                OnPropertyChanged(nameof(LoopName));
                OnPropertyChanged(nameof(Looping));
                _changed?.Invoke();
                return;
            }
        }
    }

    /// <summary>
    /// True when the piece in hand repeats, so the picture shows its loop and lets it be moved.
    /// </summary>
    /// <remarks>
    /// Off, there is nothing to show and nothing to take hold of, which is also what stops a
    /// loop handle and a boundary sitting on the same pixel and arguing about a click.
    /// </remarks>
    public bool Looping => Window?.IsLooping ?? false;

    /// <summary>Where the loop starts, kept inside the piece it belongs to.</summary>
    public double LoopStart
    {
        get => Window?.LoopStart ?? 0;
        set => MoveLoop(w => w.LoopStart = value, nameof(LoopStart));
    }

    public double LoopEnd
    {
        get => Window?.LoopEnd ?? 1;
        set => MoveLoop(w => w.LoopEnd = value, nameof(LoopEnd));
    }

    private void MoveLoop(Action<SampleShape> write, string name)
    {
        var window = Window;
        if (window == null) return;

        write(window);
        window.Clamp();

        OnPropertyChanged(name);
        _changed?.Invoke();
    }

    /// <summary>Says the loop values again, for a piece that has just been picked.</summary>
    private void SaidAboutTheLoop()
    {
        OnPropertyChanged(nameof(LoopName));
        OnPropertyChanged(nameof(Looping));
        OnPropertyChanged(nameof(LoopStart));
        OnPropertyChanged(nameof(LoopEnd));
    }

    public bool IsOpen => FilePath.Length > 0;

    /// <summary>The take's name, for the row above the picture.</summary>
    public string TakeText => IsOpen ? Path.GetFileNameWithoutExtension(FilePath) : "no take";

    /// <summary>Cuts the recording up, throwing away wherever it was cut before.</summary>
    public IRelayCommand SliceCommand => new RelayCommand(Cut);

    /// <summary>The same act, for a panel that presses a button rather than binding a command.</summary>
    /// <remarks>
    /// A machine drawn from its own description has no bindings: it is handed the thing it is
    /// showing and calls it. So the act is offered plainly as well, and both go to the same
    /// place.
    /// </remarks>
    public void Chop() => Cut();

    /// <summary>
    /// Follows whatever recording the machine is holding, and wherever it is already cut.
    /// </summary>
    /// <remarks>
    /// Called every time the machine changes, which includes every movement of a boundary being
    /// dragged. So the same recording with the same cuts is left alone rather than being set
    /// again: settling the points mid-drag would put the boundary back where it was a moment
    /// ago and the drag would fight itself.
    /// </remarks>
    public void Follow(string? filePath, IReadOnlyList<double>? points)
    {
        string path = filePath ?? "";

        if (path.Length == 0)
        {
            Close();
            return;
        }

        if (Tracker.FilePaths.Same(path, FilePath))
        {
            // Same recording, but a preset may have landed on it with cuts of its own.
            if (!Holds(points)) Settle(points);
            return;
        }

        FilePath = path;
        Peaks = null;
        _seconds = 0;

        Read();
        Settle(points);

        if (SelectedSlice >= Points.Count - 1) SelectedSlice = Points.Count - 2;
    }

    /// <summary>True when the points on show are already the ones being offered.</summary>
    private bool Holds(IReadOnlyList<double>? points)
    {
        int count = points?.Count ?? 0;

        if (Points.Count != count) return false;

        for (int i = 0; i < count; i++)
            if (Math.Abs(Points[i] - points![i]) > 1e-12) return false;

        return true;
    }

    /// <summary>Puts the editor away, for a machine that is no longer holding a sliced take.</summary>
    public void Close()
    {
        FilePath = "";
        Peaks = null;
        _seconds = 0;
        SelectedSlice = -1;
        Settle(null);
        Status = "";
    }

    private void Cut()
    {
        if (!IsOpen) return;

        Ready(() => Settle(Found(), tell: true));
    }

    /// <summary>Where this take would be cut, given what the panel is asking for.</summary>
    private List<double> Found()
    {
        int want = (int)Math.Clamp(Math.Round(Pieces), 1, MaxSlices);

        var points = CutBy switch
        {
            "Gaps" => SampleSlicer.Gaps(Peaks, _seconds, want),
            "Even" => SampleSlicer.Even(want),
            _ => SampleSlicer.Transients(Peaks, _seconds, want)
        };

        return SampleSlicer.Clean(points, _seconds);
    }

    /// <summary>Does the thing once the recording has been read, or at once if it already has.</summary>
    private void Ready(Action then)
    {
        if (Peaks != null) { then(); return; }

        Status = "Reading the take...";

        _pending = then;
    }

    private Action? _pending;

    /// <summary>
    /// Sets the points without that reading as an edit, so opening a take does not write its
    /// own cuts straight back over the ones it was given.
    /// </summary>
    private void Settle(IReadOnlyList<double>? points, bool tell = false)
    {
        _settling = true;

        try
        {
            Points.Clear();

            if (points != null)
                foreach (double point in points) Points.Add(point);
        }
        finally
        {
            _settling = false;
        }

        if (Points.Count >= 2 && SelectedSlice < 0) SelectedSlice = 0;
        if (Points.Count < 2) SelectedSlice = -1;

        OnPropertyChanged(nameof(SliceCount));
        OnPropertyChanged(nameof(CountText));

        if (tell) Edited();
    }

    /// <summary>How many pieces the take is in right now.</summary>
    public int SliceCount => Math.Max(0, Points.Count - 1);

    public string CountText =>
        SliceCount == 0 ? "not chopped" : SliceCount == 1 ? "1 slice" : SliceCount + " slices";

    private void Edited()
    {
        OnPropertyChanged(nameof(SliceCount));
        OnPropertyChanged(nameof(CountText));

        if (_settling || !IsOpen || Points.Count < 2) return;

        Status = "";

        _apply(FilePath, Points.ToList());

        SaidAboutTheLoop();
    }

    /// <summary>
    /// Reduces the take to peaks off the UI thread. A long one takes a moment, and the panel
    /// should not stop while it does.
    /// </summary>
    private void Read()
    {
        if (_waveforms == null || !IsOpen) return;

        string path = FilePath;

        if (!File.Exists(path))
        {
            Status = "That take is missing.";
            return;
        }

        Task.Run(() =>
        {
            try
            {
                return _waveforms.AnalyzeFile(path);
            }
            catch (Exception)
            {
                return null;
            }
        }).ContinueWith(read => Dispatcher.UIThread.Post(() =>
        {
            // The panel may have moved on to another take while this one was being read.
            if (!Tracker.FilePaths.Same(path, FilePath)) return;

            var data = read.Result;

            if (data == null)
            {
                Status = "That take could not be read.";
                return;
            }

            _seconds = data.SampleRate > 0 ? (double)data.TotalSamples / data.SampleRate : 0;

            Peaks = data.PeakData;
            Status = "";

            var waiting = _pending;
            _pending = null;
            waiting?.Invoke();
        }));
    }
}
