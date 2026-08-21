using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using System;
using System.Collections.ObjectModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// A plugin's knobs, sorted into what you set, what you switch, and what it reports back.
/// </summary>
/// <remarks>
/// Shared by the window an effect opens in and by the instrument editor, because a plugin's
/// controls are a plugin's controls wherever they are shown. It knows nothing about chains or
/// tracks, which is what lets both use it.
/// </remarks>
public sealed partial class PluginControlsViewModel : ObservableObject
{
    /// <summary>How many knobs one panel will draw before it stops.</summary>
    public const int MaxShown = 256;

    private readonly Action? _changed;
    private bool _prepared;

    /// <summary>
    /// Runs once after a window has been up a while, to say that opening it did not kill the
    /// application. See <see cref="PluginCrashGuard"/> for why that has to be said out loud.
    /// </summary>
    /// <remarks>
    /// A plain timer rather than the UI one: all it does at the end is rub out a note on disk,
    /// which is nobody's business but this class's and does not need the drawing thread.
    /// </remarks>
    private System.Threading.Timer? _settle;

    public PluginControlsViewModel(IPluginParameters plugin, Action? changed = null)
    {
        Plugin = plugin;
        _changed = changed;
    }

    /// <summary>The plugin's own interface, when it has one and it has been opened.</summary>
    public IPluginEditor? Editor { get; private set; }

    /// <summary>True when the plugin draws itself and the host's knobs are not needed.</summary>
    public bool HasOwnWindow => Editor != null;

    /// <summary>True when there is nothing but the host's knobs to show.</summary>
    public bool HasKnobs => Editor == null && HasParameters;

