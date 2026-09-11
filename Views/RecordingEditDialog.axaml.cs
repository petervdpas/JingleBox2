using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using JingleBox2.Audio.Enums;
using JingleBox2.ViewModels;
using JingleBox2.Waveform;
using JingleBox2.Waveform.Interfaces;
using JingleBox2.Views.Enums;
using JingleBox2.Rack.Controls;

namespace JingleBox2.Views;

/// <summary>
/// One take, its picture, and the tools that work on it.
/// </summary>
/// <remarks>
/// A workshop rather than a dialog. The picture has the room, the tools stand in a box of square
/// marks down the left, and what the tool in hand needs set is in the panel under the box.
/// Picking a tool does nothing to the take: the panel holds the button that does it, so an edit
/// that cannot be undone is never one stray click on a button somebody was only reading.
///
/// Nothing here is pending: every edit rewrites the file the moment its button is pressed, so
/// the window stays open afterwards and the picture is drawn again from what is now on the disc.
///
/// Which tool is in hand is the tool box's own answer rather than something kept here, which is
/// the whole reason it is a tab strip: one picked at a time with its own panel under it is what
/// a tab control already is, and the alternative is a flag in this file and a list of panels to
/// be shown and hidden by hand.
///
/// The picture is <c>WaveformView</c>, the same control a machine's face and RECORD draw with,
/// so what a region is, how far its ends may travel, what a drag across the picture marks out,
/// how the wheel zooms and where the play cursor goes are all its business rather than this
/// window's. What this window adds is the readings: the picture deals in fractions of itself,
/// which is not a unit anybody works in, so <see cref="IRegionTimes"/> turns each handle into a
/// time and <see cref="TimeReadout"/> says it in the same words the transport does.
/// </remarks>
public partial class RecordingEditDialog : Window
{
    /// <summary>What plays the preview, and what reports where it has got to.</summary>
    private readonly WaveformPlayer _player;

    /// <summary>What turns a place on the picture into a time.</summary>
    private readonly IRegionTimes _times = new RegionTimes();

    /// <summary>
    /// The picture, which is the one waveform control this application has.
    /// </summary>
    /// <remarks>
    /// Found once the window is up rather than in the constructor, since it does not exist until
    /// the template has been applied. It owns what a region is and what is on screen: this
    /// window used to own both and drew the lot itself.
    /// </remarks>
    private WaveformView? _waveform;

    /// <summary>The mark on the play button, which is a triangle or a square as the preview runs.</summary>
    private ToolIcon? _playMark;

    /// <summary>And the word beside it, which says what pressing it now would do.</summary>
    private TextBlock? _playWord;

    /// <summary>Where the region begins, in the take's own time.</summary>
    private TimeReadout? _startTime;

    /// <summary>Where it ends.</summary>
    private TimeReadout? _endTime;

    /// <summary>And how long it lasts, which is the reading somebody is usually after.</summary>
    private TimeReadout? _lengthTime;

    /// <summary>How far the preview has got, against the take rather than against the region.</summary>
    private TimeReadout? _playheadTime;

    /// <summary>How long the whole take is.</summary>
    private TimeReadout? _totalTime;

    /// <summary>
    /// The RECORD page's view model, which owns the take being edited. Kept so its changes can
    /// be let go of when the window is pointed at another one.
    /// </summary>
    private RecordViewModel? _vm;

    /// <summary>Guards against a second Apply landing while the file is being rewritten.</summary>
    private bool _applying;

    /// <summary>True once the question about unsaved work has been answered and closing is on.</summary>
    private bool _leaving;

    /// <summary>The same, for a rename: the file is moving and cannot move twice.</summary>
    private bool _renaming;

    /// <summary>How much closer the buttons take you. A step you can see in one press.</summary>
    private const double ButtonZoomStep = 1.5;

    /// <summary>Where the region begins, or the start of the take before the picture is up.</summary>
    private double RegionStart => _waveform?.Start ?? 0;

    /// <summary>And where it ends.</summary>
    private double RegionEnd => _waveform?.End ?? 1;

    /// <summary>How many sample frames the take holds, or none before one has been read.</summary>
    private long Frames => _vm?.CurrentWaveform?.TotalSamples ?? 0;

    /// <summary>How many of them go past in a second.</summary>
    private int Rate => _vm?.CurrentWaveform?.SampleRate ?? 0;

    /// <summary>
    /// Builds the window and wires the picture up: the player's reports in, the pointer
    /// gestures out.
    /// </summary>
    /// <remarks>
    /// The canvas, the play button and the five readings are found when the window loads rather
    /// than here, since none of them exists until the template has been applied.
    ///
    /// The view model's changes are let go of before being taken again, because the data
    /// context announcement fires on every reassignment and would otherwise leave the window
    /// subscribed to every take it had ever shown.
    /// </remarks>
    public RecordingEditDialog() : this(WaveformPlayer.Silent())
    {
    }

