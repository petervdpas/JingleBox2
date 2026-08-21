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

        Remember(config?.KnownPlugins);
    }

    /// <summary>
    /// Takes the last scan's results as they are, without opening anything. A scan has to load
    /// every plugin library to ask what is inside it, which is slow and is what makes hosts
    /// crash; there is no reason to do it again every time the app starts.
    /// </summary>
    private void Remember(List<PluginInfo>? known)
    {
        if (known == null || known.Count == 0) return;

        int gone = 0;

        foreach (var plugin in known)
        {
            // A plugin uninstalled since the last scan is dropped rather than offered and then
            // failing to load.
            if (!PluginHost.Exists(plugin))
            {
                gone++;
                continue;
            }

            Plugins.Add(plugin);
        }

        Sort();

        Status = gone == 0
            ? $"{Plugins.Count} plugin(s) known. Scan again after installing more."
            : $"{Plugins.Count} plugin(s) known, {gone} no longer installed. Scan again to tidy the list.";
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

    /// <summary>Keeps what was found, so the next start does not have to look again.</summary>
    private void Save(List<PluginInfo> found)
    {
        if (_store == null || _config == null) return;

        _config.KnownPlugins = found;
        _store.Save(_config);
    }

    private void SaveFolders()
    {
        OnPropertyChanged(nameof(HasFolders));
        OnPropertyChanged(nameof(SearchPaths));

        if (_store == null || _config == null) return;

        _config.PluginFolders = Folders.ToList();
        _store.Save(_config);
    }

    /// <summary>
    /// Plugins that took the application down while opening their own window. Shown here so
    /// somebody who never opens that plugin's panel can still find out why, and undo it.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Audio.Plugins.PluginCrash> BlockedPlugins =>
        Audio.Plugins.PluginCrashGuard.Blocked;

    public bool HasBlockedPlugins => BlockedPlugins.Count > 0;

    public IRelayCommand AllowBlockedCommand => new RelayCommand(AllowBlocked);

    private void AllowBlocked()
    {
        Audio.Plugins.PluginCrashGuard.AllowEverything();

        OnPropertyChanged(nameof(BlockedPlugins));
        OnPropertyChanged(nameof(HasBlockedPlugins));

        Status = "Those plugins may open their own windows again.";
    }

    public ObservableCollection<PluginInfo> Plugins { get; } = new();

    /// <summary>
    /// The ones that can go in a chain. An instrument makes sound from notes and has no audio
    /// input at all, so putting one on a pad would replace the pad with silence. They stay on
    /// the SETTINGS list, because knowing they are installed is worth something.
    /// </summary>
    public ObservableCollection<PluginInfo> Effects { get; } = new();

    public bool HasEffects => Effects.Count > 0;

    /// <summary>Refills the effects list from the full one. Called after either is rebuilt.</summary>
    private void Sort()
    {
        Effects.Clear();

        foreach (var plugin in Plugins)
        {
            if (plugin.CanInsert) Effects.Add(plugin);
        }

        OnPropertyChanged(nameof(HasPlugins));
        OnPropertyChanged(nameof(HasEffects));
    }

    [ObservableProperty] private string status = "Not scanned yet";

    /// <summary>The directories a scan looks in, as one line for the page to show.</summary>
    public string SearchPaths => string.Join("\n", PluginHost.SearchPaths(Folders));

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
            var found = await Task.Run(() => PluginHost.Scan(folders));

            Plugins.Clear();
            foreach (var plugin in found) Plugins.Add(plugin);

            Sort();

            Save(found);

            Status = found.Count == 0
                ? "No CLAP or VST3 plugins found. The places looked in are listed above."
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

}
