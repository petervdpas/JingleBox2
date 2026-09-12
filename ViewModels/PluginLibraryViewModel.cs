using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Config;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Audio.Plugins.Interfaces;

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
    /// <summary>The one place that knows both plugin standards. Holds nothing, so one is enough.</summary>
    private readonly IPluginHost _plugins = new PluginHost();

    /// <summary>Set while a scan is running, so a second one cannot start on top of it.</summary>
    private bool _scanning;

    /// <summary>The settings block, or null when nothing here is to be kept.</summary>
    /// <remarks>
    /// Optional, because the plugin pickers are shown in places that have no settings behind them
    /// at all, and a scan is worth having in those too.
    /// </remarks>
    private readonly Config.Interfaces.ISettingsBlock? _settings;

    /// <summary>The settings, which is where the folders and the last scan's results live.</summary>
    private readonly AppConfig? _config;

    /// <summary>
    /// What is switched off: folders that are not walked and plugins that are not offered.
    /// </summary>
    /// <remarks>
    /// Over the settings' own two lists, so a tick is written where everything else here is.
    /// Given out, because the pickers ask it: this is what decides whether a plugin is offered
    /// and the rack's instrument list has to ask the same question the effects list does.
    /// </remarks>
    public Audio.Plugins.Interfaces.IPluginSwitches Switches { get; }

    /// <summary>
    /// Takes the folders and the last scan's results out of the settings, without scanning.
    /// </summary>
    /// <remarks>
    /// Deliberately quiet at startup. A scan loads every plugin on the machine, and doing that
    /// before anybody has asked to see a plugin would put the cost of the whole library on
    /// opening the application.
    /// </remarks>
    public PluginLibraryViewModel(Config.Interfaces.ISettingsBlock? settings = null)
    {
        _settings = settings;
        _config = settings?.Config;

        var config = _config;

        Switches = new Audio.Plugins.PluginSwitches(
            config?.PluginPlacesOff, config?.PluginsOff, () => settings?.Moved());

        foreach (var folder in config?.PluginFolders ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(folder)) Folders.Add(folder);
        }

        Audio.Plugins.PluginShelf.Wants(config?.KnownPlugins);

        Remember(config?.KnownPlugins);

        Restock();
    }

    /// <summary>
    /// The folders a scan walks, one row per standard, each with its own tick.
    /// </summary>
    /// <remarks>
    /// Rebuilt rather than kept in step, since the list only moves when a folder is added or
    /// taken away, and rebuilding it is a handful of rows.
    /// </remarks>
    public ObservableCollection<PluginPlaceViewModel> Places { get; } = new();

    /// <summary>Fills the list of folders from what a scan would really walk.</summary>
    private void Restock()
    {
        Places.Clear();

        foreach (var place in _plugins.Places(Folders))
            Places.Add(new PluginPlaceViewModel(place, Switches));
    }

    /// <summary>
    /// Takes the last scan's results as they are, without opening anything. A scan has to load
    /// every plugin library to ask what is inside it, which is slow and is what makes hosts
    /// crash; there is no reason to do it again every time the app starts.
    /// </summary>
    /// <remarks>
    /// A plugin uninstalled since the last scan is dropped rather than offered and then failing
    /// to load, and the count of those is said out loud: a list quietly one shorter than it was
    /// is a list nobody can trust.
    /// </remarks>
    private void Remember(List<PluginInfo>? known)
    {
        if (known == null || known.Count == 0) return;

        int gone = 0;

        foreach (var plugin in known)
        {
            if (!_plugins.Exists(plugin))
            {
                gone++;
                continue;
            }

            Plugins.Add(plugin);
        }

        Listed();
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

    /// <summary>True when anything has been added by hand, for the page to show a list at all.</summary>
    public bool HasFolders => Folders.Count > 0;

    /// <summary>Adds a folder and scans again, so the effect of adding one is immediate.</summary>
    /// <remarks>
    /// The same folder twice is refused with a reason rather than accepted quietly, since the
    /// list is the only evidence of what was asked for. The comparison ignores case, which is
    /// right on Windows and merely cautious elsewhere.
    /// </remarks>
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

    /// <summary>Takes a folder back off the list, which is the cross beside each row.</summary>
    /// <remarks>Always enabled: a row that is there can always be removed.</remarks>
    public IRelayCommand<string> RemoveFolderCommand => new RelayCommand<string>(RemoveFolder);

    /// <summary>
    /// Forgets a folder and scans again, so what is on the list matches what is offered.
    /// </summary>
    /// <remarks>
    /// The scan is what actually removes the plugins that were only found there. Leaving them
    /// listed until the next start would mean offering plugins from a folder somebody has just
    /// said they do not want looked in.
    /// </remarks>
    public void RemoveFolder(string? folder)
    {
        if (folder == null || !Folders.Remove(folder)) return;

        SaveFolders();
        _ = ScanAsync();
    }

    /// <summary>Keeps what was found, so the next start does not have to look again.</summary>
    /// <remarks>
    /// The shelf is told at the same moment, since it is what a song's plugin is looked up in and
    /// a scan is the only thing that changes the answer. Told rather than left to read the
    /// settings, so nothing on the loading path opens a file.
    /// </remarks>
    private void Save(List<PluginInfo> found)
    {
        Audio.Plugins.PluginShelf.Wants(found);

        if (_settings == null || _config == null) return;

        _config.KnownPlugins = found;
        _settings.Moved();
    }

    /// <summary>Writes the folder list out, and tells the page the paths it shows have moved.</summary>
    private void SaveFolders()
    {
        OnPropertyChanged(nameof(HasFolders));

        Restock();

        OnPropertyChanged(nameof(HasPlaces));

        if (_settings == null || _config == null) return;

        _config.PluginFolders = Folders.ToList();
        _settings.Moved();
    }

    /// <summary>
    /// Plugins that took the application down while opening their own window. Shown here so
    /// somebody who never opens that plugin's panel can still find out why, and undo it.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<Audio.Plugins.PluginCrash> BlockedPlugins =>
        Audio.Plugins.PluginCrashGuard.Blocked;

    /// <summary>True when anything is being held back, which is what shows that part of the page.</summary>
    public bool HasBlockedPlugins => BlockedPlugins.Count > 0;

    /// <summary>Lifts every block at once, for somebody who has just updated their plugins.</summary>
    /// <remarks>
    /// Always enabled, including when nothing is blocked, because the part of the page it lives
    /// on is only shown when something is.
    /// </remarks>
    public IRelayCommand AllowBlockedCommand => new RelayCommand(AllowBlocked);

    /// <summary>
    /// Lets every blocked plugin try again, and puts back the ones that were kept out of the
    /// pickers for being unloadable.
    /// </summary>
    /// <remarks>
    /// Any of them that goes down again is back on the list straight away, so this costs at
    /// worst the crash it already caused once.
    /// </remarks>
    private void AllowBlocked()
    {
        Audio.Plugins.PluginCrashGuard.AllowEverything();

        Sort();

        OnPropertyChanged(nameof(BlockedPlugins));
        OnPropertyChanged(nameof(HasBlockedPlugins));


        Status = "Those plugins may open their own windows again.";
    }

    /// <summary>Everything the last scan found, instruments included.</summary>
    public ObservableCollection<PluginInfo> Plugins { get; } = new();

    /// <summary>
    /// The same list as rows, each with the tick that says whether it is offered.
    /// </summary>
    /// <remarks>
    /// Beside <see cref="Plugins"/> rather than instead of it, because the two answer different
    /// questions: that one is what this installation has, which is what everything else reads,
    /// and this is the page showing it with something to do to each entry.
    /// </remarks>
    public ObservableCollection<PluginRowViewModel> Rows { get; } = new();

    /// <summary>
    /// The ones that can go in a chain. An instrument makes sound from notes and has no audio
    /// input at all, so putting one on a pad would replace the pad with silence. They stay on
    /// the SETTINGS list, because knowing they are installed is worth something.
    /// </summary>
    public ObservableCollection<PluginInfo> Effects { get; } = new();

    /// <summary>True when the plus button has anything to offer.</summary>
    public bool HasEffects => Effects.Count > 0;

    /// <summary>Refills the effects list from the full one. Called after either is rebuilt.</summary>
    /// <remarks>
    /// A plugin whose loading is what killed the last run is not offered, but it stays on the
    /// list above, where the reason for that is shown and can be undone.
    /// </remarks>
    private void Sort()
    {
        Effects.Clear();

        foreach (var plugin in Plugins)
        {
            if (plugin.CanInsert
                && Switches.Wanted(plugin)
                && !Audio.Plugins.PluginCrashGuard.IsLoadBlocked(plugin))
                Effects.Add(plugin);
        }

        OnPropertyChanged(nameof(HasPlugins));
        OnPropertyChanged(nameof(HasEffects));

        Turned?.Invoke();
    }

    /// <summary>
    /// Builds the rows the page shows, one per plugin.
    /// </summary>
    /// <remarks>
    /// **Only where the plugins themselves change**, which is a scan or the list being read back
    /// at startup, and deliberately not when a tick moves. A row rebuilt under the pointer is the
    /// tick somebody has just clicked being thrown away and replaced, which loses the scroll
    /// position on a list of two hundred and reads as a click that did not take.
    /// </remarks>
    private void Listed()
    {
        Rows.Clear();

        foreach (var plugin in Plugins) Rows.Add(new PluginRowViewModel(plugin, Switches, Sort));
    }

    /// <summary>
    /// Told whenever what is offered has moved, for a picker that is not bound to these lists.
    /// </summary>
    /// <remarks>
    /// The rack's instrument list is built from the plugins rather than from
    /// <see cref="Effects"/>, since an instrument is picked beside the machines rather than off a
    /// chain, so it has to hear about a tick some other way.
    /// </remarks>
    public Action? Turned { get; set; }

    /// <summary>Whether that plugin may be offered, which is the tick on its row.</summary>
    /// <remarks>
    /// Asked rather than filtered into a list of its own, because the one place that wants it is
    /// the rack, which is already building its own list out of these and the machines together.
    /// </remarks>
    /// <param name="plugin">The plugin to ask about.</param>
    public bool Wanted(PluginInfo plugin) => Switches.Wanted(plugin);

    /// <summary>What the last thing that happened here has to say, for the line on the page.</summary>
    /// <remarks>
    /// Never empty. "Not scanned yet" is a real answer and is why a fresh install shows no
    /// plugins, which is the question this line exists to answer.
    /// </remarks>
    [ObservableProperty] private string status = "Not scanned yet";

    /// <summary>True when there is anywhere at all to look, so the page can say when there is not.</summary>
    public bool HasPlaces => Places.Count > 0;

    /// <summary>True when anything at all is known, scanned now or remembered from last time.</summary>
    public bool HasPlugins => Plugins.Count > 0;

    /// <summary>Scans, which is the button in SETTINGS.</summary>
    /// <remarks>
    /// Always enabled; a second scan while one is running is dropped in <see cref="ScanAsync"/>
    /// rather than greyed out, because the button is also how somebody finds out that a scan is
    /// happening at all.
    /// </remarks>
    public IAsyncRelayCommand ScanCommand => new AsyncRelayCommand(ScanAsync);

    /// <summary>
    /// Looks in every standard place, opens what it finds, and asks each bundle what is in it.
    /// A bundle that will not open is skipped rather than stopping the scan: one bad plugin
    /// is not a machine with no plugins.
    /// </summary>
    /// <remarks>
    /// The loading itself happens off the drawing thread, since it is seconds rather than
    /// milliseconds and every one of those seconds is another plugin's own startup. What comes
    /// back is kept in the settings, so this is a thing somebody asks for rather than something
    /// that happens on every start.
    /// </remarks>
    public async Task ScanAsync()
    {
        if (_scanning) return;

        _scanning = true;
        Status = "Scanning...";

        try
        {
            var folders = Folders.ToList();
            var off = Switches.Off;

            var found = await Task.Run(() => _plugins.Scan(folders, off));

            Plugins.Clear();
            foreach (var plugin in found) Plugins.Add(plugin);

            Listed();
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