    /// <summary>The same, over the bus a take goes onto.</summary>
    /// <remarks>
    /// Two constructors rather than one with a default, because the toolkit's runtime XAML loader
    /// looks for a public constructor taking nothing and an optional parameter is not one: with
    /// only the defaulted version the page builds with a warning saying it cannot be reached that
    /// way, and this build is kept at nought warnings.
    /// </remarks>
    /// <param name="recordings">
    /// How a recording is made to sound, which is the one way everything here plays one. Handed
    /// in rather than reached for, since this window is opened from RECORD and RECORD is what
    /// holds it.
    /// </param>
    /// <param name="takes">The bus a take goes onto, on the same terms.</param>
    public RecordingEditDialog(
        JingleBox2.Audio.Interfaces.IRecordingSource recordings,
        JingleBox2.Audio.Interfaces.IOutputBus takes)
        : this(new WaveformPlayer(recordings, takes))
    {
    }

    /// <summary>The window over whatever will be playing its preview.</summary>
    /// <remarks>
    /// Private, and the one place the window is really built: the two public ones differ only in
    /// what they hand it, which is a player that can sound a take or one that cannot, and neither
    /// is allowed to be half a player.
    /// </remarks>
    /// <param name="player">What plays the preview.</param>
    private RecordingEditDialog(WaveformPlayer player)
    {
        _player = player;

        InitializeComponent();

        _player.PositionChanged += position =>
        {
            if (_waveform != null) _waveform.Playhead = position;

            Say(_playheadTime, _times.At(position, Frames, Rate));
        };

        _player.Stopped += () =>
        {
            if (_waveform != null) _waveform.Playhead = -1;

            Say(_playheadTime, TimeSpan.Zero);
            SaysPlay(true);
        };

        Loaded += (_, _) =>
        {
            _playMark = this.FindControl<ToolIcon>("PlayMark");
            _playWord = this.FindControl<TextBlock>("PlayWord");
            _startTime = this.FindControl<TimeReadout>("RegionStartTime");
            _endTime = this.FindControl<TimeReadout>("RegionEndTime");
            _lengthTime = this.FindControl<TimeReadout>("RegionLengthTime");
            _playheadTime = this.FindControl<TimeReadout>("PlayheadTime");
            _totalTime = this.FindControl<TimeReadout>("TotalTime");
            _waveform = this.FindControl<WaveformView>("Waveform");

            if (_waveform != null) _waveform.PropertyChanged += RegionMoved;

            Readings();
        };

        DataContextChanged += (_, _) =>
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= TakeChanged;
                _vm.TakeRewriting -= Stop;
            }

            _vm = DataContext as RecordViewModel;

            if (_vm != null)
            {
                _vm.PropertyChanged += TakeChanged;
                _vm.TakeRewriting += Stop;
            }

