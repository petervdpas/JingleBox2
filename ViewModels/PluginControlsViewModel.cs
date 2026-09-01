using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Audio.Plugins.Bridge;
using JingleBox2.Diagnostics;
using System;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Audio.Plugins.Interfaces;

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

    /// <summary>
    /// Told when anything here moves, so whatever holds the plugin knows it has work to save.
    /// </summary>
    /// <remarks>
    /// Optional, since a panel can be built over a plugin that belongs to nobody. When it is
    /// null a knob still turns and the sound still changes, and nothing is written down, which
    /// is the one thing the log says out loud when a move arrives.
    /// </remarks>
    private readonly Action? _changed;

    /// <summary>Whether the panel has been got ready, so a second look does not build it again.</summary>
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

    /// <summary>
    /// Wraps a loaded plugin, without touching it: nothing is read until <see cref="Prepare"/>.
    /// </summary>
    /// <remarks>
    /// Three things are wired here, and each is a way the plugin can say something without being
    /// asked. It runs in a process of its own, so it can go away while the application carries
    /// on: that is the whole point of putting it there, and it means somebody has to say so and
    /// offer to start it again. A knob turned in the plugin's own window is still a change to
    /// whatever holds the plugin, and without hearing it nothing would ever know there was
    /// something to save. And a preset arriving is every knob at once: no plugin reports two
    /// thousand separate moves for that, so it comes through on its own and means the same
    /// thing, that there is something to save and what is on screen is out of date.
    ///
    /// All three are posted to the drawing thread, since they arrive from the plugin's own.
    /// </remarks>
    /// <param name="plugin">The running plugin, which is the only thing that knows its own knobs.</param>
    /// <param name="changed">Told when a value moves, so whoever holds the patch can write it down.</param>
    public PluginControlsViewModel(IPluginParameters plugin, Action? changed = null)
    {
        Plugin = plugin;
        _changed = changed;

        if (plugin is BridgedPlugin bridged) bridged.Stopped += () => Dispatcher.UIThread.Post(Fell);

        plugin.Edited += (id, value) => Dispatcher.UIThread.Post(() => Moved(id, value));

        plugin.Reloaded += () => Dispatcher.UIThread.Post(Reloaded);
    }

    /// <summary>The plugin loaded a whole new sound.</summary>
    /// <remarks>
    /// Every row is read again rather than the ones that moved, because a patch moves all of
    /// them at once and the plugin has not said which.
    /// </remarks>
    private void Reloaded()
    {
        foreach (var row in Parameters) row.Refresh();

        _changed?.Invoke();
    }

    /// <summary>The knobs by the parameter they stand for, for a move reported by the plugin.</summary>
    private readonly System.Collections.Generic.Dictionary<uint, PluginParameterViewModel> _rows = new();

    /// <summary>
    /// The plugin moved one of its own knobs. The host's copy of that knob follows it, and
    /// whatever owns the plugin is told there is something worth saving.
    /// </summary>
    /// <remarks>
    /// Except for the ones the plugin moves by itself. A compressor reports its gain reduction
    /// and its output level the same way it reports a knob, sixty times a second, and treating
    /// those as edits would leave a song that can never be saved because it is always about to
    /// need saving again.
    ///
    /// The log says both which parameter moved and whether anybody is listening, because a knob
    /// that changes the sound and leaves the song looking saved is a fault with no other
    /// evidence at all.
    /// </remarks>
    private void Moved(uint id, double value)
    {
        if (_rows.TryGetValue(id, out var row)) row.Adopt(value);

        Log.Write(LogArea.Plugins, () =>
            Plugin.Info.Name + " moved its own " + id + " to " + value.ToString("0.####") +
            (Reads(id) ? ", a reading, ignored" : "") +
            (_changed == null ? ", AND NOBODY IS LISTENING" : ", telling whatever holds it"));

        if (Reads(id)) return;

        _changed?.Invoke();
    }

    /// <summary>Which parameters the plugin is reporting rather than being set to.</summary>
    /// <remarks>
    /// Asked of the plugin once and kept, since it is a fact about the plugin rather than about
    /// the moment, and a move arrives often enough that walking every parameter each time would
    /// cost something.
    /// </remarks>
    private System.Collections.Generic.HashSet<uint>? _readings;

    /// <summary>True when this parameter is a meter, so a move of it is not an edit.</summary>
    private bool Reads(uint id)
    {
        if (_readings == null)
        {
            _readings = new System.Collections.Generic.HashSet<uint>();

            foreach (var parameter in Plugin.Parameters())
            {
                if (parameter.IsReadOnly) _readings.Add(parameter.Id);
            }
        }

        return _readings.Contains(id);
    }

    /// <summary>True when the plugin's process has gone and it is not playing.</summary>
    [ObservableProperty] private bool hasStopped;

    /// <summary>What happened to it, in words fit to put on the page.</summary>
    [ObservableProperty] private string stoppedNote = "";

    /// <summary>
    /// The plugin's process has gone. The panel says so and offers to start it again.
    /// </summary>
    /// <remarks>
    /// Nothing else is affected, which is the whole reason plugins are run out of process: an
    /// effect that stops passes its audio through and an instrument goes quiet. The interface it
    /// was drawing in belongs to a process that is not there any more, so it is let go of here
    /// rather than left as a window over nothing.
    /// </remarks>
    private void Fell()
    {
        if (Plugin is not BridgedPlugin bridged) return;

        StoppedNote = bridged.StoppedNote + " Nothing else was affected.";
        HasStopped = true;

        Editor = null;

        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(HasOwnWindow));
        OnPropertyChanged(nameof(HasKnobs));
    }

    /// <summary>
    /// Starts the plugin again, with the settings it had. Anything it was holding that was
    /// never saved is not coming back, which is why the button says settings.
    /// </summary>
    /// <remarks>Always enabled; the button is only shown once the plugin has stopped.</remarks>
    public IRelayCommand RestartCommand => new RelayCommand(Restart);

    /// <summary>
    /// Loads the plugin again and builds its panel afresh.
    /// </summary>
    /// <remarks>
    /// The panel is marked unprepared first, because a plugin started again is a new plugin with
    /// the same name: its parameters have to be read again and its interface opened again. One
    /// that will not start says so and stays stopped.
    /// </remarks>
    private void Restart()
    {
        if (Plugin is not BridgedPlugin bridged) return;

        if (!bridged.Restart())
        {
            StoppedNote = Plugin.Info.Name + " would not start again.";
            return;
        }

        HasStopped = false;
        StoppedNote = "";

        _prepared = false;
        _knobs = false;
        Prepare();

        _changed?.Invoke();
    }

    /// <summary>The plugin's own interface, when it has one and it has been opened.</summary>
    public IPluginEditor? Editor { get; private set; }

    /// <summary>True when the plugin draws itself, whether or not that is what is on show.</summary>
    public bool HasOwnWindow => Editor != null;

    /// <summary>
    /// Whether the host's own knobs are shown instead of the plugin's face.
    /// </summary>
    /// <remarks>
    /// This is the whole of what makes a plugin linkable. A knob is pointed at by resting the
    /// pointer on a control the host drew, and the host drew nothing for a plugin with a face of
    /// its own: <see cref="Prepare"/> stopped as soon as it had opened the editor, so the knobs
    /// were never built and there was no dial to rest a pointer on. Every plugin worth having
    /// has a face, so pointing at one was impossible rather than merely awkward, and it was
    /// impossible for instruments as much as for effects.
    ///
    /// Off for a plugin that draws itself, since its own face is what you opened it for, and on
    /// for one that does not, where the knobs are all there is. Switched by hand in the window's
    /// header, which is also the only place the two can be told apart.
    /// </remarks>
    public bool ShowsKnobs
    {
        get => _showsKnobs;
        set
        {
            if (_showsKnobs == value) return;

            _showsKnobs = value;

            if (value) BuildKnobsOnce();

            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowsFace));
            OnPropertyChanged(nameof(HasKnobs));
        }
    }

    /// <summary>Behind <see cref="ShowsKnobs"/>.</summary>
    private bool _showsKnobs;

    /// <summary>True when the plugin's own face is what is on show.</summary>
    public bool ShowsFace => Editor != null && !ShowsKnobs;

    /// <summary>True when there is a choice to make, so the window can offer it.</summary>
    /// <remarks>
    /// Only where the plugin draws itself. Without a face there is nothing to switch to and a
    /// button offering to show the knobs that are already showing says nothing.
    /// </remarks>
    public bool CanShowKnobs => Editor != null;

    /// <summary>True when there is nothing but the host's knobs to show.</summary>
    public bool HasKnobs => ShowsKnobs && HasParameters;

    /// <summary>
    /// Builds the host's knobs the first time somebody asks to see them, and not before.
    /// </summary>
    /// <remarks>
    /// Reading two thousand parameters into two thousand controls costs a visible pause, and
    /// Serum answers with 2622 of them. A plugin drawing its own face is opened for that face,
    /// so the knobs are built when they are wanted and never for the plugin nobody switches.
    /// </remarks>
    private void BuildKnobsOnce()
    {
        if (_knobs) return;
        _knobs = true;

        BuildKnobs();

        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(HasSwitches));
        OnPropertyChanged(nameof(HasReadouts));
        OnPropertyChanged(nameof(IsTruncated));
        OnPropertyChanged(nameof(TruncationNote));
    }

    /// <summary>Whether the host's knobs have been built yet.</summary>
    private bool _knobs;

    /// <summary>
    /// Gets the panel ready to be shown: the plugin's own interface if it has one, and the
    /// host's knobs if it has not.
    /// </summary>
    /// <remarks>
    /// Held back until something actually wants to look. Opening a plugin's interface costs it
    /// a window and a toolkit, and reading two thousand parameters into two thousand controls
    /// costs a visible pause, and a chain of effects loaded with a song wants neither until
    /// somebody opens one.
    ///
    /// The plugin's own interface wins whenever there is one: nobody programs a synth with two
    /// thousand parameters through an alphabetical list of dials. A plugin that has already
    /// taken the application down once does not get another go at it, and its knobs still work
    /// and its sound is untouched. The attempt is written down before the plugin is touched,
    /// because if it goes down there is no afterwards in which to write anything, and a plugin
    /// that will not open its window still has knobs.
    /// </remarks>
    public void Prepare()
    {
        if (_prepared) return;
        _prepared = true;

        IsBlocked = PluginCrashGuard.IsBlocked(Plugin.Info);

        if (!IsBlocked && Plugin is IPluginWindowSource source)
        {
            PluginCrashGuard.Risky(Plugin.Info, PluginStage.Window);

            try
            {
                Editor = source.OpenEditor();
            }
            catch (Exception)
            {
                Editor = null;
            }

            if (Editor == null) PluginCrashGuard.Survived(Plugin.Info);
            else Watch();
        }

        if (Editor != null)
        {
            OnPropertyChanged(nameof(Editor));
            OnPropertyChanged(nameof(HasOwnWindow));
            OnPropertyChanged(nameof(CanShowKnobs));
            OnPropertyChanged(nameof(ShowsFace));
            OnPropertyChanged(nameof(HasKnobs));
            return;
        }

        OnPropertyChanged(nameof(BlockedNote));

        _showsKnobs = true;

        BuildKnobsOnce();

        OnPropertyChanged(nameof(ShowsKnobs));
        OnPropertyChanged(nameof(ShowsFace));
        OnPropertyChanged(nameof(HasKnobs));
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

    /// <summary>What the guard has to say about it, for the line where the panel would be.</summary>
    public string BlockedNote => PluginCrashGuard.Reason(Plugin.Info);

    /// <summary>
    /// Lets a plugin that crashed try again, for one that has been updated since or for
    /// somebody who wants to find out. It goes straight back on the list if it goes down again.
    /// </summary>
    /// <remarks>Always enabled; it is only shown while the plugin is blocked.</remarks>
    public IRelayCommand AllowCommand => new RelayCommand(Allow);

    /// <summary>
    /// Lifts the block on this one plugin.
    /// </summary>
    /// <remarks>
    /// Nothing is opened here. The panel has already been prepared without an interface, so the
    /// window has to be closed and opened again, which is what <see cref="WasAllowed"/> is for.
    /// </remarks>
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
    /// <remarks>
    /// The panel is made ready to be prepared again. Without that a plugin opens once: the
    /// second window finds the panel already prepared, and prepared means an interface that has
    /// just been put away and knobs that were never built.
    ///
    /// Taking a plugin's window away is as likely to go wrong as putting it up, and a crash
    /// there used to leave nothing behind to find afterwards, since the note from opening had
    /// already been rubbed out. So closing is written down too.
    /// </remarks>
    public void Close()
    {
        _settle?.Dispose();
        _settle = null;

        _prepared = false;
        _knobs = false;

        var editor = Editor;
        Editor = null;

        if (editor == null) return;

        PluginCrashGuard.Risky(Plugin.Info, PluginStage.Window);

        editor.Dispose();

        PluginCrashGuard.Survived(Plugin.Info);

        OnPropertyChanged(nameof(Editor));
        OnPropertyChanged(nameof(HasOwnWindow));
    }

    /// <summary>
    /// Reads the plugin's parameters and sorts them into knobs, switches and readings.
    /// </summary>
    /// <remarks>
    /// Built from scratch each time, because a plugin that has been started again is a new
    /// plugin with the same name and its parameters are read fresh.
    ///
    /// A hidden parameter is one the plugin does not want shown, and its own bypass is something
    /// the host offers in its own way, so neither is counted at all. Past
    /// <see cref="MaxShown"/> the drawing stops and the panel says so: a big synth declares
    /// thousands, Serum 2622 and Vital 2852, and a panel with that many knobs in it is not a
    /// panel anybody can use. Everything is still loaded, still played and still saved.
    /// </remarks>
    private void BuildKnobs()
    {
        Parameters.Clear();
        Controls.Clear();
        Switches.Clear();
        Readouts.Clear();
        _rows.Clear();

        Total = 0;

        foreach (var parameter in Plugin.Parameters())
        {
            if (parameter.IsHidden || parameter.IsBypass) continue;

            Total++;

            if (Parameters.Count >= MaxShown) continue;

            var row = new PluginParameterViewModel(Plugin, parameter, _changed);

            Parameters.Add(row);
            _rows[parameter.Id] = row;

            if (parameter.IsReadOnly) Readouts.Add(row);
            else if (row.IsSwitch) Switches.Add(row);
            else Controls.Add(row);
        }
    }

    /// <summary>The plugin itself, for the things only it can answer.</summary>
    public IPluginParameters Plugin { get; }

    /// <summary>What the plugin calls itself.</summary>
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

    /// <summary>True when there are tick boxes, so the panel draws that part at all.</summary>
    public bool HasSwitches => Switches.Count > 0;

    /// <summary>True when anything was found to draw, which a plugin that failed leaves false.</summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <summary>True when the plugin reports something back, which most do not.</summary>
    public bool HasReadouts => Readouts.Count > 0;

    /// <summary>True when the plugin has more than a panel can usefully hold.</summary>
    public bool IsTruncated => Total > Parameters.Count;

    /// <summary>Said out loud rather than left as a list that quietly stops.</summary>
    public string TruncationNote =>
        IsTruncated
            ? $"Showing the first {Parameters.Count} of {Total} parameters. The rest are loaded and saved, just not drawn."
            : "";

    /// <summary>Takes the readings back from the plugin. Only the ones it moves by itself.</summary>
    /// <remarks>
    /// The knobs are left alone deliberately: a knob only moves when a hand moves it or when the
    /// plugin says so, and reading every one back would be thousands of calls into the plugin
    /// per tick.
    /// </remarks>
    public void Refresh()
    {
        foreach (var readout in Readouts) readout.Refresh();
    }
}
