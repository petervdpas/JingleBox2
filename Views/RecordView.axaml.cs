using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JingleBox2.Audio;
using JingleBox2.ViewModels;
using System;
using System.Linq;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The RECORD page: what is coming in, the take being made, and the shelf of takes there are.
/// </summary>
/// <remarks>
/// The shelf is what every other part of the application draws its recordings from, so a take
/// deleted here goes to <c>deleted/</c> rather than away, and undo on this page fetches back the
/// last one.
/// </remarks>
public partial class RecordView : UserControl
{
    /// <summary>The one door recordings come in through. Holds nothing, so one is enough.</summary>
    private readonly IRecordingImport _import = new RecordingImport();

    /// <summary>
    /// The view model this page is currently watching the input on, or none.
    /// </summary>
    /// <remarks>
    /// Kept so the page lets go of exactly what it took hold of. The counting, the delay before
    /// the input really closes, and the fact that the mixer holds it too are all
    /// <see cref="ViewModels.Interfaces.IInputWatch"/>'s: this end only says whether this page is looking.
    /// </remarks>
    private RecordViewModel? _watching;

    /// <summary>Whether the page is up, which is what decides whether the input is held open.</summary>
    private bool _onScreen;

    /// <summary>
    /// Builds the page.
    /// </summary>
    /// <remarks>
    /// The picture is a <c>WaveformView</c> in the layout now rather than a canvas drawn from
    /// here, so there is nothing to draw and nothing to subscribe to: what the take looks like
    /// and where the play cursor is are both bindings. The data context can arrive after the
    /// page is up, which is why the input is opened from here as well as from the attach, and
    /// getting that wrong is silent, since the meter simply never moves.
    /// </remarks>
    public RecordView()
    {
        InitializeComponent();

        this.DataContextChanged += (_, _) => UpdateMonitoring();
    }

    /// <summary>
    /// The input is watched while this page is up, so the meter reads before a take rather
    /// than only during one. It is closed again on the way out: holding a capture device open
    /// for a tab nobody is looking at is rude to whatever else wants the microphone.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _onScreen = true;
        UpdateMonitoring();
    }

    /// <summary>
    /// Leaving the page says so and no more. Whether the input really closes, and how long a
    /// departure has to last before it does, is
    /// <see cref="ViewModels.Interfaces.IInputWatch.LetGo"/>'s.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _onScreen = false;
        UpdateMonitoring();
    }

    /// <summary>
    /// Opens or closes the input to match whether the page is up and what it is showing.
    /// </summary>
    /// <remarks>
    /// A view model this page has let go of must not be left holding the input open, so it is
    /// stopped at once whatever the page is doing: the delayed close is about this page coming
    /// straight back, and a different view model is not that.
    /// </remarks>
    private void UpdateMonitoring()
    {
        var vm = DataContext as RecordViewModel;

        if (ReferenceEquals(_watching, vm) == (_onScreen && vm != null)) return;

        if (_watching != null)
        {
            _watching.LetRoutesGo();
            _watching.LetGo();
            _watching = null;
        }

        if (vm == null || !_onScreen) return;

        vm.Watch();
        vm.WatchRoutes();

        _watching = vm;
    }

    /// <summary>
    /// Brings recordings in from the disc onto the shelf of takes.
    /// </summary>
    /// <remarks>
    /// The picker belongs to the window, so it is opened here and only the answer goes to the
    /// view model, which is the same arrangement the instrument designer's importer uses.
    ///
    /// Sorted the way the folder reads, since a set of takes is nearly always named so that it
    /// sorts, and that order is the order they will be wanted in.
    /// </remarks>
    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not RecordViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import recordings",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Recordings")
                {
                    Patterns = _import.Kinds.Select(k => "*" + k).ToArray()
                }
            }
        });

        vm.Import(picked
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList());
    }

}
