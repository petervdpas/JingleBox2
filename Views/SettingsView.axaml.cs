using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.Platform.Storage;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The SETTINGS page: the audio device, the pad matrix, the themes, the MIDI devices and their
/// roles, the control surfaces, the desk's own controller links, the log, and the machine shelf.
/// </summary>
/// <remarks>
/// Almost all of it is bindings onto <see cref="MainViewModel"/>. The two things answered here
/// are the shape of the page, which no style can decide because a style cannot ask how wide
/// anything is, and the two file pickers, which belong to the window rather than to a view
/// model.
/// </remarks>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// How narrow the page has to get before the sections move from the side to the top.
    /// </summary>
    /// <remarks>
    /// The rail costs a hundred and fifty pixels whatever the window is doing, and on a narrow
    /// one that is a third of the page spent on five words. Across the top it costs a line.
    /// </remarks>
    private const double RailNeeds = 620;

    /// <summary>
    /// Builds the page and watches its width so the sections can move.
    /// </summary>
    /// <remarks>
    /// Watched here rather than expressed as a style, because Avalonia has no way to ask a
    /// style how wide anything is. The shape is chosen here and the look follows from the
    /// class that is set.
    /// </remarks>
    public SettingsView()
    {
        InitializeComponent();

        this.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(Shape));

        AddHandler(KeyDownEvent, Learning, RoutingStrategies.Tunnel);

        LostFocus += (_, _) => Shortcuts?.Stop();
    }

    /// <summary>The shortcuts page, when this page has a view model behind it.</summary>
    private ShortcutsViewModel? Shortcuts => (DataContext as MainViewModel)?.Shortcuts;

    /// <summary>Starts or stops listening on the row that was clicked.</summary>
    /// <param name="sender">The button on that row.</param>
    /// <param name="e">Ignored: which row it is comes from the button's own row.</param>
    private void OnListenForKey(object? sender, RoutedEventArgs e)
    {
        if (Row(sender) is { } row) Shortcuts?.ListenCommand.Execute(row);
    }

    /// <summary>Takes the key off the row that was clicked.</summary>
    /// <param name="sender">The button on that row.</param>
    /// <param name="e">Ignored, as above.</param>
    private void OnClearKey(object? sender, RoutedEventArgs e)
    {
        if (Row(sender) is { } row) Shortcuts?.ClearCommand.Execute(row);
    }

    /// <summary>Which row a button on it belongs to.</summary>
    /// <param name="sender">The button that was clicked.</param>
    private static ShortcutRowViewModel? Row(object? sender) =>
        (sender as Control)?.DataContext as ShortcutRowViewModel;

    /// <summary>
    /// A key arrived while a shortcut row was listening for one.
    /// </summary>
    /// <remarks>
    /// Heard on the way down and before anything else, which is the whole of what makes this
    /// work: the keys somebody is most likely to want are the ones something already answers,
    /// and Ctrl+H, the space bar and Ctrl+S would each be taken by their own door before a
    /// listening row ever saw them. Nothing is swallowed unless a row really was listening, so
    /// the page is as it was the rest of the time.
    ///
    /// The listening stops when the page loses the keyboard, since a row left waiting would
    /// take whatever was pressed on the way back to it.
    /// </remarks>
    private void Learning(object? sender, KeyEventArgs e)
    {
        if (Shortcuts is not { } keys) return;

        if (keys.Took(e.Key, e.KeyModifiers)) e.Handled = true;
    }

    /// <summary>Sections down the side while there is room, across the top when there is not.</summary>
    private void Shape(Rect bounds)
    {
        bool rail = bounds.Width >= RailNeeds;

        if (Sections.Classes.Contains("rail") == rail) return;

        Sections.TabStripPlacement = rail ? Dock.Left : Dock.Top;

        Sections.Classes.Set("rail", rail);
        Sections.Classes.Set("strip", !rail);
    }

    /// <summary>
    /// Picks another folder to look for plugins in. The picker belongs to the window, so it
    /// is opened here and only the answer goes to the view model.
    /// </summary>
    private async void OnAddPluginFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Where to look for plugins",
            AllowMultiple = false
        });

        if (picked.Count == 0) return;

        string? path = picked[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) vm.Plugins.AddFolder(path);
    }

    /// <summary>
    /// Brings a machine in from a zip. Same arrangement as the folder above: the picker belongs
    /// to the window, and only the path goes to the view model.
    /// </summary>
    private async void OnImportMachine(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import a machine",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Machines") { Patterns = new[] { "*.zip" } }
            }
        });

        if (picked.Count == 0) return;

        string? path = picked[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) vm.MachineShelf.Import(path);
    }

    /// <summary>
    /// Brings an effect in from a zip, which is the same act and the same code as a machine's.
    /// </summary>
    private async void OnImportEffect(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import an effect",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Effects") { Patterns = new[] { "*.zip" } }
            }
        });

        if (picked.Count == 0) return;

        string? path = picked[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) vm.EffectShelf.Import(path);
    }
}
