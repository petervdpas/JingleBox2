using Avalonia;
using Avalonia.Controls;
using JingleBox2.ViewModels;
using JingleBox2.Waveform;
using JingleBox2.Rack.Controls;

namespace JingleBox2.Views;

/// <summary>
/// One take, its picture, and the two things that can be done to it: trimmed to what is
/// selected, and lifted to full level.
/// </summary>
/// <remarks>
/// Buttons in, a file rewritten out. The picture is <c>WaveformView</c>, the same control a
/// machine's face and RECORD draw with, so what a region is, how far its ends may travel, what
/// a drag across the picture marks out, how the wheel zooms and where the play cursor goes are
/// all its business rather than this window's.
///
/// This window drew its own for years: a canvas, a viewport, two trim handles, a selection
/// tint, a playhead marker and the pointer handling for all of it, some six hundred lines. Two
/// things kept them apart and both were small. The control could not be zoomed from a button,
/// which is what the two magnifying glasses do, and it had no way to drag a region out from
/// nothing, which is the gesture this window was written to have. Both are the control's now,
/// and a machine's face gets them as well.
///
/// Both edits rewrite the file where it lies rather than making a new take, so the window
/// stays open afterwards and the picture is drawn again from what is now on the disc.
/// </remarks>
public partial class RecordingEditDialog : Window
{
    /// <summary>What plays the preview, and what reports where it has got to.</summary>
    private readonly WaveformPlayer _player;

    /// <summary>
    /// The picture, which is the one waveform control this application has.
    /// </summary>
    /// <remarks>
    /// Found once the window is up rather than in the constructor, since it does not exist until
    /// the template has been applied. It owns what a region is and what is on screen: this
    /// window used to own both and drew the lot itself.
    /// </remarks>
    private WaveformView? _waveform;

    /// <summary>Kept because its wording is written to: it says Play or Stop as the preview runs.</summary>
    private Button? _playButton;

    /// <summary>
    /// The RECORD page's view model, which owns the take being edited. Kept so its changes can
    /// be let go of when the window is pointed at another one.
    /// </summary>
    private RecordViewModel? _vm;

    /// <summary>Guards against a second Apply landing while the file is being rewritten.</summary>
    private bool _applying;

    /// <summary>The same, for a rename: the file is moving and cannot move twice.</summary>
    private bool _renaming;

    /// <summary>How much closer the buttons take you. A step you can see in one press.</summary>
    private const double ButtonZoomStep = 1.5;

    /// <summary>Where the region begins, or the start of the take before the picture is up.</summary>
    private double RegionStart => _waveform?.Start ?? 0;

    /// <summary>And where it ends.</summary>
    private double RegionEnd => _waveform?.End ?? 1;

    /// <summary>
    /// Builds the window and wires the picture up: the player's reports in, the pointer
    /// gestures out.
    /// </summary>
    /// <remarks>
    /// The canvas and the play button are found when the window loads rather than here, since
    /// neither exists until the template has been applied.
    ///
    /// The view model's changes are let go of before being taken again, because the data
    /// context announcement fires on every reassignment and would otherwise leave the window
    /// subscribed to every take it had ever shown.
    /// </remarks>
    public RecordingEditDialog() : this(null)
    {
    }

    /// <summary>The same, over the bus a take goes onto.</summary>
    /// <remarks>
    /// Two constructors rather than one with a default, because the toolkit's runtime XAML loader
    /// looks for a public constructor taking nothing and an optional parameter is not one: with
    /// only the defaulted version the page builds with a warning saying it cannot be reached that
    /// way, and this build is kept at nought warnings.
    /// </remarks>
    /// <param name="takes">
    /// The bus a take goes onto, or nothing to play it the way it always was. Handed in rather
    /// than reached for, since this window is opened from RECORD and RECORD is what holds it.
    /// </param>
    public RecordingEditDialog(JingleBox2.Audio.Interfaces.IOutputBus? takes)
    {
        _player = new WaveformPlayer(takes);

        InitializeComponent();

        _player.PositionChanged += position =>
        {
            if (_waveform != null) _waveform.Playhead = position;
        };

        _player.Stopped += () =>
        {
            if (_waveform != null) _waveform.Playhead = -1;

            SetPlayButtonContent("▶ Play");
        };

        Loaded += (_, _) =>
        {
            _playButton = this.FindControl<Button>("PlayButton");
            _waveform = this.FindControl<WaveformView>("Waveform");

            if (_waveform == null) return;

            _waveform.PropertyChanged += RegionMoved;
        };

        DataContextChanged += (_, _) =>
        {
            _vm = DataContext as RecordViewModel;
        };

        Closing += (_, _) =>
        {
            _player.Dispose();

            if (_waveform != null) _waveform.PropertyChanged -= RegionMoved;
        };
    }

