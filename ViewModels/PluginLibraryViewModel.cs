using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JingleBox2.ViewModels;

/// <summary>
/// The plugins this machine has, as SETTINGS shows them. Scanning is what proves the host
/// works before anything is put on a track, and it is where the answer lives when a plugin
/// somebody expects to see is not there.
/// </summary>
/// <remarks>
/// A scan opens every plugin library it finds, which is the only way to ask one what it holds.
/// That happens off the UI thread, and the libraries stay loaded afterwards: unloading them is
/// what crashes hosts. On this scale, a few dozen plugins, that costs a few megabytes.
/// </remarks>
public sealed partial class PluginLibraryViewModel : ObservableObject
{
    /// <summary>Set while a scan is running, so a second one cannot start on top of it.</summary>
    private bool _scanning;

    private readonly ConfigStore? _store;
    private readonly AppConfig? _config;

    public PluginLibraryViewModel(ConfigStore? store = null, AppConfig? config = null)
    {
        _store = store;
        _config = config;

        foreach (var folder in config?.PluginFolders ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(folder)) Folders.Add(folder);
        }
    }

    /// <summary>
    /// Folders of your own, on top of the ones the format specifies. Kept with the rest of the
    /// settings, so a plugin kept somewhere unusual is still found next time.
    /// </summary>
    public ObservableCollection<string> Folders { get; } = new();

    public bool HasFolders => Folders.Count > 0;

    /// <summary>Adds a folder and scans again, so the effect of adding one is immediate.</summary>
    public void AddFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;

        string path = folder.Trim();

        if (Folders.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
        {
            Status = "That folder is already on the list";
            return;
        }

        Folders.Add(path);
        SaveFolders();

        _ = ScanAsync();
    }

    public IRelayCommand<string> RemoveFolderCommand => new RelayCommand<string>(RemoveFolder);

    public void RemoveFolder(string? folder)
    {
        if (folder == null || !Folders.Remove(folder)) return;

        SaveFolders();
        _ = ScanAsync();
    }

    private void SaveFolders()
    {
        OnPropertyChanged(nameof(HasFolders));
        OnPropertyChanged(nameof(SearchPaths));

        if (_store == null || _config == null) return;

        _config.PluginFolders = Folders.ToList();
        _store.Save(_config);
    }

    public ObservableCollection<ClapPluginInfo> Plugins { get; } = new();

    [ObservableProperty] private string status = "Not scanned yet";

    /// <summary>The directories a scan looks in, as one line for the page to show.</summary>
    public string SearchPaths => string.Join("\n", ClapScanner.SearchPaths(Folders));

    public bool HasPlugins => Plugins.Count > 0;

    public IAsyncRelayCommand ScanCommand => new AsyncRelayCommand(ScanAsync);

    /// <summary>
    /// Looks in every standard place, opens what it finds, and asks each bundle what is in it.
    /// A bundle that will not open is skipped rather than stopping the scan: one bad plugin
    /// is not a machine with no plugins.
    /// </summary>
    public async Task ScanAsync()
    {
        if (_scanning) return;

        _scanning = true;
        Status = "Scanning...";

        try
        {
            var folders = Folders.ToList();
            var found = await Task.Run(() => Scan(folders));

            Plugins.Clear();
            foreach (var plugin in found) Plugins.Add(plugin);

            OnPropertyChanged(nameof(HasPlugins));

            Status = found.Count == 0
                ? "No CLAP plugins found. The places looked in are listed above."
                : $"{found.Count} plugin(s) found";
        }
        catch (Exception ex)
        {
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _scanning = false;
        }
    }

    private static List<ClapPluginInfo> Scan(IReadOnlyList<string> folders)
    {
        var found = new List<ClapPluginInfo>();

        foreach (var path in ClapScanner.Bundles(folders))
        {
            var bundle = ClapBundle.Acquire(path);
            if (bundle == null) continue;

            found.AddRange(bundle.Plugins());

            // The reference goes back straight away. The library itself stays loaded, which is
            // deliberate and explained where that is decided.
            bundle.Dispose();
        }

        found.Sort((first, second) => string.Compare(first.Name, second.Name, StringComparison.OrdinalIgnoreCase));
        return found;
    }
}
