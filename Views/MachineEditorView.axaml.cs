using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JingleBox2.ViewModels;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// Where a machine is built.
/// </summary>
/// <remarks>
/// It opens a machine project, which is a folder on disc: the machine's own, kept wherever you
/// keep your work. The rack is the other side of it, what is installed and ready for a song to
/// take an instrument off, and that stays in the tracker where a song is written.
///
/// The pickers belong to the window, so they are opened here and only the answer goes to the
/// view model, the same arrangement the recordings importer uses.
/// </remarks>
public partial class MachineEditorView : UserControl
{
    public MachineEditorView()
    {
        InitializeComponent();

        // The preview is painted in the machine's own colours, so it is repainted whenever
        // another machine is opened, and mixed again when the theme moves under both.
        DataContextChanged += (_, _) => Watch();
        UI.ThemeManager.Changed += Later;
        DetachedFromVisualTree += (_, _) => UI.ThemeManager.Changed -= Later;
    }

    private System.ComponentModel.INotifyPropertyChanged? _watched;

    private void Watch()
    {
        if (_watched != null) _watched.PropertyChanged -= OnEditorChanged;

        _watched = Editor;

        if (_watched != null) _watched.PropertyChanged += OnEditorChanged;

        Retint();
    }

    private void OnEditorChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MachineEditorViewModel.Project)) Retint();
    }

    private void Later() => Avalonia.Threading.Dispatcher.UIThread.Post(Retint);

    /// <summary>Puts the machine's colours on the preview, so it looks like the box it is.</summary>
    private void Retint() =>
        MachineTint.Apply(this.FindControl<Border>("PanelPreview")!, Editor?.Project?.Theme);

    private MachineEditorViewModel? Editor => (DataContext as MainViewModel)?.MachineEditor;

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor) return;

        string? folder = await PickFolder("Open a machine");

        if (folder != null) editor.Open(folder);
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (Editor is not { } editor || editor.Project == null) return;

        if (!editor.NeedsFolder)
        {
            editor.Save();
            return;
        }

        string? folder = await PickFolder("Where to keep this machine");

        if (folder != null) editor.Save(folder);
    }

    private async System.Threading.Tasks.Task<string?> PickFolder(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage == null) return null;

        var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return picked.Count == 0 ? null : picked[0].TryGetLocalPath();
    }
}