            Readings();
        };

        Closing += (_, e) => Leaving(e);

        Shortcuts.ShortcutKeys.Listen(this);
    }

    /// <summary>
    /// The take under the window changed, so every reading about it is stale.
    /// </summary>
    /// <remarks>
    /// The shape is read again after each of the three edits as well as when another take is
    /// opened, so this is what carries a trim through to the clock: the picture is redrawn by
    /// its own binding and the readings beside it would otherwise still be about the file as it
    /// was before the cut.
    /// </remarks>
    /// <param name="sender">The view model. Not read: there is one.</param>
    /// <param name="e">Which of its properties moved.</param>
    private void TakeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(RecordViewModel.CurrentWaveform)) Readings();
    }

    /// <summary>
    /// The region moved on the picture, so what is playing and what is written down have to
    /// move with it.
    /// </summary>
    /// <remarks>
    /// The end was told to the player when Play was pressed and stayed where it was told, so
    /// dragging a handle inwards while a take played left the cursor running past the region
    /// and on to the end of the file. What is playing is the region, so the region moving has
    /// to reach it, and dragging the end back past what you are hearing stops it.
    /// </remarks>
    /// <param name="sender">The picture. Not read: there is one.</param>
    /// <param name="e">Which of its properties moved.</param>
    private void RegionMoved(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != WaveformView.StartProperty && e.Property != WaveformView.EndProperty) return;

        if (e.Property == WaveformView.EndProperty) _player.PlayUntil(RegionEnd);

        Readings();
    }

    /// <summary>
    /// Writes the four readings that are about where things are rather than about what is
    /// playing: the two handles, the stretch between them, and the length of the take.
    /// </summary>
    /// <remarks>
    /// Written rather than bound, because what the handles stand over is the picture's and the
    /// rate that turns it into a time is the take's, and nothing owns both. Safe before the
    /// window has been laid out and safe with no take open: a reading nobody has found yet is
    /// left alone, and a take that says nothing about itself reads nought.
    /// </remarks>
    private void Readings()
    {
        long frames = Frames;
        int rate = Rate;

        Say(_startTime, _times.At(RegionStart, frames, rate));
        Say(_endTime, _times.At(RegionEnd, frames, rate));
        Say(_lengthTime, _times.Between(RegionStart, RegionEnd, frames, rate));
        Say(_totalTime, _times.At(1, frames, rate));
    }

    /// <summary>Puts a time on one of the readings, where that reading is on the window yet.</summary>
    /// <param name="reading">The clock to write, or nothing before the window has been laid out.</param>
    /// <param name="time">What it should say.</param>
    private static void Say(TimeReadout? reading, TimeSpan time)
    {
        if (reading != null) reading.Time = time;
    }

    /// <summary>
    /// Puts both handles back on the two ends of the recording.
    /// </summary>
    /// <remarks>
    /// The way back from a region marked out by mistake, and the way to normalise after a trim
    /// without hunting the last handle back into the corner of the picture.
    /// </remarks>
    /// <param name="sender">The button. Not read.</param>
    /// <param name="e">Ignored.</param>
    private void SelectAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_waveform is not { } picture) return;

        picture.Start = 0;
        picture.End = 1;
    }

    /// <summary>
    /// Puts the whole recording back on the screen at once.
    /// </summary>
    /// <remarks>
    /// The region is left exactly where it is: this is about what can be seen and not about what
    /// is marked, and a button that quietly threw a selection away on the way to showing it all
    /// would be the worst kind of help.
    /// </remarks>
    /// <param name="sender">The button. Not read.</param>
    /// <param name="e">Ignored.</param>
    private void Fit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_waveform is not { } picture) return;

        picture.Zoom = WaveformViewport.MinZoom;
        picture.Scroll = 0;
    }

    /// <summary>
    /// Takes the picture closer, about the middle of what is on screen.
    /// </summary>
    /// <remarks>
    /// There is no pointer to hold still, unlike the wheel, so the middle is what stays. The
    /// control clamps at its own ends, and a press at the far end means "as far as it goes"
    /// rather than nothing.
    /// </remarks>
    /// <param name="sender">The button. Not read.</param>
    /// <param name="e">Ignored.</param>
    private void ZoomIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_waveform != null) _waveform.Zoom *= ButtonZoomStep;
    }

    /// <summary>Further out, by the same step, and stopping at the whole file.</summary>
    /// <inheritdoc cref="ZoomIn_Click"/>
    /// <param name="sender">The button. Not read.</param>
    /// <param name="e">Ignored.</param>
    private void ZoomOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_waveform != null) _waveform.Zoom /= ButtonZoomStep;
    }

    /// <summary>
    /// Plays what would survive the cut, from the play cursor, or stops what is playing.
    /// </summary>
    /// <remarks>
    /// One button for both, and its wording is written rather than bound, because the player is
    /// not a view model and its stopping is an event: it also ends on its own at the trim's end.
    /// </remarks>
    private void Play_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_player.IsPlaying)
        {
            _player.Stop();
            return;
        }

        if (_vm?.EditingPath is not { } path || _vm.CurrentWaveform == null) return;

        _player.Play(
            path,
            RegionStart,
            RegionEnd,
            _vm.CurrentWaveform.TotalSamples);

        if (_player.IsPlaying) SaysPlay(false);
    }

    /// <summary>
    /// Lets the working copy go, because it is about to be written over.
    /// </summary>
    /// <remarks>
    /// A method rather than a lambda so it can be taken off again: an event handler this window
    /// could not unsubscribe would keep a closed editor listening to the page for the rest of
    /// the session.
    /// </remarks>
    private void Stop() => _player.Stop();

    /// <summary>
    /// Puts the play button into one of its two states.
    /// </summary>
    /// <remarks>
    /// Written rather than bound, because the player is not a view model and its stopping is an
    /// event: it also ends on its own at the end of the selection. The mark is drawn rather than
    /// written for the reason <see cref="ToolMark"/> gives, which is that the two characters
    /// meaning these things are not in every font a machine might fall back to.
    /// </remarks>
    /// <param name="idle">True for the triangle, false for the square.</param>
    private void SaysPlay(bool idle)
    {
        if (_playMark != null) _playMark.Mark = idle ? ToolMark.Play : ToolMark.Stop;
        if (_playWord != null) _playWord.Text = idle ? "Play" : "Stop";
    }

    /// <summary>Closes the window, having asked about anything unsaved on the way out.</summary>
    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    /// <summary>
    /// The window is going, unless there is unsaved work and somebody would rather it did not.
    /// </summary>
    /// <remarks>
    /// **Asked rather than saved**, because saving is the explicit act this whole window is
    /// arranged around: a close that quietly wrote over somebody's take would be the worst
    /// possible reading of a button that says Close. Answering no leaves the window exactly as
    /// it was, with the work still in the history, which is why the question is the safe way
    /// round: the dangerous answer is the one somebody has to choose.
    ///
    /// The working copy is let go of only where the window really goes, so a cancelled close
    /// does not leave an editor standing over a copy that has been deleted.
    /// </remarks>
    /// <param name="e">The closing itself, which is what is cancelled while the question stands.</param>
    private async void Leaving(WindowClosingEventArgs e)
    {
        _player.Stop();

        if (!_leaving && _vm is { HasEdits: true } page)
        {
            e.Cancel = true;

            string many = page.EditSteps.Count > 2 ? "changes" : "change";

            if (!await ConfirmDialog.AskAsync(
                    "Close without saving",
                    $"'{page.EditName}' has {page.EditAt} {many} that are not saved. "
                    + "Closing now throws them away and leaves the take as it was.",
                    "Throw them away"))
                return;

            _leaving = true;

            Close();
            return;
        }

        _player.Dispose();

        if (_waveform != null) _waveform.PropertyChanged -= RegionMoved;

        if (_vm != null)
        {
            _vm.PropertyChanged -= TakeChanged;
            _vm.TakeRewriting -= Stop;
            _vm.EndEdit();
        }
    }

    /// <summary>
    /// Gives the recording another name. The dialog stays open: renaming is not finishing, and
    /// the usual next thing is to trim what you have just named.
    /// </summary>
    /// <remarks>
    /// The preview is stopped first. Playing from inside the dialog holds the file open, and a
    /// file that is open is one that will not move on Windows.
    /// </remarks>
    private async void Rename_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _renaming) return;

        _player.Stop();

        _renaming = true;

        try
        {
            await _vm.RenameAsync(_vm.EditName);
        }
        finally
        {
            _renaming = false;
        }
    }

    /// <summary>Keeps what is selected and throws the rest away.</summary>
    private async void Trim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await Edit(TakeEditKind.Trim)) return;

        if (_waveform is not { } picture) return;

        picture.Start = 0;
        picture.End = 1;
        picture.Playhead = -1;
        picture.Zoom = WaveformViewport.MinZoom;
    }

    /// <summary>Empties the selection.</summary>
    private async void Silence_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Edit(TakeEditKind.Silence);

    /// <summary>Turns it back to front.</summary>
    private async void Reverse_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Edit(TakeEditKind.Reverse);

    /// <summary>Brings it up from silence.</summary>
    private async void FadeIn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Edit(TakeEditKind.FadeIn);

    /// <summary>And takes it down to silence.</summary>
    private async void FadeOut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Edit(TakeEditKind.FadeOut);

    /// <summary>Lifts the whole take to the peak beside the button.</summary>
    private async void Normalize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await Edit(TakeEditKind.Normalize);

    /// <summary>
    /// Does one of the tools to the working copy, with the guards every one of them needs.
    /// </summary>
    /// <remarks>
    /// The preview is stopped first, since a file that is open is one that will not be rewritten
    /// on Windows, and every edit is switched off while one runs: a second write landing over
    /// the top of the first is the one way to lose work here.
    ///
    /// Nothing is undone by any of it in this window: the edit goes into the history on the page
    /// and the way back is the history rather than a memory of what this window did.
    /// </remarks>
    /// <param name="kind">Which edit.</param>
    /// <returns>True where the working copy changed.</returns>
    private async System.Threading.Tasks.Task<bool> Edit(TakeEditKind kind)
    {
        if (_vm == null || _applying) return false;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            return await _vm.EditAsync(kind, RegionStart, RegionEnd);
        }
        finally
        {
            _applying = false;
            SetApplyEnabled(true);
        }
    }

    /// <summary>
    /// Every edit goes off together: while the file is being rewritten, none of them may start a
    /// second write over the top of it.
    /// </summary>
    /// <remarks>
    /// By name rather than by walking the tool box, because a panel that has never been opened
    /// has not been built: a tab control makes its content when the tab is first picked, so
    /// anything found by walking it would be whichever tools somebody happened to have visited.
    /// A button that is not there yet cannot be pressed either, so the two agree.
    /// </remarks>
    /// <param name="enabled">True to let the edits be pressed again.</param>
    private void SetApplyEnabled(bool enabled)
    {
        foreach (string named in new[]
                 {
                     "ApplyTrimButton", "SilenceButton", "ReverseButton",
                     "FadeInButton", "FadeOutButton", "NormalizeButton"
                 })
        {
            var button = this.FindControl<Button>(named);

            if (button != null) button.IsEnabled = enabled;
        }
    }
}
