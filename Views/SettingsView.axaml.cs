using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
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
}
