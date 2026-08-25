using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using System;
using Avalonia.Platform.Storage;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

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

    public SettingsView()
    {
        InitializeComponent();

        // Not a style: Avalonia has no way to ask a style how wide anything is, so the shape
        // is chosen here and the look follows from the class.
        this.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(Shape));
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
}