    /// <summary>
    /// The region moved on the picture, so what is playing has to move with it.
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
        if (e.Property == WaveformView.EndProperty) _player.PlayUntil(RegionEnd);
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

        if (_vm?.SelectedRecordingForEdit == null || _vm.CurrentWaveform == null) return;

        _player.Play(
            _vm.SelectedRecordingForEdit.FilePath,
            RegionStart,
            RegionEnd,
            _vm.CurrentWaveform.TotalSamples);

        if (_player.IsPlaying)
            SetPlayButtonContent("⏹ Stop");
    }

    /// <summary>Writes the wording on the play button, which says what pressing it now would do.</summary>
    private void SetPlayButtonContent(string text)
    {
        if (_playButton != null) _playButton.Content = text;
    }

    /// <summary>
    /// Closes the window. Nothing is undone by it: trimming and normalising rewrite the file
    /// when they are pressed, so there is nothing pending for this to abandon.
    /// </summary>
    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

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

    /// <summary>
    /// Cuts the file down to what is selected, and rewrites it.
    /// </summary>
    /// <remarks>
    /// Afterwards every stored position points at audio that no longer exists, so the trim, the
    /// play cursor, the playhead and the zoom are all put back to the whole file: what survived
    /// the cut is the whole file from here on.
    ///
    /// Both destructive buttons are switched off while it runs, and the preview is stopped
    /// first, since a file that is open is one that will not be rewritten on Windows.
    /// </remarks>
    /// <summary>
    /// Empties the region, leaving the take its length.
    /// </summary>
    /// <remarks>
    /// The region, the playhead and the zoom are left where they are, unlike a trim: nothing has
    /// moved, so every stored position is still about the part of the file it was about. The
    /// preview is stopped first, since a file that is open is one that will not be rewritten on
    /// Windows, and both destructive buttons are switched off while it runs.
    /// </remarks>
    private async void Silence_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _applying) return;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            await _vm.SilenceAsync(RegionStart, RegionEnd);
        }
        finally
        {
            _applying = false;
            SetApplyEnabled(true);
        }
    }

    private async void ApplyTrim_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _applying) return;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            if (!await _vm.ApplyTrimAsync(RegionStart, RegionEnd)) return;

            if (_waveform is not { } picture) return;

            picture.Start = 0;
            picture.End = 1;
            picture.Playhead = -1;
            picture.Zoom = WaveformViewport.MinZoom;
        }
        finally
        {
            _applying = false;
            SetApplyEnabled(true);
        }
    }

    /// <summary>
    /// Lifts the file's level. The audio changes under every stored position but the timeline
    /// does not, so the trim region and the playhead stay where they are.
    /// </summary>
    private async void Normalize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm == null || _applying) return;

        _player.Stop();

        _applying = true;
        SetApplyEnabled(false);

        try
        {
            await _vm.NormalizeAsync();
        }
        finally
        {
            _applying = false;
            SetApplyEnabled(true);
        }
    }

    /// <summary>
    /// Both destructive buttons go together: while the file is being rewritten, neither the
    /// trim nor the normalize may start a second write over the top of it.
    /// </summary>
    private void SetApplyEnabled(bool enabled)
    {
        var trim = this.FindControl<Button>("ApplyTrimButton");
        if (trim != null) trim.IsEnabled = enabled;

        var normalize = this.FindControl<Button>("NormalizeButton");
        if (normalize != null) normalize.IsEnabled = enabled;

        var silence = this.FindControl<Button>("SilenceButton");
        if (silence != null) silence.IsEnabled = enabled;
    }
}