    /// <summary>
    /// Gets the panel ready to be shown: the plugin's own interface if it has one, and the
    /// host's knobs if it has not.
    /// </summary>
    /// <remarks>
    /// Held back until something actually wants to look. Opening a plugin's interface costs it
    /// a window and a toolkit, and reading two thousand parameters into two thousand controls
    /// costs a visible pause, and a chain of effects loaded with a song wants neither until
    /// somebody opens one.
    /// </remarks>
    public void Prepare()
    {
        if (_prepared) return;
        _prepared = true;

        // A plugin that has already taken the application down once does not get another go
        // at it. Its knobs still work and its sound is untouched.
        IsBlocked = PluginCrashGuard.IsBlocked(Plugin.Info);

        // The plugin's own interface wins whenever there is one. Nobody programs a synth with
        // two thousand parameters through an alphabetical list of dials.
        if (!IsBlocked && Plugin is IPluginWindowSource source)
        {
            // Written down before the plugin is touched, because if it goes down there is no
            // afterwards in which to write anything.
            PluginCrashGuard.Opening(Plugin.Info);

            try
            {
                Editor = source.OpenEditor();
            }
            catch (Exception)
            {
                // A plugin that will not open its window still has knobs.
                Editor = null;
            }

            if (Editor == null) PluginCrashGuard.Survived(Plugin.Info);
            else Watch();
        }

        if (Editor != null)
        {
            OnPropertyChanged(nameof(Editor));
            OnPropertyChanged(nameof(HasOwnWindow));
            OnPropertyChanged(nameof(HasKnobs));
            return;
        }

        OnPropertyChanged(nameof(BlockedNote));

        BuildKnobs();

        OnPropertyChanged(nameof(HasKnobs));
        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(HasSwitches));
        OnPropertyChanged(nameof(HasReadouts));
        OnPropertyChanged(nameof(IsTruncated));
        OnPropertyChanged(nameof(TruncationNote));
    }

    /// <summary>
    /// Starts the wait that says this window opened rather than exploded. A window closed by
    /// hand before the wait is up counts as having opened too: the application is still here.
    /// </summary>
    private void Watch()
    {
        _settle?.Dispose();

        var info = Plugin.Info;

        _settle = new System.Threading.Timer(
            _ => PluginCrashGuard.Survived(info),
            null,
            TimeSpan.FromSeconds(PluginCrashGuard.SettleSeconds),
            System.Threading.Timeout.InfiniteTimeSpan);
    }

    /// <summary>True when this plugin is not being given a window, and why.</summary>
    [ObservableProperty] private bool isBlocked;

    public string BlockedNote => PluginCrashGuard.Reason(Plugin.Info);

    /// <summary>
    /// Lets a plugin that crashed try again, for one that has been updated since or for
    /// somebody who wants to find out. It goes straight back on the list if it goes down again.
    /// </summary>
    public IRelayCommand AllowCommand => new RelayCommand(Allow);

    private void Allow()
    {
        PluginCrashGuard.Allow(Plugin.Info);

        IsBlocked = false;
        WasAllowed = true;

        OnPropertyChanged(nameof(BlockedNote));
        OnPropertyChanged(nameof(WasAllowed));
    }

    /// <summary>Set after a block is lifted, so the page can say what to do next.</summary>
    public bool WasAllowed { get; private set; }

    /// <summary>Puts the plugin's interface away. The plugin itself carries on playing.</summary>
    public void Close()
    {
        _settle?.Dispose();
        _settle = null;

        var editor = Editor;
        Editor = null;

        editor?.Dispose();

        // Closed by hand, which means opening it did not kill anything.
        if (editor != null) PluginCrashGuard.Survived(Plugin.Info);

        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(HasOwnWindow));
    }

    private void BuildKnobs()
    {
        foreach (var parameter in Plugin.Parameters())
        {
            // A hidden parameter is one the plugin does not want shown, and its own bypass is
            // something the host offers in its own way.
            if (parameter.IsHidden || parameter.IsBypass) continue;

            Total++;

            // A big synth declares thousands. Serum has 2622 and Vital 2852, and a panel with
            // that many knobs in it is not a panel anybody can use, so the drawing stops here
            // and says so. Everything is still loaded, still played and still saved.
            if (Parameters.Count >= MaxShown) continue;

            var row = new PluginParameterViewModel(Plugin, parameter, _changed);

            Parameters.Add(row);

            if (parameter.IsReadOnly) Readouts.Add(row);
            else if (row.IsSwitch) Switches.Add(row);
            else Controls.Add(row);
        }
    }

    public IPluginParameters Plugin { get; }

    public string Name => Plugin.Info.Name;

    /// <summary>How many the plugin actually has, shown or not.</summary>
    public int Total { get; private set; }

    /// <summary>Everything shown, controls and readings alike, for polling.</summary>
    public ObservableCollection<PluginParameterViewModel> Parameters { get; } = new();

    /// <summary>The knobs: what you set.</summary>
    public ObservableCollection<PluginParameterViewModel> Controls { get; } = new();

    /// <summary>The two-position ones, which are tick boxes rather than dials.</summary>
    public ObservableCollection<PluginParameterViewModel> Switches { get; } = new();

    /// <summary>The readings: what the plugin reports back, such as gain reduction.</summary>
    public ObservableCollection<PluginParameterViewModel> Readouts { get; } = new();

    public bool HasSwitches => Switches.Count > 0;

    public bool HasParameters => Parameters.Count > 0;

    public bool HasReadouts => Readouts.Count > 0;

    /// <summary>True when the plugin has more than a panel can usefully hold.</summary>
    public bool IsTruncated => Total > Parameters.Count;

    /// <summary>Said out loud rather than left as a list that quietly stops.</summary>
    public string TruncationNote =>
        IsTruncated
            ? $"Showing the first {Parameters.Count} of {Total} parameters. The rest are loaded and saved, just not drawn."
            : "";

    /// <summary>Takes the readings back from the plugin. Only the ones it moves by itself.</summary>
    public void Refresh()
    {
        foreach (var readout in Readouts) readout.Refresh();
    }
}
