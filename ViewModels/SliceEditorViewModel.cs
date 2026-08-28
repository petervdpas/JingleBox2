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

    /// <summary>How a recording can be divided, and what each is called on the panel.</summary>
    /// <remarks>
    /// Three, because a recording can be one of three things. Struck sounds have attacks to find.
    /// Spoken or played phrases have silences between them and no attacks worth ranking, because
    /// a word is several attacks and the quietest moment inside one is louder than the pause
    /// after it. And a loop that is neither is simply divided.
    ///
    /// The first is what a fresh panel offers, since a chopped take is usually a break.
    /// </remarks>
    private static readonly string[] CutNames = { "Hits", "Gaps", "Even" };

    /// <summary>
    /// The three ways a piece can repeat, with what each is called on the panel.
    /// </summary>
    /// <remarks>
    /// Written out rather than worked out from the enum, so the words on the switch are chosen
    /// here and the order is the order they appear in. A switch built from an enum's own names
    /// would put whatever somebody called the third case in front of a person.
    /// </remarks>
    private static readonly (string Name, SampleLoopMode Mode)[] Loops =
    {
        ("Off", SampleLoopMode.None),
        ("Fwd", SampleLoopMode.Forward),
        ("Ping", SampleLoopMode.PingPong)
    };

    /// <summary>
    /// Who reduces a take to peaks. Null for a panel that is only being looked at, which is
    /// what the designer shows: there is no take, so there is nothing to read.
    /// </summary>
    private readonly IWaveformService? _waveforms;

    /// <summary>
    /// Where the cuts are written down, which is whatever holds the pieces.
    /// </summary>
    /// <remarks>
    /// The one direction that matters: this editor never keeps the cuts, so a piece put on a
    /// different key or given a different take is still the machine's business alone and cannot
    /// disagree with what is drawn.
    /// </remarks>
    private readonly Action<string, IReadOnlyList<double>> _apply;

    /// <summary>The window of the piece at a place, or null for a machine that has no pieces.</summary>
    private readonly Func<int, SampleShape?>? _windowFor;

    /// <summary>
    /// Told when a loop moved.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="_apply"/> rather than folded into it because a loop is a change to one
    /// piece and the cuts have not moved: saying it through the cutting callback would have the
    /// machine re-slice itself every time a loop handle was dragged.
    /// </remarks>
    private readonly Action? _changed;

    /// <summary>True while the points are being set from outside, so that is not an edit.</summary>
    private bool _settling;

    /// <summary>
    /// How long the take is, which is what turns a fraction into a place in the audio.
    /// </summary>
    /// <remarks>
    /// Nought until the take has been read, and the slicer is only ever asked for cuts after
    /// that, since a length of nought would put every cut at the same place.
    /// </remarks>
    private double _seconds;

    /// <summary>
    /// Opens an editor over whatever the machine is holding, cutting into at most that many
    /// pieces.
    /// </summary>
    /// <param name="waveforms">
    /// Reads the take's shape and its length, and is null where there is no decoder, in which
    /// case the picture stays empty and the cuts fall back to an even division.
    /// </param>
    /// <param name="maxSlices">
    /// How many places the machine has for a piece, held to what the slicer can produce: a kit
    /// has sixteen pads and a map has as many zones as it has, and cutting a take into more
    /// pieces than there are places to put them loses the end of the recording silently.
    /// </param>
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

    /// <inheritdoc/>
    /// <remarks>
    /// The control drags, adds to and removes from this, and every one of those is heard as an
    /// edit, so a boundary moved by hand reaches the machine without the control knowing there
    /// is a machine.
    /// </remarks>
    public ObservableCollection<double> Points { get; } = new();

    /// <inheritdoc/>
    public int MaxSlices { get; }

    /// <summary>The recording's shape, or null while it is being read.</summary>
    /// <remarks>
    /// Also the answer to whether the take has been read at all, which is what
    /// <see cref="Ready"/> asks before it cuts anything.
    /// </remarks>
    [ObservableProperty] private float[]? peaks;

    /// <summary>The recording under the picture, or empty for a panel holding none.</summary>
    /// <remarks>
    /// Compared with <see cref="Tracker.FilePaths.Same"/> rather than as text wherever it
    /// decides something, since the same take reaches this from a machine and from a song by
    /// two spellings of one path.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    [NotifyPropertyChangedFor(nameof(TakeText))]
    private string filePath = "";

    /// <summary>Which piece the settings underneath are about, or -1 for none.</summary>
    [ObservableProperty] private int selectedSlice = -1;

    /// <summary>
    /// A different piece is in hand, so the loop settings under the picture are about that one
    /// now and every one of them has to be said again.
    /// </summary>
    partial void OnSelectedSliceChanged(int value) => SaidAboutTheLoop();

    /// <summary>How many pieces the next slicing aims for.</summary>
    /// <remarks>
    /// What it aims for and not what it gets: finding attacks answers with however many it
    /// found, and only an even division can promise a number.
    /// </remarks>
    [ObservableProperty] private double pieces;

    /// <summary>Which of the three ways the next chop uses.</summary>
    [ObservableProperty] private string cutBy = CutNames[0];

    /// <summary>What the panel has to say for itself, or empty when it has nothing.</summary>
    /// <remarks>
    /// Cleared as soon as a cutting lands, so a message about a take that could not be read
    /// does not sit under a picture of one that could.
    /// </remarks>
    [ObservableProperty] private string status = "";

    /// <inheritdoc/>
    /// <remarks>
    /// Written from outside forty times a second while something is sounding, so it is compared
    /// before it is stored: a set that changes nothing would still repaint the picture, and the
    /// picture is the whole take.
    ///
    /// The tolerance is a fraction of the recording rather than a time, so on a long take it is
    /// finer than a pixel and on a short one it is still smaller than anything a hand can see.
    /// </remarks>
    public double Playhead
    {
        get => _playhead;
        set
        {
            if (Math.Abs(_playhead - value) < PlayheadTolerance) return;

            _playhead = value;
            OnPropertyChanged();
        }
    }

    /// <summary>How far the playhead has to move before it is worth repainting the take.</summary>
    private const double PlayheadTolerance = 1e-6;

    /// <summary>Where the sound has got to, -1 while nothing is sounding.</summary>
    private double _playhead = -1;

    /// <inheritdoc/>
    public IReadOnlyList<string> CutOptions { get; } = CutNames;

    /// <inheritdoc/>
    public IReadOnlyList<string> LoopNames { get; } = Loops.Select(l => l.Name).ToList();

    /// <summary>
    /// The window of the piece being worked on, or null when none is.
    /// </summary>
    /// <remarks>
    /// Asked for again every time rather than kept, because the pieces belong to the machine
    /// and a held one would be last selection's window after a preset landed.
    /// </remarks>
    private SampleShape? Window => _windowFor?.Invoke(SelectedSlice);

    /// <inheritdoc/>
    /// <remarks>
    /// Reads as off for a piece nobody is holding, and a name that is already what the piece is
    /// set to writes nothing: this is bound to a switch, and a switch says its value again as
    /// it is built.
    /// </remarks>
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

    /// <inheritdoc/>
    /// <remarks>
    /// Off, there is nothing to show and nothing to take hold of, which is also what stops a
    /// loop handle and a boundary sitting on the same pixel and arguing about a click.
    /// </remarks>
    public bool Looping => Window?.IsLooping ?? false;

    /// <inheritdoc/>
    /// <remarks>Nought when no piece is in hand, so the handle has somewhere to be drawn.</remarks>
    public double LoopStart
    {
        get => Window?.LoopStart ?? 0;
        set => MoveLoop(w => w.LoopStart = value, nameof(LoopStart));
    }

    /// <inheritdoc/>
    /// <remarks>And one, which is the far end of a piece rather than the far end of the take.</remarks>
    public double LoopEnd
    {
        get => Window?.LoopEnd ?? 1;
        set => MoveLoop(w => w.LoopEnd = value, nameof(LoopEnd));
    }

    /// <summary>
    /// Moves one end of the loop and tells the machine, having first put it back inside its piece.
    /// </summary>
    /// <remarks>
    /// Both ends go through here so the clamping cannot be written twice and differ: a handle
    /// dragged past its neighbour is an ordinary thing for a hand to do, and a loop whose ends
    /// had crossed plays nothing at all.
    /// </remarks>
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

    /// <inheritdoc/>
    public bool IsOpen => FilePath.Length > 0;

    /// <inheritdoc/>
    /// <remarks>
    /// The name without its folder or its extension, since the row above the picture is narrow
    /// and a path is mostly the parts that are the same for every take on the shelf.
    /// </remarks>
    public string TakeText => IsOpen ? Path.GetFileNameWithoutExtension(FilePath) : "no take";

    /// <summary>
    /// Cuts the recording up, throwing away wherever it was cut before.
    /// </summary>
    /// <remarks>
    /// Always enabled, and does nothing with no take open: a Chop button that greyed out on a
    /// panel drawn from a machine's own description would need the machine to say when, and it
    /// has nothing to say about it.
    /// </remarks>
    public IRelayCommand SliceCommand => new RelayCommand(Cut);

    /// <inheritdoc cref="IMachineSlices.Chop"/>
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
    ///
    /// The same recording with different cuts is still followed, because a preset can land on
    /// the take that is already open and bring its own cuts with it.
    ///
    /// The piece in hand is pulled back when the new cutting has fewer pieces than the old one,
    /// or the settings under the picture would be about a piece that is no longer there.
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

    /// <summary>
    /// True when the points on show are already the ones being offered.
    /// </summary>
    /// <remarks>
    /// Compared with a tolerance far below anything a hand can drag, because a point makes the
    /// round trip through the machine as a fraction and comes back as the same number written a
    /// different way. Comparing exactly would report every take as changed and re-settle the
    /// points under a drag.
    /// </remarks>
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

    /// <summary>
    /// Finds the cuts and settles them, once the take has been read.
    /// </summary>
    /// <remarks>
    /// Told to tell, which is the one place cutting is an edit: everywhere else the points are
    /// settled because the machine said what they were, and writing those back would be this
    /// panel answering its own question.
    /// </remarks>
    private void Cut()
    {
        if (!IsOpen) return;

        Ready(() => Settle(Found(), tell: true));
    }

    /// <summary>
    /// Where this take would be cut, given what the panel is asking for.
    /// </summary>
    /// <remarks>
    /// Anything the switch does not name falls to hunting attacks, which is the useful answer
    /// for most of what gets chopped and cannot fail: with nothing to find it divides evenly.
    /// The result goes through the slicer's own cleaning, so two cuts too close together to be
    /// a piece do not reach the machine as one.
    /// </remarks>
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

    /// <summary>
    /// Does the thing once the recording has been read, or at once if it already has.
    /// </summary>
    /// <remarks>
    /// Pressing Chop the moment a take is opened is the ordinary case, and the peaks are read
    /// off the drawing thread, so the button would otherwise cut a take nobody had looked at
    /// and produce nothing. It waits instead, and says so.
    /// </remarks>
    private void Ready(Action then)
    {
        if (Peaks != null) { then(); return; }

        Status = "Reading the take...";

        _pending = then;
    }

    /// <summary>
    /// What to do when the reading lands, or null when nobody is waiting.
    /// </summary>
    /// <remarks>
    /// One, not a queue: pressing Chop twice while a take is being read means the second set of
    /// settings, and cutting it twice would be work nobody would see the first half of.
    /// </remarks>
    private Action? _pending;

    /// <summary>
    /// Sets the points without that reading as an edit, so opening a take does not write its
    /// own cuts straight back over the ones it was given.
    /// </summary>
    /// <remarks>
    /// A piece is picked as soon as there is one, and unpicked when there is not, because the
    /// settings under the picture are about the piece in hand and a panel that opened with none
    /// held reads as a panel that does not work.
    /// </remarks>
    /// <param name="points">The boundaries to show, or null to leave the editor with none.</param>
    /// <param name="tell">
    /// True only where the points really are an edit, which is a cutting somebody asked for.
    /// </param>
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

    /// <summary>
    /// How many pieces the take is in right now.
    /// </summary>
    /// <remarks>
    /// One fewer than there are points, since the points include both ends of the recording. A
    /// take with one point is not in one piece, it is a take somebody has half cut, so it counts
    /// as nought.
    /// </remarks>
    public int SliceCount => Math.Max(0, Points.Count - 1);

    /// <inheritdoc/>
    public string CountText =>
        SliceCount == 0 ? "not chopped" : SliceCount == 1 ? "1 slice" : SliceCount + " slices";

    /// <summary>
    /// The cutting changed, so the machine is told and the counts are said again.
    /// </summary>
    /// <remarks>
    /// The one door out of this class, and everything that moves a boundary ends here, which is
    /// why the collection is watched rather than every gesture on the picture being hooked.
    /// Nothing is written back while the points are being settled from outside, or a take opened
    /// would write its own cuts over the machine's.
    ///
    /// A take cut into fewer than two points is not written back either: that is a panel with a
    /// recording on it and no chopping, and telling the machine so would take the pieces off an
    /// instrument that has them.
    /// </remarks>
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
    /// <remarks>
    /// What comes back is thrown away when the panel has moved on to another take in the
    /// meantime, which is what clicking down a list of recordings does: the reads land in
    /// whatever order they finish, and the last one to land is not the one being looked at.
    ///
    /// A take that cannot be read says so and leaves the peaks empty rather than throwing, since
    /// a missing or damaged recording is an ordinary thing to find on a shelf somebody has been
    /// tidying.
    /// </remarks>
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
